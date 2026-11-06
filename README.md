# RandySDR
An RTL-SDR receiver for SDR Blog V4 hardware with scanning, spectrum analysis and multiple demod modes. Built on .NET 10 and NAudio.

## Quick start
- Clone and then run `.\RunApp.ps1` to build and launch.
- Open the RTL‑SDR device, set a frequency and choose Wide FM. Press **Start**. You should hear FM broadcast or an airband station depending on your antenna.

## Key features
**Scanning with squelch lock**  
The app can scan a band for signals above a threshold. When "Link squelch to detection threshold" is enabled, the in‑channel audio squelch follows the noise floor plus the scan threshold plus a small offset you set. This keeps the squelch behavior consistent across the band and makes scanning quieter. Run one scan to learn the current noise floor; until then, linked squelch shows "linked (scan to learn)".

**Spectrum analyzer**  
A real‑time FFT display with Auto range and manual controls for Zoom, Range and Reference level. You can tune while viewing the spectrum. The spectrum is off by default on startup to save time.

**Demodulation modes**  
Wide FM, Narrow FM, AM (broadcast band), USB, LSB, CW and RAW IQ are supported. For airband stations that transmit in wideband AM, use Wide FM demod with de‑emphasis enabled or switch explicitly to AM mode and tune the carrier. The app defaults to 103.1 MHz Wide FM for easy testing on first launch.

**Gain settings**  
Manual tuner gain is set to a high value (49.6 dB) and baseband AGC is enabled via `rtlsdr_set_agc_mode`. You can toggle these in the UI, but the defaults give strong weak signals and avoid clipping after the first seconds of playback.

## Build and run
- Prerequisites: .NET 10 SDK installed, a RTL‑SDR dongle with `rtl-sdr.dll` in the system path or copied into the output folder (RTL‑SDR Blog V4 recommended).
- From the repository root:
  ```powershell
  dotnet build RandySDR/RandySDR.csproj
  .\RunApp.ps1
  ```
  `RunApp.ps1` builds and launches the app in one step.

## UI layout notes
- The left panel holds Device, Tuning, Demod Mode, Audio & Signal, Gain & Input and Spectrum controls. Cards group each area with bold headers.
- The right panel is the Scanner/Monitor: presets, start frequency and scan method, plus a small graph and log for detections.
- Frequency digits use Consolas on a dark background. The status bar shows connection state and retune progress during scans.

## Troubleshooting
- If signals are weak or lost in the noise floor, ensure "Manual tuner gain" is on and set to a high value; baseband AGC should be enabled (`rtlsdr_set_agc_mode`).  
- If audio clips or glitches after a few seconds, retune once (set a new frequency and press Enter) to flush internal queues.
- For airband garble on wideband AM stations, switch demod mode to AM or use Wide FM with de‑emphasis enabled. Ensure the antenna is positioned correctly and the station's channel width matches your scan method.

## Tech stack
- Runtime: .NET 10 (net10.0-windows)
- Libraries: NAudio, `rtl-sdr` via C++ interop (`rtl-sdr.dll`)
- Architecture: separate modules for SDR (`Sdr/`), DSP (`Dsp/`), Audio (`Audio/`), UI (`UI/`) and scanning (`Scan/`).

## License and credits
RTL‑SDR Blog V4 dongle with official `rtl-sdr.dll`. Uses NAudio for audio playback. CardGroupBox and style tweaks provide the card-based dark theme.
