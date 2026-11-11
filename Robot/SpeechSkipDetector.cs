namespace RandySDR
{
    /// <summary>Conservative, per-lock speech screening; uncertain audio disables skipping for the lock.</summary>
    public sealed class SpeechSkipDetector
    {
        private readonly int _sampleRate;
        private readonly float[] _window;
        private int _filled;
        private long _screenedSamples;
        private bool _speechOrUncertain;

        public SpeechSkipDetector(int sampleRate)
        {
            _sampleRate = sampleRate;
            _window = new float[sampleRate * 2];
        }

        public void Add(float[] samples)
        {
            int offset = 0;
            while (offset < samples.Length)
            {
                int count = Math.Min(_window.Length - _filled, samples.Length - offset);
                Array.Copy(samples, offset, _window, _filled, count);
                _filled += count;
                offset += count;
                if (_filled != _window.Length) continue;
                var metrics = ClipAnalyzer.Analyze(_window, _sampleRate);
                if (metrics.Class is ClipClass.Voice or ClipClass.Uncertain || metrics.SpeechSec >= 0.3)
                    _speechOrUncertain = true;
                _screenedSamples += _window.Length;
                _filled = 0;
            }
        }

        public bool ShouldSkip(double seconds) =>
            !_speechOrUncertain && _screenedSamples >= Math.Max(6, seconds) * _sampleRate;
    }
}
