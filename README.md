# RandySDR - FM Receiver for RTL-SDR

## Project Overview

RandySDR is a Windows Forms application built with .NET 10 that provides FM broadcast reception using RTL-SDR USB dongles. It implements native FM demodulation, audio processing, and provides comprehensive controls for tuning and signal optimization.

## Current Status

**Version:** 1.0 (Development)
**Status:** Functional with known tuning issues

### Working Features ✅
- RTL-SDR device detection and enumeration
- Direct native API access (bypasses RtlSdrManager for reliability)
- FM demodulation with phase discriminator
- Real-time audio output via NAudio
- PPM (parts-per-million) frequency correction
- Separate Tuner AGC and RTL AGC controls
- Manual gain control with device-specific gain table
- Volume, squelch, and de-emphasis controls
- Signal strength meter with real-time updates
- Frequency offset detection
- FM band scanner (88-108 MHz)
- Comprehensive diagnostics system
- Clean, responsive UI

### Known Issues ⚠️
- **Tuning Accuracy:** May require fine-tuning to lock onto stations properly
- **Reception Quality:** Audio is barely legible on some stations, worse than SDR#
- **PPM Calibration:** Requires manual calibration for accurate frequency tuning
- **Adjacent Channel Interference:** IF filtering may need optimization

## Architecture

### Core Components

#### 1. NativeRtlSdr.cs
- **Purpose:** Direct P/Invoke wrapper for rtlsdr.dll
- **Functions:** Low-level device control, streaming, gain management
- **Key Features:**
  - Synchronous read with dedicated thread
  - PPM correction support
  - Separate tuner and RTL AGC modes
  - Buffer management (256KB buffers)

#### 2. RtlSdrWrapper.cs
- **Purpose:** High-level device management interface
- **Provides:** Device enumeration, lifecycle management, settings control
- **Handles:** Device opening/closing, frequency/gain/PPM settings

#### 3. FmDemodulator.cs
- **Purpose:** FM signal processing and demodulation
- **Algorithm:** Phase discriminator using atan2()
- **Processing Chain:**
  1. IF Bandpass Filter (±100 kHz) - reduces adjacent channel interference
  2. FM Demodulation (phase derivative)
  3. Decimation to audio sample rate
  4. Audio Lowpass Filter (15 kHz)
  5. De-emphasis Filter (75µs USA standard)
  6. Volume control and squelch
- **Output:** 48 kHz mono audio

#### 4. AudioPlayer.cs
- **Purpose:** Audio output via NAudio
- **Features:**
  - Soft clipping using tanh() for smooth distortion
  - 80ms latency, 4 buffers for smooth playback
  - 16-bit PCM @ 48 kHz
  - Automatic overflow handling

#### 5. SpectrumAnalyzer.cs
- **Purpose:** Frequency analysis and offset detection
- **Method:** Discrete Fourier Transform (DFT)
- **Output:** Peak frequency offset in kHz
- **Used for:** Diagnostics and tuning assistance

#### 6. FrequencyDiagnostics.cs
- **Purpose:** Intelligent troubleshooting assistance
- **Provides:** Context-aware suggestions based on signal level and settings

## Setup Instructions

### Prerequisites
1. **RTL-SDR Hardware**
   - RTL-SDR USB dongle (RTL2832U-based)
   - WinUSB driver installed via Zadig
   - Antenna connected

2. **Software Dependencies**
   - .NET 10 Runtime
   - rtlsdr.dll (from SDR#)
   - libusb-1.0.dll (from SDR#)

### Installation Steps

1. **Copy Native DLLs:**
   ```
   From SDR# installation folder, copy to bin\Debug\net10.0-windows\:
   - rtlsdr.dll
   - libusb-1.0.dll
   ```

2. **Verify Device:**
   - Plug in RTL-SDR dongle
   - Ensure WinUSB driver is installed (not the default drivers)
   - Close SDR# or any other SDR software

3. **Run Application:**
   ```powershell
   .\RunApp.ps1
   ```

### Helper Scripts

**RunApp.ps1** - Checks for required DLLs and launches the application
**CopySDRSharpDlls.ps1** - Searches for and copies DLLs from SDR# installation

## Usage Guide

### Initial Setup

1. **Launch Application**
   - Select your RTL-SDR device from dropdown
   - Click "Open"

2. **Configure Basic Settings**
   - Sample Rate: 2.4 MSPS (recommended, DO NOT CHANGE)
   - Tuner AGC: ON (recommended for beginners)
   - RTL AGC: OFF (recommended)
   - De-emphasis: ON (for FM broadcast)
   - Volume: 50%

3. **Find Stations**
   - Click "Scan FM" to auto-scan 88-108 MHz
   - Or manually tune to known frequency
   - Click "START" to begin receiving

### PPM Calibration (Critical!)

RTL-SDR dongles have crystal oscillator errors that cause frequency offset.

**Method 1: Known Station**
1. Tune to a known strong FM station (e.g., 98.5 FM)
2. Click "Diagnose" to see frequency offset
3. Adjust PPM value until offset is near 0 kHz
4. Common PPM values: -50 to +50

**Method 2: Trial and Error**
1. Try preset buttons: 0, -50, +50
2. Listen for clearest audio
3. Fine-tune PPM by ±1 or ±2

**PPM Formula:**
```
PPM = (received_freq - known_freq) / known_freq × 1,000,000
```

### Tuning Tips

#### For Best Reception:
1. **Sample Rate:** Always use 2.4 MSPS
   - Do NOT change sample rate to find stations
   - Use frequency tuning instead!

2. **Gain Settings:**
   - Tuner AGC: ON for automatic control
   - RTL AGC: OFF (causes distortion)
   - Manual gain: ~40 dB if AGC is off

3. **Fine Tuning:**
   - Use 0.001 MHz (1 kHz) steps for precision
   - Watch the frequency offset display
   - Green = on frequency, Orange/Red = off

4. **Audio Quality:**
   - Enable de-emphasis for FM broadcast
   - Adjust volume to 30-70% range
   - Use squelch to cut noise between stations

### Diagnostic Tools

#### Signal Meter
- 0-30%: Weak/no signal
- 30-60%: Moderate signal
- 60%+: Strong signal

#### Frequency Offset Display
- Shows how far off-frequency you are
- Target: ±2 kHz (green)
- Adjust frequency or PPM to center at 0

#### Diagnose Button
Provides comprehensive analysis:
- Signal strength assessment
- Spectrum analysis with offset
- Setting recommendations
- Sample rate validation

#### Scan FM Band
- Scans 88-108 MHz in 0.1 MHz steps
- Auto-stops on strong stations (>25% signal)
- Lists all found stations
- Takes ~2 minutes for full scan

## Troubleshooting

### No Audio / No Signal

**Check:**
1. RTL-SDR plugged in and detected
2. WinUSB driver installed (not default drivers)
3. SDR# or other SDR software is closed
4. Antenna connected
5. rtlsdr.dll and libusb-1.0.dll in app folder

**Try:**
- Turn OFF RTL AGC
- Turn ON Tuner AGC
- Set sample rate to 2.4 MSPS
- Click "Scan FM" to find stations
- Adjust PPM correction

### Poor Audio Quality

**Causes:**
1. Wrong sample rate (use 2.4 MSPS!)
2. RTL AGC enabled (turn OFF)
3. Incorrect PPM calibration
4. Off-frequency tuning
5. Poor antenna or weak signal

**Solutions:**
- Click "Diagnose" for specific guidance
- Verify sample rate is 2.4 MSPS
- Turn OFF RTL AGC
- Fine-tune frequency using offset display
- Calibrate PPM using known station
- Enable de-emphasis
- Reduce volume if distorted

### Frequency Issues

**"Different sample rates pick up different stations":**
- This is WRONG behavior - it means aliasing/imaging
- Set sample rate to 2.4 MSPS and LEAVE IT
- Use frequency tuning to change stations
- IF bandpass filter should prevent this

**"Can't find the station I know is there":**
- PPM correction is likely wrong
- Try PPM values: -50, -30, 0, +30, +50
- Use "Scan FM" to find where stations actually are
- Calculate PPM based on offset

**"Audio is garbled/distorted":**
- Reduce volume
- Turn OFF RTL AGC
- Enable de-emphasis
- Fine-tune frequency by ±0.001 MHz
- Check PPM correction

## Technical Details

### Signal Processing Chain

```
RTL-SDR Hardware
    ↓
IQ Samples (8-bit, 2.4 MSPS)
    ↓
IF Bandpass Filter (±100 kHz)
    ↓
FM Phase Discriminator
    ↓
Decimation (2.4 MHz → 48 kHz)
    ↓
Audio Lowpass Filter (15 kHz)
    ↓
De-emphasis Filter (75µs)
    ↓
Volume & Squelch
    ↓
Soft Clipping (tanh)
    ↓
16-bit PCM Audio Output
```

### Filter Specifications

**IF Bandpass:**
- Center: DC (0 Hz)
- Bandwidth: ±100 kHz
- Purpose: Isolate FM channel, reject adjacent channels

**Audio Lowpass:**
- Cutoff: 15 kHz
- Purpose: FM broadcast audio bandwidth limit

**De-emphasis:**
- Time Constant: 75µs (USA standard)
- Purpose: Restore frequency response after pre-emphasis

### Sample Rate Considerations

**Recommended: 2.4 MSPS**
- Provides 2.4 MHz bandwidth
- Captures full FM channel (200 kHz) with guard bands
- Good decimation ratio to 48 kHz (50:1)
- Standard for FM broadcast reception

**Also works: 2.0 MSPS, 1.8 MSPS**
- Narrower bandwidth
- May work for strong stations

**Avoid: Other values**
- May cause aliasing/imaging issues
- Non-optimal decimation ratios
- No benefit over 2.4 MSPS

### PPM Correction

**What it is:**
Crystal oscillator error in parts-per-million

**Why it matters:**
- 50 PPM error at 100 MHz = 5 kHz offset
- Causes stations to be off-frequency
- Temperature-dependent (stabilizes after warm-up)

**Typical values:**
- Good dongles: ±20 PPM
- Average dongles: ±50 PPM
- Cheap dongles: ±100+ PPM

**How it's applied:**
Set at device level before tuning, affects all frequencies proportionally

## Performance Comparison

### vs SDR#
**Advantages:**
- Simpler, focused interface
- Native .NET integration
- Lightweight, fast startup
- Integrated diagnostics

**Disadvantages:**
- Lower audio quality (under development)
- Fewer features (no waterfall, no modes besides FM)
- Basic spectrum analysis only

**Current Reception Quality:**
- RandySDR: Barely legible audio
- SDR#: Clear, strong audio
- **Reason:** Likely needs better filtering, AGC tuning, or demodulation optimization

## Future Improvements

### Priority Fixes
1. Improve audio quality to match SDR#
2. Better automatic PPM detection
3. Optimize IF and audio filters
4. Improve AGC algorithms

### Potential Features
- Waterfall display
- RDS decoder
- Stereo decoding
- Presets/favorites
- Recording capability
- Other modes (AM, SSB, etc.)

## Development Notes

### Key Decisions Made

1. **Native API over RtlSdrManager:**
   - RtlSdrManager had compatibility issues
   - Direct P/Invoke provides better control
   - Easier debugging and troubleshooting

2. **Separate Tuner/RTL AGC:**
   - More control than single AGC
   - Tuner AGC for RF, RTL AGC for baseband
   - RTL AGC often causes distortion

3. **Fixed 48 kHz Audio:**
   - Standard audio rate
   - Good compatibility
   - Clean decimation from common sample rates

4. **Soft Clipping:**
   - Better audio quality than hard clipping
   - Using tanh() for smooth saturation

### Build Configuration
- Target: .NET 10
- Platform: Windows (Forms)
- Dependencies: NAudio 2.2.1
- Native DLLs: rtlsdr.dll, libusb-1.0.dll

## License & Credits

**RandySDR** - Educational SDR receiver project

**Dependencies:**
- RTL-SDR library (librtlsdr)
- NAudio library
- libusb

**Inspired by:** SDR#, GQRX, and other SDR applications

## Contact & Support

For issues, improvements, or questions, refer to Debug Output window in Visual Studio for detailed diagnostic information.

---

**Last Updated:** 2024
**Status:** Development/Alpha
**Platform:** Windows .NET 10
