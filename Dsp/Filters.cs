namespace RandySDR
{
    // Small reusable DSP building blocks used by FmDemodulator.
    public class Biquad
    {
        private readonly float _b0, _b1, _b2, _a1, _a2;
        private float _z1, _z2;

        public Biquad(int sampleRate, float cutoff, float q)
        {
            double w0 = 2 * Math.PI * cutoff / sampleRate;
            double alpha = Math.Sin(w0) / (2 * q);
            double cs = Math.Cos(w0);
            double a0 = 1 + alpha;
            _b0 = (float)((1 - cs) / 2 / a0);
            _b1 = (float)((1 - cs) / a0);
            _b2 = _b0;
            _a1 = (float)(-2 * cs / a0);
            _a2 = (float)((1 - alpha) / a0);
        }

        public float Process(float x)
        {
            float y = _b0 * x + _z1;
            _z1 = _b1 * x - _a1 * y + _z2;
            _z2 = _b2 * x - _a2 * y;
            return y;
        }
    }

    /// <summary>4th-order Butterworth low-pass applied to complex baseband (channel filter).</summary>
    public class ComplexButterworth4
    {
        private readonly Biquad _i1, _i2, _q1, _q2;

        public ComplexButterworth4(int sampleRate, float cutoff)
        {
            _i1 = new Biquad(sampleRate, cutoff, 0.5412f);
            _i2 = new Biquad(sampleRate, cutoff, 1.3066f);
            _q1 = new Biquad(sampleRate, cutoff, 0.5412f);
            _q2 = new Biquad(sampleRate, cutoff, 1.3066f);
        }

        public (float I, float Q) Process(float i, float q)
            => (_i2.Process(_i1.Process(i)), _q2.Process(_q1.Process(q)));
    }

    /// <summary>Slow AGC normalising audio to a comfortable level.</summary>
    public class AudioAgc
    {
        private readonly float _attack, _decay, _target, _maxGain;
        private float _level = 1e-3f;

        public AudioAgc(int sampleRate, float target = 0.3f, float maxGain = 200f)
        {
            _attack = 1f - (float)Math.Exp(-1.0 / (0.005 * sampleRate));
            _decay = 1f - (float)Math.Exp(-1.0 / (1.0 * sampleRate));
            _target = target;
            _maxGain = maxGain;
        }

        public float Process(float x)
        {
            float a = Math.Abs(x);
            _level += (a > _level ? _attack : _decay) * (a - _level);
            float gain = Math.Min(_maxGain, _target / Math.Max(_level, 1e-6f));
            return x * gain;
        }
    }

    public class DcBlocker
    {
        private readonly float _alpha;
        private float _dcI;
        private float _dcQ;

        public DcBlocker(float alpha)
        {
            _alpha = alpha;
        }

        public (float I, float Q) Process(float I, float Q)
        {
            _dcI = (1 - _alpha) * _dcI + _alpha * I;
            _dcQ = (1 - _alpha) * _dcQ + _alpha * Q;
            return (I - _dcI, Q - _dcQ);
        }
    }

    public class ComplexLowPassFilter
    {
        private readonly float _alpha;
        private float _prevI;
        private float _prevQ;

        public ComplexLowPassFilter(int sampleRate, float cutoffFrequency)
        {
            float rc = 1.0f / (2.0f * (float)Math.PI * cutoffFrequency);
            float dt = 1.0f / sampleRate;
            _alpha = dt / (rc + dt);
        }

        public (float I, float Q) Process(float I, float Q)
        {
            _prevI = _prevI + _alpha * (I - _prevI);
            _prevQ = _prevQ + _alpha * (Q - _prevQ);
            return (_prevI, _prevQ);
        }
    }

    public class BandPassFilter
    {
        // DEPRECATED: This filter had issues. Removed from use.
        // Keeping class definition for compatibility only.
        public (float I, float Q) Process(float I, float Q)
        {
            return (I, Q);
        }
    }

    public class HighPassFilter
    {
        // DEPRECATED: Not used. Keeping for reference.
        public HighPassFilter(int sampleRate, float cutoffFrequency) { }
        public (float I, float Q) Process(float I, float Q) => (0, 0);
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

    public class RealHighPassFilter
    {
        private readonly float _alpha;
        private float _previousInput;
        private float _previousOutput;

        public RealHighPassFilter(int sampleRate, float cutoffFrequency)
        {
            float rc = 1.0f / (2.0f * (float)Math.PI * cutoffFrequency);
            float dt = 1.0f / sampleRate;
            _alpha = rc / (rc + dt);
        }

        public float Process(float input)
        {
            _previousOutput = _alpha * (_previousOutput + input - _previousInput);
            _previousInput = input;
            return _previousOutput;
        }
    }
}
