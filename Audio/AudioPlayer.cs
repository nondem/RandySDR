using NAudio.Wave;

namespace RandySDR
{
    public class AudioPlayer : IDisposable
    {
        private WaveOutEvent? _waveOut;
        private BufferedWaveProvider? _waveProvider;
        private readonly int _sampleRate;
        private long _totalSamplesAdded = 0;
        private bool _hasPlayedTestTone = false;

        public AudioPlayer(int sampleRate = 48000)
        {
            _sampleRate = sampleRate;
        }

        public void Start()
        {
            if (_waveOut != null)
                return;

            _waveProvider = new BufferedWaveProvider(new WaveFormat(_sampleRate, 16, 1))
            {
                DiscardOnBufferOverflow = false, // Don't discard - keep audio continuous
                BufferLength = _sampleRate * 4 // 4 seconds buffer for stability
            };

            _waveOut = new WaveOutEvent
            {
                DesiredLatency = 100, // Standard latency for smooth playback
                NumberOfBuffers = 6   // More buffers for stability
            };

            _waveOut.Init(_waveProvider);
            _waveOut.Play();
            
            System.Diagnostics.Debug.WriteLine($"Audio output started: {_sampleRate}Hz, Latency=80ms, Buffers=4");
        }

        public void Stop()
        {
            _waveOut?.Stop();
            _waveOut?.Dispose();
            _waveOut = null;
            _waveProvider = null;
            _hasPlayedTestTone = false;
            
            System.Diagnostics.Debug.WriteLine($"Audio output stopped. Total samples: {_totalSamplesAdded}");
        }

        public void Dispose()
        {
            Stop();
        }

        public void ClearBuffered() => _waveProvider?.ClearBuffer();

        public void AddSamples(float[] samples)
        {
            if (_waveProvider == null || samples.Length == 0)
                return;

            // Convert float samples to 16-bit PCM with soft clipping
            byte[] pcmData = new byte[samples.Length * 2];
            for (int i = 0; i < samples.Length; i++)
            {
                // Soft clipping using tanh for smoother distortion
                // Samples are already volume-adjusted in demodulator
                float sample = samples[i];
                
                // Use tanh for smooth soft clipping - prevents harsh digital distortion
                float clipped = (float)Math.Tanh(sample);
                
                short pcmSample = (short)(clipped * short.MaxValue);
                pcmData[i * 2] = (byte)(pcmSample & 0xFF);
                pcmData[i * 2 + 1] = (byte)((pcmSample >> 8) & 0xFF);
            }

            _waveProvider.AddSamples(pcmData, 0, pcmData.Length);
            _totalSamplesAdded += samples.Length;
            
            // Debug buffer status periodically
            if (_totalSamplesAdded % 10000 == 0)
            {
                System.Diagnostics.Debug.WriteLine($"Audio: {_totalSamplesAdded} samples added, buffered: {_waveProvider.BufferedDuration.TotalSeconds:F2}s");
            }
        }

        public void GenerateTestTone(int frequency = 440)
        {
            if (_waveProvider == null || _hasPlayedTestTone)
                return;

            _hasPlayedTestTone = true;

            // Generate 200ms of test tone
            int numSamples = _sampleRate / 5;
            float[] samples = new float[numSamples];
            
            for (int i = 0; i < numSamples; i++)
            {
                samples[i] = (float)Math.Sin(2 * Math.PI * frequency * i / _sampleRate) * 0.3f;
            }
            
            AddSamples(samples);
            System.Diagnostics.Debug.WriteLine($"Generated {frequency}Hz test tone");
        }

        public bool IsPlaying => _waveOut?.PlaybackState == PlaybackState.Playing;
        
        public int BufferedBytes => _waveProvider?.BufferedBytes ?? 0;
        
        public TimeSpan BufferedDuration => _waveProvider?.BufferedDuration ?? TimeSpan.Zero;
    }
}
