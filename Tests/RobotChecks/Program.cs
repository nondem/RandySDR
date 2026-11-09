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
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }
}
