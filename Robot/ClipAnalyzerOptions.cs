namespace RandySDR
{
    public sealed record ClipAnalyzerOptions(double MinSpeechSeconds, double MinSpeechRatio, double MaxCarrierRatio)
    {
        public static ClipAnalyzerOptions Default { get; } = new(1.0, 0.20, 0.05);
    }
}
