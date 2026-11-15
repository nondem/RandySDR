namespace RandySDR
{
    /// <summary>Extended CB scan plan in ascending frequency order.</summary>
    public static class CbChannels
    {
        public readonly record struct Channel(int FrequencyHz, string Label, DemodulationMode Mode);

        public const int StartHz = 26_065_000;
        public const int StopHz = 28_305_000;
        public const int StepHz = 10_000;
        private const int BaseStartHz = 26_965_000;
        private const int BankOffsetHz = 450_000;

        // Channels 23, 24 and 25 are not numbered in frequency order.
        private static readonly string[] BaseLabels =
        {
            "1", "2", "3", "3A", "4", "5", "6", "7", "7A", "8",
            "9", "10", "11", "11A", "12", "13", "14", "15", "15A", "16",
            "17", "18", "19", "19A", "20", "21", "22", "24", "25", "23",
            "26", "27", "28", "29", "30", "31", "32", "33", "34", "35",
            "36", "37", "38", "39", "40"
        };

        public static IReadOnlyList<Channel> Channels { get; } = Array.AsReadOnly(
            Enumerable.Range(-2, 5).SelectMany(bank =>
                BaseLabels.Select((label, index) =>
                {
                    int hz = BaseStartHz + bank * BankOffsetHz + index * StepHz;
                    string suffix = bank switch { -2 => "LL", -1 => "L", 0 => "", 1 => "H", _ => "HH" };
                    DemodulationMode mode = bank switch
                    {
                        -1 or 0 when index >= 40 => DemodulationMode.Lsb,
                        1 => DemodulationMode.Usb,
                        2 when index >= 14 => DemodulationMode.Usb,
                        _ => DemodulationMode.Am
                    };
                    return new Channel(hz, label + suffix, mode);
                })).ToArray());

        public static IReadOnlyList<int> FrequenciesHz { get; } =
            Array.AsReadOnly(Channels.Select(channel => channel.FrequencyHz).ToArray());

        public static bool TryGetChannel(double frequencyHz, out Channel channel)
        {
            double index = (frequencyHz - StartHz) / StepHz;
            int i = (int)Math.Round(index);
            if (i >= 0 && i < Channels.Count && Math.Abs(frequencyHz - Channels[i].FrequencyHz) < 1)
            {
                channel = Channels[i];
                return true;
            }
            channel = default;
            return false;
        }
    }
}
