// RobotStore - SQLite persistence (clips, transcripts, classifications, auto-lockouts, decision audit). One connection, serialised by a lock.
using Microsoft.Data.Sqlite;

namespace RandySDR
{
    public sealed record ClipRow(
        long Id, string Path, DateTime StartUtc, double Hz, string Mode, long LockId, long Key,
        double DurationSec, string Class, double SpeechSec, string Transcript, string Category,
        int Urgency, string Summary, double Confidence, bool Starred, bool Wrong, bool Deleted, string Status,
        // Additional analysis fields for traffic/pattern analysis
        double? RmsDb, double? SpeechRatio, double? ToneRatio, double? MeanFlatness,
        DateTime? ClassifiedAt, long? LockoutId, string EndReason = "");

    public sealed record AutoLockoutRow(long Id, long Key, double Hz, string Reason, DateTime CreatedUtc, DateTime ExpiresUtc, string Status);

    // Analysis result records
    public sealed record ChannelStats(long Key, int TotalClips, int VoiceClips, double AvgDurationSec);
    public sealed record UrgencySummary(int Routine, int Notable, int Important, int Emergency);
    public sealed record ChannelActivity(long Key, double FrequencyMhz, string Mode, int Detections, double AvgDurationSec);
    public sealed record HourlyTraffic(DateTime Date, int Hour, string Category, int ClipCount, double TotalMinutes);
    public sealed record FrequencyHeatmapEntry(double FrequencyMhz, int Detections);

    public sealed class RobotStore : IDisposable
    {
        private readonly SqliteConnection _db;
        private readonly object _gate = new();

        public RobotStore(string path)
        {
            if (path != ":memory:") Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            _db = new SqliteConnection($"Data Source={path};Pooling=False");
            try
            {
                _db.Open();
                Exec("PRAGMA journal_mode=WAL;");
                Exec(@"
CREATE TABLE IF NOT EXISTS Clips(
  Id INTEGER PRIMARY KEY AUTOINCREMENT, Path TEXT, StartUtc TEXT, Hz REAL, Mode TEXT, LockId INTEGER, Key INTEGER,
  DurationSec REAL, Class TEXT, SpeechSec REAL, Transcript TEXT DEFAULT '', Category TEXT DEFAULT '', Urgency INTEGER DEFAULT 0,
  Summary TEXT DEFAULT '', Confidence REAL DEFAULT 0, Starred INTEGER DEFAULT 0, Wrong INTEGER DEFAULT 0,
  Deleted INTEGER DEFAULT 0, Status TEXT DEFAULT 'new',
  RmsDb REAL, SpeechRatio REAL, ToneRatio REAL, MeanFlatness REAL, ClassifiedAt TEXT, LockoutId INTEGER);
CREATE INDEX IF NOT EXISTS IxClipsKey ON Clips(Key, StartUtc);
CREATE INDEX IF NOT EXISTS IxClipsCategory ON Clips(Category, StartUtc);
CREATE INDEX IF NOT EXISTS IxClipsUrgency ON Clips(Urgency) WHERE Urgency > 0;
CREATE TABLE IF NOT EXISTS AutoLockouts(
  Id INTEGER PRIMARY KEY AUTOINCREMENT, Key INTEGER, Hz REAL, Reason TEXT, CreatedUtc TEXT, ExpiresUtc TEXT, Status TEXT);
CREATE TABLE IF NOT EXISTS Decisions(
  Id INTEGER PRIMARY KEY AUTOINCREMENT, TimeUtc TEXT, Action TEXT, Key INTEGER, Detail TEXT);");
                MigrateClipColumns();
                Exec(@"UPDATE Clips SET Status='non-voice'
WHERE Status='analyzed' AND Class IN ('CARRIER','NOISE','TONE')
  AND Transcript='' AND Category='';");
                Exec(@"UPDATE Clips SET Status='inconclusive'
WHERE Status='analyzed' AND Class='UNCERTAIN' AND Transcript='' AND Category='';");
            }
            catch
            {
                _db.Dispose();
                throw;
            }
        }

        private void MigrateClipColumns()
        {
            using var transaction = _db.BeginTransaction();
            using var cmd = _db.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = "PRAGMA table_info(Clips)";
            var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (var reader = cmd.ExecuteReader())
                while (reader.Read()) columns.Add(reader.GetString(1));
            foreach (var (name, type) in new[]
            {
                ("RmsDb", "REAL"), ("SpeechRatio", "REAL"), ("ToneRatio", "REAL"),
                ("MeanFlatness", "REAL"), ("ClassifiedAt", "TEXT"), ("LockoutId", "INTEGER"),
                ("EndReason", "TEXT NOT NULL DEFAULT ''")
            })
            {
                if (columns.Contains(name)) continue;
                cmd.CommandText = $"ALTER TABLE Clips ADD COLUMN {name} {type}";
                cmd.ExecuteNonQuery();
            }
            transaction.Commit();
        }

        private void Exec(string sql, params (string, object?)[] p)
        {
            lock (_gate)
            {
                using var cmd = _db.CreateCommand();
                cmd.CommandText = sql;
                foreach (var (n, v) in p) cmd.Parameters.AddWithValue(n, v ?? DBNull.Value);
                cmd.ExecuteNonQuery();
            }
        }

        private List<T> Query<T>(string sql, Func<SqliteDataReader, T> map, params (string, object?)[] p)
        {
            lock (_gate)
            {
                using var cmd = _db.CreateCommand();
                cmd.CommandText = sql;
                foreach (var (n, v) in p) cmd.Parameters.AddWithValue(n, v ?? DBNull.Value);
                using var r = cmd.ExecuteReader();
                var list = new List<T>();
                while (r.Read()) list.Add(map(r));
                return list;
            }
        }

        private static string Iso(DateTime t) => t.ToUniversalTime().ToString("o");

        public long InsertClip(ClipResult c, ClipMetrics? m, long key, bool awaitingAi = false)
        {
            lock (_gate)
            {
                Exec(@"INSERT INTO Clips(Path,StartUtc,Hz,Mode,LockId,Key,DurationSec,Class,SpeechSec,Deleted,Status,RmsDb,SpeechRatio,ToneRatio,MeanFlatness,ClassifiedAt,LockoutId,EndReason)
VALUES($p,$s,$hz,$m,$l,$k,$d,$c,$sp,$del,$st,$rms,$sr,$tr,$mf,$ca,$lo,$end)",
                    ("$p", c.Path), ("$s", Iso(c.StartUtc)), ("$hz", c.Info.Hz), ("$m", c.Info.Mode), ("$l", c.Info.LockId), ("$k", key),
                    ("$d", c.DurationSec), ("$c", m?.Class.ToString().ToUpperInvariant() ?? "SHORT"), ("$sp", m?.SpeechSec ?? 0),
                    ("$del", c.Deleted ? 1 : 0),
                    ("$st", c.EndReason.StartsWith("screened-", StringComparison.Ordinal) ? c.EndReason : c.Deleted ? "short" : awaitingAi ? "ai-queued" : m?.Class switch
                    {
                        ClipClass.Carrier or ClipClass.Noise or ClipClass.Tone => "non-voice",
                        ClipClass.Uncertain => "inconclusive",
                        _ => "analyzed"
                    }),
                    ("$rms", m?.RmsDb), ("$sr", m?.SpeechRatio), ("$tr", m?.ToneRatio), ("$mf", m?.MeanFlatness),
                    ("$ca", null), ("$lo", null), ("$end", c.EndReason));
                using var cmd = _db.CreateCommand();
                cmd.CommandText = "SELECT last_insert_rowid()";
                return (long)(cmd.ExecuteScalar() ?? 0L);
            }
        }

        public void SetTranscript(long id, string text) =>
            Exec(@"UPDATE Clips SET Transcript=$t, Status='transcribed', Category='', Urgency=0,
Summary='', Confidence=0, ClassifiedAt=NULL WHERE Id=$i AND Deleted=0", ("$t", text), ("$i", id));

        public void SetClassification(long id, string category, int urgency, string summary, double confidence) =>
            Exec("UPDATE Clips SET Category=$c, Urgency=$u, Summary=$s, Confidence=$f, Status='classified', ClassifiedAt=$ca WHERE Id=$i AND Deleted=0",
                ("$c", category), ("$u", urgency), ("$s", summary), ("$f", confidence), ("$ca", Iso(DateTime.UtcNow)), ("$i", id));

        public void SetAudioMetrics(long id, double rmsDb, double speechRatio, double toneRatio, double meanFlatness) =>
            Exec("UPDATE Clips SET RmsDb=$rms, SpeechRatio=$sr, ToneRatio=$tr, MeanFlatness=$mf WHERE Id=$i AND Deleted=0",
                ("$rms", rmsDb), ("$sr", speechRatio), ("$tr", toneRatio), ("$mf", meanFlatness), ("$i", id));

        public void SetLockoutId(long clipId, long lockoutId) =>
            Exec("UPDATE Clips SET LockoutId=$lo WHERE Id=$i AND Deleted=0", ("$lo", lockoutId), ("$i", clipId));

        // Analysis query methods
        public List<ClipRow> GetClipsByCategory(string category, DateTime fromUtc, DateTime toUtc) =>
            Query($"SELECT {ClipCols} FROM Clips WHERE Deleted=0 AND Category=$c AND StartUtc>=$f AND StartUtc<=$t ORDER BY StartUtc DESC",
                MapClip, ("$c", category), ("$f", Iso(fromUtc)), ("$t", Iso(toUtc)));

        public List<ClipRow> GetHighPriorityClips(DateTime sinceUtc, int minUrgency = 2) =>
            Query($"SELECT {ClipCols} FROM Clips WHERE Deleted=0 AND Category<>'' AND Urgency>=$u AND StartUtc>=$s ORDER BY Urgency DESC, StartUtc DESC",
                MapClip, ("$u", minUrgency), ("$s", Iso(sinceUtc)));

        public Dictionary<long, ChannelStats> GetChannelStats(DateTime sinceUtc) =>
            Query("SELECT Key, COUNT(*) as total, SUM(CASE WHEN Class='VOICE' THEN 1 ELSE 0 END) as voice_count, AVG(DurationSec) as avg_duration FROM Clips WHERE Deleted=0 AND StartUtc>=$t GROUP BY Key",
                r => new ChannelStats(r.GetInt64(0), r.GetInt32(1), r.GetInt32(2), r.GetDouble(3)),
                ("$t", Iso(sinceUtc))).ToDictionary(k => k.Key);

        public UrgencySummary GetUrgencySummary(DateTime sinceUtc) =>
            Query("SELECT COUNT(CASE WHEN Urgency=0 THEN 1 END), COUNT(CASE WHEN Urgency=1 THEN 1 END), COUNT(CASE WHEN Urgency=2 THEN 1 END), COUNT(CASE WHEN Urgency=3 THEN 1 END) FROM Clips WHERE Deleted=0 AND Category<>'' AND StartUtc>=$t",
                r => new UrgencySummary(r.GetInt32(0), r.GetInt32(1), r.GetInt32(2), r.GetInt32(3)),
                ("$t", Iso(sinceUtc))).First();

        public List<ChannelActivity> GetChannelActivity(DateTime sinceUtc, int topN = 20) =>
            Query("SELECT Key, Hz / 1e6 as frequency_mhz, Mode, COUNT(*) as detections, AVG(DurationSec) as avg_duration_sec FROM Clips WHERE Deleted=0 AND Class='VOICE' AND StartUtc>=$t GROUP BY Key, Hz, Mode ORDER BY detections DESC LIMIT $n",
                r => new ChannelActivity(r.GetInt64(0), r.GetDouble(1), r.GetString(2), r.GetInt32(3), r.GetDouble(4)),
                ("$t", Iso(sinceUtc)), ("$n", topN));

        public List<HourlyTraffic> GetHourlyTraffic(DateTime fromUtc, DateTime toUtc) =>
            Query("SELECT strftime('%Y-%m-%d', StartUtc) as date, strftime('%H', StartUtc) as hour, Category, COUNT(*) as clip_count, SUM(DurationSec) / 60.0 as total_minutes FROM Clips WHERE Deleted=0 AND StartUtc>=$f AND StartUtc<=$t AND Category!='' GROUP BY date, hour, Category ORDER BY date DESC, hour DESC",
                r => new HourlyTraffic(DateTime.Parse(r.GetString(0)), int.Parse(r.GetString(1)), r.GetString(2), r.GetInt32(3), r.GetDouble(4)),
                ("$f", Iso(fromUtc)), ("$t", Iso(toUtc)));

        public List<FrequencyHeatmapEntry> GetFrequencyHeatmap(DateTime sinceUtc) =>
            Query("SELECT Hz / 1e6 as frequency_mhz, COUNT(*) as detections FROM Clips WHERE Deleted=0 AND StartUtc>=$t GROUP BY Hz ORDER BY detections DESC LIMIT 50",
                r => new FrequencyHeatmapEntry(r.GetDouble(0), r.GetInt32(1)),
                ("$t", Iso(sinceUtc)));

        public int GetTotalClips(DateTime sinceUtc) =>
            Query("SELECT COUNT(*) FROM Clips WHERE Deleted=0 AND StartUtc>=$t", r => r.GetInt32(0), ("$t", Iso(sinceUtc))).First();

        public double? GetAverageDuration(DateTime sinceUtc) =>
            Query("SELECT AVG(DurationSec) FROM Clips WHERE Deleted=0 AND StartUtc>=$t", r => r.IsDBNull(0) ? (double?)null : r.GetDouble(0), ("$t", Iso(sinceUtc))).First();

        public void SetStatus(long id, string status) => Exec("UPDATE Clips SET Status=$s WHERE Id=$i AND Deleted=0", ("$s", status), ("$i", id));
        public void SetClass(long id, string cls) => Exec("UPDATE Clips SET Class=$c WHERE Id=$i", ("$c", cls), ("$i", id));
        public void SetStar(long id, bool star) => Exec("UPDATE Clips SET Starred=$v WHERE Id=$i", ("$v", star ? 1 : 0), ("$i", id));
        public void SetWrong(long id, bool wrong) => Exec("UPDATE Clips SET Wrong=$v WHERE Id=$i", ("$v", wrong ? 1 : 0), ("$i", id));
        public void MarkDeleted(long id) => Exec("UPDATE Clips SET Deleted=1 WHERE Id=$i", ("$i", id));

        private const string ClipCols = "Id,Path,StartUtc,Hz,Mode,LockId,Key,DurationSec,Class,SpeechSec,Transcript,Category,Urgency,Summary,Confidence,Starred,Wrong,Deleted,Status,RmsDb,SpeechRatio,ToneRatio,MeanFlatness,ClassifiedAt,LockoutId,EndReason";

        private static ClipRow MapClip(SqliteDataReader r) => new(
            r.GetInt64(0), r.GetString(1), DateTime.Parse(r.GetString(2), null, System.Globalization.DateTimeStyles.RoundtripKind),
            r.GetDouble(3), r.GetString(4), r.GetInt64(5), r.GetInt64(6), r.GetDouble(7), r.GetString(8), r.GetDouble(9),
            r.GetString(10), r.GetString(11), r.GetInt32(12), r.GetString(13), r.GetDouble(14),
            r.GetInt32(15) != 0, r.GetInt32(16) != 0, r.GetInt32(17) != 0, r.GetString(18),
            r.IsDBNull(19) ? (double?)null : r.GetDouble(19),
            r.IsDBNull(20) ? (double?)null : r.GetDouble(20),
            r.IsDBNull(21) ? (double?)null : r.GetDouble(21),
            r.IsDBNull(22) ? (double?)null : r.GetDouble(22),
            r.IsDBNull(23) ? (DateTime?)null : DateTime.Parse(r.GetString(23), null, System.Globalization.DateTimeStyles.RoundtripKind),
            r.IsDBNull(24) ? (long?)null : r.GetInt64(24), r.GetString(25));

        public List<ClipRow> RecentClips(int n) =>
            Query($"SELECT {ClipCols} FROM Clips WHERE Deleted=0 ORDER BY Id DESC LIMIT $n", MapClip, ("$n", n));

        public ClipRow? GetClip(long id) =>
            Query($"SELECT {ClipCols} FROM Clips WHERE Id=$i", MapClip, ("$i", id)).FirstOrDefault();

        public int ClipsSince(DateTime utc) =>
            Query("SELECT COUNT(*) FROM Clips WHERE StartUtc>=$t AND Deleted=0", r => r.GetInt32(0), ("$t", Iso(utc))).First();

        public List<ClipHistoryEntry> History(long key, DateTime sinceUtc) =>
            Query(@"SELECT StartUtc,CASE
WHEN Wrong=0 AND Transcript<>'' THEN 'VOICE'
WHEN Status IN ('ai-queued','ai-queue-full','ai-failed','audio-unavailable') THEN 'UNCERTAIN'
ELSE Class END,DurationSec,LockId FROM Clips WHERE Key=$k AND StartUtc>=$t AND Status<>'short'
AND (EndReason NOT LIKE 'screened-%' OR Wrong=1 OR Starred=1 OR Transcript<>'')",
                r => new ClipHistoryEntry(
                    DateTime.Parse(r.GetString(0), null, System.Globalization.DateTimeStyles.RoundtripKind),
                    Enum.TryParse<ClipClass>(r.GetString(1), true, out var c) ? c : ClipClass.Uncertain, r.GetDouble(2), r.GetInt64(3)),
                ("$k", key), ("$t", Iso(sinceUtc)));

        public bool HasVoice(long key, DateTime sinceUtc) =>
            Query("SELECT 1 FROM Clips WHERE Key=$k AND StartUtc>=$t AND (Class='VOICE' OR Starred=1 OR (Wrong=0 AND Transcript<>'')) LIMIT 1",
                r => 1, ("$k", key), ("$t", Iso(sinceUtc))).Count > 0;

        public int ScreeningCorrections() =>
            Query("SELECT COUNT(*) FROM Clips WHERE EndReason LIKE 'screened-%' AND Wrong=1",
                r => r.GetInt32(0)).First();

        // ---- auto-lockouts ----
        public List<AutoLockoutRow> Lockouts(params string[] statuses)
        {
            string list = string.Join(",", statuses.Select(s => "'" + s.Replace("'", "") + "'"));
            return Query($"SELECT Id,Key,Hz,Reason,CreatedUtc,ExpiresUtc,Status FROM AutoLockouts WHERE Status IN ({list}) ORDER BY Id DESC",
                r => new AutoLockoutRow(r.GetInt64(0), r.GetInt64(1), r.GetDouble(2), r.GetString(3),
                    DateTime.Parse(r.GetString(4), null, System.Globalization.DateTimeStyles.RoundtripKind),
                    DateTime.Parse(r.GetString(5), null, System.Globalization.DateTimeStyles.RoundtripKind), r.GetString(6)));
        }

        public bool HasOpenLockout(long key) =>
            Query("SELECT 1 FROM AutoLockouts WHERE Key=$k AND Status IN ('suggested','active') LIMIT 1", r => 1, ("$k", key)).Count > 0;

        public long AddLockout(long key, double hz, string reason, string status, DateTime nowUtc)
        {
            lock (_gate)
            {
                Exec("INSERT INTO AutoLockouts(Key,Hz,Reason,CreatedUtc,ExpiresUtc,Status) VALUES($k,$h,$r,$c,$e,$s)",
                    ("$k", key), ("$h", hz), ("$r", reason), ("$c", Iso(nowUtc)), ("$e", Iso(nowUtc + LockoutPolicy.Ttl)), ("$s", status));
                using var cmd = _db.CreateCommand();
                cmd.CommandText = "SELECT last_insert_rowid()";
                return (long)(cmd.ExecuteScalar() ?? 0L);
            }
        }

        public void SetLockoutStatus(long id, string status, DateTime nowUtc) =>
            Exec("UPDATE AutoLockouts SET Status=$s, ExpiresUtc=CASE WHEN $s='active' THEN $e ELSE ExpiresUtc END WHERE Id=$i",
                ("$s", status), ("$e", Iso(nowUtc + LockoutPolicy.Ttl)), ("$i", id));

        public int ExpireLockouts(DateTime nowUtc)
        {
            lock (_gate)
            {
                Exec("UPDATE AutoLockouts SET Status='expired' WHERE Status='active' AND ExpiresUtc<$n", ("$n", Iso(nowUtc)));
                using var cmd = _db.CreateCommand();
                cmd.CommandText = "SELECT changes()";
                return Convert.ToInt32(cmd.ExecuteScalar());
            }
        }

        public void Log(string action, long key, string detail) =>
            Exec("INSERT INTO Decisions(TimeUtc,Action,Key,Detail) VALUES($t,$a,$k,$d)",
                ("$t", Iso(DateTime.UtcNow)), ("$a", action), ("$k", key), ("$d", detail));

        /// <summary>Deletes expired clip audio (voice vs carrier/noise retention, starred kept) and keeps the metadata rows.</summary>
        public int PurgeAudio(int voiceDays, int junkDays, DateTime nowUtc)
        {
            int removed = 0;
            var rows = Query($"SELECT {ClipCols} FROM Clips WHERE Deleted=0 AND Starred=0 AND StartUtc<$t", MapClip, ("$t", Iso(nowUtc.AddDays(-Math.Min(voiceDays, junkDays)))));
            foreach (var c in rows)
            {
                bool needsVoiceRetention = c.Class is "VOICE" or "UNCERTAIN"
                    || (!c.Wrong && c.Transcript.Length > 0)
                    || c.Status is "ai-queued" or "ai-queue-full" or "ai-failed" or "audio-unavailable";
                int days = needsVoiceRetention ? voiceDays : junkDays;
                if (nowUtc - c.StartUtc < TimeSpan.FromDays(days)) continue;
                if (File.Exists(c.Path)) File.Delete(c.Path);
                MarkDeleted(c.Id);
                removed++;
            }
            return removed;
        }

        public void Dispose()
        {
            lock (_gate) _db.Dispose();
        }
    }
}
