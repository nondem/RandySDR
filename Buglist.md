Scan Robot:
Far right column seems useless
disable ollama with checkbox/thinking?
-Better ability to select and delete from recent clips table
-Auto Lockout function doesn't seem to work?
-Accept/Restore lockouts doesn't seem to do anything to the lockouts list
accept/restore lockouts far right column seems useless?
waiting/Idle/rec status needs expanding and visual rewrite
tabbed paging 
recent clips buttons rethink
recent clips column autosizing
seperate tables for each band scan output OR column with band identified
Channel activity analysis and classification...summrizations over time.
More obvious notice when signal is locked
scheduled scan starts



Overall App:
--Auto-arrange - improved gui ergonomics
-Start on mute
Hot Frequency Database filled by analyzing comms
Mini-mode
-Text and control spacing automatic management
--Detection log redundancy
-One-click frequency marking with dialog for metadata entry.
Determine data dictionary for saved frequencies.
better iconery

Frequency Spectrum:
--Ability to click-tune on the frequency spectrum
--Current frequency mark on spectrum

New functions:
--ID Digital modes
--decode digital modea

Notes:
Settings are stored in two places:
User settings (JSON files in %AppData%\RandySDR\):
robot-config.json – Scan robot options (recording, transcription, classification, lockouts, 
retention, early skip, Whisper/Ollama settings)
scan-settings.json – Scanner configuration (frequency range, step, threshold, hold mode, 
presets, lockouts)
Database (%AppData%\RandySDR\robot.db):
SQLite database storing clip metadata, transcripts, classifications, and auto-lockout decisions
You can view/edit the JSON files directly or use the app's UI dialogs (Robot Settings, Scanner cards) to modify settings. The Whisper model is cached in %AppData%\RandySDR\models\.