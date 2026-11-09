// RobotController - owns the automation pipeline: scanner events -> recorder -> analyzer -> (Whisper -> Ollama) -> lockout policy.
// Nothing here may block the scanner, the DSP thread or the UI thread; every slow step runs on a worker thread.
using System.Collections.Concurrent;

namespace RandySDR
{
    public sealed class RobotController : IDisposable
    {
        private sealed record AiJob(long ClipId, string Path, double Hz, string Mode, long Key);

        private readonly Func<double> _stepHz;
        private readonly ClipRecorder _recorder;
        private readonly BlockingCollection<ClipResult> _analysisQueue = new(new ConcurrentQueue<ClipResult>(), 500);
        private readonly BlockingCollection<AiJob> _aiQueue = new(new ConcurrentQueue<AiJob>(), 200);
        private readonly Thread _analysisThread, _aiThread;
        private readonly CancellationTokenSource _cts = new();
        private ReceiverPipeline? _pipeline;
        private long _lockCounter;
        private HashSet<long> _activeKeys = new();
        private DateTime _lastPurge = DateTime.MinValue;

        public RobotConfig Config { get; }
        public RobotStore Store { get; }
        public WhisperTranscriber Whisper { get; }
        public OllamaClient Ollama { get; }
        public ClipRecorder Recorder => _recorder;
        public string AiStatus { get; private set; } = "";
        public int AiBacklog => _aiQueue.Count;

        /// <summary>Raised from worker threads when clips or lockouts changed; marshal to the UI thread before touching controls.</summary>
        public event Action? Changed;

        public RobotController(RobotConfig config, RobotStore store, Func<double> stepHz)
        {
            Config = config;
            Store = store;
            _stepHz = stepHz;
            Whisper = new WhisperTranscriber(() => Config);
            Ollama = new OllamaClient(() => Config);
            _recorder = new ClipRecorder(() => Config.ClipFolder, () => Config.MaxClipSeconds, () => Config.MinFreeGb);
            _recorder.ClipCompleted += r => { if (!_analysisQueue.TryAdd(r)) System.Diagnostics.Debug.WriteLine("Robot analysis queue full"); };
            _analysisThread = new Thread(AnalysisLoop) { IsBackground = true, Name = "RobotAnalysis" };
            _aiThread = new Thread(AiLoop) { IsBackground = true, Name = "RobotAi" };
            _analysisThread.Start();
            _aiThread.Start();
            RefreshLockoutCache();
        }

        // ---------- wiring ----------

        public void AttachPipeline(ReceiverPipeline? pipeline)
        {
            if (_pipeline != null && !ReferenceEquals(_pipeline, pipeline)) _pipeline.AudioTap = null;
            _pipeline = pipeline;
            ApplyConfig();
        }

        public void ApplyConfig()
        {
            if (_pipeline != null)
                _pipeline.AudioTap = Config.RecordClips ? _recorder.Enqueue : null;
            if (!Config.RecordClips) _recorder.End("recording-off");
        }

        public void AttachScanner(SignalScanner scanner)
        {
            scanner.SignalLocked += OnSignalLocked;
            scanner.SignalReleased += OnSignalReleased;
            scanner.ExtraLockout = IsAutoLockedOut;
        }

        public void DetachScanner(SignalScanner scanner)
        {
            scanner.SignalLocked -= OnSignalLocked;
            scanner.SignalReleased -= OnSignalReleased;
            scanner.ExtraLockout = null;
        }

        private Func<string> _modeProvider = () => "";
        public void SetModeProvider(Func<string> p) => _modeProvider = p;

        private void OnSignalLocked(double hz, double db)
        {
            if (!Config.RecordClips || _pipeline == null) return;
            _recorder.Begin(new ClipInfo(Interlocked.Increment(ref _lockCounter), hz, _modeProvider(), db));
        }

        private void OnSignalReleased(double hz) => _recorder.End("released");

        /// <summary>Call when scanning stops, the device closes, or the user takes manual control.</summary>
        public void EndRecording(string reason) => _recorder.End(reason);

        public bool EarlySkipDue =>
            Config.EarlySkip && _recorder.IsRecording
            && _recorder.LockAgeSeconds > Config.EarlySkipSeconds && _recorder.LockOpenSeconds < 0.2;

        // ---------- lockouts ----------

        public bool IsAutoLockedOut(double hz)
        {
            if (Config.LockoutMode == AutoLockoutMode.Off) return false;
            var keys = _activeKeys;
            return keys.Count > 0 && keys.Contains(LockoutPolicy.ChannelKey(hz, _stepHz()));
        }

        public void RefreshLockoutCache()
        {
            Store.ExpireLockouts(DateTime.UtcNow);
            _activeKeys = Store.Lockouts("active").Select(l => l.Key).ToHashSet();
        }

        public void AcceptSuggestion(long lockoutId)
        {
            Store.SetLockoutStatus(lockoutId, "active", DateTime.UtcNow);
            Store.Log("accept-lockout", lockoutId, "user accepted");
            RefreshLockoutCache();
            Changed?.Invoke();
        }

        public void RestoreLockout(long lockoutId, string status = "restored")
        {
            Store.SetLockoutStatus(lockoutId, status, DateTime.UtcNow);
            Store.Log(status + "-lockout", lockoutId, "user");
            RefreshLockoutCache();
            Changed?.Invoke();
        }

        private void EvaluateLockout(long key, double hz)
        {
            if (Config.LockoutMode == AutoLockoutMode.Off || Store.HasOpenLockout(key)) return;
            var now = DateTime.UtcNow;
            var history = Store.History(key, now - LockoutPolicy.Window);
            bool voice = Store.HasVoice(key, now.AddDays(-30));
            if (!LockoutPolicy.ShouldPropose(history, voice, now, out var reason)) return;

            bool auto = Config.LockoutMode == AutoLockoutMode.Auto;
            Store.AddLockout(key, hz, reason, auto ? "active" : "suggested", now);
            Store.Log(auto ? "auto-lockout" : "suggest-lockout", key, $"{hz / 1e6:F4} MHz: {reason}");
            RefreshLockoutCache();
        }

        // ---------- user actions on clips ----------

        public void SetStar(long clipId, bool star)
        {
            Store.SetStar(clipId, star);
            Changed?.Invoke();
        }

        /// <summary>"This was wrong": junk becomes voice (and its channel is released from lockouts); anything else becomes noise.</summary>
        public void MarkWrong(long clipId)
        {
            var c = Store.GetClip(clipId);
            if (c == null) return;
            bool wasJunk = c.Class is "CARRIER" or "NOISE" or "TONE" or "SHORT";
            Store.SetWrong(clipId, true);
            Store.SetClass(clipId, wasJunk ? "VOICE" : "NOISE");
            Store.Log("user-correction", c.Key, $"clip {clipId}: {c.Class} -> {(wasJunk ? "VOICE" : "NOISE")}");
            if (wasJunk)
                foreach (var l in Store.Lockouts("suggested", "active").Where(l => l.Key == c.Key))
                    Store.SetLockoutStatus(l.Id, "restored", DateTime.UtcNow);
            RefreshLockoutCache();
            Changed?.Invoke();
        }

        public void DeleteClip(long clipId)
        {
            var c = Store.GetClip(clipId);
            if (c == null) return;
            try { if (File.Exists(c.Path)) File.Delete(c.Path); } catch { }
            Store.MarkDeleted(clipId);
            Changed?.Invoke();
        }

        public void Reprocess(long clipId)
        {
            var c = Store.GetClip(clipId);
            if (c == null || c.Deleted || !File.Exists(c.Path)) return;
            _aiQueue.TryAdd(new AiJob(c.Id, c.Path, c.Hz, c.Mode, c.Key));
        }

        // ---------- workers ----------

        private void AnalysisLoop()
        {
            try
            {
                foreach (var r in _analysisQueue.GetConsumingEnumerable(_cts.Token))
                {
                    try { ProcessClip(r); }
                    catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Clip analysis failed: {ex.Message}"); }
                }
            }
            catch (OperationCanceledException) { }
        }

        private void ProcessClip(ClipResult r)
        {
            long key = LockoutPolicy.ChannelKey(r.Info.Hz, _stepHz());
            ClipMetrics? m = r.Deleted ? null : ClipAnalyzer.AnalyzeFile(r.Path);
            long id = Store.InsertClip(r, m, key);

            if (DateTime.UtcNow - _lastPurge > TimeSpan.FromHours(6))
            {
                _lastPurge = DateTime.UtcNow;
                Store.PurgeAudio(Config.VoiceRetentionDays, Config.CarrierRetentionDays, DateTime.UtcNow);
            }

            bool voiceish = m != null && m.Class is ClipClass.Voice or ClipClass.Uncertain && m.SpeechSec > 0.3;
            if (voiceish && Config.Transcribe)
            {
                if (!_aiQueue.TryAdd(new AiJob(id, r.Path, r.Info.Hz, r.Info.Mode, key)))
                    Store.SetStatus(id, "ai-queue-full");
            }
            else if (m != null && m.Class != ClipClass.Voice)
            {
                EvaluateLockout(key, r.Info.Hz);
            }
            Changed?.Invoke();
        }

        private void AiLoop()
        {
            try
            {
                foreach (var job in _aiQueue.GetConsumingEnumerable(_cts.Token))
                {
                    try { RunAi(job, _cts.Token).GetAwaiter().GetResult(); }
                    catch (OperationCanceledException) { throw; }
                    catch (Exception ex)
                    {
                        AiStatus = "AI error: " + ex.Message;
                        Store.SetStatus(job.ClipId, "ai-failed");
                    }
                    Changed?.Invoke();
                }
            }
            catch (OperationCanceledException) { }
        }

        private async Task RunAi(AiJob job, CancellationToken ct)
        {
            string text = await Whisper.TranscribeAsync(job.Path, ct);
            AiStatus = Whisper.Status;
            Store.SetTranscript(job.ClipId, text);

            if (text.Length == 0)
            {
                var clip = Store.GetClip(job.ClipId);
                if (clip?.Class == "UNCERTAIN") Store.SetClass(job.ClipId, "NOISE");
                Store.SetStatus(job.ClipId, "no-speech");
                EvaluateLockout(job.Key, job.Hz);
                return;
            }

            if (!Config.Classify) return;
            try
            {
                var c = await Ollama.ClassifyAsync(text, job.Hz / 1e6, job.Mode, ct);
                if (c == null) { Store.SetStatus(job.ClipId, "unclassified"); return; }
                Store.SetClassification(job.ClipId, c.Category, c.Urgency, c.Summary, c.Confidence);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                AiStatus = "Ollama unavailable: " + ex.Message;
                Store.SetStatus(job.ClipId, "unclassified");
            }
        }

        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            if (_pipeline != null) _pipeline.AudioTap = null;
            _recorder.End("shutdown");
            _recorder.Dispose();
            _analysisQueue.CompleteAdding();
            _aiQueue.CompleteAdding();
            _cts.Cancel();
            _analysisThread.Join(3000);
            _aiThread.Join(3000);
            Whisper.Dispose();
            Ollama.Dispose();
            Store.Dispose();
            _cts.Dispose();
        }
    }
}
