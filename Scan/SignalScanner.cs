using System.Diagnostics;

namespace RandySDR
{
    public enum ScanState { Idle, Scanning, Locked }

    /// <summary>
    /// Frequency scanner state machine. Call <see cref="Tick"/> from the UI thread every ~20 ms.
    /// It never touches the device directly: it asks for retunes via the tune callback and reads
    /// I/Q from the running <see cref="ReceiverPipeline"/>.
    /// </summary>
    public sealed class SignalScanner
    {
        private const double HysteresisDb = 2.0;
        private const double DcOffsetHz = 300_000;
        private const double MinUsableFraction = 0.8;

        private readonly ScanSettings _s;
        private readonly Action<uint> _tune;
        private readonly Func<ReceiverPipeline?> _pipeline;
        private readonly Func<int> _sampleRate;
        private readonly HopMeasurement _meas = new();
        private readonly Stopwatch _clock = Stopwatch.StartNew();

        private sealed record Hop(double CenterHz, int[] Channels);

        private enum Phase { Off, SettlingHop, SettlingLock, Locked }

        private Phase _phase = Phase.Off;
        private List<Hop> _hops = new();
        private double[] _channelHz = Array.Empty<double>();
        private bool[] _armed = Array.Empty<bool>();
        private int _hopIndex;
        private int _tuneBuffers;
        private long _tuneTimeMs;
        private int _lastSeenBuffers;
        private int _lockedChannel = -1;
        private long _lockStartMs;
        private long _lastPresentMs;
        private double[] _usedStep = Array.Empty<double>();

        public ScanState State => _phase == Phase.Off ? ScanState.Idle
            : (_phase == Phase.Locked || _phase == Phase.SettlingLock) ? ScanState.Locked : ScanState.Scanning;

        /// <summary>Strength (dB above floor) per channel from the latest measurement; NaN if not yet measured.</summary>
        public double[] Strength { get; private set; } = Array.Empty<double>();
        public IReadOnlyList<double> ChannelHz => _channelHz;
        public double NoiseFloorDb { get; private set; } = double.NaN;
        public double CurrentHz { get; private set; }
        public double LockedStrengthDb { get; private set; }
        public int SweepCount { get; private set; }
        public double LockedHz => _lockedChannel >= 0 ? _channelHz[_lockedChannel] : 0;

        /// <summary>A new signal was found and the receiver is now tuned to it: (Hz, dB above floor).</summary>
        public event Action<double, double>? SignalLocked;
        /// <summary>Scanning resumes after a lock: (Hz).</summary>
        public event Action<double>? SignalReleased;
        /// <summary>Strength array, floor or state changed; redraw status/graph.</summary>
        public event Action? Updated;

        public SignalScanner(ScanSettings settings, Action<uint> tune, Func<ReceiverPipeline?> pipeline, Func<int> sampleRate)
        {
            _s = settings;
            _tune = tune;
            _pipeline = pipeline;
            _sampleRate = sampleRate;
        }

        public bool Start(out string error)
        {
            error = "";
            if (_pipeline() == null)
            {
                error = "Start streaming before scanning.";
                return false;
            }
            if (_s.StopMHz <= _s.StartMHz || _s.StepKHz <= 0)
            {
                error = "Stop frequency must be above start, and step must be positive.";
                return false;
            }

            BuildPlan();
            if (_channelHz.Length == 0)
            {
                error = "No channels in range.";
                return false;
            }

            SweepCount = 0;
            _hopIndex = 0;
            _lockedChannel = -1;
            BeginHop();
            return true;
        }

        public void Stop()
        {
            _phase = Phase.Off;
            _lockedChannel = -1;
            Updated?.Invoke();
        }

        /// <summary>Leave the locked signal and continue scanning (the channel is ignored until it drops below threshold).</summary>
        public void Resume()
        {
            if (_phase != Phase.Locked && _phase != Phase.SettlingLock) return;
            int ch = _lockedChannel;
            double hz = _channelHz[ch];
            _armed[ch] = false;
            _lockedChannel = -1;
            SignalReleased?.Invoke(hz);
            BeginHop(); // re-measure the same hop: other channels in it may also be active
        }

        private void BuildPlan()
        {
            double stepHz = _s.StepKHz * 1000;
            double startHz = _s.StartMHz * 1e6;
            int n = (int)Math.Floor((_s.StopMHz * 1e6 - startHz) / stepHz + 1e-6) + 1;
            _channelHz = Enumerable.Range(0, n).Select(i => startHz + i * stepHz).ToArray();
            _armed = Enumerable.Repeat(true, n).ToArray();
            Strength = Enumerable.Repeat(double.NaN, n).ToArray();
            _usedStep = new[] { stepHz };

            double usable = _sampleRate() * MinUsableFraction;
            _hops = new List<Hop>();

            if (_s.Method == ScanMethod.TuneEachChannel || stepHz > usable)
            {
                double off = Math.Min(DcOffsetHz, _sampleRate() / 4.0);
                if (_s.Method == ScanMethod.WidebandFft) off = 0;
                for (int i = 0; i < n; i++)
                    _hops.Add(new Hop(_channelHz[i] + off, new[] { i }));
                return;
            }

            int idx = 0;
            while (idx < n)
            {
                double center = _channelHz[idx] - stepHz / 2 + usable / 2;
                var list = new List<int>();
                for (int j = idx; j < n && Math.Abs(_channelHz[j] - center) <= usable / 2 - stepHz / 2 + 1; j++)
                    list.Add(j);
                if (list.Count == 0) list.Add(idx);
                _hops.Add(new Hop(center, list.ToArray()));
                idx += list.Count;
            }
        }

        private void BeginHop()
        {
            var hop = _hops[_hopIndex];
            CurrentHz = hop.CenterHz;
            IssueTune(hop.CenterHz);
            _phase = Phase.SettlingHop;
            Updated?.Invoke();
        }

        private void IssueTune(double hz)
        {
            var p = _pipeline();
            _tuneBuffers = p?.BuffersProcessed ?? 0;
            _lastSeenBuffers = _tuneBuffers;
            _tuneTimeMs = _clock.ElapsedMilliseconds;
            _tune((uint)Math.Round(hz));
        }

        public void Tick()
        {
            if (_phase == Phase.Off) return;

            var p = _pipeline();
            if (p == null) { Stop(); return; }

            long now = _clock.ElapsedMilliseconds;
            bool settled = p.BuffersProcessed >= _tuneBuffers + 2 && now - _tuneTimeMs >= _s.SettleMs;

            switch (_phase)
            {
                case Phase.SettlingHop:
                    if (settled) MeasureHop(p);
                    break;
                case Phase.SettlingLock:
                    if (settled)
                    {
                        _phase = Phase.Locked;
                        _lockStartMs = _lastPresentMs = now;
                    }
                    break;
                case Phase.Locked:
                    if (p.BuffersProcessed != _lastSeenBuffers)
                    {
                        _lastSeenBuffers = p.BuffersProcessed;
                        MeasureLocked(p, now);
                    }
                    break;
            }
        }

        private void MeasureHop(ReceiverPipeline p)
        {
            var iq = p.LastIqData;
            if (iq == null || !_meas.Measure(iq, _sampleRate()))
                return;

            var hop = _hops[_hopIndex];
            double stepHz = _usedStep[0];
            double floor = _meas.NoiseFloorDb(Math.Clamp(stepHz, 25_000, 100_000));
            NoiseFloorDb = floor;

            int best = -1;
            double bestDb = double.NegativeInfinity;
            foreach (int ch in hop.Channels)
            {
                double db = HopMeasurement.ToDb(_meas.ChannelPower(_channelHz[ch] - hop.CenterHz, stepHz)) - floor;
                Strength[ch] = db;

                if (db < _s.ThresholdDb - HysteresisDb)
                    _armed[ch] = true;

                if (db >= _s.ThresholdDb && _armed[ch] && !IsLockedOut(_channelHz[ch]) && db > bestDb)
                {
                    best = ch;
                    bestDb = db;
                }
            }
            Updated?.Invoke();

            if (best >= 0)
            {
                Lock(best, bestDb);
                return;
            }

            _hopIndex++;
            if (_hopIndex >= _hops.Count)
            {
                _hopIndex = 0;
                SweepCount++;
            }
            BeginHop();
        }

        private void Lock(int ch, double db)
        {
            _lockedChannel = ch;
            LockedStrengthDb = db;
            CurrentHz = _channelHz[ch];
            IssueTune(CurrentHz);
            _phase = Phase.SettlingLock;
            SignalLocked?.Invoke(CurrentHz, db);
            Updated?.Invoke();
        }

        private void MeasureLocked(ReceiverPipeline p, long now)
        {
            var iq = p.LastIqData;
            if (iq == null || !_meas.Measure(iq, _sampleRate()))
                return;

            double stepHz = _usedStep[0];
            double floor = _meas.NoiseFloorDb(Math.Clamp(stepHz, 25_000, 100_000));
            NoiseFloorDb = floor;
            double db = HopMeasurement.ToDb(_meas.CentredChannelPower(stepHz)) - floor;
            LockedStrengthDb = db;
            Strength[_lockedChannel] = db;

            if (db >= _s.ThresholdDb - HysteresisDb)
                _lastPresentMs = now;

            bool release = _s.Hold switch
            {
                HoldMode.UntilSignalDrops => now - _lastPresentMs >= _s.HoldSeconds * 1000,
                HoldMode.FixedTime => now - _lockStartMs >= _s.HoldSeconds * 1000,
                _ => false
            };

            Updated?.Invoke();
            if (release) Resume();
        }

        private bool IsLockedOut(double hz)
        {
            double tol = Math.Max(_usedStep[0] / 2, 5_000);
            foreach (double mhz in _s.LockoutsMHz)
                if (Math.Abs(mhz * 1e6 - hz) < tol) return true;
            return false;
        }
    }
}
