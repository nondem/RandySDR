// RobotLogger - centralized logging for robot scanning workflow
// Provides thread-safe, circular buffer logging with file and in-memory storage

namespace RandySDR
{
    /// <summary>Log entry representing a single event in the robot workflow.</summary>
    public sealed record RobotLogEntry(
        DateTime Timestamp,
        string Level,  // DEBUG, INFO, WARN, ERROR
        string Source, // ClipRecorder, ClipAnalyzer, WhisperTranscriber, OllamaClient, RobotController
        string Message,
        string? Details);

    /// <summary>Thread-safe circular buffer for robot workflow logs.</summary>
    public sealed class RobotLogger : IDisposable
    {
        private readonly object _gate = new();
        private readonly List<RobotLogEntry> _entries = new(500);
        private readonly string _logFilePath;
        private readonly FileStream _logFile;
        private readonly StreamWriter _writer;
        private int _maxEntries = 1000;

        /// <summary>Maximum number of entries to keep in memory. Default: 1000.</summary>
        public int MaxEntries
        {
            get => _maxEntries;
            set
            {
                lock (_gate)
                {
                    _maxEntries = value;
                    if (_entries.Count > value)
                    {
                        _entries.RemoveRange(0, _entries.Count - value);
                    }
                }
            }
        }

        /// <summary>Number of log entries currently in memory.</summary>
        public int Count => _entries.Count;

        /// <summary>Path to the persistent log file.</summary>
        public string LogFilePath => _logFilePath;

        public RobotLogger(string logFolder, string logFileName = "robot-log.txt")
        {
            _logFilePath = Path.Combine(logFolder, logFileName);
            
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_logFilePath) ?? ".");
                _logFile = File.Open(_logFilePath, FileMode.Append, FileAccess.Write, FileShare.Read);
                _writer = new StreamWriter(_logFile) { AutoFlush = true };
            }
            catch (Exception ex)
            {
                RobotLogger.DebugWrite($"RobotLogger failed to open log file {_logFilePath}: {ex.Message}");
                _logFile = null;
                _writer = null;
            }
        }

        /// <summary>Logs a new entry and writes to both memory buffer and file.</summary>
        public void Log(string level, string source, string message, string? details = null)
        {
            var entry = new RobotLogEntry(DateTime.UtcNow, level, source, message, details);
            
            lock (_gate)
            {
                _entries.Add(entry);
                if (_entries.Count > _maxEntries)
                {
                    _entries.RemoveAt(0);
                }

                // Write to file
                if (_writer != null)
                {
                    try
                    {
                        var timestamp = entry.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff");
                        _writer.WriteLine($"[{timestamp}] [{level}] [{source}] {message}");
                        if (details != null)
                        {
                            _writer.WriteLine($"  Details: {details}");
                        }
                        _writer.WriteLine();
                    }
                    catch (Exception ex)
                    {
                        DebugWrite($"RobotLogger failed to write to file: {ex.Message}");
                    }
                }
            }

            // Notify listeners if any
            EntryAdded?.Invoke(this, entry);
        }

        /// <summary>Convenience methods for common log levels.</summary>
        public void LogDebug(string source, string message, string? details = null) => Log("DEBUG", source, message, details);
        public void LogInfo(string source, string message, string? details = null) => Log("INFO", source, message, details);
        public void LogWarn(string source, string message, string? details = null) => Log("WARN", source, message, details);
        public void LogError(string source, string message, string? details = null) => Log("ERROR", source, message, details);

        /// <summary>Writes a debug message to the console (for development).</summary>
        public static void DebugWrite(string message) => System.Diagnostics.Debug.WriteLine(message);

        /// <summary>Retrieves log entries within a time range, ordered newest first.</summary>
        public List<RobotLogEntry> GetEntries(DateTime sinceUtc, int maxCount = 100)
        {
            lock (_gate)
            {
                return _entries
                    .Where(e => e.Timestamp >= sinceUtc)
                    .OrderByDescending(e => e.Timestamp)
                    .Take(maxCount)
                    .ToList();
            }
        }

        /// <summary>Retrieves the last N entries regardless of time.</summary>
        public List<RobotLogEntry> GetLast(int count)
        {
            lock (_gate)
            {
                return _entries
                    .OrderByDescending(e => e.Timestamp)
                    .Take(count)
                    .ToList();
            }
        }

        /// <summary>Clears all in-memory log entries (does not affect file).</summary>
        public void Clear()
        {
            lock (_gate)
            {
                _entries.Clear();
            }
        }

        /// <summary>Event raised when a new log entry is added.</summary>
        public event EventHandler<RobotLogEntry>? EntryAdded;

        public void Dispose()
        {
            _writer?.Dispose();
            _logFile?.Dispose();
        }
    }
}
