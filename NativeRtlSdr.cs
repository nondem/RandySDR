using System.Runtime.InteropServices;

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

        [DllImport("rtlsdr", CallingConvention = CallingConvention.Cdecl)]
        private static extern int rtlsdr_set_agc_mode(IntPtr dev, int on);

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
        private static extern int rtlsdr_set_rtl_agc_mode(IntPtr dev, int on);

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
                    
                    // Reset buffer
                    rtlsdr_reset_buffer(_device);
                    
                    // Set default PPM correction to 0
                    rtlsdr_set_freq_correction(_device, 0);
                    
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
                // Disable manual gain mode for AGC, enable for manual
                rtlsdr_set_tuner_gain_mode(_device, enabled ? 0 : 1);
                rtlsdr_set_agc_mode(_device, enabled ? 1 : 0);
                System.Diagnostics.Debug.WriteLine($"✓ Tuner AGC {(enabled ? "enabled" : "disabled")}");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error setting AGC: {ex.Message}");
                return false;
            }
        }

        public bool SetRtlAgcMode(bool enabled)
        {
            if (_device == IntPtr.Zero)
                return false;

            try
            {
                int result = rtlsdr_set_rtl_agc_mode(_device, enabled ? 1 : 0);
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
                if (result == 0)
                {
                    System.Diagnostics.Debug.WriteLine($"✓ Set PPM correction to {ppm}");
                    return true;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"✗ Failed to set PPM correction, result: {result}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error setting PPM: {ex.Message}");
                return false;
            }
        }

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
                // Get number of gains
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

        public void Start(Action<byte[]> dataCallback)
        {
            if (_device == IntPtr.Zero || _isRunning)
                return;

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
                        System.Diagnostics.Debug.WriteLine($"✗ Read error: {result}");
                        break;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"✗ Exception in read loop: {ex.Message}");
                    break;
                }
            }

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

        public void Dispose()
        {
            Close();
        }
    }
}
