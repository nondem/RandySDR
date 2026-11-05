using System.Text.Json;

namespace RandySDR
{
    public enum ScanMethod
    {
        /// <summary>Tune once per ~1.9 MHz hop and measure every channel in it from one FFT. Fast.</summary>
        WidebandFft,
        /// <summary>Tune to each channel individually (slow, but each measurement is centred on the channel).</summary>
        TuneEachChannel
    }

    public enum HoldMode
    {
        /// <summary>Stay while the signal is present; resume after it has been gone for HoldSeconds.</summary>
        UntilSignalDrops,
        /// <summary>Stay until the user presses Resume.</summary>
        UntilResumed,
        /// <summary>Stay HoldSeconds, then resume regardless of signal.</summary>
        FixedTime
    }

    /// <summary>User-editable scanner configuration. Persisted as JSON in %AppData%\RandySDR\scan-settings.json.</summary>
    public sealed class ScanSettings
    {
        public double StartMHz { get; set; } = 88.1;
        public double StopMHz { get; set; } = 107.9;
        public double StepKHz { get; set; } = 200;
        public ScanMethod Method { get; set; } = ScanMethod.WidebandFft;
        /// <summary>A channel counts as a signal when it is this many dB above the measured noise floor.</summary>
        public int ThresholdDb { get; set; } = 10;
        /// <summary>Time to wait after retuning before measuring (USB buffers hold old-frequency data).</summary>
        public int SettleMs { get; set; } = 120;
        public HoldMode Hold { get; set; } = HoldMode.UntilSignalDrops;
        public double HoldSeconds { get; set; } = 3;
        public bool AutoSelectMode { get; set; } = true;
        /// <summary>When true the audio squelch follows the detection threshold (see Form1.SquelchLink.cs).</summary>
        public bool LinkSquelch { get; set; } = true;
        /// <summary>Extra dB added to the linked squelch level (positive = squelch more aggressively).</summary>
        public int SquelchOffsetDb { get; set; } = 0;
        public bool LogDetections { get; set; } = true;
        /// <summary>Frequencies (MHz) the scanner must skip.</summary>
        public List<double> LockoutsMHz { get; set; } = new();

        private static string FilePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RandySDR", "scan-settings.json");

        public static ScanSettings Load()
        {
            try
            {
                if (File.Exists(FilePath))
                    return JsonSerializer.Deserialize<ScanSettings>(File.ReadAllText(FilePath)) ?? new ScanSettings();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Could not load scan settings: {ex.Message}");
            }
            return new ScanSettings();
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Could not save scan settings: {ex.Message}");
            }
        }
    }
}
