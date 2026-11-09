# Agent guide

Read [ARCHITECTURE.md](ARCHITECTURE.md) first (data flow, file map, threading rules, gotchas).

- Build: `dotnet build RandySDR/RandySDR.csproj` from the repo root (`d:\00codeLab\RandySDR`). Must end with 0 errors.
- One type per file; folders `Sdr/`, `Dsp/`, `Audio/`, `UI/`; single namespace `RandySDR`.
- Form1 is a partial class split by concern (`UI/Form1.*.cs`). Put new handlers in the matching file.
- Keep DSP out of the USB read thread and UI out of the DSP thread. Use `ReceiverPipeline`.
- Keep files CRLF. Keep final audio clamped to [-1, 1].
- `docs/archive/` is historical and may be wrong; do not trust it over the code.

- Scanner: see "Signal scanner" in ARCHITECTURE.md. Tune only through `SignalScanner`'s callback while scanning; stop it with `StopScan()` before closing/stopping the device.

- Squelch: in-channel power (post-FIR, dBFS) in FmDemodulator.UpdateSquelch with hysteresis, hang and gain ramp; slider maps 0=off, 1-100 to -80..-5 dB. UI in UI\Form1.Audio.cs (BuildAudioPanel, Auto button).

- Squelch link: UI\Form1.SquelchLink.cs learns an in-channel noise floor on settled hops whose centre appears quiet in the FFT; linked squelch = floor + scan threshold + offset (ScanSettings.LinkSquelch/SquelchOffsetDb). Calibration is invalidated by new scans, device close, gain changes, sample-rate changes, or tuner-bandwidth changes. UI shows a status label indicating whether the floor is waiting for a measurement.
- Look and feel: UI\CardGroupBox.cs (custom-painted card, used for all group boxes); UI\Form1.Style.cs applies window colour and flat buttons.
