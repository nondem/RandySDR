namespace RandySDR
{
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

            if (signalLevel < 0.04f)
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
            else if (signalLevel < 0.12f)
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
