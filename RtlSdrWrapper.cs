using System.Diagnostics;
using System.Runtime.InteropServices;

namespace RandySDR
{
    public class RtlSdrWrapper
    {
        private NativeRtlSdr? _nativeDevice;
        private bool _isRunning;
        private Action<byte[]>? _dataCallback;
        private int _callbackCount = 0;

        public static List<DeviceInfo> GetConnectedDevices()
        {
            var devices = new List<DeviceInfo>();
            
            try
            {
                Debug.WriteLine("=== Enumerating RTL-SDR devices (NATIVE) ===");
                
                uint deviceCount = NativeRtlSdr.GetDeviceCount();
                Debug.WriteLine($"Native rtlsdr_get_device_count() = {deviceCount}");
                
                if (deviceCount == 0)
                {
                    Debug.WriteLine("⚠ NO DEVICES FOUND");
                    Debug.WriteLine("  Make sure:");
                    Debug.WriteLine("  1. RTL-SDR is plugged in via USB");
                    Debug.WriteLine("  2. WinUSB driver is installed (via Zadig)");
                    Debug.WriteLine("  3. SDR# is completely closed");
                    Debug.WriteLine("  4. rtlsdr.dll and libusb-1.0.dll are in the app folder");
                }
                
                for (uint i = 0; i < deviceCount; i++)
                {
                    string name = NativeRtlSdr.GetDeviceName(i);
                    Debug.WriteLine($"  Device {i}: {name}");
                    
                    devices.Add(new DeviceInfo
                    {
                        Index = i,
                        Name = name,
                        Serial = ""
                    });
                }
                
                if (devices.Count > 0)
                {
                    Debug.WriteLine($"✓ Found {devices.Count} RTL-SDR device(s)");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"✗ Error enumerating devices: {ex.Message}");
                Debug.WriteLine($"  This usually means rtlsdr.dll or libusb-1.0.dll is missing or incompatible");
            }

            return devices;
        }

        public bool OpenDevice(uint index)
        {
            try
            {
                CloseDevice();
                
                Debug.WriteLine($"=== Opening RTL-SDR device {index} ===");
                
                _nativeDevice = new NativeRtlSdr();
                
                if (!_nativeDevice.Open(index))
                {
                    _nativeDevice.Dispose();
                    _nativeDevice = null;
                    return false;
                }
                
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"✗ Exception opening device: {ex.Message}");
                _nativeDevice?.Dispose();
                _nativeDevice = null;
                return false;
            }
        }

        public void CloseDevice()
        {
            Stop();
            _nativeDevice?.Dispose();
            _nativeDevice = null;
        }

        public void SetFrequency(uint frequencyHz)
        {
            _nativeDevice?.SetCenterFrequency(frequencyHz);
        }

        public void SetSampleRate(uint sampleRate)
        {
            _nativeDevice?.SetSampleRate(sampleRate);
        }

        public void SetTunerGain(int gain)
        {
            _nativeDevice?.SetTunerGain(gain);
        }

        public void SetTunerAgc(bool enabled)
        {
            _nativeDevice?.SetAgcMode(enabled);
        }

        public void SetRtlAgc(bool enabled)
        {
            _nativeDevice?.SetRtlAgcMode(enabled);
        }

        public void SetPpmCorrection(int ppm)
        {
            _nativeDevice?.SetPpmCorrection(ppm);
        }

        public int GetPpmCorrection()
        {
            return _nativeDevice?.GetPpmCorrection() ?? 0;
        }

        public void Start(Action<byte[]> dataCallback)
        {
            if (_nativeDevice == null || _isRunning)
                return;

            _dataCallback = dataCallback;
            _callbackCount = 0;
            _isRunning = true;

            Debug.WriteLine("=== Starting RTL-SDR streaming ===");
            
            _nativeDevice.Start(OnDataReceived);
        }

        private void OnDataReceived(byte[] iqData)
        {
            try
            {
                _callbackCount++;
                
                if (_callbackCount == 1)
                {
                    Debug.WriteLine($"✓ First data callback: {iqData.Length} bytes");
                }
                
                _dataCallback?.Invoke(iqData);
                
                if (_callbackCount % 100 == 0)
                {
                    Debug.WriteLine($"Received {_callbackCount} data buffers, last: {iqData.Length} bytes");
                }
            }
            catch (Exception ex)
            {
                if (_callbackCount <= 5)
                    Debug.WriteLine($"✗ Error in data callback: {ex.Message}");
            }
        }

        public void Stop()
        {
            if (!_isRunning)
                return;

            _isRunning = false;
            _nativeDevice?.Stop();
            
            Debug.WriteLine($"Stopped. Total callbacks: {_callbackCount}");
        }

        public bool IsRunning => _isRunning;
        public bool IsTestMode => _nativeDevice == null || !_nativeDevice.IsOpen;

        public int[] GetTunerGains()
        {
            if (_nativeDevice != null && _nativeDevice.IsOpen)
            {
                return _nativeDevice.GetTunerGains();
            }
            
            return new int[] { 0, 10, 20, 30, 40, 50 };
        }
    }

    public class DeviceInfo
    {
        public uint Index { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Serial { get; set; } = string.Empty;

        public override string ToString()
        {
            return $"[{Index}] {Name}";
        }
    }

    internal static class NativeMethods
    {
        [DllImport("rtlsdr.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern uint rtlsdr_get_device_count();

        [DllImport("rtlsdr.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr rtlsdr_get_device_name(uint index);

        [DllImport("rtlsdr.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int rtlsdr_open(out IntPtr dev, uint index);

        [DllImport("rtlsdr.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int rtlsdr_close(IntPtr dev);
    }
}
