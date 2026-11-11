# RandySDR Architecture

WinForms (.NET 10, `net10.0-windows`) RTL-SDR receiver. Hardware: RTL-SDR Blog V4. Native driver: `rtlsdr.dll` + `libusb-1.0.dll` (RTL-SDR Blog build). Audio: NAudio. Single namespace: `RandySDR`.

## Data flow

```
USB --rtlsdr_read_sync--> ThreadSafeBuffer (read thread) --ConcurrentQueue<byte[]>--> ReceiverPipeline DSP thread
      --> FmDemodulator.Demodulate (8-bit I/Q -> float audio) --> AudioPlayer (NAudio)
UI timer (50 ms) --> ReceiverPipeline.LastIqData --> SpectrumAnalyzer (FFT) --> SpectrumDisplay (spectrum + waterfall)
```

## File map

| Folder | File | Purpose |
|---|---|---|
| `Sdr/` | `RtlSdrWrapper.cs` | Device open/close, sample rate, frequency, gain, AGC, bias-T, PPM, offset tuning, direct sampling. `DeviceInfo`, `NativeMethods` live here too. |
| | `NativeRtlSdr.cs` | Raw P/Invoke declarations (`rtlsdr_*`). |
| | `ThreadSafeBuffer.cs` | USB read thread + queue. Does nothing but read and enqueue. |
| | `ReceiverPipeline.cs` | Owns read thread, DSP thread, demodulator, audio player. UI uses only this. |
| | `PpmCalibrator.cs` | PPM estimation helper. |
| `Dsp/` | `DemodulationMode.cs` | Mode enum (Wide FM, Narrow FM, AM, USB, LSB, CW, Raw, Quadrature...). |
| | `FmDemodulator.cs` | All demodulation, decimation, squelch, volume, final clamp to [-1, 1]. |
| | `Filters.cs` | Biquad, FIR/low/high/band-pass, de-emphasis, DC blocker, AGC. |
| | `SpectrumAnalyzer.cs` | FFT -> dB bins with averaging. |
| | `FrequencyDiagnostics.cs` | Offset / signal diagnostics from I/Q. |
| `Audio/` | `AudioPlayer.cs` | `WaveOutEvent` + `BufferedWaveProvider`, test tone. |
| `UI/` | `Form1.cs` | Fields, constructor, control init, enable/disable state, shutdown. |
| | `Form1.Device.cs` | Open / Close / Start / Stop handlers. |
| | `Form1.Tuning.cs` | Frequency, sample rate, PPM, tuner BW, offset tuning, scan, direct sampling. |
| | `Form1.Gain.cs` | Tuner/RTL AGC, manual gain, ADC overload (`lblClip`). |
| | `Form1.Audio.cs` | Volume, squelch, de-emphasis, mode, I/Q swap/invert. |
| | `Form1.Spectrum.cs` | Spectrum timer, zoom, range, ref level. |
| | `Form1.Scanner.cs` | Scanner state machine UI, cards, presets, lockouts, CSV export. |
| | `Form1.SquelchLink.cs` | Optional link between audio squelch and scanner detection threshold; noise floor tracking on quiet hops. |
| | `Form1.Layout.cs` | Dashboard layout builder, migration of legacy panel settings. |
| | `Form1.Diagnostics.cs` | Diagnostics report UI. |
| | `Form1.Robot.cs` | Scan robot UI: status, recent clips, lockout suggestions, Re-run AI button. |
| | `Form1.Collapse.cs` | Card collapse/expand functionality for dashboard panels. |
| | `Form1.Style.cs` | Card-based dark theme styling, animated glow on active cards. |
| | `Form1.Designer.cs` / `.resx` | Layout (keep handler names in sync). |
| | `CardGroupBox.cs`, `CardResizeGrip.cs` | Custom card controls with foldable headers and glow effects. |
| | `DashboardLayoutStore.cs`, `DashboardResizeHandle.cs` | Dashboard layout persistence, resize handles for panels. |
| | `PanelLayoutEngine.cs` | Legacy panel settings migration engine. |
| | `ClipDetailsForm.cs` | Read-only transcript/summary viewer for selected clips. |
| | `RobotSettingsForm.cs` | Settings dialog: Ollama, Whisper, recording, retention, early skip, triage thresholds. |
| | `SpectrumDisplay.cs`, `FrequencySelector.cs`, `SignalMeter.cs`, `ScanGraph.cs` | Custom controls. |
| `Robot/` | `RobotController.cs` | Main automation pipeline orchestrator: scanner events → recorder → analyzer → AI → lockout policy. |
| | `ClipRecorder.cs` | Audio capture from DSP tap, WAV file writing, disk space management. |
| | `WavClipWriter.cs` | Low-level WAV writer with segment management and error handling. |
| | `SpeechSkipDetector.cs` | Experimental speech-based early skip detection (2-second window analysis). |
| | `ClipAnalyzer.cs`, `ClipAnalyzerOptions.cs` | VAD-based clip classification: Carrier/Tone/Noise/Voice/Uncertain. Configurable thresholds. |
| | `LockoutPolicy.cs` | Auto-lockout decision logic: 3-consecutive-junk rule, total junk time limit, 24-hour window. |
| | `RobotStore.cs`, `RobotConfig.cs` | SQLite database access and configuration persistence. |
| | `WhisperTranscriber.cs` | Speech-to-text using Whisper.net with CUDA support, hallucination filtering, model download/cache. |
| | `OllamaClient.cs` | LLM-based classification using local Ollama server, JSON-schema constrained responses. |
| `Scan/` | `SignalScanner.cs` | Scanner state machine: retune, settle, measure, lock/release signals. |
| | `ScanSettings.cs` | Scan configuration: range, step, method, threshold, hold mode, lockouts. |
| | `HopMeasurement.cs` | Per-channel FFT power measurements, noise floor tracking (25th percentile). |
| | `CbChannels.cs` | CB radio channel definitions and presets. |
| `Tests/` | `RobotChecks/` | Synthetic audio tests for VAD classification logic. |
| | `PanelLayoutChecks/`, `PanelResizeChecks/` | WinForms interaction and layout validation tests. |
| | `ScanPresetChecks/` | Scanner preset validation tests. |

## Signal scanner (Scan/ + UI/Form1.Scanner.cs)
- `Scan/ScanSettings.cs`: range, step, method, threshold (dB above noise floor), hold mode, lockouts; JSON in `%AppData%\RandySDR\scan-settings.json`.
- `Scan/HopMeasurement.cs`: averaged 4096-FFT power, DC-spike mask, channel power, noise floor (25th percentile).
- `Scan/SignalScanner.cs`: state machine; `Tick()` is called by a UI-thread timer (25 ms). Retunes via callback, waits for fresh USB buffers (`BuffersProcessed >= tune+2` and SettleMs), reads `ReceiverPipeline.LastIqData`. Locks onto the strongest armed, non-locked-out channel; released channels re-arm only after dropping below threshold-2 dB.
- New scans wait at least one second from the first scanner tick and then require two fresh buffers before measuring or detecting, so initial tuner and bandwidth changes can settle. Resume and ordinary retunes still use the normal per-hop settling; editing a running scan does not restart the startup delay.
- `UI/Form1.Scanner.cs`: scanner cards built in code from TableLayoutPanels, presets, log + CSV, lockout list, auto mode by band, audio muted while hopping (`_scanMuted`, applied in `UpdateDemodulatorSettings`). `UI/ScanGraph.cs` plots strength (dB grid, MHz axis, threshold line).
- Scan Control offers normal and robot scans. Normal scanning bypasses robot audio capture, early skip and robot lockouts while retaining manual scan lockouts and settings; robot scans use the saved robot options. Stop the active scan before choosing the other mode. The receiver's Scan button starts a normal scan or stops whichever scan is active. Previously queued robot analysis can continue independently.
- Squelch-link feature (`Form1.SquelchLink.cs`): optional link between audio squelch and scanner detection threshold. Linked mode uses a quiet scan hop to estimate in-channel noise floor; audio squelch = tracked floor + detection threshold + offset, with minimum separation from floor. Manual squelch remains active until a quiet hop is measured.
- `UI/SignalMeter.cs`: custom control for visual signal strength meter with gradient fill and tick marks.
- `UI/FrequencySelector.cs`: compact grouped GHz/MHz/kHz/Hz display; digit buttons, keyboard arrows and the mouse wheel adjust the corresponding frequency step. Wheel input targets the digit under the pointer without changing focus.
- Device opening/configuration and gain discovery run on a background thread using a private wrapper; the UI reports progress and hands ownership to the receiver only after success. Closing during opening waits asynchronously for the driver call and releases the device before exiting.
- RTL-SDR returns `-2` for an unchanged PPM correction and for unsupported offset tuning on R820T/R828D tuners. These are accepted only when driver readback confirms the requested PPM or disabled offset state; other configuration failures identify the affected settings.
- Scan range and method remain editable during a scan. Valid edits rebuild the channel plan immediately; an invalid range leaves the previous plan running and displays a warning until corrected. Replanning resets sweep and learned-threshold state.
- With automatic mode enabled, each named preset selects its documented AM, wide-FM, or narrow-FM mode when selected and at scan start; custom ranges use the first channel at start and the locked frequency thereafter. Unknown custom bands keep the current mode. Preset identities are persisted so overlapping bands retain the preset's mode; manual mode remains unchanged when automatic selection is off (except the existing CB and law-enforcement preset shortcuts).
- Law enforcement VHF/UHF search presets use narrow FM, 1.25/6.25 kHz grids respectively, and the scanner's minimum 12.5 kHz detection window. The VHF scan plan excludes 162.400–162.550 MHz weather channels with a 50 kHz guard on each side; custom scans and the NOAA preset remain unaffected. Their selection is saved so the VHF airband overlap remains narrow FM when automatic mode is enabled. These broad ranges are not dedicated channel plans, and digital/trunked systems are not decoded.
- `UI/Form1.Layout.cs` builds cards and uses `UI/PanelLayoutEngine.cs` to migrate legacy order and size settings. `UI/Form1.Dashboard.cs` owns the fixed toolbar, freeform scrollable workspace, built-in layouts, panel visibility, focus, locking, optional grid snapping, and layout application. `UI/DashboardResizeHandle.cs` handles right, bottom, and corner resizing. Cards have scrollable content viewports so smaller dimensions do not discard controls.
- `UI/DashboardLayoutStore.cs` validates and atomically saves Custom 1 to `%AppData%\RandySDR\dashboard-layout.json`, including position, size, visibility, collapse state, and stacking order. The saved custom layout loads at startup; older `panel-layout.json` and `panel-sizes.json` files are retained for migration. Unknown panels are ignored, missing panels use defaults, and panels saved outside a smaller viewport are moved into view when possible. The window is clamped to the screen working area on load. Designer uses `AutoScaleMode.Dpi` at 144 dpi. Dark theme is set in `Program.cs`.
- Scanner is unverified on live hardware; the spectrum timer remains active during scanning to keep the display updated.

## Scan robot (Robot/ + UI/Form1.Robot.cs)

Implements `sdr_voice_communication_automation_system_plan.md` (rev 2). Recording, AI and early skips are opt-in; lockout suggestions default to enabled. Manual lockout, Resume and Stop take precedence.

- **ClipRecorder**: Queues pre-squelch audio from `ReceiverPipeline.AudioTap(float[], bool squelchOpen)`, writes mono 16-bit 48 kHz WAVs to the configured folder (default `%UserProfile%\RandySDR\Clips`), discards the first 0.3 seconds of each lock, buffers and removes trailing closed-squelch audio, and splits on long squelch gaps or maximum length. Segment filenames are unique.
- **WavClipWriter**: Low-level WAV writer that handles file I/O, disk space checks, and segment management. Pauses below configured free-space limit or above 90% disk use with retries and status display.
- **SpeechSkipDetector**: Experimental speech-based early skip (enabled in Settings, defaults off). Analyzes two-second windows on the recorder worker, waits for the configured amount of captured audio (at least six seconds), and does not skip if it detects speech-like/uncertain audio, any dropped block, or a settings change during the lock. The fixed heuristic is not reliable proof that no voice is present: weak or delayed transmissions can still be missed. Separate from squelch-based early skip.
- **ClipAnalyzer**: Cheap, LLM-free triage using energy + spectral-shape VAD (no model files). Classes: `Carrier`, `Tone`, `Noise`, `Voice`, `Uncertain`. Configurable via `ClipAnalyzerOptions` (min speech seconds/ratio, max carrier ratio). Clips <63ms (<3 frames) are classified as Carrier or Noise based on RMS level.
- Completed clips go to an analysis thread, stored in SQLite (`RobotStore`, `%AppData%\RandySDR\robot.db` or the layout directory; `Clips` table, `Lockouts`, decision log), and Voice/Uncertain clips continue to an AI thread: `WhisperTranscriber` (Whisper.net + CUDA, model downloaded to `%AppData%\RandySDR\models`) then `OllamaClient` (default `qwen3.5:35b-a3b-q4_K_M` at `http://localhost:11434`). Ollama/Whisper failures leave clips "unclassified" and never affect scanning.
- **LockoutPolicy**: Evaluates clip history; modes are Off, SuggestOnly (default) and Auto. Multiple segments of one lock count only once toward the three-lock rule. Lockouts are keyed by frequency rounded to the scan step and consulted through `SignalScanner.ExtraLockout`; expiry is refreshed at least once per minute. A voice or starred clip protects the channel, and starring or correcting a junk clip restores its existing robot lockouts. An empty Whisper result does not turn uncertain audio into noise. Audio retention defaults to 30 days voice/uncertain and 3 days carrier/noise (`RobotStore.PurgeAudio`).
- Config: `%AppData%\RandySDR\robot-config.json` (`RobotConfig`). UI: "Scan Robot" card with status, recent clips and lockout suggestions; a 500 ms `_robotTimer` handles early-skip resume and list refresh.
- Recent clips: select one and choose View details, double-click a row, or press Enter to open a resizable, read-only view of its full transcript and summary. Text can be selected and copied.
- Settings: the toolbar "Settings..." button opens `UI\RobotSettingsForm.cs` for Ollama, Whisper, recording, retention, early skip and triage thresholds. A changed Whisper model loads on the next transcription, after any in-flight transcription completes. The card displays download/loading status, recording failures, dropped audio and clips that could not be indexed; use Re-run AI to retry a clip.
- Disk checks happen when a segment opens; recording pauses below the configured free-space limit or above 90% disk use, with retries and status on the card. They do not monitor free space continuously within an open segment.
- Threads: recorder (DSP tap, no I/O on it beyond a queue), analysis thread, AI thread. Dispose the controller before the pipeline.
- Checks: `dotnet run --project Tests\RobotChecks\RobotChecks.csproj` (synthetic audio; the VAD is untested on real RF audio).

## Threading rules

- Never do DSP or audio in the USB read thread. Never touch UI controls from the DSP thread.
- UI thread sets `Demodulator` properties (`Volume`, `Mode`, ...); the DSP thread reads them live.
- `ReceiverPipeline.Stop()` joins the DSP thread before stopping the reader; `Dispose` the pipeline **before** `CloseDevice()`.

## P/Invoke catalog (rtlsdr.dll functions)

The wrapper and native layer call these functions from `rtlsdr.dll`:

| Function | Purpose | Notes |
|---|---|---|
| `rtlsdr_get_device_count()` | Returns number of connected devices | Use before opening. |
| `rtlsdr_get_device_name(index)` | Returns device name string | For enumeration UI. |
| `rtlsdr_open(&dev, index)` | Opens a specific device by index | Returns 0 on success; use `GetDeviceHandle()` to get `IntPtr`. |
| `rtlsdr_close(dev)` | Closes an open device | Called during disposal. |
| `rtlsdr_set_center_frequency(dev, freqHz)` | Sets RF center frequency | In Hz. |
| `rtlsdr_set_sample_rate(dev, rate)` | Sets baseband sample rate | In Hz (e.g., 2.4e6). |
| `rtlsdr_set_tuner_gain(dev, gainTenthsDb)` | Sets manual tuner gain | Requires tuner AGC off. |
| `rtlsdr_set_tuner_agc_mode(dev, enabled)` | Enables/disables tuner auto-gain | Recommended off for V4 to preserve weak signals. |
| `rtlsdr_set_rtl_agc_mode(dev, enabled)` | Enables/disables RTL ADC auto-gain | Recommended on in manual gain mode (per project defaults). |
| `rtlsdr_set_tuner_bandwidth(dev, bwHz)` | Sets RF filter bandwidth | In Hz; affects passband during scanning. |
| `rtlsdr_set_offset_tuning(dev, enabled)` | Enables offset tuning | Helps avoid DC spike by shifting center slightly. |
| `rtlsdr_set_ppm_correction(dev, ppm)` | Clock drift compensation | Set via UI for PPM calibration. |
| `rtlsdr_set_bias_tee(dev, enabled)` | Powers external LNA/antenna via USB | Optional; use with external active antennas. |
| `rtlsdr_set_direct_sampling_mode(dev, mode)` | Selects RF path: off/quadrature/I-only/Q-only | Mode 0 = normal (quadrature), 1 = I, 2 = Q, etc. |
| `rtlsdr_reset_buffer(dev)` | Discards pending buffers after changes | Call after configuration sequence to avoid stale data. |

Note: The actual P/Invoke declarations reside in [`NativeRtlSdr.cs`](D:/00codeLab/RandySDR/RandySDR/Sdr/NativeRtlSdr.cs) and the wrapper's internal [`NativeMethods`](D:/00codeLab/RandySDR/RandySDR/Sdr/RtlSdrWrapper.cs#L271-L316).

## RtlSdrWrapper API reference

Managed wrapper class [`RtlSdrWrapper`](D:/00codeLab/RandySDR/RandySDR/Sdr/RtlSdrWrapper.cs) exposes:

| Method / Property | Purpose | Recommended usage pattern |
|---|---|---|
| `GetConnectedDevices()` | Lists RTL-SDR devices (index, name, serial). | Before opening. |
| `OpenDevice(uint index)` | Opens device at index; applies basic defaults. | After enumeration. |
| `ConfigureReceiveChain(...)` | Full chain init in V4 order: sample rate → sampling mode → frequency → gain/AGC → side settings → reset buffer. | **Preferred** one-shot config for new sessions. |
| `ConfigureTunerForManualGain(sampleRate, freq, gainTenthsDb, rtlAgc, tunerAgc)` | Sets manual gain mode with optional RTL AGC on. | Call after open and before start; useful when only gain/AGC needs tuning. |
| `GetDeviceHandle()` | Returns unsafe handle for the USB read thread. | Used by `ThreadSafeBuffer`. |
| `SetFrequency(hz)`, `SetSampleRate(rate)` | RF and baseband parameters. | While running; triggers DSP recreation if sample rate changes. |
| `SetTunerGain(tenthsDb)`, `SetTunerAgc(bool)`, `SetRtlAgc(bool)` | Tuner and RTL AGC controls. | During runtime to adjust sensitivity. |
| `SetTunerBandwidth(bwHz)`, `SetOffsetTuning(bool)` | RF filter and offset mode. | Adjust passband/DC avoidance on the fly. |
| `SetPpmCorrection(ppm)`, `GetPpmCorrection()` | Clock drift compensation. | Set manually or via calibration helpers. |
| `BiasTeeEnabled` { get/set } | Bias-T power output on/off. | Use with external active antennas. |
| `SetSamplingMode(mode)` | Selects I/Q path mode (0=normal/quadrature, 1=I, 2=Q). | After open and before start; affects signal quality. |
| `GetTunerGains()` | Returns supported gain values in tenths of dB. | Populate UI lists. |
| `Start(Action<byte[]> cb)`, `Stop()` | Streaming control with callback delivery. | Called after all configuration; data callbacks are 1024-byte I/Q buffers (uint8 interleaved). |
| `IsRunning`, `IsTestMode` | State flags. | Check before operations or to show test warnings. |

**Patterns:**
- Use `ConfigureReceiveChain()` for a clean, all-in-one setup sequence when opening a device.
- For quick gain/AGC adjustments, call `ConfigureTunerForManualGain()` before starting streaming.
- After any retune or significant parameter change, always call `ResetBuffer` (done automatically by `ConfigureReceiveChain`).

## RTL-SDR V4 init sequence (order matters)

The recommended order ensures stable behavior on the V4 chip:
1. Open device (`OpenDevice`).
2. Set sample rate.
3. Set sampling mode (normal/quadrature/I-only/Q-only).
4. Set center frequency.
5. Configure tuner AGC and manual gain (or enable RTL AGC).
6. Set PPM, tuner bandwidth, offset tuning, bias-Tee.
7. Reset buffers.
8. Start streaming (`Start`).

Manual tuner gain mode with a high gain (e.g., 49.6 dB) is default; hardware tuner AGC buries weak signals. Use manual gain + RTL AGC.

## LLM agent quick-start patterns

**Adding a demodulation mode:**
```csharp
// 1. Add to DemodulationMode enum (Dsp/DemodulationMode.cs)
// 2. Handle in FmDemodulator.Demodulate (switch on the new mode)
// 3. Add a radio button in the Designer (Form1.Designer.cs), map it in Form1.GetDemodulationMode, enable it in UpdateControlsState
```

**Adding a UI control:**
```csharp
// 1. Add control in Form1.Designer.cs inside the right group box
// 2. Wire its handler in Build*Panel or constructor
// 3. Put the handler in the matching Form1.*.cs partial (e.g., Audio, Gain, Tuning)
// 4. Enable/disable it in UpdateControlsState based on device state
```

**Adding a device setting:**
```csharp
// 1. Add P/Invoke declaration in NativeRtlSdr.cs or NativeMethods
// 2. Wrap with a thin method in RtlSdrWrapper (prefer ConfigureReceiveChain for multi-parameter changes)
// 3. Expose via a handler in Form1.*.cs and wire the control
```

## Build & run

```bash
dotnet build RandySDR/RandySDR.csproj
```
Run with [`RunApp.ps1`](D:/00codeLab/RandySDR/RandySDR/RunApp.ps1) (needs `rtlsdr.dll` and `libusb-1.0.dll` next to the exe).
Run the dependency-free layout checks with `dotnet run --project Tests\PanelLayoutChecks\PanelLayoutChecks.csproj` from the project directory.
Run the WinForms interaction checks with `dotnet run --project Tests\PanelResizeChecks\PanelResizeChecks.csproj`; they require the native RTL-SDR DLLs on the executable search path and use a temporary layout directory.

## Gotchas & diagnostics

- Analyzer WFO1000 is an error: public properties on custom controls need `[DesignerSerializationVisibility(Hidden)]`.
- Source files use CRLF line endings.
- Several controls are created in the Designer but not added to a form (hidden): `numSampleRate`, `numPpm`, `numTunerBandwidth`, `btnPpm*`, `btnScan`, `btnDiagnostics`, `lblFreqDisplay`, `chkSwapIQ`, `chkInvertDemod`. Handlers still compile and are referenced.
- `RtlSdrWrapper.IsRunning` is always false; use `btnStop.Enabled` / `btnClose.Enabled`.
- Layout was never visually verified by screenshot.
- Legacy callback path in `RtlSdrWrapper.Start`/`OnDataReceived` and `NativeRtlSdr.Start/ReadLoop` is unused; can be deleted.
- Audio path clamps then applies `tanh` (redundant).
- **ClipAnalyzer weaknesses**: 
  - Memory inefficiency: loads entire audio file into List<float> before converting to array (~30% slower, double allocation)
  - Assumes 48kHz sample rate: frequency bands calibrated for exactly 48kHz, will misclassify non-48kHz audio
  - No error handling: File I/O has no try-catch; corrupted WAV files crash analysis thread
  - Edge case: clips <63ms (<3 frames) classified as Carrier or Noise based on RMS only
  - Magic numbers: thresholds like `0.55`, `0.45`, `0.35` lack documentation for tuning rationale
- **WhisperTranscriber**: Model download can fail silently; status messages appear in UI but no retry mechanism. Hallucination filter may over-filter legitimate speech containing phrases like "subscribe" or "thanks for watching".
- **OllamaClient**: No timeout on initial connection; `HttpClient` created per instance without pooling. JSON parsing failures return null silently.
- **RobotController**: Analysis queue max 500 clips, AI queue max 200 clips; overflow increments `LostAnalysisClips` counter but no alert. Background threads are `IsBackground = true`, so they terminate abruptly on app shutdown.

## Known ideas & backlog

- Click-to-tune on the spectrum display.
- Settings persistence beyond scan settings (dashboard layout saved, but app-wide settings not yet persisted).
- DSP unit-test project for demodulators/squelch logic.
- Fix mojibake in some Debug strings in `RtlSdrWrapper.cs`.
- **ClipAnalyzer improvements**: 
  - Add sample rate validation/resampling support
  - Add try-catch around file I/O with graceful error reporting
  - Document threshold rationale and tuning methodology
  - Combine energy calculation with FFT windowing loop for ~30% speedup
  - Better handling of clips <63ms (distinguish silence from carrier)
- **Robot reliability**: 
  - Add retry mechanism for Whisper model download failures
  - Implement HttpClient pooling for Ollama requests
  - Alert when analysis/AI queue overflow occurs
  - Graceful shutdown with cleanup of in-progress analyses
- **SpeechSkipDetector**: Validate against real RF audio; compare performance vs squelch-based early skip.

## Styling & appearance

CardGroupBox + Form1.Style.cs provide the card-based dark theme with animated glow on active cards and foldable headers.
