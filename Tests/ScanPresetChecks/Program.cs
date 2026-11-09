using System.Reflection;
using System.Text.Json;
using RandySDR;

internal static class Program
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    [STAThread]
    private static void Main()
    {
        string directory = Path.Combine(Path.GetTempPath(), "RandySDR-scan-check-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        try
        {
            var constructor = typeof(Form1).GetConstructor(PrivateInstance, null, new[] { typeof(string) }, null)!;
            using var form = (Form1)constructor.Invoke(new object[] { directory });
            var presets = Field<ComboBox>(form, "_cmbPreset");
            var auto = Field<CheckBox>(form, "_chkAutoMode");
            var nfm = Field<RadioButton>(form, "rdoDemodNarrowFm");
            var am = Field<RadioButton>(form, "rdoDemodAm");
            var settings = Field<ScanSettings>(form, "_scanSettings");

            CheckPreset(form, presets, settings, nfm, "Law Enforcement VHF", 136, 174, 1.25, 30_200);
            CheckWeatherExclusion(settings);
            Call(form, "SelectModeForBand", 136.5);
            Check(nfm.Checked, "VHF preset must override the overlapping airband AM rule.");

            string json = JsonSerializer.Serialize(settings);
            var restored = JsonSerializer.Deserialize<ScanSettings>(json)!;
            settings.StartMHz = restored.StartMHz;
            settings.StopMHz = restored.StopMHz;
            settings.StepKHz = restored.StepKHz;
            settings.AutoSelectMode = restored.AutoSelectMode;
            settings.PresetName = restored.PresetName;
            Call(form, "LoadScanSettingsToUi");
            Check(presets.SelectedItem!.ToString()!.StartsWith("Law Enforcement VHF"), "VHF preset should restore from saved settings.");
            CheckWeatherExclusion(settings);
            Call(form, "SelectModeForBand", 136.5);
            Check(nfm.Checked, "Restored VHF preset must retain narrow-FM selection.");

            auto.Checked = false;
            am.Checked = true;
            settings.LogDetections = false;
            Call(form, "OnScanLocked", 136_500_000d, 15d);
            Check(am.Checked, "Disabling automatic mode must preserve the user's selected mode.");

            CheckPreset(form, presets, settings, nfm, "Law Enforcement UHF", 450, 470, 6.25, 3_201);
            Call(form, "SelectModeForBand", 465d);
            Check(nfm.Checked, "UHF preset should select narrow FM.");

            Field<NumericUpDown>(form, "_numStep").Value = 5m;
            Check(presets.SelectedIndex == 0 && settings.PresetName == null, "Editing a preset must switch to custom.");
            Field<NumericUpDown>(form, "_numStart").Value = 136m;
            Field<NumericUpDown>(form, "_numStop").Value = 174m;
            Check(BuildChannels(settings).Contains(162_400_000), "Custom scanning must not inherit the weather exclusion.");
            Call(form, "SelectModeForBand", 136.5);
            Check(am.Checked, "Custom ranges must retain normal airband mode selection.");

            settings.PresetName = restored.PresetName;
            Call(form, "LoadScanSettingsToUi");
            Check(presets.SelectedIndex == 0, "A saved preset name must not override a changed frequency grid.");

            presets.SelectedIndex = 6;
            Check(settings.UseCbChannels && am.Checked, "The CB preset must retain its channel plan and AM mode.");

            presets.SelectedIndex = Enumerable.Range(0, presets.Items.Count)
                .Single(i => presets.Items[i]!.ToString()!.StartsWith("NOAA weather"));
            var weather = BuildChannels(settings);
            Check(weather.Count == 7 && weather[0] == 162_400_000 && weather[^1] == 162_550_000,
                "The NOAA preset must continue scanning all seven weather channels.");

            Console.WriteLine("Scan preset, channel grid, mode and persistence checks passed.");
        }
        finally
        {
            Directory.Delete(directory, false);
        }
    }

    private static void CheckPreset(Form1 form, ComboBox presets, ScanSettings settings, RadioButton nfm,
        string name, double start, double stop, double step, int count)
    {
        int index = Enumerable.Range(0, presets.Items.Count)
            .Single(i => presets.Items[i]!.ToString()!.StartsWith(name));
        if (presets.SelectedIndex == index) presets.SelectedIndex = 0;
        presets.SelectedIndex = index;
        Check(settings.StartMHz == start && settings.StopMHz == stop && settings.StepKHz == step,
            $"{name}: incorrect range or step.");
        Check(settings.AutoSelectMode && nfm.Checked && !settings.UseCbChannels,
            $"{name}: incorrect mode or channel plan.");

        var channels = BuildChannels(settings);
        Check(channels.Count == count && channels[0] == start * 1_000_000 && channels[^1] == stop * 1_000_000,
            $"{name}: incorrect channel count or endpoints.");
        Check(channels[1] - channels[0] == step * 1_000, $"{name}: incorrect channel spacing.");
    }

    private static IReadOnlyList<double> BuildChannels(ScanSettings settings)
    {
        var scanner = new SignalScanner(settings, _ => { }, () => null, () => 2_400_000);
        Call(scanner, "BuildPlan");
        return scanner.ChannelHz;
    }

    private static void CheckWeatherExclusion(ScanSettings settings)
    {
        var originalMethod = settings.Method;
        try
        {
            foreach (var method in new[] { ScanMethod.WidebandFft, ScanMethod.TuneEachChannel })
            {
                settings.Method = method;
                var scanner = new SignalScanner(settings, _ => { }, () => null, () => 2_400_000);
                Call(scanner, "BuildPlan");
                var channels = scanner.ChannelHz;
                Check(!channels.Any(hz => hz >= 162_350_000 && hz <= 162_600_000),
                    $"{method}: weather frequencies or guard channels were not excluded.");
                Check(!channels.Contains(162_392_500), $"{method}: reported weather interference frequency must be excluded.");
                Check(!channels.Contains(162_386_250), $"{method}: reported 162.3863 MHz interference frequency must be excluded.");
                Check(channels.Contains(162_348_750) && channels.Contains(162_601_250),
                    $"{method}: neighboring channels should remain available.");
                Check(channels.Count == 30_200, $"{method}: incorrect VHF channel count.");
                var hops = (System.Collections.IEnumerable)scanner.GetType()
                    .GetField("_hops", PrivateInstance)!.GetValue(scanner)!;
                var plannedIndices = new List<int>();
                foreach (var hop in hops)
                    plannedIndices.AddRange((int[])hop.GetType().GetProperty("Channels")!.GetValue(hop)!);
                Check(plannedIndices.SequenceEqual(Enumerable.Range(0, channels.Count)),
                    $"{method}: scan hops must cover every remaining channel exactly once.");
            }
        }
        finally
        {
            settings.Method = originalMethod;
        }
    }

    private static T Field<T>(object instance, string name) =>
        (T)instance.GetType().GetField(name, PrivateInstance)!.GetValue(instance)!;

    private static void Call(object instance, string method, params object[] arguments) =>
        instance.GetType().GetMethod(method, PrivateInstance)!.Invoke(instance, arguments);

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
