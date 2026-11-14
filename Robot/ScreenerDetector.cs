namespace RandySDR
{
    /// <summary>Conservative per-lock screening of consecutive open-squelch audio.</summary>
    public sealed class ScreenerDetector
    {
        private readonly int _sampleRate;
        private readonly float[] _window;
        private readonly int _requiredWindows;
        private int _filled, _windows;
        private ClipClass? _candidate;
        private bool _protected;
        private bool _partialClear;

        public ScreenerDetector(int sampleRate, double seconds)
        {
            if (sampleRate <= 0 || !double.IsFinite(seconds) || seconds < 4 || seconds > 30)
                throw new ArgumentOutOfRangeException(nameof(seconds));
            _sampleRate = sampleRate;
            _window = new float[sampleRate * 2];
            _requiredWindows = (int)Math.Ceiling(seconds / 2);
        }

        public string Status => _protected ? "continuing: speech or uncertain audio"
            : Reason is { } reason ? $"filtering: {reason}" : "analyzing";

        public string? Reason => !_protected && (_filled == 0 || _partialClear) && _windows >= _requiredWindows && _candidate is { } candidate
            ? candidate.ToString().ToLowerInvariant() : null;

        public void Add(float[] samples)
        {
            if (_protected) return;
            _partialClear = false;
            int offset = 0;
            while (offset < samples.Length && !_protected)
            {
                int count = Math.Min(_window.Length - _filled, samples.Length - offset);
                Array.Copy(samples, offset, _window, _filled, count);
                _filled += count;
                offset += count;
                if (_filled != _window.Length) continue;

                var metrics = ClipAnalyzer.Analyze(_window, _sampleRate);
                _filled = 0;
                if (metrics.Class is ClipClass.Voice or ClipClass.Uncertain || metrics.SpeechSec >= 0.3)
                {
                    _protected = true;
                    break;
                }
                if (_candidate != null && _candidate != metrics.Class)
                {
                    _protected = true;
                    break;
                }
                _candidate = metrics.Class;
                _windows++;
            }
            if (!_protected && _windows >= _requiredWindows && _filled >= _sampleRate / 4)
            {
                var partial = ClipAnalyzer.Analyze(_window[.._filled], _sampleRate);
                if (partial.Class is ClipClass.Voice or ClipClass.Uncertain || partial.SpeechSec >= 0.3 || partial.Class != _candidate)
                    _protected = true;
                else
                    _partialClear = true;
            }
        }

        public void Invalidate() => _protected = true;
    }
}
