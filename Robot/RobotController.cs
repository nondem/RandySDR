// RobotController - owns the automation pipeline: scanner events -> recorder -> analyzer -> (Whisper -> Ollama) -> lockout policy.
// Nothing here may block the scanner, the DSP thread or the UI thread; every slow step runs on a worker thread.
using System.Collections.Concurrent;
using System.Diagnostics;

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
        private SignalScanner? _scanner;
        private long _lockCounter = DateTime.UtcNow.Ticks;
        private HashSet<long> _activeKeys = new();
        private DateTime _lastPurge = DateTime.MinValue;
        private long _lostAnalysisClips;
        public RobotLogger Logger { get; }

        public RobotConfig Config { get; }
        public RobotStore Store { get; }
        public WhisperTranscriber Whisper { get; }
        public OllamaClient Ollama { get; }
        public ClipRecorder Recorder => _recorder;
        public string AiStatus { get; private set; } = "";
        public int AiBacklog => _aiQueue.Count;
        public long LostAnalysisClips => Interlocked.Read(ref _lostAnalysisClips);

        /// <summary>Raised from worker threads when clips or lockouts changed; marshal to the UI thread before touching controls.</summary>
        public event Action? Changed;

        public RobotController(RobotConfig config, RobotStore store, Func<double> stepHz)
        {
            Config = config;
            Store = store;
            _stepHz = stepHz;
            Whisper = new WhisperTranscriber(() => Config);
            Ollama = new OllamaClient(() => Config);
            _recorder = new ClipRecorder(() => Config.ClipFolder, () => Config.MaxClipSeconds, () => Config.MinFreeGb,
                () => Config.SpeechEarlySkip, () => Config.SpeechEarlySkipSeconds);
            Logger = new RobotLogger(config.ClipFolder, "robot-log.txt");
            _recorder.ClipCompleted += r =>
            {
                if (!_analysisQueue.TryAdd(r))
                {
                    Interlocked.Increment(ref _lostAnalysisClips);
                    Logger.LogError("RobotController", "Analysis queue full; clip not indexed", r.Path);
                }
                else
                {
                    Logger.LogInfo("ClipRecorder", $"Clip completed: {Path.GetFileName(r.Path)} ({r.Info.Hz / 1e6:F3} MHz)");
                }
            };
            
            // Log startup for debugging
            Logger.LogInfo("RobotController", "RobotController initialized", 
                $"Clip folder: {config.ClipFolder}, Transcribe: {Config.Transcribe}, Classify: {Config.Classify}");
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
            _recorder.InvalidateSpeechSkip();
            if (_pipeline != null)
                _pipeline.AudioTap = _scanner != null && Config.RecordClips ? _recorder.Enqueue : null;
            if (!Config.RecordClips && _recorder.IsRecording) _recorder.End("recording-off");
        }

        public void AttachScanner(SignalScanner scanner)
        {
            if (ReferenceEquals(_scanner, scanner)) return;
            if (_scanner != null) DetachScanner(_scanner);
            _scanner = scanner;
            scanner.SignalLocked += OnSignalLocked;
            scanner.SignalReleased += OnSignalReleased;
            scanner.ExtraLockout = IsAutoLockedOut;
            ApplyConfig();
        }

        public void DetachScanner(SignalScanner scanner)
        {
            if (!ReferenceEquals(_scanner, scanner)) return;
            scanner.SignalLocked -= OnSignalLocked;
            scanner.SignalReleased -= OnSignalReleased;
            scanner.ExtraLockout = null;
            _scanner = null;
            if (_recorder.IsRecording) _recorder.End("scan-stopped");
            ApplyConfig();
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
            (Config.EarlySkip && _recorder.IsRecording
            && _recorder.LockAgeSeconds > Config.EarlySkipSeconds && _recorder.LockOpenSeconds < 0.2)
            || (Config.SpeechEarlySkip && _recorder.SpeechSkipDue);

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
            if (star && Store.GetClip(clipId) is { } clip)
            {
                foreach (var l in Store.Lockouts("suggested", "active").Where(l => l.Key == clip.Key))
                    Store.SetLockoutStatus(l.Id, "restored", DateTime.UtcNow);
                RefreshLockoutCache();
            }
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
            if (File.Exists(c.Path)) File.Delete(c.Path);
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
                Logger.LogInfo("RobotController", "Analysis loop started");
                foreach (var r in _analysisQueue.GetConsumingEnumerable(_cts.Token))
                {
                    try 
                    { 
                        ProcessClip(r); 
                    }
                    catch (Exception ex)
                    {
                        Interlocked.Increment(ref _lostAnalysisClips);
                        Logger.LogError("RobotController", $"Clip analysis failed", $"{Path.GetFileName(r.Path)}: {ex.Message}");
                    }
                }
            }
            catch (OperationCanceledException) 
            { 
                Logger.LogInfo("RobotController", "Analysis loop stopped");
            }
        }

        private void ProcessClip(ClipResult r)
        {
            long key = LockoutPolicy.ChannelKey(r.Info.Hz, _stepHz());
            var options = new ClipAnalyzerOptions(Config.TriageMinSpeechSeconds, Config.TriageMinSpeechRatio, Config.TriageMaxCarrierRatio);
            
            Logger.LogDebug("ClipAnalyzer", $"Starting analysis for {Path.GetFileName(r.Path)}", 
                $"Frequency: {r.Info.Hz / 1e6:F3} MHz, Mode: {r.Info.Mode}, Duration: {r.DurationSec:F2}s");
            
            ClipMetrics? m = r.Deleted ? null : ClipAnalyzer.AnalyzeFile(r.Path, options);
            long id = Store.InsertClip(r, m, key);

            if (m != null)
            {
                Logger.LogInfo("ClipAnalyzer", $"Analysis complete: {Path.GetFileName(r.Path)}", 
                    $"Class: {m.Class}, RMS: {m.RmsDb:F1}dB, Speech: {m.SpeechSec:F2}s ({m.SpeechRatio:P0}), Tone: {m.ToneRatio:P0}");
            }
            else
            {
                Logger.LogWarn("ClipAnalyzer", "Analysis skipped (deleted clip)", $"Clip ID: {id}");
            }

            if (DateTime.UtcNow - _lastPurge > TimeSpan.FromHours(6))
            {
                _lastPurge = DateTime.UtcNow;
                var removed = Store.PurgeAudio(Config.VoiceRetentionDays, Config.CarrierRetentionDays, DateTime.UtcNow);
                Logger.LogInfo("RobotController", $"Audio purge completed", $"{removed} clips removed");
            }

            bool voiceish = m != null && m.Class is ClipClass.Voice or ClipClass.Uncertain && m.SpeechSec > 0.3;
            if (voiceish && Config.Transcribe)
            {
                if (!_aiQueue.TryAdd(new AiJob(id, r.Path, r.Info.Hz, r.Info.Mode, key)))
                {
                    Store.SetStatus(id, "ai-queue-full");
                    Logger.LogWarn("RobotController", "AI queue full; clip marked as unclassified", $"Clip ID: {id}");
                }
                else
                {
                    Logger.LogInfo("RobotController", $"Queued for transcription", $"Clip ID: {id}, Class: {m?.Class}");
                }
            }
            else if (m != null && m.Class != ClipClass.Voice)
            {
                EvaluateLockout(key, r.Info.Hz);
                Logger.LogDebug("LockoutPolicy", $"Lockout evaluation triggered", $"Key: {key}, Class: {m.Class}");
            }
            
            Changed?.Invoke();
        }

        private void AiLoop()
        {
            try
            {
                Logger.LogInfo("RobotController", "AI loop started");
                foreach (var job in _aiQueue.GetConsumingEnumerable(_cts.Token))
                {
                    try 
                    { 
                        RunAi(job, _cts.Token).GetAwaiter().GetResult(); 
                    }
                    catch (OperationCanceledException) when (_cts.IsCancellationRequested) { throw; }
                    catch (Exception ex)
                    {
                        AiStatus = "AI error: " + ex.Message;
                        Store.SetStatus(job.ClipId, "ai-failed");
                        Logger.LogError("RobotController", $"AI processing failed", $"Clip ID: {job.ClipId}: {ex.Message}");
                    }
                    Changed?.Invoke();
                }
            }
            catch (OperationCanceledException) 
            { 
                Logger.LogInfo("RobotController", "AI loop stopped");
            }
        }

        private async Task RunAi(AiJob job, CancellationToken ct)
        {
            var clipInfo = Store.GetClip(job.ClipId);
            if (clipInfo is null || clipInfo.Deleted || !File.Exists(job.Path))
            {
                Logger.LogWarn("RobotController", "AI job skipped; clip not available", 
                    $"Clip ID: {job.ClipId}, Exists: {File.Exists(job.Path)}");
                return;
            }

            var fileName = Path.GetFileName(job.Path);
            Logger.LogInfo("WhisperTranscriber", $"Starting transcription", $"{fileName} ({job.Hz / 1e6:F3} MHz)");
            
            string text = await Whisper.TranscribeAsync(job.Path, ct);
            AiStatus = Whisper.Status;
            Store.SetTranscript(job.ClipId, text);

            if (text.Length == 0)
            {
                Logger.LogWarn("WhisperTranscriber", "No speech detected in transcript", 
                    $"{fileName}: transcription returned empty string");
                Store.SetStatus(job.ClipId, "no-speech");
                return;
            }

            Logger.LogInfo("WhisperTranscriber", $"Transcription complete", 
                $"{fileName}: {text.Length} chars, {text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Count()} words");

            if (!Config.Classify)
            {
                Logger.LogDebug("RobotController", "Classification disabled; skipping Ollama call");
                return;
            }

            try
            {
                Logger.LogInfo("OllamaClient", $"Starting classification", 
                    $"{fileName}: {text.Length} chars to classify");
                
                var c = await Ollama.ClassifyAsync(text, job.Hz / 1e6, job.Mode, ct);
                
                if (c == null)
                {
                    Logger.LogWarn("OllamaClient", "Classification returned null", $"{fileName}");
                    Store.SetStatus(job.ClipId, "unclassified");
                    return;
                }

                Store.SetClassification(job.ClipId, c.Category, c.Urgency, c.Summary, c.Confidence);
                
                Logger.LogInfo("OllamaClient", $"Classification complete", 
                    $"{fileName}: {c.Category} (urgency {c.Urgency}, confidence {c.Confidence:P0})");
                Logger.LogDebug("OllamaClient", "Summary", c.Summary);
                Logger.LogDebug("OllamaClient", "Keywords", string.Join(", ", c.Keywords));
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) 
            { 
                throw; 
            }
            catch (Exception ex)
            {
                // Provide helpful diagnostic message based on error type
                string errorMsg = ex.Message.Contains("timed out") 
                    ? $"Ollama timeout ({Config.OllamaTimeoutSeconds}s): server may be busy or slow"
                    : ex.Message.Contains("HTTP 404")
                        ? "Ollama API endpoint not found: check URL and server version"
                        : ex.Message.Contains("HTTP")
                            ? $"Ollama HTTP error: {ex.Message}"
                            : ex.Message.Contains("Cannot connect")
                                ? "Cannot connect to Ollama: is 'ollama serve' running?"
                                : "Ollama unavailable: " + ex.Message;
                
                AiStatus = errorMsg;
                Store.SetStatus(job.ClipId, "unclassified");
                Logger.LogError("OllamaClient", $"Classification failed", $"{errorMsg}");
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
            bool analysisStopped = _analysisThread.Join(3000);
            bool aiStopped = _aiThread.Join(3000);
            void ReleaseResources()
            {
                Whisper.Dispose();
                Ollama.Dispose();
                Store.Dispose();
                _analysisQueue.Dispose();
                _aiQueue.Dispose();
                _cts.Dispose();
            }
            if (analysisStopped && aiStopped) ReleaseResources();
            else
            {
                System.Diagnostics.Debug.WriteLine("Robot workers are still stopping; resource cleanup deferred.");
                _ = Task.Run(() =>
                {
                    _analysisThread.Join();
                    _aiThread.Join();
                    ReleaseResources();
                });
            }
        }
    }
}
