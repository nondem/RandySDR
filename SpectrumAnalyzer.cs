namespace RandySDR
{
    public class SpectrumAnalyzer
    {
        private readonly int _fftSize;
        private readonly float[] _window;
        private readonly float[] _magnitude;
        private System.Numerics.Complex[] _fftBuffer;

        public SpectrumAnalyzer(int fftSize = 2048)
        {
            _fftSize = fftSize;
            _window = new float[fftSize];
            _magnitude = new float[fftSize / 2];
            _fftBuffer = new System.Numerics.Complex[fftSize];

            // Create Hamming window
            for (int i = 0; i < fftSize; i++)
            {
                _window[i] = 0.54f - 0.46f * (float)Math.Cos(2.0 * Math.PI * i / (fftSize - 1));
            }
        }

        public float[] ProcessIQ(byte[] iqData)
        {
            if (iqData.Length < _fftSize * 2)
                return _magnitude;

            // Convert IQ bytes to complex, apply window
            for (int i = 0; i < _fftSize; i++)
            {
                float I = (iqData[i * 2] - 127.5f) / 127.5f;
                float Q = (iqData[i * 2 + 1] - 127.5f) / 127.5f;
                
                _fftBuffer[i] = new System.Numerics.Complex(I * _window[i], Q * _window[i]);
            }

            // Simple DFT for lower half (we only need positive frequencies for display)
            for (int k = 0; k < _fftSize / 2; k++)
            {
                System.Numerics.Complex sum = System.Numerics.Complex.Zero;
                for (int n = 0; n < _fftSize; n++)
                {
                    double angle = -2.0 * Math.PI * k * n / _fftSize;
                    var exp = new System.Numerics.Complex(Math.Cos(angle), Math.Sin(angle));
                    sum += _fftBuffer[n] * exp;
                }
                
                _magnitude[k] = (float)sum.Magnitude;
            }

            return _magnitude;
        }

        public int FindPeakBin()
        {
            int peakBin = 0;
            float peakValue = 0;

            for (int i = 0; i < _magnitude.Length; i++)
            {
                if (_magnitude[i] > peakValue)
                {
                    peakValue = _magnitude[i];
                    peakBin = i;
                }
            }

            return peakBin;
        }

        public float GetFrequencyOffset(int sampleRate)
        {
            int peakBin = FindPeakBin();
            
            // Convert bin to frequency offset
            // Bin 0 = DC, Bin N/2 = Nyquist
            float freqPerBin = (float)sampleRate / _fftSize;
            float offset = (peakBin - _fftSize / 4) * freqPerBin; // Center around 0
            
            return offset;
        }
    }

    public class FrequencyDiagnostics
    {
        public static string DiagnoseFrequencyIssue(float signalLevel, int currentPpm, double currentFreqMhz, double sampleRateMsps)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== FREQUENCY DIAGNOSTICS ===\n");
            
            sb.AppendLine($"Current Frequency: {currentFreqMhz:F3} MHz");
            sb.AppendLine($"Current PPM: {currentPpm}");
            sb.AppendLine($"Sample Rate: {sampleRateMsps:F1} MSPS");
            sb.AppendLine($"Signal Level: {signalLevel * 100:F1}%\n");

            // Check sample rate
            if (Math.Abs(sampleRateMsps - 2.4) > 0.1 && Math.Abs(sampleRateMsps - 2.0) > 0.1)
            {
                sb.AppendLine("⚠️ SAMPLE RATE ISSUE");
                sb.AppendLine($"Current: {sampleRateMsps:F1} MSPS");
                sb.AppendLine("Recommended: 2.4 MSPS for FM broadcast");
                sb.AppendLine("\n⚠️ WARNING: Different sample rates should NOT");
                sb.AppendLine("pick up different stations! If this happens,");
                sb.AppendLine("set sample rate to 2.4 MSPS and leave it there.");
                sb.AppendLine("Use frequency tuning to change stations!\n");
            }

            if (signalLevel < 0.1f)
            {
                sb.AppendLine("❌ VERY WEAK/NO SIGNAL");
                sb.AppendLine("\nPossible causes:");
                sb.AppendLine("1. Wrong frequency - try scanning");
                sb.AppendLine("2. No antenna or poor antenna");
                sb.AppendLine("3. RTL AGC or Tuner AGC issues");
                sb.AppendLine("4. Severe PPM offset (try ±100 PPM)");
                sb.AppendLine("\nSuggestions:");
                sb.AppendLine("• Turn OFF RTL AGC");
                sb.AppendLine("• Turn ON Tuner AGC");
                sb.AppendLine("• Set Sample Rate to 2.4 MSPS");
                sb.AppendLine("• Click 'Scan FM Band'");
                sb.AppendLine("• Check antenna connection");
            }
            else if (signalLevel < 0.3f)
            {
                sb.AppendLine("⚠️ WEAK SIGNAL");
                sb.AppendLine("\nPossible causes:");
                sb.AppendLine("1. Slightly off frequency");
                sb.AppendLine("2. PPM correction needed");
                sb.AppendLine("3. Low gain settings");
                sb.AppendLine("4. Poor antenna position");
                sb.AppendLine("\nSuggestions:");
                sb.AppendLine("• Fine-tune frequency ±0.02 MHz");
                sb.AppendLine("• Try PPM: -50, 0, +50");
                sb.AppendLine("• Increase tuner gain if AGC is off");
                sb.AppendLine("• Keep sample rate at 2.4 MSPS");
            }
            else
            {
                sb.AppendLine("✅ GOOD SIGNAL");
                sb.AppendLine("\nIf audio quality is poor:");
                sb.AppendLine("• Verify sample rate is 2.4 MSPS");
                sb.AppendLine("• Enable de-emphasis");
                sb.AppendLine("• Turn OFF RTL AGC");
                sb.AppendLine("• Reduce volume if distorted");
                sb.AppendLine("• Fine-tune frequency ±0.001 MHz");
                sb.AppendLine("\nIf audio sounds wrong but signal is good:");
                sb.AppendLine("• Adjust PPM by ±1 or ±2");
                sb.AppendLine("• Check you're on the exact frequency");
            }

            return sb.ToString();
        }
    }
}
