using System.Runtime.InteropServices;
using System.Text;

namespace RandySDR
{
    public class NativeRtlSdr : IDisposable
    {
        private IntPtr _device = IntPtr.Zero;
        private bool _isRunning = false;
        private Thread? _readThread;
        private Action<byte[]>? _dataCallback;

        // P/Invoke declarations
        [DllImport("rtlsdr", CallingConvention = CallingConvention.Cdecl)]
        private static extern uint rtlsdr_get_device_count();

        [DllImport("rtlsdr", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr rtlsdr_get_device_name(uint index);

        [DllImport("rtlsdr", CallingConvention = CallingConvention.Cdecl)]
        private static extern int rtlsdr_open(out IntPtr dev, uint index);

        [DllImport("rtlsdr", CallingConvention = CallingConvention.Cdecl)]
        private static extern int rtlsdr_close(IntPtr dev);

        [DllImport("rtlsdr", CallingConvention = CallingConvention.Cdecl)]
        private static extern int rtlsdr_set_center_freq(IntPtr dev, uint freq);

        [DllImport("rtlsdr", CallingConvention = CallingConvention.Cdecl)]
        private static extern int rtlsdr_set_sample_rate(IntPtr dev, uint rate);

        [DllImport("rtlsdr", CallingConvention = CallingConvention.Cdecl)]
        private static extern int rtlsdr_set_tuner_gain_mode(IntPtr dev, int manual);

        [DllImport("rtlsdr", CallingConvention = CallingConvention.Cdecl)]
        private static extern int rtlsdr_set_tuner_gain(IntPtr dev, int gain);

        // CRITICAL FIX #2: RTL baseband AGC control
        [DllImport("rtlsdr", CallingConvention = CallingConvention.Cdecl)]
        public static extern int rtlsdr_set_agc_mode(IntPtr dev, int on);

        [DllImport("rtlsdr", CallingConvention = CallingConvention.Cdecl)]
        private static extern int rtlsdr_reset_buffer(IntPtr dev);

        [DllImport("rtlsdr", CallingConvention = CallingConvention.Cdecl)]
        private static extern int rtlsdr_read_sync(IntPtr dev, byte[] buf, int len, out int n_read);

        [DllImport("rtlsdr", CallingConvention = CallingConvention.Cdecl)]
        private static extern int rtlsdr_get_tuner_gains(IntPtr dev, int[] gains);

        [DllImport("rtlsdr", CallingConvention = CallingConvention.Cdecl)]
        private static extern uint rtlsdr_get_center_freq(IntPtr dev);

        [DllImport("rtlsdr", CallingConvention = CallingConvention.Cdecl)]
        private static extern int rtlsdr_set_freq_correction(IntPtr dev, int ppm);

        [DllImport("rtlsdr", CallingConvention = CallingConvention.Cdecl)]
        private static extern int rtlsdr_get_freq_correction(IntPtr dev);

        [DllImport("rtlsdr", CallingConvention = CallingConvention.Cdecl)]
        private static extern int rtlsdr_set_tuner_if_gain(IntPtr dev, int stage, int gain);

        [DllImport("rtlsdr", CallingConvention = CallingConvention.Cdecl)]
        private static extern int rtlsdr_get_tuner_type(IntPtr dev);

        [DllImport("rtlsdr", CallingConvention = CallingConvention.Cdecl)]
        private static extern int rtlsdr_get_xtal_freq(IntPtr dev, out uint rtlFreq, out uint tunerFreq);

        [DllImport("rtlsdr", CallingConvention = CallingConvention.Cdecl)]
        private static extern int rtlsdr_get_device_usb_strings(uint index, byte[]? manufact, byte[]? product, byte[]? serial);

        [DllImport("rtlsdr", CallingConvention = CallingConvention.Cdecl)]
        private static extern int rtlsdr_set_tuner_bandwidth(IntPtr dev, uint bw);

        [DllImport("rtlsdr", CallingConvention = CallingConvention.Cdecl)]
        private static extern int rtlsdr_set_offset_tuning(IntPtr dev, int on);

        [DllImport("rtlsdr", CallingConvention = CallingConvention.Cdecl)]
        private static extern int rtlsdr_get_offset_tuning(IntPtr dev);

        [DllImport("rtlsdr", CallingConvention = CallingConvention.Cdecl)]
        private static extern int rtlsdr_set_direct_sampling(IntPtr dev, int on);

        [DllImport("rtlsdr", CallingConvention = CallingConvention.Cdecl, EntryPoint = "rtlsdr_set_bias_tee")]
        private static extern int rtlsdr_set_bias_tee(IntPtr dev, int on);

        public static uint GetDeviceCount()
        {
            try
            {
                return rtlsdr_get_device_count();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting device count: {ex.Message}");
                return 0;
            }
        }

        public static string GetDeviceName(uint index)
        {
            try
            {
                IntPtr namePtr = rtlsdr_get_device_name(index);
                return Marshal.PtrToStringAnsi(namePtr) ?? $"RTL-SDR Device {index}";
            }
            catch
            {
                return $"RTL-SDR Device {index}";
            }
        }

        public bool Open(uint index)
        {
            try
            {
                int result = rtlsdr_open(out _device, index);
                if (result == 0 && _device != IntPtr.Zero)
                {
                    System.Diagnostics.Debug.WriteLine($"✓ Opened RTL-SDR device {index} (handle: {_device})");

                    LogDeviceInfo();
                    LogDriverCompatibilityHint(index);

                    rtlsdr_reset_buffer(_device);
                    rtlsdr_set_freq_correction(_device, 0);

                    // Default to 200 kHz tuner bandwidth for FM broadcast
                    SetTunerBandwidth(200_000);

                    return true;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"✗ Failed to open device {index}, result: {result}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Exception opening device: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Configure the tuner with manual gain mode and enable RTL AGC.
        /// This should be called after Open() and before Start().
        /// </summary>
        public bool ConfigureTunerForManualGain(uint sampleRate, uint centerFrequency,         int gainTenthsDb = 496, bool rtlAgc = true, bool tunerAgc = false)
        {
            if (_device == IntPtr.Zero)
                return false;

            try
            {
                // Step 1: RTL baseband AGC (digital scaling)
                int result = rtlsdr_set_agc_mode(_device, rtlAgc ? 1 : 0);
                if (result != 0)
                {
                    System.Diagnostics.Debug.WriteLine($"⚠ Failed to enable RTL AGC, result: {result}");
                }

                // Step 2: Set sample rate
                result = rtlsdr_set_sample_rate(_device, sampleRate);
                if (result != 0)
                {
                    System.Diagnostics.Debug.WriteLine($"✗ Failed to set sample rate, result: {result}");
                    return false;
                }

                // Step 3: Set center frequency
                result = rtlsdr_set_center_freq(_device, centerFrequency);
                if (result != 0)
                {
                    System.Diagnostics.Debug.WriteLine($"✗ Failed to set frequency, result: {result}");
                    return false;
                }

                // Step 4: Switch to MANUAL gain mode (disable tuner AGC)
                result = rtlsdr_set_tuner_gain_mode(_device, tunerAgc ? 0 : 1);
                if (result != 0)
                {
                    System.Diagnostics.Debug.WriteLine($"✗ Failed to set gain mode, result: {result}");
                    return false;
                }

                // Step 5: Apply the requested manual gain (tenths of dB) unless tuner AGC is on
                if (!tunerAgc)
                {
                    result = rtlsdr_set_tuner_gain(_device, gainTenthsDb);
                    if (result != 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"✗ Failed to set tuner gain, result: {result}");
                        return false;
                    }
                }

                System.Diagnostics.Debug.WriteLine($"✓ Tuner configured: Manual Gain, RTL AGC={rtlAgc}, Gain={gainTenthsDb / 10.0:F1} dB");
                rtlsdr_reset_buffer(_device);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error configuring tuner: {ex.Message}");
                return false;
            }
        }

        private static void LogDriverCompatibilityHint(uint index)
        {
            try
            {
                byte[] manufact = new byte[256];
                byte[] product = new byte[256];
                byte[] serial = new byte[256];

                int r = rtlsdr_get_device_usb_strings(index, manufact, product, serial);
                if (r == 0)
                {
                    string m = Encoding.ASCII.GetString(manufact).TrimEnd('\0');
                    string p = Encoding.ASCII.GetString(product).TrimEnd('\0');

                    bool looksBlog = m.Contains("RTL-SDR Blog", StringComparison.OrdinalIgnoreCase)
                                  || p.Contains("Blog", StringComparison.OrdinalIgnoreCase)
                                  || p.Contains("V4", StringComparison.OrdinalIgnoreCase);

                    if (!looksBlog)
                    {
                        System.Diagnostics.Debug.WriteLine("⚠ Device strings do not look like RTL-SDR Blog V4. Ensure RTL-SDR Blog librtlsdr is in use.");
                    }
                }
            }
            catch
            {
                // Best-effort diagnostic only.
            }
        }

        private void LogDeviceInfo()
        {
            if (_device == IntPtr.Zero)
                return;

            try
            {
                int tunerType = rtlsdr_get_tuner_type(_device);
                System.Diagnostics.Debug.WriteLine($"RTL-SDR Tuner Type: {tunerType}");

                if (rtlsdr_get_xtal_freq(_device, out uint rtlXtal, out uint tunerXtal) == 0)
                {
                    System.Diagnostics.Debug.WriteLine($"RTL XTAL: {rtlXtal} Hz, Tuner XTAL: {tunerXtal} Hz");
                }

                byte[] manufact = new byte[256];
                byte[] product = new byte[256];
                byte[] serial = new byte[256];
                if (rtlsdr_get_device_usb_strings(0, manufact, product, serial) == 0)
                {
                    string m = System.Text.Encoding.ASCII.GetString(manufact).TrimEnd('\0');
                    string p = System.Text.Encoding.ASCII.GetString(product).TrimEnd('\0');
                    string s = System.Text.Encoding.ASCII.GetString(serial).TrimEnd('\0');
                    System.Diagnostics.Debug.WriteLine($"USB Strings: {m} / {p} / {s}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error reading device info: {ex.Message}");
            }
        }

        public void Close()
        {
            Stop();

            if (_device != IntPtr.Zero)
            {
                rtlsdr_close(_device);
                _device = IntPtr.Zero;
                System.Diagnostics.Debug.WriteLine("✓ Closed RTL-SDR device");
            }
        }

        public bool SetCenterFrequency(uint frequencyHz)
        {
            if (_device == IntPtr.Zero)
                return false;

            try
            {
                int result = rtlsdr_set_center_freq(_device, frequencyHz);
                if (result == 0)
                {
                    uint actual = rtlsdr_get_center_freq(_device);
                    System.Diagnostics.Debug.WriteLine($"✓ Set frequency to {frequencyHz / 1e6:F3} MHz (actual: {actual / 1e6:F3} MHz)");
                    rtlsdr_reset_buffer(_device);
                    return true;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"✗ Failed to set frequency, result: {result}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error setting frequency: {ex.Message}");
                return false;
            }
        }

        public bool SetSampleRate(uint sampleRate)
        {
            if (_device == IntPtr.Zero)
                return false;

            try
            {
                int result = rtlsdr_set_sample_rate(_device, sampleRate);
                if (result == 0)
                {
                    System.Diagnostics.Debug.WriteLine($"✓ Set sample rate to {sampleRate / 1e6:F2} MSPS");
                    rtlsdr_reset_buffer(_device);
                    return true;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"✗ Failed to set sample rate, result: {result}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error setting sample rate: {ex.Message}");
                return false;
            }
        }

        public bool SetTunerGain(int gain)
        {
            if (_device == IntPtr.Zero)
                return false;

            try
            {
                rtlsdr_set_tuner_gain_mode(_device, 1); // manual gain
                int result = rtlsdr_set_tuner_gain(_device, gain);
                if (result == 0)
                {
                    System.Diagnostics.Debug.WriteLine($"✓ Set tuner gain to {gain / 10.0:F1} dB");
                    return true;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"✗ Failed to set tuner gain, result: {result}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error setting tuner gain: {ex.Message}");
                return false;
            }
        }

        public bool SetAgcMode(bool enabled)
        {
            if (_device == IntPtr.Zero)
                return false;

            try
            {
                // Tuner AGC is controlled by tuner gain mode (auto = 0, manual = 1).
                // Keep RTL AGC separate via SetRtlAgcMode.
                int result = rtlsdr_set_tuner_gain_mode(_device, enabled ? 0 : 1);
                if (result == 0)
                {
                    System.Diagnostics.Debug.WriteLine($"✓ Tuner AGC {(enabled ? "enabled" : "disabled")}");
                    return true;
                }

                System.Diagnostics.Debug.WriteLine($"✗ Failed to set tuner AGC, result: {result}");
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error setting AGC: {ex.Message}");
                return false;
            }
        }

        public bool SetTunerBandwidth(uint bandwidthHz)
        {
            if (_device == IntPtr.Zero)
                return false;

            try
            {
                int result = rtlsdr_set_tuner_bandwidth(_device, bandwidthHz);
                if (result == 0)
                {
                    System.Diagnostics.Debug.WriteLine($"✓ Set tuner bandwidth to {bandwidthHz / 1000} kHz");
                    return true;
                }
                System.Diagnostics.Debug.WriteLine($"✗ Failed to set tuner bandwidth, result: {result}");
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error setting tuner bandwidth: {ex.Message}");
                return false;
            }
        }

        public bool SetOffsetTuning(bool enabled)
        {
            if (_device == IntPtr.Zero)
                return false;

            try
            {
                int result = rtlsdr_set_offset_tuning(_device, enabled ? 1 : 0);
                if (OffsetTuningSucceeded(result, enabled, () => rtlsdr_get_offset_tuning(_device)))
                {
                    System.Diagnostics.Debug.WriteLine($"✓ Offset tuning {(enabled ? "enabled" : "disabled")}");
                    return true;
                }
                System.Diagnostics.Debug.WriteLine($"✗ Failed to set offset tuning, result: {result}");
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error setting offset tuning: {ex.Message}");
                return false;
            }
        }

        internal static bool OffsetTuningSucceeded(int result, bool enabled, Func<int> readOffsetTuning) =>
            result == 0 || (result == -2 && !enabled && readOffsetTuning() == 0);

        public bool Start(Action<byte[]> dataCallback)
        {
            if (_device == IntPtr.Zero || _isRunning)
                return false;

            _dataCallback = dataCallback;
            _isRunning = true;

            rtlsdr_reset_buffer(_device);

            _readThread = new Thread(ReadLoop)
            {
                Name = "RTL-SDR Read Thread",
                IsBackground = true,
                Priority = ThreadPriority.AboveNormal
            };
            _readThread.Start();

            System.Diagnostics.Debug.WriteLine("✓ Started RTL-SDR read thread");
            return true;
        }

        private void ReadLoop()
        {
            byte[] buffer = new byte[262144]; // 256KB buffer
            int readCount = 0;

            System.Diagnostics.Debug.WriteLine("Read thread started");

            while (_isRunning && _device != IntPtr.Zero)
            {
                try
                {
                    int result = rtlsdr_read_sync(_device, buffer, buffer.Length, out int n_read);

                    if (result == 0 && n_read > 0)
                    {
                        readCount++;

                        if (readCount == 1)
                        {
                            System.Diagnostics.Debug.WriteLine($"✓ First read successful: {n_read} bytes");
                        }

                        // Create a copy of the data to send to callback
                        byte[] data = new byte[n_read];
                        Array.Copy(buffer, data, n_read);

                        _dataCallback?.Invoke(data);

                        if (readCount % 100 == 0)
                        {
                            System.Diagnostics.Debug.WriteLine($"Read {readCount} buffers, last: {n_read} bytes");
                        }
                    }
                    else if (result != 0)
                    {
                        // Some tuner setting changes while streaming can cause transient read errors.
                        // Recover instead of terminating the read thread.
                        System.Diagnostics.Debug.WriteLine($"Read error: {result} (recovering)");

                        if (!_isRunning || _device == IntPtr.Zero)
                            break;

                        try
                        {
                            rtlsdr_reset_buffer(_device);
                        }
                        catch { }

                        Thread.Sleep(20);
                        continue;
                    }
                }
                catch (Exception ex)
                {
                    // Keep streaming alive for transient native/API issues.
                    System.Diagnostics.Debug.WriteLine($"Exception in read loop: {ex.Message} (recovering)");

                    if (!_isRunning || _device == IntPtr.Zero)
                        break;

                    try
                    {
                        rtlsdr_reset_buffer(_device);
                    }
                    catch { }

                    Thread.Sleep(20);
                }
            }

            _isRunning = false;
            System.Diagnostics.Debug.WriteLine($"Read thread stopped. Total reads: {readCount}");
        }

        public void Stop()
        {
            if (!_isRunning)
                return;

            _isRunning = false;
            _readThread?.Join(1000);
            _readThread = null;

            System.Diagnostics.Debug.WriteLine("✓ Stopped RTL-SDR");
        }

        public bool IsOpen => _device != IntPtr.Zero;
        public bool IsRunning => _isRunning;

        /// <summary>
        /// Get the device handle for use with thread-safe buffer.
        /// </summary>
        public IntPtr GetDeviceHandle()
        {
            return _device;
        }

        public void Dispose()
        {
            Close();
        }

        public bool SetRtlAgcMode(bool enabled)
        {
            if (_device == IntPtr.Zero)
                return false;

            try
            {
                int result = rtlsdr_set_agc_mode(_device, enabled ? 1 : 0);
                if (result == 0)
                {
                    System.Diagnostics.Debug.WriteLine($"✓ RTL AGC {(enabled ? "enabled" : "disabled")}");
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error setting RTL AGC: {ex.Message}");
                return false;
            }
        }

        public bool SetPpmCorrection(int ppm)
        {
            if (_device == IntPtr.Zero)
                return false;

            try
            {
                int result = rtlsdr_set_freq_correction(_device, ppm);
                if (PpmCorrectionSucceeded(result, ppm, () => rtlsdr_get_freq_correction(_device)))
                {
                    System.Diagnostics.Debug.WriteLine($"✓ Set PPM correction to {ppm}");
                    return true;
                }
                System.Diagnostics.Debug.WriteLine($"✗ Failed to set PPM correction, result: {result}");
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error setting PPM: {ex.Message}");
                return false;
            }
        }

        internal static bool PpmCorrectionSucceeded(int result, int ppm, Func<int> readCorrection) =>
            result == 0 || (result == -2 && readCorrection() == ppm);

        public int GetPpmCorrection()
        {
            if (_device == IntPtr.Zero)
                return 0;

            try
            {
                return rtlsdr_get_freq_correction(_device);
            }
            catch
            {
                return 0;
            }
        }

        public int[] GetTunerGains()
        {
            if (_device == IntPtr.Zero)
                return new int[] { 0, 10, 20, 30, 40, 50 };

            try
            {
                int count = rtlsdr_get_tuner_gains(_device, null!);
                if (count > 0)
                {
                    int[] gains = new int[count];
                    rtlsdr_get_tuner_gains(_device, gains);
                    System.Diagnostics.Debug.WriteLine($"✓ Got {count} supported gains");
                    return gains;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error getting gains: {ex.Message}");
            }

            return new int[] { 0, 10, 20, 30, 40, 50 };
        }

        public bool ResetBuffer()
        {
            if (_device == IntPtr.Zero)
                return false;

            try
            {
                int result = rtlsdr_reset_buffer(_device);
                if (result == 0)
                {
                    System.Diagnostics.Debug.WriteLine("✓ Reset RTL-SDR buffer");
                    return true;
                }

                System.Diagnostics.Debug.WriteLine($"✗ Failed to reset buffer, result: {result}");
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error resetting buffer: {ex.Message}");
                return false;
            }
        }

        public bool SetBiasTee(bool enabled)
        {
            if (_device == IntPtr.Zero)
                return false;

            try
            {
                int result = rtlsdr_set_bias_tee(_device, enabled ? 1 : 0);
                if (result == 0)
                {
                    System.Diagnostics.Debug.WriteLine($"✓ Bias-Tee {(enabled ? "enabled" : "disabled")}");
                    return true;
                }

                System.Diagnostics.Debug.WriteLine($"✗ Failed to set Bias-Tee, result: {result}");
                return false;
            }
            catch (EntryPointNotFoundException)
            {
                System.Diagnostics.Debug.WriteLine("⚠ rtlsdr_set_bias_tee is missing. Use RTL-SDR Blog librtlsdr for RTL-SDR V4 support.");
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error setting Bias-Tee: {ex.Message}");
                return false;
            }
        }

        public bool SetDirectSamplingMode(int mode)
        {
            if (_device == IntPtr.Zero)
                return false;

            try
            {
                // 0 = Quadrature (normal), 1 = Direct sampling I, 2 = Direct sampling Q
                int result = rtlsdr_set_direct_sampling(_device, mode);
                if (result == 0)
                {
                    string modeText = mode switch
                    {
                        1 => "Direct Sampling I",
                        2 => "Direct Sampling Q",
                        _ => "Quadrature"
                    };

                    System.Diagnostics.Debug.WriteLine($"✓ Sampling mode set: {modeText}");
                    return true;
                }

                System.Diagnostics.Debug.WriteLine($"✗ Failed to set sampling mode {mode}, result: {result}");
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error setting sampling mode: {ex.Message}");
                return false;
            }
        }
    }
}
