using RandySDR;

internal static class Program
{
    private static int _failures;
    private const int Sr = ClipRecorder.SampleRate;

    private static void Check(bool ok, string name)
    {
        if (!ok) { _failures++; Console.WriteLine("FAIL: " + name); }
        else Console.WriteLine("ok:   " + name);
    }

    private static float[] Tone(double hz, double sec, double amp = 0.5)
    {
        var x = new float[(int)(sec * Sr)];
        for (int i = 0; i < x.Length; i++) x[i] = (float)(amp * Math.Sin(2 * Math.PI * hz * i / Sr));
        return x;
    }

    private static float[] Noise(double sec, double amp, int seed = 1)
    {
        var rng = new Random(seed);
        var x = new float[(int)(sec * Sr)];
        for (int i = 0; i < x.Length; i++) x[i] = (float)(amp * (rng.NextDouble() * 2 - 1));
        return x;
    }

    // Syllable-like bursts of a formant-shaped harmonic voice with drifting pitch plus a little noise.
    private static float[] SpeechLike(double sec)
    {
        var rng = new Random(7);
        var x = new float[(int)(sec * Sr)];
        double phase = 0;
        for (int i = 0; i < x.Length; i++)
        {
            double t = (double)i / Sr;
            double env = Math.Max(0, Math.Sin(2 * Math.PI * 3.5 * t));
            double f0 = 120 + 30 * Math.Sin(2 * Math.PI * 0.7 * t);
            phase += 2 * Math.PI * f0 / Sr;
            double s = 0;
            for (int h = 1; h <= 30; h++)
            {
                double f = f0 * h;
                if (f > 3800) break;
                double g = Math.Exp(-Math.Pow((f - 500) / 250, 2)) + 0.7 * Math.Exp(-Math.Pow((f - 1500) / 350, 2)) + 0.5 * Math.Exp(-Math.Pow((f - 2500) / 400, 2));
                s += g * Math.Sin(h * phase + h);
            }
            x[i] = (float)(0.15 * env * s + 0.01 * (rng.NextDouble() - 0.5));
        }
        return x;
    }

    private static void Feed(ClipRecorder r, float[] audio, bool open)
    {
        for (int o = 0; o < audio.Length; o += 960)
            r.Enqueue(audio.AsSpan(o, Math.Min(960, audio.Length - o)).ToArray(), open);
    }

    [STAThread]
    private static int Main()
    {
        AnalyzerChecks();
        PolicyChecks();
        StoreChecks();
        ParseChecks();
        RecorderChecks();
        SpeechSkipChecks();
        SpeechSkipRecorderChecks();
        SettingsChecks();
        OllamaTimeoutChecks();
        ClipDetailsChecks();
        Console.WriteLine(_failures == 0 ? "ALL PASSED" : $"{_failures} FAILED");
        return _failures == 0 ? 0 : 1;
    }

    private static void AnalyzerChecks()
    {
        Check(ClipAnalyzer.Analyze(new float[Sr * 3], Sr).Class == ClipClass.Carrier, "silence -> Carrier");
        Check(ClipAnalyzer.Analyze(Tone(1000, 3), Sr).Class == ClipClass.Tone, "steady tone -> Tone");
        var noise = ClipAnalyzer.Analyze(Noise(3, 0.3), Sr);
        Check(noise.Class is ClipClass.Noise or ClipClass.Carrier, $"white noise not Voice/Tone ({noise.Class})");
        var speech = ClipAnalyzer.Analyze(SpeechLike(4), Sr);
        Check(speech.Class == ClipClass.Voice, $"speech-like -> Voice ({speech.Class}, speech {speech.SpeechSec:F1}s)");
        var strict = ClipAnalyzer.Analyze(SpeechLike(4), Sr, new ClipAnalyzerOptions(10, 0.20, 0.05));
        Check(strict.Class == ClipClass.Uncertain, "configured speech duration changes triage");
    }

    private static void PolicyChecks()
    {
        var now = DateTime.UtcNow;
        ClipHistoryEntry E(double hoursAgo, ClipClass c, double dur = 10) => new(now.AddHours(-hoursAgo), c, dur);
        Check(LockoutPolicy.ShouldPropose(new[] { E(1, ClipClass.Carrier), E(2, ClipClass.Noise), E(3, ClipClass.Tone) }, false, now, out _), "3 junk -> propose");
        Check(!LockoutPolicy.ShouldPropose(new[] { E(1, ClipClass.Carrier), E(2, ClipClass.Noise) }, false, now, out _), "2 junk -> no");
        Check(!LockoutPolicy.ShouldPropose(new[] { E(1, ClipClass.Carrier), E(2, ClipClass.Voice), E(3, ClipClass.Tone), E(4, ClipClass.Noise) }, false, now, out _), "voice in window -> no");
        Check(!LockoutPolicy.ShouldPropose(new[] { E(1, ClipClass.Carrier), E(2, ClipClass.Noise), E(3, ClipClass.Tone) }, true, now, out _), "voice in 30 days -> no");
        Check(!LockoutPolicy.ShouldPropose(new[] { E(1, ClipClass.Carrier), E(2, ClipClass.Uncertain), E(3, ClipClass.Noise) }, false, now, out _), "uncertain -> no");
        Check(LockoutPolicy.ShouldPropose(new[] { E(1, ClipClass.Carrier, 400) }, false, now, out _), "300s junk -> propose");
        var sameLock = new[] { new ClipHistoryEntry(now, ClipClass.Tone, 10, 7), new ClipHistoryEntry(now.AddSeconds(-1), ClipClass.Tone, 10, 7), new ClipHistoryEntry(now.AddSeconds(-2), ClipClass.Tone, 10, 7) };
        Check(!LockoutPolicy.ShouldPropose(sameLock, false, now, out _), "split clips from one lock do not count as three locks");
        Check(!LockoutPolicy.ShouldPropose(new[] { E(30, ClipClass.Carrier), E(31, ClipClass.Carrier), E(32, ClipClass.Carrier) }, false, now, out _), "old history ignored");
        Check(LockoutPolicy.ChannelKey(146_520_100, 25_000) == LockoutPolicy.ChannelKey(146_525_000, 25_000), "key rounds to step");
    }

    private static void StoreChecks()
    {
        using var s = new RobotStore(":memory:");
        var info = new ClipInfo(1, 146_520_000, "NFM", 12);
        var now = DateTime.UtcNow;
        var res = new ClipResult(info, "x.wav", now, 5, 4, "released", false);
        long key = LockoutPolicy.ChannelKey(146_520_000, 25_000);
        long id = s.InsertClip(res, ClipAnalyzer.Analyze(Tone(1000, 2), Sr), key);
        Check(id > 0 && s.GetClip(id)?.Class == "TONE", "insert/get clip");
        Check(s.History(key, now.AddHours(-1)).Count == 1, "history");
        Check(!s.HasVoice(key, now.AddDays(-1)), "no voice yet");
        s.SetStar(id, true);
        Check(s.HasVoice(key, now.AddDays(-1)), "starred counts as voice");
        s.SetTranscript(id, "hello");
        Check(s.GetClip(id)?.Transcript == "hello", "transcript saved");
        long lid = s.AddLockout(key, 146_520_000, "test", "suggested", now);
        Check(s.HasOpenLockout(key), "open lockout");
        s.SetLockoutStatus(lid, "active", now);
        Check(s.Lockouts("active").Count == 1, "lockout activated");
        s.SetLockoutStatus(lid, "restored", now);
        Check(!s.HasOpenLockout(key), "lockout restored");
        s.AddLockout(key, 146_520_000, "t", "active", now.AddDays(-30));
        s.ExpireLockouts(now);
        Check(s.Lockouts("active").Count == 0, "lockout expired by TTL");
    }

    private static void ParseChecks()
    {
        var p = OllamaClient.Parse("{\"category\":\"Aviation\",\"urgency\":2,\"summary\":\"tower\",\"confidence\":0.8,\"keywords\":[\"tower\"]}");
        Check(p != null && p.Urgency == 2 && p.Summary == "tower", "ollama json parse");
        Check(OllamaClient.Parse("not json") == null, "bad json -> null");
        Check(WhisperTranscriber.IsHallucination("Thanks for watching!"), "hallucination filter");
        Check(WhisperTranscriber.IsRepetitiveLoop(string.Join(" ", Enumerable.Repeat("go go go", 20))), "loop filter");
        Check(!WhisperTranscriber.IsRepetitiveLoop("unit five respond to main street"), "normal text passes");
    }

    private static void RecorderChecks()
    {
        string dir = Path.Combine(Path.GetTempPath(), "RandySDR-robot-" + Guid.NewGuid());
        try
        {
            var results = new List<ClipResult>();
            using var rec = new ClipRecorder(() => dir, () => 120, () => 0);
            rec.ClipCompleted += r => { lock (results) results.Add(r); };

            rec.Begin(new ClipInfo(1, 146_520_000, "NFM", 10));
            Feed(rec, Tone(500, 3.5), true);
            rec.End("released");
            rec.Drain();
            Check(results.Count == 1 && !results[0].Deleted && File.Exists(results[0].Path), "clip written");
            if (results.Count == 1)
            {
                var bytes = File.ReadAllBytes(results[0].Path);
                int dataLen = BitConverter.ToInt32(bytes, 40);
                Check(bytes.Length == 44 + dataLen && System.Text.Encoding.ASCII.GetString(bytes, 0, 4) == "RIFF", "WAV header valid");
                Check(results[0].DurationSec is > 2.9 and < 3.4, $"duration trimmed ({results[0].DurationSec:F2}s)");
            }

            rec.Begin(new ClipInfo(2, 150_000_000, "NFM", 10));
            Feed(rec, Tone(500, 0.6), true);
            rec.End("released");
            rec.Drain();
            Check(results.Count == 2 && results[1].Deleted && !File.Exists(results[1].Path), "short clip deleted");

            rec.Begin(new ClipInfo(3, 155_000_000, "NFM", 10));
            Feed(rec, Tone(500, 2.5), true);
            Feed(rec, new float[(int)(2.0 * Sr)], false);
            Feed(rec, Tone(500, 2.5), true);
            rec.End("released");
            rec.Drain();
            Check(results.Count(r => r.Info.LockId == 3 && !r.Deleted) == 2, "squelch gap splits clip");
            var split = results.Where(r => r.Info.LockId == 3 && !r.Deleted).ToList();
            Check(split.Count == 2 && split.All(r => r.DurationSec < 2.6), "trailing squelch gap is omitted");
            Check(split.Count == 2 && split[1].DurationSec > 2.4, "later segment does not repeat retune trim");
            Check(split.Count == 2 && split[0].Path != split[1].Path && split.All(r => File.Exists(r.Path)), "segments have distinct files");
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    private static void SpeechSkipChecks()
    {
        var silence = new SpeechSkipDetector(Sr);
        silence.Add(new float[Sr * 4]);
        Check(!silence.ShouldSkip(6), "speech skip waits for minimum screened audio");
        silence.Add(new float[Sr * 2]);
        Check(silence.ShouldSkip(6), "speech skip detects screened silence");
        var speech = new SpeechSkipDetector(Sr);
        speech.Add(SpeechLike(2));
        speech.Add(new float[Sr * 8]);
        Check(!speech.ShouldSkip(6), "speech-like audio protects whole lock");
        var tone = new SpeechSkipDetector(Sr);
        tone.Add(Tone(1000, 6));
        Check(tone.ShouldSkip(6), "speech skip can screen a steady tone");
    }

    private static void SpeechSkipRecorderChecks()
    {
        string dir = Path.Combine(Path.GetTempPath(), "RandySDR-skip-" + Guid.NewGuid());
        try
        {
            using var rec = new ClipRecorder(() => dir, () => 120, () => 0, () => true, () => 6);
            rec.Begin(new ClipInfo(1, 146_520_000, "NFM", 10));
            Feed(rec, new float[(int)(6.4 * Sr)], true);
            rec.Drain();
            Check(rec.SpeechSkipDue, "recorder exposes screened no-speech decision");
            rec.InvalidateSpeechSkip();
            Check(!rec.SpeechSkipDue, "settings changes invalidate speech skip for the active lock");
            rec.End("test");
            rec.Begin(new ClipInfo(2, 146_520_000, "NFM", 10));
            Feed(rec, SpeechLike(2.4), true);
            Feed(rec, new float[Sr * 6], true);
            rec.Drain();
            Check(!rec.SpeechSkipDue, "recorder protects speech on a new lock");
            rec.End("test");
            rec.Drain();
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    private static void SettingsChecks()
    {
        var defaults = new RobotConfig();
        Check(!defaults.SpeechEarlySkip && defaults.SpeechEarlySkipSeconds == 10, "speech skip is opt-in");
        var original = new RobotConfig { SpeechEarlySkip = true, SpeechEarlySkipSeconds = 16, TriageMinSpeechRatio = 0.4 };
        var json = System.Text.Json.JsonSerializer.Serialize(original);
        var restored = System.Text.Json.JsonSerializer.Deserialize<RobotConfig>(json);
        Check(restored is { SpeechEarlySkip: true, SpeechEarlySkipSeconds: 16, TriageMinSpeechRatio: 0.4 }, "new settings round trip");
    }

    private static void OllamaTimeoutChecks()
    {
        using var client = new OllamaClient(() => new RobotConfig(), new TimeoutThenSuccessHandler());
        bool timedOut = false;
        try { client.ClassifyAsync("hello", 146, "NFM", CancellationToken.None).GetAwaiter().GetResult(); }
        catch (TimeoutException) { timedOut = true; }
        Check(timedOut, "request timeout distinguished from shutdown cancellation");
        var result = client.ClassifyAsync("hello", 146, "NFM", CancellationToken.None).GetAwaiter().GetResult();
        Check(result?.Summary == "test", "Ollama accepts later request after timeout");
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        bool cancelledCorrectly = false;
        try { client.ClassifyAsync("hello", 146, "NFM", cancelled.Token).GetAwaiter().GetResult(); }
        catch (OperationCanceledException) { cancelledCorrectly = true; }
        Check(cancelledCorrectly, "shutdown cancellation preserved");
    }

    private static void ClipDetailsChecks()
    {
        string transcript = new string('a', 40000) + " end";
        string summary = "A complete summary";
        var clip = new ClipRow(1, "test.wav", DateTime.UtcNow, 146_520_000, "NFM", 1, 1, 12,
            "VOICE", 4, transcript, "amateur", 0, summary, 0.8, false, false, false, "classified");
        using var form = new ClipDetailsForm(clip);
        form.CreateControl();
        var transcriptBox = (TextBox)form.Controls.Find("clipTranscript", true).Single();
        var summaryBox = (TextBox)form.Controls.Find("clipSummary", true).Single();
        _ = transcriptBox.Handle;
        _ = summaryBox.Handle;
        Check(transcriptBox.Text == transcript && transcriptBox.ReadOnly && transcriptBox.Multiline && transcriptBox.ScrollBars == ScrollBars.Vertical,
            "clip details preserve the entire long transcript in a scrollable read-only field");
        Check(summaryBox.Text == summary && summaryBox.ReadOnly, "clip details show selectable summary");
        using var emptyForm = new ClipDetailsForm(clip with { Transcript = "", Status = "ai-failed" });
        var emptyTranscript = (TextBox)emptyForm.Controls.Find("clipTranscript", true).Single();
        Check(emptyTranscript.Text == "", "missing transcript is not replaced with status text");
    }
}
