using System.Collections.Concurrent;
using System.Runtime.InteropServices;

namespace RandySDR
{
    /// <summary>
    /// Extension method for ConcurrentQueue with backpressure support.
    /// </summary>
    public static class ConcurrentQueueExtensions
    {
        /// <summary>
        /// Try to enqueue an item, returning false if queue is at capacity (backpressure).
        /// </summary>
        public static bool TryEnqueue<T>(this ConcurrentQueue<T> queue, T item)
        {
            // Check current size before enqueueing
            int currentSize = queue.Count;
            if (currentSize >= 100) // Backpressure threshold
                return false;

            queue.Enqueue(item);
            return true;
        }
    }

    /// <summary>
    /// Thread-safe buffer manager for RTL-SDR data streaming.
    /// Separates USB read operations from DSP/audio processing to prevent buffer overflows.
    /// </summary>
    public class ThreadSafeBuffer : IDisposable
    {
        private readonly ConcurrentQueue<byte[]> _bufferQueue = new ConcurrentQueue<byte[]>();
        private readonly object _queueLock = new object();
        private bool _isRunning = false;
        private Thread? _readThread;
        private IntPtr _deviceHandle = IntPtr.Zero;
        private int _totalBuffersRead = 0;
        private int _totalBuffersDropped = 0;

        public int TotalBuffersRead => _totalBuffersRead;
        public int TotalBuffersDropped => _totalBuffersDropped;
        public int CurrentQueueSize => _bufferQueue.Count;
        public bool IsRunning => _isRunning;

        private int _generation;
        private volatile bool _resetDevice;

        /// <summary>
        /// Discard everything captured before now (call right after a retune): empties the queue,
        /// drops any read already in flight, and asks the read thread to reset the USB endpoint.
        /// </summary>
        public void Flush()
        {
            Interlocked.Increment(ref _generation);
            _resetDevice = true;
            while (_bufferQueue.TryDequeue(out _)) { }
        }

        /// <summary>
        /// Start the dedicated read thread that pulls data from USB.
        /// </summary>
        public bool StartReadThread(IntPtr deviceHandle, uint sampleRate)
        {
            if (_isRunning || deviceHandle == IntPtr.Zero)
                return false;

            _deviceHandle = deviceHandle;
            _isRunning = true;
            _totalBuffersRead = 0;
            _totalBuffersDropped = 0;

            rtlsdr_reset_buffer(deviceHandle);

            _readThread = new Thread(ReadLoop)
            {
                Name = "RTL-SDR USB Read Thread",
                IsBackground = true,
                Priority = ThreadPriority.AboveNormal
            };
            _readThread.Start();

            System.Diagnostics.Debug.WriteLine("✓ Started dedicated USB read thread");
            return true;
        }

        private void ReadLoop()
        {
            byte[] buffer = new byte[262144]; // 256KB buffer
            int readCount = 0;

            System.Diagnostics.Debug.WriteLine("USB read thread started");

            while (_isRunning && _deviceHandle != IntPtr.Zero)
            {
                try
                {
                    if (_resetDevice)
                    {
                        _resetDevice = false;
                        rtlsdr_reset_buffer(_deviceHandle);
                    }

                    int gen = Volatile.Read(ref _generation);
                    int result = rtlsdr_read_sync(_deviceHandle, buffer, buffer.Length, out int n_read);

                    if (gen != Volatile.Read(ref _generation))
                        continue; // flushed while reading: data predates the retune

                    if (result == 0 && n_read > 0)
                    {
                        readCount++;
                        _totalBuffersRead++;

                        // Create a copy of the data to avoid race conditions
                        byte[] data = new byte[n_read];
                        Array.Copy(buffer, data, n_read);

                        // Try to add to queue with backpressure
                        if (!_bufferQueue.TryEnqueue(data))
                        {
                            _totalBuffersDropped++;
                            System.Diagnostics.Debug.WriteLine($"⚠ Queue full, dropped buffer (queue size: {_bufferQueue.Count})");
                        }

                        if (readCount % 100 == 0)
                        {
                            System.Diagnostics.Debug.WriteLine($"USB read: {readCount} buffers, queue: {_bufferQueue.Count}, dropped: {_totalBuffersDropped}");
                        }
                    }
                    else if (result != 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"Read error: {result} (recovering)");

                        if (!_isRunning || _deviceHandle == IntPtr.Zero)
                            break;

                        try
                        {
                            rtlsdr_reset_buffer(_deviceHandle);
                        }
                        catch { }

                        Thread.Sleep(20);
                        continue;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Exception in read loop: {ex.Message} (recovering)");

                    if (!_isRunning || _deviceHandle == IntPtr.Zero)
                        break;

                    try
                    {
                        rtlsdr_reset_buffer(_deviceHandle);
                    }
                    catch { }

                    Thread.Sleep(20);
                }
            }

            _isRunning = false;
            System.Diagnostics.Debug.WriteLine($"USB read thread stopped. Total: {_totalBuffersRead}, Dropped: {_totalBuffersDropped}");
        }

        /// <summary>
        /// Try to get a buffer from the queue (non-blocking).
        /// </summary>
        public bool TryDequeue(out byte[]? data, int timeoutMs = 0)
        {
            if (timeoutMs > 0)
            {
                // Blocking wait with timeout
                var startTime = DateTime.Now;
                while (_isRunning)
                {
                    if (_bufferQueue.TryDequeue(out data))
                        return true;

                    if ((DateTime.Now - startTime).TotalMilliseconds >= timeoutMs)
                    {
                        data = null;
                        return false;
                    }

                    Thread.Sleep(1);
                }
            }
            else
            {
                // Non-blocking
                return _bufferQueue.TryDequeue(out data);
            }

            data = null;
            return false;
        }

        /// <summary>
        /// Stop the read thread and clear all buffers.
        /// </summary>
        public void Stop()
        {
            if (!_isRunning)
                return;

            _isRunning = false;
            _readThread?.Join(1000);
            _readThread = null;

            // Clear any remaining buffers
            while (_bufferQueue.TryDequeue(out _))
            {
                // Discard
            }

            System.Diagnostics.Debug.WriteLine("✓ USB read thread stopped");
        }

        public void Dispose()
        {
            Stop();
        }

        // P/Invoke for rtlsdr_read_sync (already in NativeRtlSdr, but needed here too)
        [DllImport("rtlsdr", CallingConvention = CallingConvention.Cdecl)]
        private static extern int rtlsdr_read_sync(IntPtr dev, byte[] buf, int len, out int n_read);

        [DllImport("rtlsdr", CallingConvention = CallingConvention.Cdecl)]
        private static extern int rtlsdr_reset_buffer(IntPtr dev);
    }
}
