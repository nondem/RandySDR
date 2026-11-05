namespace RandySDR
{
    /// <summary>
    /// FFT-based spectrum analyzer for complex 8-bit IQ data.
    /// Output is a centered (fft-shifted) power spectrum in dBFS: index N/2 is the tuned center frequency.
    /// </summary>
    public class SpectrumAnalyzer
    {
        private readonly int _fftSize;
        private readonly float[] _window;
        private readonly float[] _re;
        private readonly float[] _im;
        private readonly float[] _spectrumDb;
        private readonly float _windowGain;
        private bool _primed;

        // Smoothing between frames (0 = none, closer to 1 = slower)
        public float Averaging { get; set; } = 0.55f;

        public int FftSize => _fftSize;

        public SpectrumAnalyzer(int fftSize = 2048)
        {
            if (fftSize < 64 || (fftSize & (fftSize - 1)) != 0)
                throw new ArgumentException("FFT size must be a power of two >= 64.", nameof(fftSize));

            _fftSize = fftSize;
            _window = new float[fftSize];
            _re = new float[fftSize];
            _im = new float[fftSize];
            _spectrumDb = new float[fftSize];

            // Blackman-Harris window: very low leakage, good for seeing weak signals near strong ones
            double sum = 0;
            for (int i = 0; i < fftSize; i++)
            {
                double x = 2.0 * Math.PI * i / (fftSize - 1);
                double w = 0.35875 - 0.48829 * Math.Cos(x) + 0.14128 * Math.Cos(2 * x) - 0.01168 * Math.Cos(3 * x);
                _window[i] = (float)w;
                sum += w;
            }
            _windowGain = (float)sum;

            for (int i = 0; i < fftSize; i++)
                _spectrumDb[i] = -120f;
        }

        public float[] ProcessIQ(byte[] iqData) => ProcessIQ(iqData, 0);

        /// <summary>Same as ProcessIQ(byte[]) but analyses the FFT block starting at the given complex-sample offset.</summary>
        public float[] ProcessIQ(byte[] iqData, int offsetSamples)
        {
            if (iqData == null || iqData.Length < (offsetSamples + _fftSize) * 2)
                return _spectrumDb;

            int o = offsetSamples * 2;
            for (int i = 0; i < _fftSize; i++)
            {
                float w = _window[i];
                _re[i] = (iqData[o + i * 2] - 127.5f) / 127.5f * w;
                _im[i] = (iqData[o + i * 2 + 1] - 127.5f) / 127.5f * w;
            }

            Fft(_re, _im);

            int half = _fftSize / 2;
            float a = _primed ? Averaging : 0f;
            for (int k = 0; k < _fftSize; k++)
            {
                // fft-shift: negative frequencies first, center at index N/2
                int src = (k + half) % _fftSize;
                float mag = MathF.Sqrt(_re[src] * _re[src] + _im[src] * _im[src]) / _windowGain * 2f;
                float db = 20f * MathF.Log10(mag + 1e-9f);
                _spectrumDb[k] = a * _spectrumDb[k] + (1f - a) * db;
            }
            _primed = true;

            return _spectrumDb;
        }

        public void Reset() => _primed = false;

        /// <summary>Strongest bin within +/- searchHz of the center, as a bin index.</summary>
        public int FindPeakBin(int sampleRate = 0, int searchHz = 100_000)
        {
            int center = _fftSize / 2;
            int span = sampleRate > 0 ? (int)((double)searchHz / sampleRate * _fftSize) : _fftSize / 2 - 1;
            span = Math.Clamp(span, 1, center - 1);

            int peak = center;
            float best = float.MinValue;
            for (int i = center - span; i <= center + span; i++)
            {
                if (_spectrumDb[i] > best)
                {
                    best = _spectrumDb[i];
                    peak = i;
                }
            }
            return peak;
        }

        /// <summary>Offset in Hz of the strongest signal within +/-100 kHz of the tuned center.</summary>
        public float GetFrequencyOffset(int sampleRate)
        {
            int peak = FindPeakBin(sampleRate);
            return (peak - _fftSize / 2) * ((float)sampleRate / _fftSize);
        }

        // In-place iterative radix-2 FFT
        private static void Fft(float[] re, float[] im)
        {
            int n = re.Length;

            for (int i = 1, j = 0; i < n; i++)
            {
                int bit = n >> 1;
                for (; (j & bit) != 0; bit >>= 1)
                    j ^= bit;
                j ^= bit;
                if (i < j)
                {
                    (re[i], re[j]) = (re[j], re[i]);
                    (im[i], im[j]) = (im[j], im[i]);
                }
            }

            for (int len = 2; len <= n; len <<= 1)
            {
                double ang = -2.0 * Math.PI / len;
                float wlenR = (float)Math.Cos(ang);
                float wlenI = (float)Math.Sin(ang);
                for (int i = 0; i < n; i += len)
                {
                    float wr = 1f, wi = 0f;
                    int halfLen = len >> 1;
                    for (int k = 0; k < halfLen; k++)
                    {
                        int u = i + k, v = i + k + halfLen;
                        float xr = re[v] * wr - im[v] * wi;
                        float xi = re[v] * wi + im[v] * wr;
                        re[v] = re[u] - xr;
                        im[v] = im[u] - xi;
                        re[u] += xr;
                        im[u] += xi;
                        float nwr = wr * wlenR - wi * wlenI;
                        wi = wr * wlenI + wi * wlenR;
                        wr = nwr;
                    }
                }
            }
        }
    }
}
