// ClipAnalyzer - cheap, LLM-free triage of a recorded clip: carrier / tone / noise / voice / uncertain.
// Uses an energy + spectral-shape voice-activity detector (no model files).
using NAudio.Wave;

namespace RandySDR
{
    public enum ClipClass { Carrier, Tone, Noise, Voice, Uncertain }

    public sealed record ClipMetrics(
        double DurationSec, double RmsDb, double SpeechSec, double SpeechRatio,
        double ToneRatio, double MeanFlatness, ClipClass Class);

    public static class ClipAnalyzer
    {
        public const double MinVoiceSpeechSec = 1.0;
        public const double MinVoiceRatio = 0.20;
        public const double CarrierMaxRatio = 0.05;
        private const int Frame = 1024; // ~21 ms at 48 kHz

        public static ClipMetrics AnalyzeFile(string wavPath, ClipAnalyzerOptions? options = null)
        {
            using var reader = new AudioFileReader(wavPath);
            var samples = new List<float>((int)reader.Length / 4);
            var buf = new float[4800];
            int n;
            while ((n = reader.Read(buf, 0, buf.Length)) > 0)
                for (int i = 0; i < n; i++) samples.Add(buf[i]);
            return Analyze(samples.ToArray(), reader.WaveFormat.SampleRate, options);
        }

        public static ClipMetrics Analyze(float[] x, int sampleRate, ClipAnalyzerOptions? options = null)
        {
            options ??= ClipAnalyzerOptions.Default;
            if (options.MinSpeechSeconds < 0 || options.MinSpeechRatio is < 0 or > 1 ||
                options.MaxCarrierRatio is < 0 or > 1 || options.MaxCarrierRatio > options.MinSpeechRatio)
                throw new ArgumentOutOfRangeException(nameof(options), "Invalid triage thresholds.");
            double dur = x.Length / (double)sampleRate;
            int frames = x.Length / Frame;
            if (frames < 3)
                return new ClipMetrics(dur, -120, 0, 0, 0, 0, ClipClass.Carrier);

            double total = 0;
            foreach (var s in x) total += (double)s * s;
            double rmsDb = 10 * Math.Log10(total / x.Length + 1e-12);

            var frameDb = new double[frames];
            var bandRatio = new double[frames];
            var flat = new double[frames];
            var toneDom = new double[frames];
            var re = new double[Frame];
            var im = new double[Frame];
            var window = new double[Frame];
            for (int i = 0; i < Frame; i++) window[i] = 0.5 - 0.5 * Math.Cos(2 * Math.PI * i / (Frame - 1));
            double binHz = sampleRate / (double)Frame;
            int lo = (int)(300 / binHz), hi = (int)(3400 / binHz), fLo = (int)(200 / binHz), fHi = (int)(4000 / binHz);

            for (int f = 0; f < frames; f++)
            {
                double e = 0;
                for (int i = 0; i < Frame; i++)
                {
                    double v = x[f * Frame + i];
                    e += v * v;
                    re[i] = v * window[i];
                    im[i] = 0;
                }
                frameDb[f] = 10 * Math.Log10(e / Frame + 1e-12);
                Fft(re, im);

                double all = 0, band = 0, logSum = 0, arith = 0, peak = 0;
                int cnt = 0, peakK = 0;
                for (int k = 1; k < Frame / 2; k++)
                {
                    double p = re[k] * re[k] + im[k] * im[k];
                    all += p;
                    if (k >= lo && k <= hi) band += p;
                    if (k >= fLo && k <= fHi)
                    {
                        logSum += Math.Log(p + 1e-18);
                        arith += p;
                        cnt++;
                        if (p > peak) { peak = p; peakK = k; }
                    }
                }
                // A windowed sine leaks into neighbouring bins, so count the peak with its two neighbours.
                if (peakK > 0)
                    foreach (int k in new[] { peakK - 1, peakK + 1 })
                        peak += re[k] * re[k] + im[k] * im[k];
                bandRatio[f] = all > 1e-12 ? band / all : 0;
                flat[f] = cnt > 0 && arith > 0 ? Math.Exp(logSum / cnt) / (arith / cnt) : 0;
                toneDom[f] = arith > 0 ? Math.Min(1, peak / arith) : 0;
            }

            var sorted = (double[])frameDb.Clone();
            Array.Sort(sorted);
            double floorDb = sorted[(int)(sorted.Length * 0.10)];
            double minActiveDb = Math.Max(Math.Min(floorDb + 6, rmsDb - 3), -55); // steady signals have no quiet floor

            int speech = 0, tone = 0;
            double flatSum = 0;
            for (int f = 0; f < frames; f++)
            {
                flatSum += flat[f];
                bool active = frameDb[f] >= minActiveDb;
                if (active && toneDom[f] > 0.5) tone++;
                else if (active && bandRatio[f] > 0.55 && flat[f] < 0.45 && toneDom[f] < 0.35) speech++;
            }

            double speechRatio = speech / (double)frames;
            double speechSec = speech * Frame / (double)sampleRate;
            double toneRatio = tone / (double)frames;
            double meanFlat = flatSum / frames;

            ClipClass cls;
            if (rmsDb < -60) cls = ClipClass.Carrier;
            else if (toneRatio >= 0.6) cls = ClipClass.Tone;
            else if (speechSec >= options.MinSpeechSeconds && speechRatio >= options.MinSpeechRatio) cls = ClipClass.Voice;
            else if (speechRatio < options.MaxCarrierRatio) cls = meanFlat > 0.3 && rmsDb > -45 ? ClipClass.Noise : ClipClass.Carrier;
            else cls = ClipClass.Uncertain;

            return new ClipMetrics(dur, rmsDb, speechSec, speechRatio, toneRatio, meanFlat, cls);
        }

        // In-place radix-2 FFT (length must be a power of two).
        private static void Fft(double[] re, double[] im)
        {
            int n = re.Length;
            for (int i = 1, j = 0; i < n; i++)
            {
                int bit = n >> 1;
                for (; (j & bit) != 0; bit >>= 1) j ^= bit;
                j ^= bit;
                if (i < j)
                {
                    (re[i], re[j]) = (re[j], re[i]);
                    (im[i], im[j]) = (im[j], im[i]);
                }
            }
            for (int len = 2; len <= n; len <<= 1)
            {
                double ang = -2 * Math.PI / len;
                double wr = Math.Cos(ang), wi = Math.Sin(ang);
                for (int i = 0; i < n; i += len)
                {
                    double cr = 1, ci = 0;
                    for (int k = 0; k < len / 2; k++)
                    {
                        int a = i + k, b = i + k + len / 2;
                        double tr = re[b] * cr - im[b] * ci;
                        double ti = re[b] * ci + im[b] * cr;
                        re[b] = re[a] - tr; im[b] = im[a] - ti;
                        re[a] += tr; im[a] += ti;
                        double ncr = cr * wr - ci * wi;
                        ci = cr * wi + ci * wr;
                        cr = ncr;
                    }
                }
            }
        }
    }
}
