// RobotStore - SQLite persistence (clips, transcripts, classifications, auto-lockouts, decision audit). One connection, serialised by a lock.
using Microsoft.Data.Sqlite;

namespace RandySDR
{
    public sealed record ClipRow(
        long Id, string Path, DateTime StartUtc, double Hz, string Mode, long LockId, long Key,
        double DurationSec, string Class, double SpeechSec, string Transcript, string Category,
        int Urgency, string Summary, double Confidence, bool Starred, bool Wrong, bool Deleted, string Status);

    public sealed record AutoLockoutRow(long Id, long Key, double Hz, string Reason, DateTime CreatedUtc, DateTime ExpiresUtc, string Status);

    public sealed class RobotStore : IDisposable
    {
        private readonly SqliteConnection _db;
        private readonly object _gate = new();

        public RobotStore(string path)
        {
            if (path != ":memory:") Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            _db = new SqliteConnection($"Data Source={path};Pooling=False");
            _db.Open();
            Exec("PRAGMA journal_mode=WAL;");
            Exec(@"
CREATE TABLE IF NOT EXISTS Clips(
  Id INTEGER PRIMARY KEY AUTOINCREMENT, Path TEXT, StartUtc TEXT, Hz REAL, Mode TEXT, LockId INTEGER, Key INTEGER,
  DurationSec REAL, Class TEXT, SpeechSec REAL, Transcript TEXT DEFAULT '', Category TEXT DEFAULT '', Urgency INTEGER DEFAULT 0,
  Summary TEXT DEFAULT '', Confidence REAL DEFAULT 0, Starred INTEGER DEFAULT 0, Wrong INTEGER DEFAULT 0,
  Deleted INTEGER DEFAULT 0, Status TEXT DEFAULT 'new');
CREATE INDEX IF NOT EXISTS IxClipsKey ON Clips(Key, StartUtc);
CREATE TABLE IF NOT EXISTS AutoLockouts(
  Id INTEGER PRIMARY KEY AUTOINCREMENT, Key INTEGER, Hz REAL, Reason TEXT, CreatedUtc TEXT, ExpiresUtc TEXT, Status TEXT);
CREATE TABLE IF NOT EXISTS Decisions(
  Id INTEGER PRIMARY KEY AUTOINCREMENT, TimeUtc TEXT, Action TEXT, Key INTEGER, Detail TEXT);");
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

        public long InsertClip(ClipResult c, ClipMetrics? m, long key)
        {
            lock (_gate)
            {
                Exec(@"INSERT INTO Clips(Path,StartUtc,Hz,Mode,LockId,Key,DurationSec,Class,SpeechSec,Deleted,Status)
VALUES($p,$s,$hz,$m,$l,$k,$d,$c,$sp,$del,$st)",
                    ("$p", c.Path), ("$s", Iso(c.StartUtc)), ("$hz", c.Info.Hz), ("$m", c.Info.Mode), ("$l", c.Info.LockId), ("$k", key),
                    ("$d", c.DurationSec), ("$c", m?.Class.ToString().ToUpperInvariant() ?? "SHORT"), ("$sp", m?.SpeechSec ?? 0),
                    ("$del", c.Deleted ? 1 : 0), ("$st", c.Deleted ? "short" : "analyzed"));
                using var cmd = _db.CreateCommand();
                cmd.CommandText = "SELECT last_insert_rowid()";
                return (long)(cmd.ExecuteScalar() ?? 0L);
            }
        }

        public void SetTranscript(long id, string text) =>
            Exec("UPDATE Clips SET Transcript=$t, Status='transcribed' WHERE Id=$i AND Deleted=0", ("$t", text), ("$i", id));

        public void SetClassification(long id, string category, int urgency, string summary, double confidence) =>
            Exec("UPDATE Clips SET Category=$c, Urgency=$u, Summary=$s, Confidence=$f, Status='classified' WHERE Id=$i AND Deleted=0",
                ("$c", category), ("$u", urgency), ("$s", summary), ("$f", confidence), ("$i", id));

        public void SetStatus(long id, string status) => Exec("UPDATE Clips SET Status=$s WHERE Id=$i AND Deleted=0", ("$s", status), ("$i", id));
        public void SetClass(long id, string cls) => Exec("UPDATE Clips SET Class=$c WHERE Id=$i", ("$c", cls), ("$i", id));
        public void SetStar(long id, bool star) => Exec("UPDATE Clips SET Starred=$v WHERE Id=$i", ("$v", star ? 1 : 0), ("$i", id));
        public void SetWrong(long id, bool wrong) => Exec("UPDATE Clips SET Wrong=$v WHERE Id=$i", ("$v", wrong ? 1 : 0), ("$i", id));
        public void MarkDeleted(long id) => Exec("UPDATE Clips SET Deleted=1 WHERE Id=$i", ("$i", id));

        private const string ClipCols = "Id,Path,StartUtc,Hz,Mode,LockId,Key,DurationSec,Class,SpeechSec,Transcript,Category,Urgency,Summary,Confidence,Starred,Wrong,Deleted,Status";

        private static ClipRow MapClip(SqliteDataReader r) => new(
            r.GetInt64(0), r.GetString(1), DateTime.Parse(r.GetString(2), null, System.Globalization.DateTimeStyles.RoundtripKind),
            r.GetDouble(3), r.GetString(4), r.GetInt64(5), r.GetInt64(6), r.GetDouble(7), r.GetString(8), r.GetDouble(9),
            r.GetString(10), r.GetString(11), r.GetInt32(12), r.GetString(13), r.GetDouble(14),
            r.GetInt32(15) != 0, r.GetInt32(16) != 0, r.GetInt32(17) != 0, r.GetString(18));

        public List<ClipRow> RecentClips(int n) =>
            Query($"SELECT {ClipCols} FROM Clips WHERE Deleted=0 ORDER BY Id DESC LIMIT $n", MapClip, ("$n", n));

        public ClipRow? GetClip(long id) =>
            Query($"SELECT {ClipCols} FROM Clips WHERE Id=$i", MapClip, ("$i", id)).FirstOrDefault();

        public int ClipsSince(DateTime utc) =>
            Query("SELECT COUNT(*) FROM Clips WHERE StartUtc>=$t AND Deleted=0", r => r.GetInt32(0), ("$t", Iso(utc))).First();

        public List<ClipHistoryEntry> History(long key, DateTime sinceUtc) =>
            Query("SELECT StartUtc,Class,DurationSec,LockId FROM Clips WHERE Key=$k AND StartUtc>=$t AND Status<>'short'",
                r => new ClipHistoryEntry(
                    DateTime.Parse(r.GetString(0), null, System.Globalization.DateTimeStyles.RoundtripKind),
                    Enum.TryParse<ClipClass>(r.GetString(1), true, out var c) ? c : ClipClass.Uncertain, r.GetDouble(2), r.GetInt64(3)),
                ("$k", key), ("$t", Iso(sinceUtc)));

        public bool HasVoice(long key, DateTime sinceUtc) =>
            Query("SELECT 1 FROM Clips WHERE Key=$k AND StartUtc>=$t AND (Class='VOICE' OR Starred=1) LIMIT 1",
                r => 1, ("$k", key), ("$t", Iso(sinceUtc))).Count > 0;

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
                int days = c.Class == "VOICE" || c.Class == "UNCERTAIN" ? voiceDays : junkDays;
                if (nowUtc - c.StartUtc < TimeSpan.FromDays(days)) continue;
                try { if (File.Exists(c.Path)) File.Delete(c.Path); } catch { continue; }
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
