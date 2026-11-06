namespace RandySDR
{
    /// <summary>
    /// Power measurements on one block of I/Q samples. Pure DSP, no UI or device access.
    /// Power is linear; index N/2 of the spectrum is the tuned centre frequency.
    /// </summary>
    public sealed class HopMeasurement
    {
        public const int FftSize = 4096;
        private const int Segments = 8;
        private const int DcMaskBins = 4;

        private readonly SpectrumAnalyzer _fft = new(FftSize) { Averaging = 0f };
        private readonly double[] _prefix = new double[FftSize + 1];
        private readonly float[] _power = new float[FftSize];

        public double BinHz { get; private set; }
        public int SampleRate { get; private set; }

        /// <summary>Analyse a raw I/Q buffer (averaging several FFT segments) and mask the RTL DC spike.</summary>
        public bool Measure(byte[] iq, int sampleRate)
        {
            int samples = iq.Length / 2;
            if (samples < FftSize)
                return false;

            SampleRate = sampleRate;
            BinHz = (double)sampleRate / FftSize;
            Array.Clear(_power);

            int segs = Math.Min(Segments, samples / FftSize);
            for (int s = 0; s < segs; s++)
            {
                var db = _fft.ProcessIQ(iq, s * FftSize);
                for (int k = 0; k < FftSize; k++)
                    _power[k] += MathF.Pow(10f, db[k] / 10f);
            }
            for (int k = 0; k < FftSize; k++)
                _power[k] /= segs;

            int c = FftSize / 2;
            float lo = _power[c - DcMaskBins - 1];
            float hi = _power[c + DcMaskBins + 1];
            for (int k = c - DcMaskBins; k <= c + DcMaskBins; k++)
                _power[k] = (lo + hi) / 2f;

            for (int k = 0; k < FftSize; k++)
                _prefix[k + 1] = _prefix[k] + _power[k];
            return true;
        }

        /// <summary>Mean linear power in a window centred at offsetHz from the tuned frequency.</summary>
        public double ChannelPower(double offsetHz, double widthHz)
        {
            int c = FftSize / 2;
            int k0 = c + (int)Math.Round((offsetHz - widthHz / 2) / BinHz);
            int k1 = c + (int)Math.Round((offsetHz + widthHz / 2) / BinHz);
            if (k1 <= k0) k1 = k0 + 1;
            k0 = Math.Clamp(k0, 0, FftSize - 1);
            k1 = Math.Clamp(k1, k0 + 1, FftSize);
            return (_prefix[k1] - _prefix[k0]) / (k1 - k0);
        }

        /// <summary>
        /// Power of a channel sitting exactly on the tuned frequency, skipping the DC-masked bins and using at least ~7 kHz each side.
        /// Used while listening to a signal, when it is centred on the DC spike.
        /// </summary>
        public double CentredChannelPower(double widthHz)
        {
            int c = FftSize / 2;
            int half = Math.Max((int)Math.Round(widthHz / 2 / BinHz), 12);
            half = Math.Min(half, c - 1);
            double sum = (_prefix[c - DcMaskBins] - _prefix[c - half])
                       + (_prefix[c + half + 1] - _prefix[c + DcMaskBins + 1]);
            return sum / (2 * (half - DcMaskBins));
        }

        /// <summary>Noise-floor estimate (dB): 25th percentile of block-mean power across the central 80% of the band.</summary>
        public double NoiseFloorDb(double blockHz)
        {
            blockHz = Math.Max(blockHz, BinHz * 4);
            double usable = SampleRate * 0.8;
            var values = new List<double>();
            for (double off = -usable / 2 + blockHz / 2; off <= usable / 2 - blockHz / 2; off += blockHz)
                values.Add(ToDb(ChannelPower(off, blockHz)));
            if (values.Count == 0)
                return ToDb(ChannelPower(0, usable));
            values.Sort();
            return values[values.Count / 4];
        }

        /// <summary>
        /// Robust spread of channel-width blocks across the usable band, in dB relative to floorDb:
        /// the median and a MAD-based sigma, so a few occupied channels do not skew the result.
        /// </summary>
        public (double MedianDb, double SigmaDb, int Count) NoiseSpread(double blockHz, double floorDb)
        {
            blockHz = Math.Max(blockHz, BinHz * 4);
            double usable = SampleRate * 0.8;
            var values = new List<double>();
            for (double off = -usable / 2 + blockHz / 2; off <= usable / 2 - blockHz / 2; off += blockHz)
                values.Add(ToDb(ChannelPower(off, blockHz)) - floorDb);
            if (values.Count < 8)
                return (0, 0, values.Count);

            values.Sort();
            double median = values[values.Count / 2];
            var dev = values.Select(v => Math.Abs(v - median)).OrderBy(v => v).ToList();
            double sigma = 1.4826 * dev[dev.Count / 2];
            return (median, sigma, values.Count);
        }

        public static double ToDb(double power) => 10 * Math.Log10(power + 1e-12);
    }
}
