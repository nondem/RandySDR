namespace RandySDR
{
    /// <summary>
    /// The streaming data path, independent of any UI:
    /// USB read thread (ThreadSafeBuffer) -> queue -> DSP thread (FmDemodulator) -> AudioPlayer.
    /// The UI owns one instance while a device is open and only talks to it through this class.
    /// </summary>
    public sealed class ReceiverPipeline : IDisposable
    {
        public const int AudioSampleRate = 48000;

        private readonly ThreadSafeBuffer _usbBuffer = new();
        private readonly AudioPlayer _audio = new(AudioSampleRate);
        private volatile FmDemodulator _demodulator;
        private Thread? _dspThread;
        private volatile bool _dspRunning;
        private volatile byte[]? _lastIqData;
        private int _buffersProcessed;

        public ReceiverPipeline(int sampleRateHz)
        {
            _demodulator = new FmDemodulator(sampleRateHz, AudioSampleRate);
        }

        /// <summary>Current demodulator. Set its properties (Volume, Mode, ...) from the UI thread; the DSP thread reads them live.</summary>
        public FmDemodulator Demodulator => _demodulator;

        /// <summary>Most recent raw I/Q buffer (unsigned 8-bit interleaved). Used by spectrum and ADC-overload meters.</summary>
        public byte[]? LastIqData => _lastIqData;

        public float SignalLevel => _demodulator.SignalLevel;
        public int BuffersProcessed => _buffersProcessed;
        public int QueueSize => _usbBuffer.CurrentQueueSize;
        public int BuffersDropped => _usbBuffer.TotalBuffersDropped;
        public bool IsRunning => _dspRunning;

        /// <summary>
        /// Drop all queued I/Q and buffered audio from before a retune and reset demodulator
        /// transients, so nothing from the old frequency is heard on the new one.
        /// </summary>
        public void FlushForRetune(bool clearAudio)
        {
            _usbBuffer.Flush();
            _demodulator.RequestReset();
            if (clearAudio) _audio.ClearBuffered();
        }

        /// <summary>Replace the demodulator (e.g. after a sample rate change). Returns the new instance so the caller can re-apply settings.</summary>
        public FmDemodulator ReplaceDemodulator(int sampleRateHz)
        {
            _demodulator = new FmDemodulator(sampleRateHz, AudioSampleRate);
            return _demodulator;
        }

        public bool Start(IntPtr deviceHandle, uint sampleRateHz)
        {
            if (_dspRunning)
                return true;

            _audio.Start();

            if (!_usbBuffer.StartReadThread(deviceHandle, sampleRateHz))
                return false;

            _buffersProcessed = 0;
            _dspRunning = true;
            _dspThread = new Thread(DspLoop)
            {
                Name = "RTL-SDR DSP Thread",
                IsBackground = true,
                Priority = ThreadPriority.AboveNormal
            };
            _dspThread.Start();
            return true;
        }

        /// <summary>Stops DSP first, then the USB reader, so the device is never read after this returns.</summary>
        public void Stop()
        {
            _dspRunning = false;
            _dspThread?.Join(1000);
            _dspThread = null;
            _usbBuffer.Stop();
            _audio.Stop();
        }

        private void DspLoop()
        {
            while (_dspRunning)
            {
                if (!_usbBuffer.TryDequeue(out byte[]? iq) || iq == null)
                {
                    Thread.Sleep(1);
                    continue;
                }

                _buffersProcessed++;
                _lastIqData = iq;

                float[]? audio = _demodulator.Demodulate(iq);
                if (audio is { Length: > 0 })
                    _audio.AddSamples(audio);
            }
        }

        public void Dispose()
        {
            Stop();
            _usbBuffer.Dispose();
            _audio.Dispose();
        }
    }
}
