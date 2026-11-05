using System.Diagnostics;

namespace RandySDR
{
    public class PpmCalibrator
    {
        // Common FM stations with known frequencies for calibration
        public static readonly Dictionary<string, double> KnownStations = new()
        {
            { "Enter your local station", 0.0 }
        };

        public static int CalculatePpm(double knownFrequencyMhz, double receivedFrequencyMhz)
        {
            // PPM = ((received - known) / known) * 1,000,000
            double ppm = ((receivedFrequencyMhz - knownFrequencyMhz) / knownFrequencyMhz) * 1_000_000;
            return (int)Math.Round(ppm);
        }

        public static double ApplyPpmCorrection(double frequencyMhz, int ppm)
        {
            // Corrected frequency = original * (1 + ppm/1,000,000)
            return frequencyMhz * (1.0 + ppm / 1_000_000.0);
        }

        public static string GetCalibrationHelp()
        {
            return @"PPM Calibration Help:

1. Find a strong local FM station with known frequency
2. Tune to that frequency
3. Slowly adjust PPM until audio is clearest
4. Fine-tune frequency in 0.001 MHz steps if needed

Example:
- Known station: 98.5 FM
- You hear it clearly at: 98.52 FM
- Difference: +0.02 MHz (20 kHz)
- PPM = (0.02 / 98.5) × 1,000,000 = +203 PPM
- But try ±20 to ±50 first, most dongles are in this range

Tips:
- Temperature affects PPM (dongle warms up after 5-10 mins)
- Once calibrated, PPM stays fairly constant
- Use RTL AGC OFF for better signal quality
- Use Tuner AGC ON for automatic RF gain";
        }
    }
}
