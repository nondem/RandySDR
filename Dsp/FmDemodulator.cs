namespace RandySDR
{
    /// <summary>Converts raw RTL-SDR 8-bit I/Q bytes into 48 kHz mono float audio for the selected DemodulationMode.</summary>
    public class FmDemodulator
    {
        private float _lastI = 0;
        private float _lastQ = 0;
        private readonly int _sampleRate;
        private readonly int _audioSampleRate;
        private readonly int _decimation;
        private readonly ComplexLowPassFilter _ifLowPass;
        private readonly DcBlocker _dcBlocker;
        private readonly LowPassFilter _audioFilter;
        private readonly LowPassFilter _narrowAudioFilter;
        private readonly LowPassFilter _amAudioFilter;
        private readonly LowPassFilter _ssbAudioFilter;
        private readonly LowPassFilter _cwLowPass;
        private readonly DeEmphasisFilter _deEmphasis;
        private readonly RealHighPassFilter _amHighPass;
        private readonly RealHighPassFilter _ssbHighPass;
        private readonly RealHighPassFilter _cwHighPass;
        private bool _haveLastSample = false;
        private readonly int _stage1Decim;
        private readonly double _stage2Ratio;
        private readonly float[] _firTaps;
        private readonly float[] _histI;
        private readonly float[] _histQ;
        private int _histPos;
        private int _stage1Counter;
        private double _stage2Phase;
        private float _avgFm, _avgI, _avgQ;
        private readonly ComplexButterworth4 _nfmChannel, _amChannel, _ssbChannel, _ssbWeaverLp, _cwChannel;
        private readonly AudioAgc _ssbAgc, _cwAgc;
        private float _nfmLastI, _nfmLastQ, _amAvg;
        private int _amSeed;
        private double _ssbPhase, _cwPhase;
        private int _avgCount;

        // Configurable parameters
        public float Volume { get; set; } = 1.0f;
        public bool DeEmphasisEnabled { get; set; } = true;
        public bool SwapIQ { get; set; } = false;
        public bool InvertDemod { get; set; } = false;
        private DemodulationMode _mode = DemodulationMode.WideFm;
        public DemodulationMode Mode
        {
            get => _mode;
            set
            {
                if (_mode == value) return;
                _mode = value;
                _amAvg = 0; // re-seed the AM carrier tracker
            }
        }

        private float _signalLevel = 0;
        public float SignalLevel => _signalLevel;

        public FmDemodulator(int sampleRate, int audioSampleRate = 48000)
        {
            _sampleRate = sampleRate;
            _audioSampleRate = audioSampleRate;
            _decimation = Math.Max(1, sampleRate / audioSampleRate);

            _stage1Decim = Math.Max(1, (int)Math.Round(sampleRate / 250000.0));
            double interRate = sampleRate / (double)_stage1Decim;
            _stage2Ratio = Math.Max(1.0, interRate / audioSampleRate);
            int taps = _stage1Decim * 6 + 1;
            _firTaps = new float[taps];
            double fc = Math.Min(100000.0, interRate * 0.45) / sampleRate;
            double sum = 0;
            for (int n = 0; n < taps; n++)
            {
                double m = n - (taps - 1) / 2.0;
                double sinc = Math.Abs(m) < 1e-9 ? 2 * fc : Math.Sin(2 * Math.PI * fc * m) / (Math.PI * m);
                double w = 0.54 - 0.46 * Math.Cos(2 * Math.PI * n / (taps - 1));
                _firTaps[n] = (float)(sinc * w);
                sum += _firTaps[n];
            }
            for (int n = 0; n < taps; n++) _firTaps[n] /= (float)sum;
            _histI = new float[taps];
            _histQ = new float[taps];

            _dcBlocker = new DcBlocker(0.001f);
            _ifLowPass = new ComplexLowPassFilter(sampleRate, 180000); // 180 kHz baseband filter for WBFM
            _audioFilter = new LowPassFilter(audioSampleRate, 15000);
            _narrowAudioFilter = new LowPassFilter(audioSampleRate, 4000);
            _amAudioFilter = new LowPassFilter(audioSampleRate, 5000);
            _ssbAudioFilter = new LowPassFilter(audioSampleRate, 2800);
            _cwLowPass = new LowPassFilter(audioSampleRate, 900);
            _deEmphasis = new DeEmphasisFilter(audioSampleRate, 75);
            _amHighPass = new RealHighPassFilter(audioSampleRate, 40);
            _ssbHighPass = new RealHighPassFilter(audioSampleRate, 250);
            _cwHighPass = new RealHighPassFilter(audioSampleRate, 350);
            _nfmChannel = new ComplexButterworth4(audioSampleRate, 8000);
            _amChannel = new ComplexButterworth4(audioSampleRate, 6000);
            _ssbChannel = new ComplexButterworth4(audioSampleRate, 20000); // anti-alias only; Weaver LP does the selection
            _ssbWeaverLp = new ComplexButterworth4(audioSampleRate, 1400);
            _cwChannel = new ComplexButterworth4(audioSampleRate, 250);
            _ssbAgc = new AudioAgc(audioSampleRate);
            _cwAgc = new AudioAgc(audioSampleRate, 0.4f);

            System.Diagnostics.Debug.WriteLine($"FM Demod: SampleRate={sampleRate}, AudioRate={audioSampleRate}, Decimation={_decimation}");
            System.Diagnostics.Debug.WriteLine($"  IF Lowpass: 180 kHz");
            System.Diagnostics.Debug.WriteLine($"  Decimation Filter: {sampleRate}/{audioSampleRate}x with 15 kHz cutoff");
            System.Diagnostics.Debug.WriteLine($"  Audio Filter: 15 kHz lowpass");
        }

        private volatile bool _resetRequested;

        // ---- Squelch -------------------------------------------------------------------------
        // Slider 0 = off; 1..100 maps linearly onto SquelchMinDb..SquelchMaxDb (dBFS of in-channel I/Q power).
        public const float SquelchMinDb = -80f;
        public const float SquelchMaxDb = -5f;
        private const float SquelchHysteresisDb = 2f;
        private const int SquelchHangSamples = 12000; // 250 ms at 48 kHz
        private const float SquelchAttackStep = 1f / 240f; // 5 ms
        private const float SquelchReleaseStep = 1f / 960f; // 20 ms

        public static float SquelchPercentToDb(int percent) => SquelchMinDb + percent * (SquelchMaxDb - SquelchMinDb) / 100f;
        public static int DbToSquelchPercent(float db) => (int)Math.Clamp(Math.Round((db - SquelchMinDb) * 100f / (SquelchMaxDb - SquelchMinDb)), 0, 100);

        private volatile float _squelchThresholdDb = float.NaN;
        /// <summary>Squelch threshold in dBFS; null disables squelch.</summary>
        public float? SquelchThresholdDb
        {
            get
            {
                float value = _squelchThresholdDb;
                return float.IsNaN(value) ? null : value;
            }
            set => _squelchThresholdDb = value ?? float.NaN;
        }
        /// <summary>Smoothed in-channel signal level in dBFS (what the squelch compares against).</summary>
        public float ChannelDb => _channelDb;
        public bool SquelchOpen => _sqOpen;

        private volatile float _channelDb = -120f;
        private float _chanPower, _sqGain;
        private bool _sqOpen;
        private int _sqHang;

        private float UpdateSquelch(float i, float q)
        {
            _chanPower += 0.001f * ((i * i + q * q) - _chanPower);
            float db = 10f * MathF.Log10(_chanPower + 1e-12f);
            _channelDb = db;

            if (SquelchThresholdDb is not float thr)
            {
                _sqOpen = true;
                _sqGain = 1f;
                return 1f;
            }

            if (db >= thr)
            {
                _sqOpen = true;
                _sqHang = SquelchHangSamples;
            }
            else if (_sqOpen && db < thr - SquelchHysteresisDb)
            {
                if (_sqHang > 0) _sqHang--;
                else _sqOpen = false;
            }

            _sqGain = _sqOpen
                ? Math.Min(1f, _sqGain + SquelchAttackStep)
                : Math.Max(0f, _sqGain - SquelchReleaseStep);
            return _sqGain;
        }

        /// <summary>Clear history/phase/AM-level state on the next buffer (safe to call from any thread).</summary>
        public void RequestReset() => _resetRequested = true;

        private void ResetTransients()
        {
            Array.Clear(_histI);
            Array.Clear(_histQ);
            _stage1Counter = 0;
            _stage2Phase = 0;
            _avgFm = _avgI = _avgQ = 0;
            _avgCount = 0;
            _haveLastSample = false;
            _nfmLastI = _nfmLastQ = 0;
            _amAvg = 0;
            _chanPower = 0;
            _sqOpen = false;
            _sqGain = 0;
            _sqHang = 0;
        }

        public float[] Demodulate(byte[] iqData)
        {
            if (_resetRequested)
            {
                _resetRequested = false;
                ResetTransients();
            }
            List<float> audioSamples = new List<float>();
            float rawPowerSum = 0;
            int signalCount = 0;

            // Process IQ samples (2 bytes per sample: I and Q)
            for (int i = 0; i < iqData.Length - 1; i += 2)
            {
                // Convert unsigned bytes (0-255) to float (-1 to 1)
                float I, Q;

                if (SwapIQ)
                {
                    // Swapped I/Q order
                    Q = (iqData[i] - 127.5f) / 127.5f;
                    I = (iqData[i + 1] - 127.5f) / 127.5f;
                }
                else
                {
                    // Standard I/Q order
                    I = (iqData[i] - 127.5f) / 127.5f;
                    Q = (iqData[i + 1] - 127.5f) / 127.5f;
                }

                // Use raw IQ power for a more stable signal meter reference.
                rawPowerSum += (I * I + Q * Q);
                signalCount++;

                // AM's carrier sits at 0 Hz when tuned on-channel; a DC blocker would remove it and
                // turn the envelope detector into a distorting full-wave rectifier of the sidebands.
                if (Mode != DemodulationMode.Am)
                    (I, Q) = _dcBlocker.Process(I, Q);
                // Stage 1: FIR low-pass + decimate to ~250 kHz intermediate rate
                _histI[_histPos] = I;
                _histQ[_histPos] = Q;
                _histPos = (_histPos + 1) % _firTaps.Length;
                if (++_stage1Counter < _stage1Decim)
                    continue;
                _stage1Counter = 0;

                float accI = 0, accQ = 0;
                int idx = _histPos;
                for (int t = 0; t < _firTaps.Length; t++)
                {
                    float h = _firTaps[t];
                    accI += _histI[idx] * h;
                    accQ += _histQ[idx] * h;
                    idx = (idx + 1) % _firTaps.Length;
                }
                I = accI;
                Q = accQ;

                // FM demodulation using phase discriminator
                float fmDemod = 0f;
                if (_haveLastSample)
                {
                    fmDemod = (float)Math.Atan2(Q * _lastI - I * _lastQ, I * _lastI + Q * _lastQ);
                }
                else
                {
                    _haveLastSample = true;
                }

                if (InvertDemod)
                {
                    fmDemod = -fmDemod;
                }

                _lastI = I;
                _lastQ = Q;

                // Stage 2: average over the audio window (fractional ratio) before the audio filters
                _avgFm += fmDemod;
                _avgI += I;
                _avgQ += Q;
                _avgCount++;
                _stage2Phase += 1.0;
                if (_stage2Phase >= _stage2Ratio)
                {
                    _stage2Phase -= _stage2Ratio;
                    fmDemod = _avgFm / _avgCount;
                    I = _avgI / _avgCount;
                    Q = _avgQ / _avgCount;
                    _avgFm = _avgI = _avgQ = 0;
                    _avgCount = 0;

                    // Per-mode channel selection at the audio rate (WFM/Raw bypass)
                    switch (Mode)
                    {
                        case DemodulationMode.NarrowFm: (I, Q) = _nfmChannel.Process(I, Q); break;
                        case DemodulationMode.Am: (I, Q) = _amChannel.Process(I, Q); break;
                        case DemodulationMode.Usb:
                        case DemodulationMode.Lsb: (I, Q) = _ssbChannel.Process(I, Q); break;
                        case DemodulationMode.Cw: (I, Q) = _cwChannel.Process(I, Q); break;
                    }

                    float filtered = Mode switch
                    {
                        DemodulationMode.WideFm => ProcessWideFm(fmDemod),
                        DemodulationMode.NarrowFm => ProcessNarrowFm(I, Q),
                        DemodulationMode.Am => ProcessAm(I, Q),
                        DemodulationMode.Usb => ProcessUsb(I, Q),
                        DemodulationMode.Lsb => ProcessLsb(I, Q),
                        DemodulationMode.Raw => ProcessRaw(I),
                        DemodulationMode.Cw => ProcessCw(I, Q),
                        _ => ProcessWideFm(fmDemod)
                    };

                    // Squelch: gate on in-channel power (not wideband power) with hysteresis, hang time and a soft ramp
                    filtered *= Volume * UpdateSquelch(I, Q);

                    // CRITICAL FIX #4: Hard-clamp to prevent digital clipping/overdrive
                    // Ensure output never exceeds [-1.0, +1.0] range for float audio
                    if (filtered > 1.0f)
                        filtered = 1.0f;
                    else if (filtered < -1.0f)
                        filtered = -1.0f;

                    audioSamples.Add(filtered);
                }
            }

            // Update signal level (smoothed and normalized)
            if (signalCount > 0)
            {
                float rms = (float)Math.Sqrt(rawPowerSum / signalCount);

                // Empirical normalization for RTL-SDR 8-bit IQ.
                float normalized = (rms - 0.18f) / 0.55f;
                normalized = Math.Clamp(normalized, 0f, 1f);

                _signalLevel = _signalLevel * 0.90f + normalized * 0.10f;
            }

            return audioSamples.ToArray();
        }

        private float ProcessWideFm(float fmDemod)
        {
            float y = _audioFilter.Process(fmDemod);
            if (DeEmphasisEnabled)
            {
                y = _deEmphasis.Process(y);
            }
            return y;
        }

        private float ProcessNarrowFm(float i, float q)
        {
            float d = (float)Math.Atan2(q * _nfmLastI - i * _nfmLastQ, i * _nfmLastI + q * _nfmLastQ);
            _nfmLastI = i;
            _nfmLastQ = q;
            if (InvertDemod) d = -d;
            // 5 kHz deviation at 48 kHz = ~0.65 rad/sample
            return _narrowAudioFilter.Process(d * 1.4f);
        }

        // Weaver-style single-sideband demodulation: shift the wanted sideband around DC,
        // channel filter has already been applied around the shifted centre.
        private float ProcessSsb(float i, float q, bool upper)
        {
            const double shiftHz = 1500.0;
            double dphi = 2 * Math.PI * shiftHz / _audioSampleRate;
            _ssbPhase += dphi;
            if (_ssbPhase > Math.PI * 2) _ssbPhase -= Math.PI * 2;
            float c = (float)Math.Cos(_ssbPhase), s = (float)Math.Sin(_ssbPhase);
            // USB: spectrum at +0..3k -> shift down by 1.5k, filter, shift back up and take real part.
            // The channel filter is symmetric around DC so it is applied to the shifted signal.
            float sign = upper ? 1f : -1f;
            // shift down: x * e^{-j sign w t}
            float si = s * sign;
            float di = i * c + q * si;
            float dq = q * c - i * si;
            (di, dq) = _ssbWeaverLp.Process(di, dq);
            // shift back up and take real part
            float audio = di * c - dq * si;
            audio = _ssbHighPass.Process(audio);
            return _ssbAgc.Process(audio);
        }

        private float ProcessUsb(float i, float q) => ProcessSsb(i, q, true);

        private float ProcessAm(float i, float q)
        {
            float env = (float)Math.Sqrt((i * i) + (q * q));
            // Fast attack while seeding / after retune, slow tracking once settled
            float rate = _amSeed > 0 ? 0.01f : 0.0005f;
            if (_amSeed > 0) _amSeed--;
            if (_amAvg == 0) { _amAvg = env; _amSeed = 2400; }
            _amAvg += rate * (env - _amAvg);
            float norm = Math.Max(_amAvg, 0.002f);
            float audio = (env - _amAvg) / norm;
            audio = _amHighPass.Process(audio);
            return _amAudioFilter.Process(audio * 0.7f);
        }

        private float ProcessLsb(float i, float q) => ProcessSsb(i, q, false);

        private static float ProcessRaw(float i)
        {
            return i;
        }

        private float ProcessCw(float i, float q)
        {
            const double toneHz = 700.0;
            _cwPhase += 2 * Math.PI * toneHz / _audioSampleRate;
            if (_cwPhase > Math.PI * 2) _cwPhase -= Math.PI * 2;
            float tone = i * (float)Math.Cos(_cwPhase) - q * (float)Math.Sin(_cwPhase);
            return _cwAgc.Process(tone);
        }
    }
}
