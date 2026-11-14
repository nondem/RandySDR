// RobotConfig - persisted settings for the scan-automation features (%AppData%\RandySDR\robot-config.json).
using System.Text.Json;

namespace RandySDR
{
    public enum AutoLockoutMode { Off, SuggestOnly, Auto }

    public sealed class RobotConfig
    {
        // Every feature is opt-in.
        public bool RecordClips { get; set; }
        public bool Transcribe { get; set; }
        public bool Classify { get; set; }
        public AutoLockoutMode LockoutMode { get; set; } = AutoLockoutMode.SuggestOnly;
        /// <summary>Resume scanning early when a locked channel carries no audio for EarlySkipSeconds (requires an active squelch).</summary>
        public bool EarlySkip { get; set; }
        public double EarlySkipSeconds { get; set; } = 4;
        public bool SpeechEarlySkip { get; set; }
        public double SpeechEarlySkipSeconds { get; set; } = 10;
        public double ScreenerSeconds { get; set; } = 4;
        public double TriageMinSpeechSeconds { get; set; } = 1.0;
        public double TriageMinSpeechRatio { get; set; } = 0.20;
        public double TriageMaxCarrierRatio { get; set; } = 0.05;

        public string ClipFolder { get; set; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "RandySDR", "Clips");
        public int VoiceRetentionDays { get; set; } = 30;
        public int CarrierRetentionDays { get; set; } = 3;
        public int MaxClipSeconds { get; set; } = 120;
        public double MinFreeGb { get; set; } = 5;

        public string WhisperModel { get; set; } = "LargeV3Turbo";
        public string WhisperLanguage { get; set; } = "en";
        public string OllamaUrl { get; set; } = "http://localhost:11434";
        public string OllamaModel { get; set; } = "qwen3.5:35b-a3b-q4_K_M";
        public double OllamaTemperature { get; set; } = 0;
        public int OllamaContextTokens { get; set; } = 4096;
        public int OllamaTimeoutSeconds { get; set; } = 90;
        public string OllamaKeepAlive { get; set; } = "5m";
        public bool OllamaThink { get; set; }
        /// <summary>Extra guidance appended to the classification prompt (e.g. local agencies, jargon).</summary>
        public string OllamaExtraPrompt { get; set; } = "";

        public double WhisperNoSpeechThreshold { get; set; } = 0.6;
        public int WhisperThreads { get; set; }
        /// <summary>Optional vocabulary hint passed to Whisper (call signs, place names, 10-codes).</summary>
        public string WhisperPrompt { get; set; } = "";

        public static string DataDirectory => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RandySDR");
        private static string FilePath => Path.Combine(DataDirectory, "robot-config.json");

        public static RobotConfig Load()
        {
            try
            {
                if (File.Exists(FilePath))
                    return JsonSerializer.Deserialize<RobotConfig>(File.ReadAllText(FilePath)) ?? new RobotConfig();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Could not load robot config: {ex.Message}");
            }
            return new RobotConfig();
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(DataDirectory);
                File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Could not save robot config: {ex.Message}");
            }
        }
    }
}
