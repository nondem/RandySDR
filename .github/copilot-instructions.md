# Copilot Instructions

See `ARCHITECTURE.md` and `AGENTS.md` in the project root for the file map, threading rules and conventions.
Build: `dotnet build RandySDR/RandySDR.csproj` from the repo root. Streaming goes through `ReceiverPipeline`; never do DSP on the USB read thread. Form1 is split into `UI/Form1.*.cs` partials (Device, Tuning, Gain, Audio, Spectrum, Scanner, SquelchLink, Layout, Diagnostics). Public properties on custom controls need `[DesignerSerializationVisibility(Hidden)]` (WFO1000). Files use CRLF.

## Project Guidelines
- For RTL-SDR V4 support, initialization should follow: open device, set sample rate, set center frequency, configure gain/AGC (tuner AGC or manual gain 40-49 dB, RTL AGC state), configure bias-tee if needed, reset buffer, then start async reading. Use RTL-SDR Blog branch librtlsdr/rtlsdr.dll for correct V4 tuning behavior.
- Scanner feature includes squelch-link mode that tracks noise floor on quiet hops to dynamically adjust audio squelch threshold.
- Custom controls: `SpectrumDisplay`, `FrequencySelector`, `SignalMeter` (gradient fill, tick marks).