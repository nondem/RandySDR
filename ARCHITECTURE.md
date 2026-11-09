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
| | `Form1.Gain.cs` | Tuner/RTL AGC, manual gain, ADC overload (`lblClip`), signal meter. |
| | `Form1.Audio.cs` | Volume, squelch, de-emphasis, mode, I/Q swap/invert. |
| | `Form1.Spectrum.cs` | Spectrum timer, zoom, range, ref level. |
| | `Form1.Diagnostics.cs` | Diagnostics report. |
| | `Form1.Designer.cs` / `.resx` | Layout (keep handler names in sync). |
| | `SpectrumDisplay.cs`, `FrequencySelector.cs` | Custom controls. |

## Signal scanner (Scan/ + UI/Form1.Scanner.cs)
- `Scan/ScanSettings.cs`: range, step, method, threshold (dB above noise floor), hold mode, lockouts; JSON in `%AppData%\RandySDR\scan-settings.json`.
- `Scan/HopMeasurement.cs`: averaged 4096-FFT power, DC-spike mask, channel power, noise floor (25th percentile).
- `Scan/SignalScanner.cs`: state machine; `Tick()` is called by a UI-thread timer (25 ms). Retunes via callback, waits for fresh USB buffers (`BuffersProcessed >= tune+2` and SettleMs), reads `ReceiverPipeline.LastIqData`. Locks onto the strongest armed, non-locked-out channel; released channels re-arm only after dropping below threshold-2 dB.
- `UI/Form1.Scanner.cs`: scanner cards built in code from TableLayoutPanels, presets, log + CSV, lockout list, auto mode by band, audio muted while hopping (`_scanMuted`, applied in `UpdateDemodulatorSettings`). `UI/ScanGraph.cs` plots strength (dB grid, MHz axis, threshold line).
- Law enforcement VHF/UHF search presets use narrow FM, 1.25/6.25 kHz grids respectively, and the scanner's minimum 12.5 kHz detection window. The VHF scan plan excludes 162.400–162.550 MHz weather channels with a 50 kHz guard on each side; custom scans and the NOAA preset remain unaffected. Their selection is saved so the VHF airband overlap remains narrow FM when automatic mode is enabled. These broad ranges are not dedicated channel plans, and digital/trunked systems are not decoded.
- `UI/Form1.Layout.cs` builds cards and uses `UI/PanelLayoutEngine.cs` to migrate legacy order and size settings. `UI/Form1.Dashboard.cs` owns the fixed toolbar, freeform scrollable workspace, built-in layouts, panel visibility, focus, locking, optional grid snapping, and layout application. `UI/DashboardResizeHandle.cs` handles right, bottom, and corner resizing. Cards have scrollable content viewports so smaller dimensions do not discard controls.
- `UI/DashboardLayoutStore.cs` validates and atomically saves Custom 1 to `%AppData%\RandySDR\dashboard-layout.json`, including position, size, visibility, collapse state, and stacking order. The saved custom layout loads at startup; older `panel-layout.json` and `panel-sizes.json` files are retained for migration. Unknown panels are ignored, missing panels use defaults, and panels saved outside a smaller viewport are moved into view when possible. The window is clamped to the screen working area on load. Designer uses `AutoScaleMode.Dpi` at 144 dpi. Dark theme is set in `Program.cs`.
- Scanner is unverified on live hardware; the spectrum timer remains active during scanning to keep the display updated.

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

## Known ideas & backlog

- Bias-T checkbox with runtime on/off UI control.
- Click-to-tune on the spectrum display.
- Fully working scan UI with lockouts and presets enhancements.
- Settings persistence beyond scan settings.
- DSP unit-test project for demodulators/squelch logic.
- Fix mojibake in some Debug strings in `RtlSdrWrapper.cs`.

## Styling & appearance

CardGroupBox + Form1.Style.cs provide the card-based dark theme with animated glow on active cards and foldable headers.
