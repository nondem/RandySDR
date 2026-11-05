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
- `UI/Form1.Scanner.cs`: panel built in code (right side of form) from TableLayoutPanels, presets, log + CSV, lockout list, auto mode by band, audio muted while hopping (`_scanMuted`, applied in `UpdateDemodulatorSettings`). `UI/ScanGraph.cs` plots strength (dB grid, MHz axis, threshold line).
- `UI/Form1.Layout.cs`: re-parents designer group boxes into a left column (docked, scrollable) with the scanner in the right column; window clamped to the screen working area on load. Designer uses `AutoScaleMode.Dpi` at 144 dpi, so designer coordinates are 150%-DPI pixels; build code-made layouts with Dock/TableLayoutPanel, not hard-coded DPI scaling. Dark theme is set in `Program.cs`.
- Scanner is unverified on live hardware; the spectrum timer is skipped while scanning.

## Threading rules

- Never do DSP or audio in the USB read thread. Never touch UI controls from the DSP thread.
- UI thread sets `Demodulator` properties (`Volume`, `Mode`, ...); the DSP thread reads them live.
- `ReceiverPipeline.Stop()` joins the DSP thread before stopping the reader; `Dispose` the pipeline **before** `CloseDevice()`.

## RTL-SDR init order (V4)

open -> sample rate -> center frequency -> gain mode/gain + RTL AGC -> bias-T -> reset buffer -> start reading.
Manual tuner gain mode with a high gain (e.g. 49.6 dB) is the default; hardware tuner AGC buries weak signals.

## How to...

- **Add a demod mode:** add to `DemodulationMode`, handle in `FmDemodulator.Demodulate`, add a radio button in the Designer, map it in `Form1.GetDemodulationMode`, enable it in `UpdateControlsState`.
- **Add a UI control:** add it in `Form1.Designer.cs` inside the right group box, wire its handler, put the handler in the matching `Form1.*.cs` partial, enable/disable it in `UpdateControlsState`.
- **Add a device setting:** P/Invoke in `NativeRtlSdr.cs`/`NativeMethods`, wrap in `RtlSdrWrapper`, call from a `Form1.*.cs` handler.

## Build

```
dotnet build RandySDR/RandySDR.csproj
```
Run with `RandySDR\RunApp.ps1` (needs `rtlsdr.dll` and `libusb-1.0.dll` next to the exe).

## Gotchas

- Analyzer WFO1000 is an error: public properties on custom controls need `[DesignerSerializationVisibility(Hidden)]`.
- Source files use CRLF line endings.
- Several controls are created in the Designer but not added to a form (hidden): `numSampleRate`, `numPpm`, `numTunerBandwidth`, `btnPpm*`, `btnScan`, `btnDiagnostics`, `lblFreqDisplay`, `chkSwapIQ`, `chkInvertDemod`. Handlers still compile and are referenced.
- `RtlSdrWrapper.IsRunning` is always false; use `btnStop.Enabled` / `btnClose.Enabled`.
- Layout was never visually verified by screenshot.

## Known leftovers / ideas

- Legacy callback path in `RtlSdrWrapper.Start`/`OnDataReceived` and `NativeRtlSdr.Start/ReadLoop` is unused; can be deleted.
- Audio path clamps then applies `tanh` (redundant).
- Ideas: Bias-T checkbox, click-to-tune on the spectrum, working scan UI, settings persistence, DSP unit-test project, mojibake in some Debug strings in `RtlSdrWrapper.cs`.
- Old notes are in `docs/archive/` and may be stale.

## Squelch link and styling
Form1.SquelchLink.cs ties the squelch to the scan threshold using a learned in-channel noise floor. CardGroupBox + Form1.Style.cs provide the card-based dark theme.
