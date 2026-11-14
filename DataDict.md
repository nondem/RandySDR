# RandySDR Robot Scanning Data Dictionary

## Purpose
This document defines the data schema and analysis capabilities for the RandySDR robot scanning system. It supports communications analysis, traffic pattern detection, and operational insights.

The app stores clip metadata in the existing SQLite database at `%APPDATA%\RandySDR\robot.db`.
At startup it adds missing analysis columns to older databases without replacing existing rows.
New metrics are NULL for older clips unless explicitly analyzed and saved again. WAV files remain
in the configured clip folder; metadata is not the audio itself. Clips whose indexing failed
before the database upgrade are not automatically imported.

Clip rows represent recorded segments, not all detected frequencies or distinct transmissions:
a lock may produce multiple segments and some detections may produce none. Counts in the examples
below are clip counts. Classification and speech detection are estimates, not verified facts.

---

## Core Clip Metadata (Database Schema)

| Field | Type | Description | Analysis Use Case |
|-------|------|-------------|-------------------|
| `Id` | INTEGER | Unique clip identifier | Primary key for all analysis queries |
| `Path` | TEXT | Filesystem path to WAV audio | Audio retrieval, storage management |
| `StartUtc` | DATETIME | UTC timestamp when recording began | Traffic timing, temporal pattern analysis |
| `Hz` | REAL | Center frequency in Hz (e.g., 146520000) | Frequency distribution, band usage patterns |
| `Mode` | TEXT | Demodulation mode (NFM, AM, USB, etc.) | Mode preference analysis by band/time |
| `LockId` | INTEGER | Lock identifier for multi-segment clips | Clip continuity tracking |
| `Key` | INTEGER | Channel key (frequency rounded to step) | Channel-level aggregation across locks |
| `DurationSec` | REAL | Total clip duration in seconds | Communication length patterns |
| `Class` | TEXT | Audio class: CARRIER, TONE, NOISE, VOICE, UNCERTAIN | Traffic type distribution |
| `SpeechSec` | REAL | Estimated speech duration in seconds | Active communication time analysis |
| `Transcript` | TEXT | Whisper ASR transcription text | Content analysis, keyword extraction |
| `Category` | TEXT | Ollama classification: LAW_ENFORCEMENT, AVIATION, etc. | Traffic categorization by service type |
| `Urgency` | INTEGER | 0=routine, 1=notable, 2=important, 3=emergency | Priority traffic monitoring |
| `Summary` | TEXT | One-sentence AI summary | Quick content overview |
| `Confidence` | REAL | AI classification confidence (0-1) | Classification reliability scoring |
| `Starred` | INTEGER | Boolean flag for user marking | Important communications tracking |
| `Wrong` | INTEGER | Boolean flag for user correction | Data quality metrics |
| `Deleted` | INTEGER | Boolean deletion flag | Retention analysis |
| `Status` | TEXT | Processing status: non-voice, analyzed, transcribed, classified | Pipeline health monitoring |

---

## Audio Analysis Metrics (ClipAnalyzer)

| Field | Type | Description | Analysis Use Case |
|-------|------|-------------|-------------------|
| `RmsDb` | REAL | Root-mean-square audio level in dBFS, or NULL if unavailable | Audio-level analysis; not RF signal strength |
| `SpeechRatio` | REAL | Ratio of speech frames to total frames (0-1) | Communication density per clip |
| `ToneRatio` | REAL | Ratio of tone-dominated frames (0-1) | Identify continuous carriers vs voice |
| `MeanFlatness` | REAL | Spectral flatness measure (0-1) | Distinguish noise from structured signals |

---

## Scanner Detection Metadata

These fields describe runtime values, not columns currently saved in `Clips`. They cannot be
queried historically from this database.

| Field | Type | Description | Analysis Use Case |
|-------|------|-------------|-------------------|
| `StrengthDb` | REAL | Signal strength above noise floor at lock | Signal quality trends, coverage analysis |
| `NoiseFloorDb` | REAL | Measured noise floor during scan | Environmental conditions, band congestion |
| `ThresholdDb` | REAL | Detection threshold in use | Scanner sensitivity configuration |
| `SweepCount` | INTEGER | Number of complete frequency sweeps | Scan activity volume |
| `HoldMode` | TEXT | Hold behavior: UNTIL_SIGNAL_DROPS, FIXED_TIME, UNTIL_RESUMED | Operational mode analysis |
| `HoldSeconds` | REAL | Configured hold duration | Lock persistence patterns |

---

## Lockout & Policy Data

| Field | Type | Description | Analysis Use Case |
|-------|------|-------------|-------------------|
| `LockoutId` | INTEGER | Nullable clip field; not populated automatically | No current automatic clip-to-lockout link |
| `Reason` | TEXT | `AutoLockouts` reason for lockout proposal | Pattern-based exclusion triggers |
| `Status` | TEXT | `AutoLockouts` lifecycle status: suggested, active, restored, expired | Lockout lifecycle analysis |
| `CreatedUtc` | DATETIME | Lockout creation timestamp | Exclusion timing patterns |
| `ExpiresUtc` | DATETIME | Lockout expiration timestamp | Temporary vs permanent exclusions |

---

## AI Classification Categories (Ollama)

| Category | Description | Typical Use Cases |
|----------|-------------|-------------------|
| `law_enforcement` | Police, sheriff, state trooper | Public safety monitoring |
| `fire_ems` | Fire department, EMS, emergency medical | Emergency response tracking |
| `aviation` | Air traffic control, tower, ground | Airport proximity detection |
| `marine` | Coast guard, vessel communications | Maritime activity |
| `amateur` | Ham radio operators | Hobbyist traffic analysis |
| `business` | Commercial dispatch, logistics | Business operations monitoring |
| `weather` | NOAA, meteorological broadcasts | Weather alert tracking |
| `public_works` | Municipal services, utilities | Infrastructure monitoring |
| `utility` | Power companies, telecom | Utility outage detection |
| `broadcast` | Radio/TV station test tones | Broadcast facility identification |
| `other` | Unclassified traffic | Unknown pattern analysis |
| `unknown` | Unintelligible or insufficient data | Data quality flagging |

---

## Suggested Additional Tables for Advanced Analysis

These are proposals only; the application does not create or populate these tables.

### Channel Statistics
```sql
CREATE TABLE IF NOT EXISTS ChannelStats (
  Key INTEGER PRIMARY KEY,
  TotalClips INTEGER DEFAULT 0,
  VoiceClips INTEGER DEFAULT 0,
  AvgDurationSec REAL DEFAULT 0,
  FirstSeen DATETIME,
  LastSeen DATETIME,
  ActiveHours INTEGER DEFAULT 0
);
```

### Hourly Traffic Aggregation
```sql
CREATE TABLE IF NOT EXISTS HourlyTraffic (
  Date DATE,
  Hour INTEGER,
  Category TEXT,
  ClipCount INTEGER,
  VoiceDurationSec REAL,
  PRIMARY KEY (Date, Hour, Category)
);
```

### Frequency Heatmap
```sql
CREATE TABLE IF NOT EXISTS FrequencyHeatmap (
  StartHz INTEGER,
  EndHz INTEGER,
  TotalDetections INTEGER DEFAULT 0,
  UniqueChannels INTEGER DEFAULT 0,
  PeakHour INTEGER,
  PRIMARY KEY (StartHz, EndHz)
);
```

---

## Key Analysis Queries

### Traffic by Category Over Time
```sql
SELECT 
  strftime('%Y-%m', StartUtc) as month,
  Category,
  COUNT(*) as clip_count,
  SUM(DurationSec) / 60.0 as total_minutes
FROM Clips 
WHERE Deleted = 0 AND Category != ''
GROUP BY month, Category
ORDER BY month DESC;
```

### Most Active Frequencies
```sql
SELECT 
  Hz / 1e6 as frequency_mhz,
  Mode,
  COUNT(*) as detections,
  AVG(DurationSec) as avg_duration_sec
FROM Clips 
WHERE Deleted = 0 AND Class = 'VOICE'
GROUP BY Hz, Mode
ORDER BY detections DESC
LIMIT 20;
```

### Urgency Distribution by Category
```sql
SELECT 
  Category,
  SUM(CASE WHEN Urgency = 3 THEN 1 ELSE 0 END) as emergency_count,
  SUM(CASE WHEN Urgency >= 2 THEN 1 ELSE 0 END) as high_priority_count,
  COUNT(*) as total_clips
FROM Clips 
WHERE Deleted = 0 AND Category != ''
GROUP BY Category;
```

### Channel Activity Patterns by Hour of Day
```sql
SELECT 
  Key,
  strftime('%H', StartUtc) as hour_of_day,
  COUNT(*) as clips_per_hour
FROM Clips 
WHERE Deleted = 0 AND Class = 'VOICE'
GROUP BY Key, hour_of_day
ORDER BY Key, clips_per_hour DESC;
```

### Recent High-Priority Traffic
```sql
SELECT 
  StartUtc,
  Hz / 1e6 as frequency_mhz,
  Category,
  Urgency,
  Transcript
FROM Clips 
WHERE Deleted = 0 AND Urgency >= 2
ORDER BY StartUtc DESC
LIMIT 50;
```

---

## Data Retention Recommendations

- **Voice clips**: Keep 30 days minimum for pattern analysis
- **Carrier/noise clips**: Keep 3-7 days for threshold calibration
- **Starred clips**: Permanent retention for important communications
- **Transcripts**: Archive after 90 days if no classification updates
- **Lockout history**: Retain indefinitely for policy learning

---

## Database Location

The SQLite database is stored at:
```
%APPDATA%\RandySDR\robot.db
```

Configuration files are stored at:
```
%APPDATA%\RandySDR\robot-config.json
%APPDATA%\RandySDR\scan-settings.json
```

---

## API Methods for Analysis Queries

### Get Clips by Category and Time Range
```csharp
public List<ClipRow> GetClipsByCategory(string category, DateTime fromUtc, DateTime toUtc) =>
    Query($"SELECT {ClipCols} FROM Clips WHERE Deleted=0 AND Category=$c AND StartUtc>=$f AND StartUtc<=$t ORDER BY StartUtc DESC",
        MapClip, ("$c", category), ("$f", Iso(fromUtc)), ("$t", Iso(toUtc)));
```

### Get Channel Statistics
```csharp
public Dictionary<long, ChannelStats> GetChannelStats(DateTime sinceUtc) =>
    Query("SELECT Key, COUNT(*) as total, SUM(CASE WHEN Class='VOICE' THEN 1 ELSE 0 END) as voice_count, AVG(DurationSec) as avg_duration FROM Clips WHERE Deleted=0 AND StartUtc>=$t GROUP BY Key",
        r => new ChannelStats(r.GetInt64(0), r.GetInt32(1), r.GetInt32(2), r.GetDouble(3)),
        ("$t", Iso(sinceUtc))).ToDictionary(k => k.Key);
```

### Get Urgency Summary
`GetUrgencySummary` counts classified clips by urgency, excluding unclassified clips whose
default urgency is zero. Empty results return zero counts. `GetAverageDuration` returns NULL
when no clips match.

---

## Version History

- **v1.0** (2026-11-11): Initial data dictionary with core clip metadata, audio metrics, and analysis queries
- **v1.1** (2026-11-11): Added suggested tables for advanced analytics and API method examples
- **v1.2** (2026-11-11): Clarified persisted fields, migration, and analysis limitations.

---

## Related Documentation

- [ARCHITECTURE.md](ARCHITECTURE.md) - System architecture and data flow
- [RobotController.cs](Robot/RobotController.cs) - Main automation pipeline orchestrator
- [ClipAnalyzer.cs](Robot/ClipAnalyzer.cs) - Audio classification logic
- [OllamaClient.cs](Robot/OllamaClient.cs) - AI classification client
