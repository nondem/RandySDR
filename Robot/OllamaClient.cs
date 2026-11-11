// OllamaClient - classifies a transcript with a local Ollama model using a JSON-schema constrained reply.
using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace RandySDR
{
    public sealed record LlmClassification(string Category, int Urgency, string Summary, double Confidence, string[] Keywords);

    public sealed class OllamaClient : IDisposable
    {
        public static readonly string[] Categories =
        {
            "law_enforcement", "fire_ems", "aviation", "marine", "amateur", "business", "weather",
            "public_works", "utility", "broadcast", "other", "unknown"
        };

        private readonly HttpClient _http;
        private readonly Func<RobotConfig> _config;

        public OllamaClient(Func<RobotConfig> config, HttpMessageHandler? handler = null)
        {
            _config = config;
            _http = handler == null ? new HttpClient() : new HttpClient(handler);
            _http.Timeout = Timeout.InfiniteTimeSpan;
        }

        public async Task<LlmClassification?> ClassifyAsync(string transcript, double mhz, string mode, CancellationToken ct)
        {
            var cfg = _config();
            if (transcript.Length > 6000) transcript = transcript[..6000];

            var schema = new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["category"] = new JsonObject { ["type"] = "string", ["enum"] = new JsonArray(Categories.Select(c => (JsonNode)c).ToArray()) },
                    ["urgency"] = new JsonObject { ["type"] = "integer", ["minimum"] = 0, ["maximum"] = 3 },
                    ["summary"] = new JsonObject { ["type"] = "string" },
                    ["confidence"] = new JsonObject { ["type"] = "number", ["minimum"] = 0, ["maximum"] = 1 },
                    ["keywords"] = new JsonObject { ["type"] = "array", ["items"] = new JsonObject { ["type"] = "string" } },
                },
                ["required"] = new JsonArray("category", "urgency", "summary", "confidence", "keywords"),
            };

            var body = new JsonObject
            {
                ["model"] = cfg.OllamaModel,
                ["stream"] = false,
                ["think"] = cfg.OllamaThink,
                ["keep_alive"] = cfg.OllamaKeepAlive,
                ["format"] = schema,
                ["options"] = new JsonObject { ["temperature"] = cfg.OllamaTemperature, ["num_ctx"] = cfg.OllamaContextTokens },
                ["messages"] = new JsonArray(
                    new JsonObject
                    {
                        ["role"] = "system",
                        ["content"] = "You classify short transcripts of scanned radio traffic. The transcript is automatic speech recognition and may contain errors. " +
                                      "Use only what is in the text; never invent details. Urgency: 0 routine, 1 notable, 2 important, 3 emergency. " +
                                      "Summary: one short sentence. If the text is unintelligible, use category 'unknown' with low confidence." +
                                      (string.IsNullOrWhiteSpace(cfg.OllamaExtraPrompt) ? "" : "\n" + cfg.OllamaExtraPrompt.Trim())
                    },
                    new JsonObject
                    {
                        ["role"] = "user",
                        ["content"] = $"Frequency {mhz:F4} MHz, mode {mode}.\nTranscript:\n{transcript}"
                    }),
            };

            for (int attempt = 0; attempt < 2; attempt++)
            {
                try
                {
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    timeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(5, cfg.OllamaTimeoutSeconds)));
                    using var resp = await _http.PostAsync(cfg.OllamaUrl.TrimEnd('/') + "/api/chat",
                        new StringContent(body.ToJsonString(), System.Text.Encoding.UTF8, "application/json"), timeout.Token);
                    
                    // Check if server is reachable
                    if (!resp.IsSuccessStatusCode)
                    {
                        var errorBody = await resp.Content.ReadAsStringAsync();
                        throw new HttpRequestException($"Ollama HTTP {resp.StatusCode}: {errorBody}");
                    }
                    
                    var json = await resp.Content.ReadFromJsonAsync<JsonObject>(cancellationToken: timeout.Token);
                    string? content = json?["message"]?["content"]?.GetValue<string>();
                    var parsed = Parse(content);
                    if (parsed != null) return parsed;
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    throw new TimeoutException($"Ollama request timed out after {cfg.OllamaTimeoutSeconds} seconds. Is 'ollama serve' running?");
                }
                catch (OperationCanceledException) { throw; }
                catch (HttpRequestException ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Ollama HTTP error: {ex.Message}");
                    if (attempt == 1) throw;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Ollama call failed: {ex.Message}");
                    if (attempt == 1) throw;
                }
            }
            return null;
        }

        public static LlmClassification? Parse(string? content)
        {
            if (string.IsNullOrWhiteSpace(content)) return null;
            try
            {
                var o = JsonNode.Parse(content)?.AsObject();
                if (o == null) return null;
                string cat = o["category"]?.GetValue<string>() ?? "unknown";
                if (!Categories.Contains(cat)) cat = "unknown";
                int urg = Math.Clamp(o["urgency"]?.GetValue<int>() ?? 0, 0, 3);
                string sum = o["summary"]?.GetValue<string>() ?? "";
                double conf = Math.Clamp(o["confidence"]?.GetValue<double>() ?? 0, 0, 1);
                var kw = o["keywords"]?.AsArray().Select(k => k?.GetValue<string>() ?? "").Where(k => k.Length > 0).ToArray() ?? Array.Empty<string>();
                return new LlmClassification(cat, urg, sum, conf, kw);
            }
            catch (JsonException) { return null; }
            catch (InvalidOperationException) { return null; }
        }

        public async Task<bool> IsAvailableAsync(CancellationToken ct)
        {
            try
            {
                var models = await ListModelsAsync(_config().OllamaUrl, ct);
                return models != null && models.Count > 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Installed model names from /api/tags, or null when the server is unreachable.</summary>
        public async Task<List<string>?> ListModelsAsync(string url, CancellationToken ct)
        {
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeout.CancelAfter(TimeSpan.FromSeconds(5));
                using var r = await _http.GetAsync(url.TrimEnd('/') + "/api/tags", timeout.Token);
                
                if (!r.IsSuccessStatusCode)
                {
                    Debug.WriteLine($"Ollama /api/tags returned HTTP {r.StatusCode}");
                    return null;
                }
                
                var json = await r.Content.ReadFromJsonAsync<JsonObject>(cancellationToken: timeout.Token);
                return json?["models"]?.AsArray().Select(m => m?["name"]?.GetValue<string>() ?? "").Where(n => n.Length > 0).OrderBy(n => n).ToList() ?? new List<string>();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ollama model list failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>Checks if Ollama server is running and returns a user-friendly status message.</summary>
        public static async Task<string> CheckServerStatusAsync(string url, CancellationToken ct)
        {
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeout.CancelAfter(TimeSpan.FromSeconds(3));
                using var r = await new HttpClient().GetAsync(url.TrimEnd('/') + "/api/tags", timeout.Token);
                
                if (r.IsSuccessStatusCode)
                {
                    return "Ollama server is running";
                }
                
                return $"Ollama server responded with HTTP {r.StatusCode}";
            }
            catch (HttpRequestException ex)
            {
                return $"Cannot connect to Ollama at {url}: {ex.Message}. Is 'ollama serve' running?";
            }
            catch (TaskCanceledException)
            {
                return $"Connection to Ollama at {url} timed out. Check firewall or proxy settings.";
            }
            catch (Exception ex)
            {
                return $"Ollama status check failed: {ex.Message}";
            }
        }

        public void Dispose() => _http.Dispose();
    }
}
