// LockoutPolicy - pure rules deciding when a channel should be auto-locked-out. Kept free of I/O so it is unit-testable.
namespace RandySDR
{
    public sealed record ClipHistoryEntry(DateTime StartUtc, ClipClass Class, double DurationSec);

    public static class LockoutPolicy
    {
        public const int ConsecutiveJunkClips = 3;
        public const double JunkSecondsLimit = 300;
        public static readonly TimeSpan Window = TimeSpan.FromHours(24);
        public static readonly TimeSpan Ttl = TimeSpan.FromDays(7);

        /// <summary>Channel key: frequency rounded to the scan step.</summary>
        public static long ChannelKey(double hz, double stepHz)
        {
            if (stepHz < 1) stepHz = 1;
            return (long)(Math.Round(hz / stepHz) * stepHz);
        }

        public static bool IsJunk(ClipClass c) => c is ClipClass.Carrier or ClipClass.Noise or ClipClass.Tone;

        /// <param name="history">Clips for one channel key, in any order.</param>
        /// <param name="voiceInLast30Days">True if the key ever produced a VOICE clip in the last 30 days (never auto-lock those).</param>
        public static bool ShouldPropose(IEnumerable<ClipHistoryEntry> history, bool voiceInLast30Days, DateTime nowUtc, out string reason)
        {
            reason = "";
            if (voiceInLast30Days) return false;

            var recent = history.Where(h => nowUtc - h.StartUtc <= Window).OrderByDescending(h => h.StartUtc).ToList();
            if (recent.Count == 0 || recent.Any(h => h.Class is ClipClass.Voice or ClipClass.Uncertain)) return false;

            if (recent.Count >= ConsecutiveJunkClips && recent.Take(ConsecutiveJunkClips).All(h => IsJunk(h.Class)))
            {
                reason = $"{ConsecutiveJunkClips}+ consecutive carrier/noise/tone locks with no speech in 24 h";
                return true;
            }

            double junkSec = recent.Sum(h => h.DurationSec);
            if (junkSec > JunkSecondsLimit)
            {
                reason = $"{junkSec / 60:F1} min of locked time with no speech in 24 h";
                return true;
            }
            return false;
        }
    }
}
