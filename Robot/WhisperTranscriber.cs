// WhisperTranscriber - Whisper.net (CUDA when available) speech-to-text with a hallucination filter. Loads the model lazily; downloads it on first use.
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using Whisper.net;
using Whisper.net.Ggml;

namespace RandySDR
{
    public sealed class WhisperTranscriber : IDisposable
    {
        private static readonly string[] HallucinationPhrases =
        {
            "thanks for watching", "thank you for watching", "subscribe", "please like", "[music]", "(music)",
            "[blank_audio]", "www.", "subtitles by", "amara.org"
        };

        private readonly Func<RobotConfig> _config;
        private WhisperFactory? _factory;
        private string? _loadedModel;
        private readonly SemaphoreSlim _loadGate = new(1, 1);

        public string Status { get; private set; } = "Whisper idle";

        public WhisperTranscriber(Func<RobotConfig> config) => _config = config;

        private async Task<WhisperFactory> EnsureFactoryAsync(CancellationToken ct)
        {
            try
            {
                var cfg = _config();
                if (!Enum.TryParse<GgmlType>(cfg.WhisperModel, true, out var type)) type = GgmlType.LargeV3Turbo;
                string model = type.ToString();
                if (_factory != null && _loadedModel == model) return _factory;
                _factory?.Dispose();
                _factory = null;
                _loadedModel = null;
                string dir = Path.Combine(RobotConfig.DataDirectory, "models");
                Directory.CreateDirectory(dir);
                string file = Path.Combine(dir, $"ggml-{type}.bin".ToLowerInvariant());
                if (!File.Exists(file))
                {
                    Status = $"Downloading Whisper model {type}...";
                    var q = type == GgmlType.LargeV3Turbo ? QuantizationType.Q5_0 : QuantizationType.NoQuantization;
                    string tmp = file + ".part";
                    using (var src = await WhisperGgmlDownloader.Default.GetGgmlModelAsync(type, q, ct))
                    using (var dst = File.Create(tmp))
                        await src.CopyToAsync(dst, ct);
                    File.Move(tmp, file, true);
                }
                Status = "Loading Whisper model...";
                _factory = WhisperFactory.FromPath(file);
                _loadedModel = model;
                Status = "Whisper ready";
                return _factory;
            }
            catch
            {
                Status = "Whisper unavailable (model download/load failed)";
                throw;
            }
        }

        private WhisperProcessor BuildProcessor(WhisperFactory factory)
        {
            var cfg = _config();
            var b = factory.CreateBuilder()
                .WithLanguage(string.IsNullOrWhiteSpace(cfg.WhisperLanguage) ? "auto" : cfg.WhisperLanguage)
                .WithNoSpeechThreshold((float)Math.Clamp(cfg.WhisperNoSpeechThreshold, 0, 1));
            if (cfg.WhisperThreads > 0) b = b.WithThreads(cfg.WhisperThreads);
            if (!string.IsNullOrWhiteSpace(cfg.WhisperPrompt)) b = b.WithPrompt(cfg.WhisperPrompt.Trim());
            return b.Build();
        }

        /// <summary>Transcribes a WAV clip. Returns an empty string when only hallucinations / silence were produced.</summary>
        public async Task<string> TranscribeAsync(string wavPath, CancellationToken ct)
        {
            await _loadGate.WaitAsync(ct);
            try
            {
                var factory = await EnsureFactoryAsync(ct);
                float[] pcm = Load16kMono(wavPath);
                if (pcm.Length < 16000 / 2) return "";
                using var processor = BuildProcessor(factory);
                var lines = new List<string>();
                Status = "Transcribing...";
                await foreach (var seg in processor.ProcessAsync(pcm, ct))
                {
                    if (seg.NoSpeechProbability > 0.7f) continue;
                    string t = seg.Text.Trim();
                    if (t.Length == 0 || IsHallucination(t)) continue;
                    if (lines.Count > 0 && string.Equals(lines[^1], t, StringComparison.OrdinalIgnoreCase)) continue;
                    lines.Add(t);
                }
                Status = "Whisper ready";
                string text = string.Join(" ", lines);
                return IsRepetitiveLoop(text) ? "" : text;
            }
            catch { Status = "Whisper unavailable"; throw; }
            finally { _loadGate.Release(); }
        }

        public static bool IsHallucination(string t)
        {
            string l = t.ToLowerInvariant();
            return HallucinationPhrases.Any(l.Contains);
        }

        /// <summary>Detects the classic "the the the the" / repeated-sentence decoder loop.</summary>
        public static bool IsRepetitiveLoop(string text)
        {
            var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length < 12) return false;
            int distinct = words.Select(w => w.ToLowerInvariant().Trim('.', ',', '!', '?')).Distinct().Count();
            return distinct <= words.Length / 6;
        }

        private static float[] Load16kMono(string path)
        {
            using var reader = new AudioFileReader(path);
            ISampleProvider sp = reader;
            if (reader.WaveFormat.Channels > 1) sp = new StereoToMonoSampleProvider(sp);
            if (reader.WaveFormat.SampleRate != 16000) sp = new WdlResamplingSampleProvider(sp, 16000);
            var list = new List<float>();
            var buf = new float[16000];
            int n;
            while ((n = sp.Read(buf, 0, buf.Length)) > 0)
                for (int i = 0; i < n; i++) list.Add(buf[i]);
            return list.ToArray();
        }

        public void Dispose() => _factory?.Dispose();
    }
}
