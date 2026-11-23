
<img width="1701" height="907" alt="image" src="https://github.com/user-attachments/assets/9a690fe0-3fb7-4b64-8415-2b7ef34dcfe2" />


# RandySDR
An RTL-SDR receiver for SDR Blog V4 hardware with scanning, spectrum analysis, AI-powered transcription/classification, and multiple demod modes. Built on .NET 10 and NAudio.

## Quick start
- Clone and then run `.\RunApp.ps1` to build and launch.
- Open the RTL‑SDR device, set a frequency and choose Wide FM. Press **Start**. You should hear FM broadcast or an airband station depending on your antenna.

## Key features
**Scanning with squelch lock**  
The app can scan a band for signals above a threshold. When "Link squelch to detection threshold" is enabled, the in‑channel audio squelch follows the noise floor plus the scan threshold plus a small offset you set. This keeps the squelch behavior consistent across the band and makes scanning quieter. Run one scan to learn the current noise floor; until then, linked squelch shows "linked (scan to learn)".

The **CB extended 225 channels** preset scans five 45-channel banks from 26.065 to 28.305 MHz, including the five Alpha slots in each bank. The scan status, graph hover and detection log identify channels with LL, L, no suffix, H or HH. With auto-select mode enabled, the preset chooses AM, LSB or USB for each channel; disable it to keep a manually selected mode. Existing saved 40-channel CB settings migrate to the expanded preset.

The extended banks and Alpha frequencies are a receive scan plan, not an assertion that they are authorized U.S. CB channels. Frequencies outside the standard 40 may belong to other services, including amateur radio; transmitting requires compliance with applicable local regulations and licensing.

"Stay until signal drops" holds the channel until the signal has been absent for the configured hold time; it does not impose a 15-second limit. Use fixed-time hold or Resume to leave a continuous carrier. Optional robot early-skip settings can also resume scanning early. Recordings can still be split by a squelch gap or the configured maximum clip length.

**Scan Robot with AI transcription and classification**  
The Scan Robot shows a separate status and heuristic audio class for each clip (Voice, Carrier, Tone, Noise, or Uncertain). When **Transcribe** is enabled, all retained incoming clips are queued for Whisper speech-to-text using `Whisper.net` with CUDA acceleration. Even if triage identifies carrier, noise, tone, or inconclusive audio, clips continue to the AI pipeline when transcription is enabled—this can increase the AI backlog. Clips marked `ai-queue-full` or `ai-failed` can be retried with **Re-run AI**. A `no-speech` result means Whisper detected no intelligible speech, not that speech was absent from the audio. When transcription is disabled, triage-only clips show their heuristic class (`non-voice`, `inconclusive`, etc.). Use **Wrong** to correct misclassifications or **Re-run AI** to reprocess clips through the full pipeline.

When **Classify** is enabled (requires Ollama server running locally), transcripts are sent to a local LLM for semantic classification. The classifier outputs: category (law enforcement, aviation, marine, amateur radio, etc.), urgency level (0-3), a one-sentence summary, confidence score, and keywords. Classification results appear in clip details and the clip list.

**ScreenerBot (optional robot scan mode)**  
For wide, noisy ranges, enable **ScreenerBot (next lock)** on the Scan Robot card and start a robot scan with **Record signals** enabled. Normal scans and robot scans without ScreenerBot are unchanged. During each lock, the recorder screens open-squelch audio on its worker thread using heuristic signal analysis; the separate **ScreenerBot settings** control sets the minimum screening time (four seconds by default). Mode and settings changes take effect at the next lock. Existing squelch, hold and early-skip settings still apply.

When consecutive 2-second windows show consistent static, carrier, or tone evidence, ScreenerBot retains the partial clip with a `screened-*` reason and resumes scanning. Filtered clips bypass automatic transcription and classification, and cannot trigger automatic frequency lockouts. Play them, mark a decision **Wrong**, or explicitly select **Re-run AI** to reprocess through the full pipeline. Clip details retain the screening reason even after reprocessing. Marking a filtered clip wrong contributes to a correction count in ScreenerBot settings, where a longer screening duration can be suggested and applied only with your approval.

Screening is heuristic: speech-like audio, digital signals, or transmissions that begin after noise can be missed. Uncertain classifications, closed squelch, dropped recorder blocks, or reaching the maximum clip length prevent screening out that lock. The retained snippet cannot recover audio after the scanner resumes, and normal retention settings still apply. The mode has not been validated against every radio environment; disable it when missing a transmission is unacceptable.

## Build and run
- Prerequisites: .NET 10 SDK installed, a RTL‑SDR dongle with `rtl-sdr.dll` in the system path or copied into the output folder (RTL‑SDR Blog V4 recommended).
- For AI features: Ollama server running locally (`ollama serve`) with a model like `qwen3.5:35b-a3b-q4_K_M`, and optionally CUDA-capable GPU for Whisper acceleration.
- From the repository root:
  ```powershell
  dotnet build RandySDR/RandySDR.csproj
  .\RunApp.ps1
  ```
  `RunApp.ps1` builds and launches the app in one step.

## AI features setup
**Whisper transcription**: The first time you enable transcription, the app downloads the Whisper model (`LargeV3Turbo` by default) to `%AppData%\RandySDR\models\`. CUDA acceleration is automatically used when available.

**Ollama classification**: Start `ollama serve` before enabling classification. Default model is `qwen3.5:35b-a3b-q4_K_M` at `http://localhost:11434`. Configure URL, model name, timeout, and other parameters in **Robot Settings**. The classifier uses JSON-schema constrained responses for reliable parsing.

## UI layout notes
- The left panel holds Device, Tuning, Demod Mode, Audio & Signal, Gain & Input and Spectrum controls. Cards group each area with bold headers.
- The right panel is the Scanner/Monitor: presets, start frequency and scan method, plus a small graph and log for detections.
- Frequency digits use Consolas on a dark background. The status bar shows connection state and retune progress during scans.
- **Robot Settings** dialog (toolbar button) configures Ollama, Whisper, recording, retention, early skip, and triage thresholds.

## Troubleshooting
- If signals are weak or lost in the noise floor, ensure "Manual tuner gain" is on and set to a high value; baseband AGC should be enabled (`rtlsdr_set_agc_mode`).  
- If audio clips or glitches after a few seconds, retune once (set a new frequency and press Enter) to flush internal queues.
- For airband garble on wideband AM stations, switch demod mode to AM or use Wide FM with de‑emphasis enabled. Ensure the antenna is positioned correctly and the station's channel width matches your scan method.
- If Ollama classification fails: verify `ollama serve` is running, check the URL in Robot Settings, ensure the model is pulled (`ollama pull qwen3.5:35b-a3b-q4_K_M`), and review the clip status for timeout or connection errors.
- If Whisper downloads fail: check internet connectivity, disk space in `%AppData%\RandySDR\`, and try manually downloading the model file.

## Tech stack
- Runtime: .NET 10 (net10.0-windows)
- Core libraries: NAudio, `rtl-sdr` via C++ interop (`rtl-sdr.dll`)
- AI/ML: `Whisper.net` with CUDA support for speech-to-text, Ollama API for LLM classification
- Database: SQLite for clip metadata and transcripts
- Architecture: separate modules for SDR (`Sdr/`), DSP (`Dsp/`), Audio (`Audio/`), UI (`UI/`), scanning (`Scan/`), and AI (`Robot/`).

## License and credits
RTL‑SDR Blog V4 dongle with official `rtl-sdr.dll`. Uses NAudio for audio playback. Whisper.net for speech recognition. CardGroupBox and style tweaks provide the card-based dark theme.
