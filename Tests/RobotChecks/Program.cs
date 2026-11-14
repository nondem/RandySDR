using RandySDR;
using Microsoft.Data.Sqlite;
using System.Reflection;

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
        MigrationPipelineChecks();
        IncomingTranscriptionChecks();
        ExistingClipStatusChecks();
        ParseChecks();
        RecorderChecks();
        SpeechSkipChecks();
        SpeechSkipRecorderChecks();
        ScreenerChecks();
        ScreenerPipelineChecks();
        SettingsChecks();
        OllamaTimeoutChecks();
        ClipDetailsChecks();
        if (Environment.GetEnvironmentVariable("RANDYSDR_WHISPER_TEST_WAV") is { Length: > 0 } wav)
        {
            using var transcriber = new WhisperTranscriber(() => new RobotConfig());
            string transcript = transcriber.TranscribeAsync(wav, CancellationToken.None).GetAwaiter().GetResult();
            Check(transcript.Contains("radio", StringComparison.OrdinalIgnoreCase),
                "local Whisper model transcribes generated speech");
        }
        Console.WriteLine(_failures == 0 ? "ALL PASSED" : $"{_failures} FAILED");
        return _failures == 0 ? 0 : 1;
    }

    private static void IncomingTranscriptionChecks()
    {
        string dir = Path.Combine(Path.GetTempPath(), "RandySDR-incoming-" + Guid.NewGuid());
        Directory.CreateDirectory(dir);
        try
        {
            var store = new RobotStore(Path.Combine(dir, "robot.db"));
            var config = new RobotConfig
            {
                ClipFolder = dir, MinFreeGb = 0, Transcribe = true, Classify = false,
                TriageMinSpeechSeconds = 10, LockoutMode = AutoLockoutMode.Auto
            };
            var responses = new System.Collections.Concurrent.ConcurrentQueue<Func<Task<string>>>();
            Func<string, CancellationToken, Task<string>> fakeTranscribe = (_, _) =>
            {
                if (!responses.TryDequeue(out var response))
                    throw new InvalidOperationException("Unexpected transcription");
                return response();
            };
            var constructor = typeof(RobotController).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null,
                new[] { typeof(RobotConfig), typeof(RobotStore), typeof(Func<double>), typeof(Func<string, CancellationToken, Task<string>>) }, null)!;
            using (var controller = (RobotController)constructor.Invoke(new object[] { config, store, (Func<double>)(() => 25_000), fakeTranscribe }))
            {
                const double hz = 146_520_000;
                long key = LockoutPolicy.ChannelKey(hz, 25_000);
                void Record(long lockId, float[] samples, double frequency = hz)
                {
                    controller.Recorder.Begin(new ClipInfo(lockId, frequency, "NFM", 10));
                    Feed(controller.Recorder, samples, true);
                    controller.Recorder.End("released");
                    controller.Recorder.Drain();
                }
                ClipRow? WaitFor(long lockId, string status)
                {
                    SpinWait.SpinUntil(() => store.RecentClips(20).Any(c => c.LockId == lockId && c.Status == status),
                        TimeSpan.FromSeconds(20));
                    return store.RecentClips(20).SingleOrDefault(c => c.LockId == lockId);
                }

                var pending = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
                responses.Enqueue(() => pending.Task);
                Record(101, Tone(1000, 3));
                var tone = WaitFor(101, "ai-queued");
                Check(tone is { Class: "TONE", Status: "ai-queued" }
                    && store.History(key, DateTime.UtcNow.AddHours(-1)).Single().Class == ClipClass.Uncertain,
                    "tone clip reaches AI and pending triage cannot trigger lockout");
                store.AddLockout(key, hz, "test", "active", DateTime.UtcNow);
                controller.RefreshLockoutCache();
                pending.SetResult("detected speech");
                tone = WaitFor(101, "transcribed");
                Check(tone is { Class: "TONE", Transcript: "detected speech", Status: "transcribed" }
                    && store.HasVoice(key, DateTime.UtcNow.AddHours(-1))
                    && SpinWait.SpinUntil(() => !store.HasOpenLockout(key), TimeSpan.FromSeconds(5)),
                    "transcript preserves triage class, protects channel and restores lockout");

                responses.Enqueue(() => Task.FromResult("uncertain speech"));
                Record(102, SpeechLike(4.5));
                Check(WaitFor(102, "transcribed") is { Class: "UNCERTAIN", Transcript: "uncertain speech", Status: "transcribed" },
                    "inconclusive clip reaches transcription");

                responses.Enqueue(() => Task.FromResult("carrier speech"));
                Record(103, new float[Sr * 3]);
                Check(WaitFor(103, "transcribed") is { Class: "CARRIER", Transcript: "carrier speech", Status: "transcribed" },
                    "carrier clip reaches transcription");

                responses.Enqueue(() => Task.FromResult(""));
                Record(104, Tone(1000, 3));
                Check(WaitFor(104, "no-speech") is { Class: "TONE", Transcript: "", Status: "no-speech" },
                    "empty transcription reports no speech");

                responses.Enqueue(() => Task.FromException<string>(new InvalidOperationException("test failure")));
                Record(105, Tone(1000, 3));
                var failed = WaitFor(105, "ai-failed");
                Check(failed is { Status: "ai-failed" }
                    && store.History(key, DateTime.UtcNow.AddHours(-1)).Any(c => c.LockId == 105 && c.Class == ClipClass.Uncertain),
                    "failed transcription is visible and does not count as junk");

                var briefSpeech = new float[Sr * 2];
                SpeechLike(0.4).CopyTo(briefSpeech, (int)(ClipRecorder.LockTrimSec * Sr));
                responses.Enqueue(() => Task.FromResult("brief speech"));
                Record(110, briefSpeech);
                Check(WaitFor(110, "transcribed") is { Class: "UNCERTAIN", SpeechSec: < 0.3, Transcript: "brief speech", Status: "transcribed" },
                    "inconclusive clip with less than 0.3 seconds of detected speech reaches transcription");

                config.Transcribe = false;
                Record(106, Tone(1000, 3));
                var disabled = WaitFor(106, "non-voice");
                Check(disabled is { Class: "TONE", Status: "non-voice" },
                    "disabled transcription retains triage-only behavior");
                responses.Enqueue(() => Task.FromResult("manual speech"));
                if (disabled != null) controller.Reprocess(disabled.Id);
                Check(WaitFor(106, "transcribed") is { Transcript: "manual speech", Status: "transcribed" },
                    "manual re-run remains available when transcription is disabled");

                config.Transcribe = true;
                const double junkHz = 151_000_000;
                long junkKey = LockoutPolicy.ChannelKey(junkHz, 25_000);
                for (int i = 0; i < 3; i++)
                {
                    responses.Enqueue(() => Task.FromResult(""));
                    Record(107 + i, Tone(1000, 3), junkHz);
                    Check(WaitFor(107 + i, "no-speech") is { Status: "no-speech" },
                        $"non-speech lock {i + 1} completes transcription");
                }
                Check(SpinWait.SpinUntil(() => store.HasOpenLockout(junkKey), TimeSpan.FromSeconds(5)),
                    "confirmed non-speech clips still trigger lockout policy");
            }

            using (var retention = new RobotStore(Path.Combine(dir, "retention.db")))
            {
                var now = DateTime.UtcNow;
                var toneMetrics = ClipAnalyzer.Analyze(Tone(1000, 2), Sr);
                var old = new ClipResult(new ClipInfo(1, 150_000_000, "NFM", 10), Path.Combine(dir, "missing.wav"),
                    now.AddDays(-4), 2, 2, "released", false);
                long transcriptId = retention.InsertClip(old, toneMetrics, 1);
                retention.SetTranscript(transcriptId, "speech");
                long pendingId = retention.InsertClip(old, toneMetrics, 2, true);
                long junkId = retention.InsertClip(old, toneMetrics, 3);
                retention.PurgeAudio(30, 3, now);
                Check(!retention.GetClip(transcriptId)!.Deleted && !retention.GetClip(pendingId)!.Deleted
                    && retention.GetClip(junkId)!.Deleted,
                    "transcribed and pending clips use voice retention despite non-voice triage");
                retention.SetWrong(transcriptId, true);
                Check(!retention.HasVoice(1, now.AddDays(-30))
                    && retention.History(1, now.AddDays(-30)).Single().Class == ClipClass.Tone,
                    "user correction overrides transcript-based channel protection");
            }
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
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
        Check(s.GetAverageDuration(DateTime.UtcNow) == null, "empty average is unknown");
        Check(s.GetUrgencySummary(DateTime.UtcNow) == new UrgencySummary(0, 0, 0, 0), "empty urgency summary is zero");
        var info = new ClipInfo(1, 146_520_000, "NFM", 12);
        var now = DateTime.UtcNow;
        var res = new ClipResult(info, "x.wav", now, 5, 4, "released", false);
        long key = LockoutPolicy.ChannelKey(146_520_000, 25_000);
        long id = s.InsertClip(res, ClipAnalyzer.Analyze(Tone(1000, 2), Sr), key);
        Check(id > 0 && s.GetClip(id) is { Class: "TONE", Status: "non-voice" }, "non-voice clip has final triage status");
        long voiceId = s.InsertClip(res, ClipAnalyzer.Analyze(SpeechLike(4), Sr), key);
        Check(s.GetClip(voiceId)?.Status == "analyzed", "voice is not marked non-voice");
        long uncertainId = s.InsertClip(res, ClipAnalyzer.Analyze(SpeechLike(4), Sr, new ClipAnalyzerOptions(10, 0.20, 0.05)), key);
        Check(s.GetClip(uncertainId)?.Status == "inconclusive", "uncertain clip has an inconclusive status");
        Check(s.History(key, now.AddHours(-1)).Count == 3, "history");
        s.SetClass(voiceId, "UNCERTAIN");
        Check(!s.HasVoice(key, now.AddDays(-1)), "no voice yet");
        s.SetStar(id, true);
        Check(s.HasVoice(key, now.AddDays(-1)), "starred counts as voice");
        s.SetTranscript(id, "hello");
        Check(s.GetClip(id)?.Transcript == "hello", "transcript saved");
        Check(s.GetClip(id)?.RmsDb is not null && s.RecentClips(10).Single(c => c.Id == id).ToneRatio is not null,
            "audio metrics round trip through clip reads");
        Check(s.GetUrgencySummary(now.AddHours(-1)).Routine == 0, "unclassified clips are not counted as routine");
        s.SetClassification(id, "amateur", 2, "summary", 0.9);
        Check(s.GetClip(id)?.ClassifiedAt != null && s.GetHighPriorityClips(now.AddHours(-1)).Count == 1,
            "classification timestamp and priority query round trip");
        Check(s.GetClipsByCategory("amateur", now.AddHours(-1), now.AddHours(1)).Count == 1
            && s.GetHourlyTraffic(now.AddHours(-1), now.AddHours(1)).Single().ClipCount == 1
            && s.GetFrequencyHeatmap(now.AddHours(-1)).Single().Detections == 3,
            "category, hourly traffic and frequency analytics query stored clips");
        s.SetTranscript(id, "new transcript");
        Check(s.GetClip(id) is { Category: "", Summary: "", Confidence: 0, Urgency: 0, ClassifiedAt: null },
            "retranscription clears obsolete classification");
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

    private static void MigrationPipelineChecks()
    {
        string dir = Path.Combine(Path.GetTempPath(), "RandySDR-migration-" + Guid.NewGuid());
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "robot.db");
        try
        {
            using (var db = new SqliteConnection($"Data Source={path};Pooling=False"))
            {
                db.Open();
                using var cmd = db.CreateCommand();
                cmd.CommandText = @"CREATE TABLE Clips(
Id INTEGER PRIMARY KEY AUTOINCREMENT, Path TEXT, StartUtc TEXT, Hz REAL, Mode TEXT, LockId INTEGER, Key INTEGER,
DurationSec REAL, Class TEXT, SpeechSec REAL, Transcript TEXT DEFAULT '', Category TEXT DEFAULT '', Urgency INTEGER DEFAULT 0,
Summary TEXT DEFAULT '', Confidence REAL DEFAULT 0, Starred INTEGER DEFAULT 0, Wrong INTEGER DEFAULT 0,
Deleted INTEGER DEFAULT 0, Status TEXT DEFAULT 'new');
INSERT INTO Clips(Path,StartUtc,Hz,Mode,LockId,Key,DurationSec,Class,SpeechSec,Transcript,Status)
VALUES('old.wav','2025-01-01T00:00:00.0000000Z',146520000,'NFM',42,1,3,'VOICE',2,'preserved transcript','transcribed');";
                cmd.ExecuteNonQuery();
            }

            var store = new RobotStore(path);
            Check(store.GetClip(1) is { Transcript: "preserved transcript", RmsDb: null },
                "legacy database migration preserves existing records and unknown metrics");
            var config = new RobotConfig { ClipFolder = dir, MinFreeGb = 0, Transcribe = true, Classify = false };
            Func<string, CancellationToken, Task<string>> fakeTranscribe = (_, _) => Task.FromResult("test speech");
            var constructor = typeof(RobotController).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null,
                new[] { typeof(RobotConfig), typeof(RobotStore), typeof(Func<double>), typeof(Func<string, CancellationToken, Task<string>>) }, null)!;
            using (var controller = (RobotController)constructor.Invoke(new object[] { config, store, (Func<double>)(() => 25_000), fakeTranscribe }))
            {
                controller.Recorder.Begin(new ClipInfo(100, 146_520_000, "NFM", 10));
                Feed(controller.Recorder, SpeechLike(4.5), true);
                controller.Recorder.End("released");
                controller.Recorder.Drain();
                bool completed = SpinWait.SpinUntil(() =>
                    store.RecentClips(10).Any(c => c.LockId == 100 && c.Status == "transcribed"), TimeSpan.FromSeconds(20));
                var clips = store.RecentClips(10).Where(c => c.LockId == 100).ToList();
                Check(completed && clips.Count == 1 && clips[0].Transcript == "test speech"
                    && clips[0].RmsDb != null && controller.LostAnalysisClips == 0,
                    "recorded voice reaches transcription and is indexed once after migration");
            }
            using var reopened = new RobotStore(path);
            Check(reopened.GetClip(1)?.Transcript == "preserved transcript", "migration is idempotent on reopen");
            Check(reopened.RecentClips(10).Single(c => c.LockId == 100).Transcript == "test speech",
                "new transcript persists after reopening migrated database");
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    private static void ExistingClipStatusChecks()
    {
        string dir = Path.Combine(Path.GetTempPath(), "RandySDR-status-" + Guid.NewGuid());
        string path = Path.Combine(dir, "robot.db");
        try
        {
            long oldId, correctedId, voiceId, completedId, uncertainId;
            var info = new ClipInfo(1, 146_520_000, "NFM", 10);
            var result = new ClipResult(info, "test.wav", DateTime.UtcNow, 2, 2, "released", false);
            var tone = ClipAnalyzer.Analyze(Tone(1000, 2), Sr);
            using (var store = new RobotStore(path))
            {
                oldId = store.InsertClip(result, tone, 1);
                store.SetStatus(oldId, "analyzed");
                correctedId = store.InsertClip(result, tone, 1);
                store.SetClass(correctedId, "VOICE");
                store.SetStatus(correctedId, "analyzed");
                voiceId = store.InsertClip(result, ClipAnalyzer.Analyze(SpeechLike(4), Sr), 1);
                uncertainId = store.InsertClip(result, ClipAnalyzer.Analyze(SpeechLike(4), Sr, new ClipAnalyzerOptions(10, 0.20, 0.05)), 1);
                store.SetStatus(uncertainId, "analyzed");
                completedId = store.InsertClip(result, tone, 1);
                store.SetTranscript(completedId, "speech");
                store.SetStatus(completedId, "analyzed");
            }
            using var reopened = new RobotStore(path);
            Check(reopened.GetClip(oldId)?.Status == "non-voice", "existing unprocessed non-voice clips gain final status");
            Check(reopened.GetClip(correctedId)?.Status == "analyzed" && reopened.GetClip(voiceId)?.Status == "analyzed",
                "existing voice and user-corrected clips retain their status");
            Check(reopened.GetClip(completedId)?.Status == "analyzed", "migration preserves clips with a transcript");
            Check(reopened.GetClip(uncertainId)?.Status == "inconclusive", "existing uncertain clips are marked inconclusive");
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
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

            rec.Begin(new ClipInfo(4, 146_520_000, "NFM", 10));
            for (int i = 0; i < 5; i++)
            {
                Feed(rec, Tone(500, 4), true);
                rec.Drain();
            }
            rec.End("released");
            rec.Drain();
            var longClip = results.Single(r => r.Info.LockId == 4);
            Check(!longClip.Deleted && longClip.DurationSec > 19 && longClip.EndReason == "released",
                "continuous recording exceeds 15 seconds without splitting");

            var maxResults = new List<ClipResult>();
            using var limited = new ClipRecorder(() => dir, () => 2, () => 0);
            limited.ClipCompleted += r => { lock (maxResults) maxResults.Add(r); };
            limited.Begin(new ClipInfo(5, 460_000_000, "NFM", 10));
            Feed(limited, Tone(500, 6), true);
            limited.End("released");
            limited.Drain();
            Check(maxResults.Count == 3 && maxResults.Take(2).All(r => r.EndReason == "max-length")
                && Math.Abs(maxResults.Sum(r => r.DurationSec) - 5.7) < 0.03,
                "maximum clip length splits long locks without losing audio");
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

    private static void ScreenerChecks()
        {
            var noise = new ScreenerDetector(Sr, 4);
            noise.Add(Noise(2, 0.5));
            Check(noise.Reason == null, "screener waits for minimum usable audio");
            noise.Add(Noise(2, 0.5, 2));
            Check(noise.Reason == "noise", "screener detects consistent static");
            var tone = new ScreenerDetector(Sr, 4);
            tone.Add(Tone(1000, 4));
            Check(tone.Reason == "tone", "screener detects consistent tone");
            var carrier = new ScreenerDetector(Sr, 4);
            carrier.Add(new float[Sr * 4]);
            Check(carrier.Reason == "carrier", "screener detects quiet carrier");
            var speech = new ScreenerDetector(Sr, 4);
            speech.Add(SpeechLike(2));
            speech.Add(Noise(4, 0.5));
            Check(speech.Reason == null, "screener protects speech-like audio for the lock");
            var mixed = new ScreenerDetector(Sr, 4);
            mixed.Add(Tone(1000, 2));
            mixed.Add(Noise(2, 0.5));
            Check(mixed.Reason == null, "screener protects mixed non-voice classifications");
            var partial = new ScreenerDetector(Sr, 4);
            partial.Add(Tone(1000, 4));
            partial.Add(SpeechLike(0.1));
            Check(partial.Reason == null, "unexamined partial window defers a screening decision");
            var interrupted = new ScreenerDetector(Sr, 4);
            interrupted.Add(Tone(1000, 2));
            interrupted.Invalidate();
            interrupted.Add(Tone(1000, 4));
            Check(interrupted.Reason == null, "squelch interruption protects the lock");
        }

    private static void ScreenerPipelineChecks()
        {
            string dir = Path.Combine(Path.GetTempPath(), "RandySDR-screen-" + Guid.NewGuid());
            Directory.CreateDirectory(dir);
            try
            {
                var store = new RobotStore(Path.Combine(dir, "robot.db"));
                var config = new RobotConfig { ClipFolder = dir, MinFreeGb = 0, Transcribe = true, LockoutMode = AutoLockoutMode.Auto };
                int calls = 0;
                Func<string, CancellationToken, Task<string>> transcribe = (_, _) =>
                {
                    Interlocked.Increment(ref calls);
                    return Task.FromResult("reviewed speech");
                };
                var constructor = typeof(RobotController).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null,
                    new[] { typeof(RobotConfig), typeof(RobotStore), typeof(Func<double>), typeof(Func<string, CancellationToken, Task<string>>) }, null)!;
                using var controller = (RobotController)constructor.Invoke(new object[] { config, store, (Func<double>)(() => 25_000), transcribe });
                Check(!controller.ScreenerEnabled, "screener starts disabled");
                var rec = controller.Recorder;
                rec.Begin(new ClipInfo(301, 146_520_000, "NFM", 10), 4);
                Feed(rec, Tone(1000, 4.3), true);
                rec.Drain();
                Check(rec.ScreeningReason == "tone", "recorder publishes screening result");
                rec.End("screened-tone");
                rec.Drain();
                Check(SpinWait.SpinUntil(() => store.RecentClips(5).Any(c => c.LockId == 301), TimeSpan.FromSeconds(10)),
                    "screened clip indexed");
                var clip = store.RecentClips(5).Single(c => c.LockId == 301);
                Check(clip.Status == "screened-tone" && clip.EndReason == "screened-tone" && File.Exists(clip.Path),
                    "screened reason and reviewable snippet retained");
                Check(calls == 0 && !store.History(clip.Key, DateTime.UtcNow.AddMinutes(-5)).Any(),
                    "screened snippet does not auto-transcribe or create lockout evidence");
                controller.MarkWrong(clip.Id);
                Check(store.ScreeningCorrections() == 1 && store.GetClip(clip.Id)?.Class == "VOICE",
                    "screening correction is recorded and protects the channel");
                controller.Reprocess(clip.Id);
                Check(SpinWait.SpinUntil(() => store.GetClip(clip.Id)?.Status == "transcribed", TimeSpan.FromSeconds(10))
                    && store.GetClip(clip.Id)?.EndReason == "screened-tone", "explicit reprocessing preserves reason");
                rec.Begin(new ClipInfo(302, 146_520_000, "NFM", 10));
                Feed(rec, Tone(1000, 2), true);
                rec.Drain();
                Check(rec.ScreeningReason == null && rec.ScreeningStatus == null, "next normal lock has no stale screening decision");
                rec.End("released");
                rec.Drain();
                rec.Begin(new ClipInfo(303, 146_520_000, "NFM", 10), 4);
                Feed(rec, Tone(1000, 4.3), true);
                rec.Drain();
                typeof(ClipRecorder).GetField("_dropped", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .SetValue(rec, rec.DroppedBlocks + 1);
                Check(rec.ScreeningReason == null, "dropped audio disables screening for the lock");
                rec.End("released");
                rec.Drain();
                using var limited = new ClipRecorder(() => dir, () => 2, () => 0);
                limited.Begin(new ClipInfo(304, 146_520_000, "NFM", 10), 4);
                Feed(limited, Tone(1000, 6), true);
                limited.Drain();
                Check(limited.ScreeningReason == null, "clip splitting disables screening for the lock");
                limited.End("released");
                limited.Drain();
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
            "VOICE", 4, transcript, "amateur", 0, summary, 0.8, false, false, false, "classified",
            null, null, null, null, null, null);
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
