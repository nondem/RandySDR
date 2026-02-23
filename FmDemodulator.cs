using NAudio.Wave;

namespace RandySDR
{
    public class FmDemodulator
    {
        private float _lastI = 0;
        private float _lastQ = 0;
        private readonly int _sampleRate;
        private readonly int _audioSampleRate;
        private readonly int _decimation;
        private readonly BandPassFilter _ifFilter;
        private readonly LowPassFilter _audioFilter;
        private readonly DeEmphasisFilter _deEmphasis;
        private int _decimationCounter = 0;
        
        // Configurable parameters
        public float Volume { get; set; } = 1.0f;
        public float SquelchLevel { get; set; } = 0.0f;
        public bool DeEmphasisEnabled { get; set; } = true;
        
        private float _signalLevel = 0;
        public float SignalLevel => _signalLevel;

        public FmDemodulator(int sampleRate, int audioSampleRate = 48000)
        {
            _sampleRate = sampleRate;
            _audioSampleRate = audioSampleRate;
            _decimation = Math.Max(1, sampleRate / audioSampleRate);
            
            // IF bandpass filter centered at DC with 200kHz bandwidth (FM broadcast channel width)
            _ifFilter = new BandPassFilter(sampleRate, -100000, 100000);
            
            // Audio lowpass at 15kHz (FM broadcast audio bandwidth)
            _audioFilter = new LowPassFilter(audioSampleRate, 15000);
            
            // De-emphasis for FM broadcast (75µs in USA)
            _deEmphasis = new DeEmphasisFilter(audioSampleRate, 75);
            
            System.Diagnostics.Debug.WriteLine($"FM Demod: SampleRate={sampleRate}, AudioRate={audioSampleRate}, Decimation={_decimation}");
            System.Diagnostics.Debug.WriteLine($"  IF Filter: ±100 kHz bandpass");
            System.Diagnostics.Debug.WriteLine($"  Audio Filter: 15 kHz lowpass");
        }

        public float[] Demodulate(byte[] iqData)
        {
            List<float> audioSamples = new List<float>();
            float signalSum = 0;
            int signalCount = 0;
            
            // Process IQ samples (2 bytes per sample: I and Q)
            for (int i = 0; i < iqData.Length - 1; i += 2)
            {
                // Convert unsigned bytes (0-255) to float (-1 to 1)
                float I = (iqData[i] - 127.5f) / 127.5f;
                float Q = (iqData[i + 1] - 127.5f) / 127.5f;

                // Apply IF bandpass filter to reduce adjacent channel interference
                (I, Q) = _ifFilter.Process(I, Q);

                // Calculate signal strength
                float magnitude = (float)Math.Sqrt(I * I + Q * Q);
                signalSum += magnitude;
                signalCount++;

                // FM demodulation using phase discriminator
                // This is the derivative of the phase
                float demod = (float)Math.Atan2(I * _lastQ - Q * _lastI, I * _lastI + Q * _lastQ);
                
                _lastI = I;
                _lastQ = Q;

                // Decimate to audio sample rate
                _decimationCounter++;
                if (_decimationCounter >= _decimation)
                {
                    _decimationCounter = 0;
                    
                    // Apply audio lowpass filter
                    float filtered = _audioFilter.Process(demod);
                    
                    // Apply de-emphasis if enabled
                    if (DeEmphasisEnabled)
                    {
                        filtered = _deEmphasis.Process(filtered);
                    }
                    
                    // Apply volume
                    filtered *= Volume;
                    
                    // Apply squelch
                    if (_signalLevel < SquelchLevel)
                    {
                        filtered = 0;
                    }
                    
                    audioSamples.Add(filtered);
                }
            }

            // Update signal level (smoothed)
            if (signalCount > 0)
            {
                float currentLevel = signalSum / signalCount;
                _signalLevel = _signalLevel * 0.95f + currentLevel * 0.05f;
            }

            return audioSamples.ToArray();
        }
    }

    public class BandPassFilter
    {
        private readonly LowPassFilter _lowPass;
        private readonly HighPassFilter _highPass;

        public BandPassFilter(int sampleRate, float lowFreq, float highFreq)
        {
            _highPass = new HighPassFilter(sampleRate, lowFreq);
            _lowPass = new LowPassFilter(sampleRate, highFreq);
        }

        public (float I, float Q) Process(float I, float Q)
        {
            // Apply high-pass then low-pass
            (I, Q) = _highPass.Process(I, Q);
            (I, Q) = _lowPass.Process(I, Q);
            return (I, Q);
        }
    }

    public class HighPassFilter
    {
        private readonly float _alpha;
        private float _previousInputI = 0;
        private float _previousInputQ = 0;
        private float _previousOutputI = 0;
        private float _previousOutputQ = 0;

        public HighPassFilter(int sampleRate, float cutoffFrequency)
        {
            float rc = 1.0f / (2.0f * (float)Math.PI * Math.Abs(cutoffFrequency));
            float dt = 1.0f / sampleRate;
            _alpha = rc / (rc + dt);
        }

        public (float I, float Q) Process(float I, float Q)
        {
            float outputI = _alpha * (_previousOutputI + I - _previousInputI);
            float outputQ = _alpha * (_previousOutputQ + Q - _previousInputQ);
            
            _previousInputI = I;
            _previousInputQ = Q;
            _previousOutputI = outputI;
            _previousOutputQ = outputQ;
            
            return (outputI, outputQ);
        }
    }

    public class LowPassFilter
    {
        private readonly float _alpha;
        private float _previousOutput = 0;

        public LowPassFilter(int sampleRate, float cutoffFrequency)
        {
            float rc = 1.0f / (2.0f * (float)Math.PI * cutoffFrequency);
            float dt = 1.0f / sampleRate;
            _alpha = dt / (rc + dt);
        }

        public float Process(float input)
        {
            _previousOutput = _previousOutput + _alpha * (input - _previousOutput);
            return _previousOutput;
        }

        public (float I, float Q) Process(float I, float Q)
        {
            // For complex signals
            float outputI = _previousOutput + _alpha * (I - _previousOutput);
            _previousOutput = outputI;
            
            float outputQ = _previousOutput + _alpha * (Q - _previousOutput);
            _previousOutput = outputQ;
            
            return (outputI, outputQ);
        }
    }

    public class DeEmphasisFilter
    {
        private readonly float _alpha;
        private float _previousOutput = 0;

        public DeEmphasisFilter(int sampleRate, float timeConstantUs)
        {
            // timeConstantUs: 75 for USA/Korea, 50 for Europe
            float timeConstant = timeConstantUs * 1e-6f;
            float dt = 1.0f / sampleRate;
            _alpha = dt / (timeConstant + dt);
        }

        public float Process(float input)
        {
            _previousOutput = _previousOutput + _alpha * (input - _previousOutput);
            return _previousOutput;
        }
    }

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
                DiscardOnBufferOverflow = true,
                BufferLength = _sampleRate * 2 // 2 seconds buffer (reduced from 4 for lower latency)
            };

            _waveOut = new WaveOutEvent
            {
                DesiredLatency = 80, // Reduced from 100ms for better quality
                NumberOfBuffers = 4  // Increased from 3 for smoother playback
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

        public void AddSamples(float[] samples)
        {
            if (_waveProvider == null || samples.Length == 0)
                return;

            // Convert float samples to 16-bit PCM with soft clipping
            byte[] pcmData = new byte[samples.Length * 2];
            for (int i = 0; i < samples.Length; i++)
            {
                // Soft clipping using tanh for smoother distortion
                float clipped = (float)Math.Tanh(samples[i] * 0.9); // Slight pre-emphasis before clipping
                short pcmSample = (short)(clipped * short.MaxValue);
                pcmData[i * 2] = (byte)(pcmSample & 0xFF);
                pcmData[i * 2 + 1] = (byte)((pcmSample >> 8) & 0xFF);
            }

            _waveProvider.AddSamples(pcmData, 0, pcmData.Length);
            _totalSamplesAdded += samples.Length;
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
