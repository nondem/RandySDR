// ClipRecorder - turns the pre-squelch audio tap into WAV clips, one per scanner lock (split on long squelch gaps / max length).
// Enqueue() runs on the DSP thread and only copies into a bounded queue; all file I/O happens on the recorder's own thread.
using System.Collections.Concurrent;

namespace RandySDR
{
    public sealed record ClipInfo(long LockId, double Hz, string Mode, double StrengthDb);

    public sealed record ClipResult(
        ClipInfo Info, string Path, DateTime StartUtc, double DurationSec, double OpenSec,
        string EndReason, bool Deleted);

    public sealed class ClipRecorder : IDisposable
    {
        public const int SampleRate = ReceiverPipeline.AudioSampleRate;
        public const double LockTrimSec = 0.3;      // audio right after a retune is stale/settling
        public const double SquelchGapSec = 1.5;    // closed-squelch gap that ends a segment
        public const double MinClipSec = 0.7;       // shorter clips keep metadata only

        private enum Kind { Audio, Begin, End, Sync, Quit }
        private sealed record Item(Kind Kind, float[]? Audio = null, bool SquelchOpen = true, ClipInfo? Info = null, string? Reason = null, ManualResetEventSlim? Done = null);

        private readonly BlockingCollection<Item> _queue = new(new ConcurrentQueue<Item>(), 600);
        private readonly Thread _thread;
        private readonly Func<string> _folder;
        private readonly Func<int> _maxClipSec;
        private readonly Func<double> _minFreeGb;
        private readonly Func<bool> _speechSkipEnabled;
        private readonly Func<double> _speechSkipSeconds;

        // Worker-thread state
        private WavClipWriter? _writer;
        private ClipInfo? _info;          // lock currently being recorded (kept while a segment is between squelch gaps)
        private DateTime _segStart;
        private long _trimLeft, _gapSamples, _openSamples;
        private volatile bool _segmentOpen;
        private readonly List<float[]> _pendingGap = new();
        private SpeechSkipDetector? _speechDetector;
        private DateTime _nextDiskRetry;

        private long _dropped;
        private volatile bool _lockActive;
        private long _lockStartTick;
        private long _openSamplesLive;
        private long _droppedAtLock;
        private long _currentLockId;
        private long _pendingAudioBlocks;
        private volatile bool _speechSkipDue;
        private volatile bool _speechSkipEligible;

        public long DroppedBlocks => Interlocked.Read(ref _dropped);
        public bool IsRecording => _lockActive;
        public bool IsWriting => _segmentOpen;
        public double LockAgeSeconds => _lockActive ? (Environment.TickCount64 - Interlocked.Read(ref _lockStartTick)) / 1000.0 : 0;
        public double LockOpenSeconds => Interlocked.Read(ref _openSamplesLive) / (double)SampleRate;
        public string? LastError { get; private set; }
        public bool SpeechSkipDue => _lockActive && _speechSkipEligible && _speechSkipDue && Interlocked.Read(ref _pendingAudioBlocks) == 0
            && Interlocked.Read(ref _dropped) == Interlocked.Read(ref _droppedAtLock);

        /// <summary>Raised on the recorder thread when a clip file is closed.</summary>
        public event Action<ClipResult>? ClipCompleted;

        public ClipRecorder(Func<string> folder, Func<int> maxClipSec, Func<double> minFreeGb,
            Func<bool>? speechSkipEnabled = null, Func<double>? speechSkipSeconds = null)
        {
            _folder = folder;
            _maxClipSec = maxClipSec;
            _minFreeGb = minFreeGb;
            _speechSkipEnabled = speechSkipEnabled ?? (() => false);
            _speechSkipSeconds = speechSkipSeconds ?? (() => 10);
            _thread = new Thread(Run) { IsBackground = true, Name = "ClipRecorder" };
            _thread.Start();
        }

        public void Begin(ClipInfo info)
        {
            _lockActive = true;
            Interlocked.Exchange(ref _lockStartTick, Environment.TickCount64);
            Interlocked.Exchange(ref _openSamplesLive, 0);
            Interlocked.Exchange(ref _droppedAtLock, Interlocked.Read(ref _dropped));
            Interlocked.Exchange(ref _currentLockId, info.LockId);
            _speechSkipEligible = _speechSkipEnabled();
            _speechSkipDue = false;
            Post(new Item(Kind.Begin, Info: info));
        }

        public void End(string reason)
        {
            _lockActive = false;
            Post(new Item(Kind.End, Reason: reason));
        }

        public void InvalidateSpeechSkip() => _speechSkipEligible = false;

        /// <summary>DSP-thread entry point. Never blocks; drops the block (and counts it) when the queue is full.</summary>
        public void Enqueue(float[] block, bool squelchOpen)
        {
            if (!_lockActive) return;
            if (squelchOpen) Interlocked.Add(ref _openSamplesLive, block.Length);
            Interlocked.Increment(ref _pendingAudioBlocks);
            if (!_queue.TryAdd(new Item(Kind.Audio, (float[])block.Clone(), squelchOpen)))
            {
                Interlocked.Decrement(ref _pendingAudioBlocks);
                Interlocked.Increment(ref _dropped);
            }
        }

        /// <summary>Blocks until everything queued so far has been processed (used by tests and shutdown).</summary>
        public void Drain()
        {
            using var done = new ManualResetEventSlim();
            if (_queue.IsAddingCompleted) return;
            try { _queue.Add(new Item(Kind.Sync, Done: done)); } catch (InvalidOperationException) { return; }
            done.Wait(10_000);
        }

        private void Post(Item item)
        {
            try { _queue.Add(item); } catch (InvalidOperationException) { }
        }

        private void Run()
        {
            foreach (var item in _queue.GetConsumingEnumerable())
            {
                try
                {
                    switch (item.Kind)
                    {
                        case Kind.Begin:
                            if (_segmentOpen) CloseSegment("new-lock");
                            _info = item.Info;
                            _speechDetector = new SpeechSkipDetector(SampleRate);
                            _nextDiskRetry = DateTime.MinValue;
                            _trimLeft = (long)(LockTrimSec * SampleRate);
                            OpenSegment();
                            break;
                        case Kind.End:
                            CloseSegment(item.Reason ?? "released");
                            _info = null;
                            _speechDetector = null;
                            _speechSkipDue = false;
                            break;
                        case Kind.Audio:
                            try { HandleAudio(item.Audio!, item.SquelchOpen); }
                            finally { Interlocked.Decrement(ref _pendingAudioBlocks); }
                            break;
                        case Kind.Sync:
                            item.Done!.Set();
                            break;
                        case Kind.Quit:
                            CloseSegment("shutdown");
                            _info = null;
                            return;
                    }
                }
                catch (Exception ex)
                {
                    LastError = ex.Message;
                    System.Diagnostics.Debug.WriteLine($"ClipRecorder error: {ex.Message}");
                    try { _writer?.Dispose(); } catch { }
                    _writer = null;
                    _segmentOpen = false;
                }
            }
        }

        private void OpenSegment()
        {
            if (_info == null) return;
            if (DateTime.UtcNow < _nextDiskRetry) return;
            if (!DiskOk(out var why)) { LastError = why; _segmentOpen = false; _nextDiskRetry = DateTime.UtcNow.AddSeconds(5); return; }
            _segStart = DateTime.UtcNow;
            string dir = Path.Combine(_folder(), DateTime.Now.ToString("yyyy-MM-dd"));
            string name = $"{DateTime.Now:yyyyMMdd_HHmmss}_{_info.Hz / 1e6:000.0000}_{_info.Mode}_{_info.LockId}_{Guid.NewGuid():N}.wav";
            _writer = new WavClipWriter(Path.Combine(dir, name), SampleRate);
            LastError = null;
            _gapSamples = 0;
            _openSamples = 0;
            _pendingGap.Clear();
            _segmentOpen = true;
        }

        private void HandleAudio(float[] audio, bool squelchOpen)
        {
            if (_info == null) return;
            if (!_segmentOpen)
            {
                // A segment ended on a squelch gap; start a new one when the squelch opens again.
                if (!squelchOpen) return;
                OpenSegment();
                if (!_segmentOpen) return;
            }

            int start = 0;
            if (_trimLeft > 0)
            {
                int skip = (int)Math.Min(_trimLeft, audio.Length);
                _trimLeft -= skip;
                start = skip;
                if (start >= audio.Length) return;
            }
            var data = start == 0 ? audio : audio[start..];

            if (_speechSkipEligible && _speechSkipEnabled() && _speechDetector != null)
            {
                _speechDetector.Add(data);
                if (_info.LockId == Interlocked.Read(ref _currentLockId))
                    _speechSkipDue = _speechDetector.ShouldSkip(_speechSkipSeconds());
            }
            if (squelchOpen)
            {
                foreach (var pending in _pendingGap) _writer!.Write(pending);
                _pendingGap.Clear();
                _gapSamples = 0;
                _openSamples += data.Length;
            }
            else
            {
                _gapSamples += data.Length;
                if (_gapSamples > SquelchGapSec * SampleRate)
                {
                    CloseSegment("squelch-gap");
                    return;
                }
                _pendingGap.Add(data);
                return;
            }

            _writer!.Write(data);
            if (_writer.SampleCount >= (long)_maxClipSec() * SampleRate)
            {
                var info = _info;
                CloseSegment("max-length");
                _info = info;
                OpenSegment();
            }
        }

        private void CloseSegment(string reason)
        {
            if (!_segmentOpen || _writer == null || _info == null) { _segmentOpen = false; return; }
            _segmentOpen = false;
            _pendingGap.Clear();
            var w = _writer;
            _writer = null;
            long samples = w.SampleCount;
            w.Close();
            double dur = samples / (double)SampleRate;
            bool deleted = dur < MinClipSec;
            string path = w.Path;
            if (deleted)
            {
                try { File.Delete(path); } catch { }
            }
            ClipCompleted?.Invoke(new ClipResult(_info, path, _segStart, dur, _openSamples / (double)SampleRate, reason, deleted));
        }

        private bool DiskOk(out string? why)
        {
            why = null;
            try
            {
                string root = Path.GetFullPath(_folder());
                Directory.CreateDirectory(root);
                var drive = new DriveInfo(Path.GetPathRoot(root)!);
                double freeGb = drive.AvailableFreeSpace / 1073741824.0;
                double usedFrac = 1.0 - (double)drive.AvailableFreeSpace / drive.TotalSize;
                if (freeGb < _minFreeGb() || usedFrac > 0.90)
                {
                    why = $"Recording paused: disk low ({freeGb:F1} GB free)";
                    return false;
                }
            }
            catch (Exception ex)
            {
                why = ex.Message;
                return false;
            }
            return true;
        }

        public void Dispose()
        {
            if (_queue.IsAddingCompleted) return;
            try { _queue.Add(new Item(Kind.Quit)); } catch (InvalidOperationException) { }
            _queue.CompleteAdding();
            _thread.Join(5000);
        }
    }
}
