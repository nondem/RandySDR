using System.Reflection;
using System.Text.Json;
using RandySDR;

internal static class Program
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    [STAThread]
    private static void Main()
    {
        CheckStartupSettling();
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

            CheckPresetModes(form, presets, auto, settings);
            Console.WriteLine("Scan preset, channel grid, mode and persistence checks passed.");
        }
        finally
        {
            Directory.Delete(directory, false);
        }
    }

    private static void CheckStartupSettling()
    {
        using var pipeline = new ReceiverPipeline(2_400_000);
        var settings = new ScanSettings
        {
            StartMHz = 100, StopMHz = 100.2, StepKHz = 200,
            Method = ScanMethod.TuneEachChannel, SettleMs = 120, AutoThreshold = false, ThresholdDb = 8
        };
        long now = 0;
        var constructor = typeof(SignalScanner).GetConstructor(PrivateInstance, null,
            new[] { typeof(ScanSettings), typeof(Action<uint>), typeof(Func<ReceiverPipeline>), typeof(Func<int>), typeof(Func<long>) }, null)!;
        var scanner = (SignalScanner)constructor.Invoke(new object[]
        {
            settings, (Action<uint>)(_ => { }), (Func<ReceiverPipeline?>)(() => pipeline),
            (Func<int>)(() => 2_400_000), (Func<long>)(() => now)
        });
        int locks = 0;
        int quietMeasurements = 0;
        scanner.SignalLocked += (_, _) => locks++;
        scanner.QuietChannelMeasured += _ => quietMeasurements++;
        byte[] signal = new byte[8192];
        for (int i = 0; i < signal.Length / 2; i++)
        {
            double phase = -2 * Math.PI * 300_000 * i / 2_400_000;
            signal[2 * i] = (byte)Math.Round(127.5 + 80 * Math.Cos(phase));
            signal[2 * i + 1] = (byte)Math.Round(127.5 + 80 * Math.Sin(phase));
        }
        SetField(pipeline, "_lastIqData", signal);
        Check(scanner.Start(out string error), $"Scanner failed to start: {error}");
        Check(scanner.IsStartupSettling && scanner.State == ScanState.Scanning,
            "A new scan must enter the startup settling state.");
        scanner.Tick();
        SetField(pipeline, "_buffersProcessed", 10);
        now = 999;
        scanner.Tick();
        Check(scanner.IsStartupSettling && scanner.State == ScanState.Scanning && locks == 0
            && quietMeasurements == 0 && scanner.Strength.All(double.IsNaN),
            "No detection or measurement may occur before the one-second startup delay.");
        now = 1000;
        scanner.Tick();
        Check(scanner.IsStartupSettling && locks == 0, "Startup must wait for buffers received after the delay.");
        SetField(pipeline, "_buffersProcessed", 11);
        scanner.Tick();
        Check(scanner.IsStartupSettling && locks == 0, "One fresh buffer is insufficient.");
        SetField(pipeline, "_buffersProcessed", 12);
        scanner.Tick();
        Check(!scanner.IsStartupSettling && scanner.State == ScanState.Locked && locks == 1,
            "A strong signal must be detectable after the delay and two fresh buffers.");

        scanner.Resume();
        Check(!scanner.IsStartupSettling && scanner.State == ScanState.Scanning,
            "Resume must not restart the startup delay.");
        SetField(pipeline, "_buffersProcessed", 14);
        now += 119;
        scanner.Tick();
        Check(scanner.CurrentHz == 100_300_000, "Resume must still respect the per-hop settling time.");
        now++;
        scanner.Tick();
        Check(scanner.CurrentHz == 100_500_000, "Resume must advance after the normal per-hop settling time.");

        Check(scanner.Start(out error) && !scanner.IsStartupSettling,
            "Replanning a running scan must not repeat the startup delay.");
        scanner.Stop();
        Check(!scanner.IsStartupSettling && scanner.State == ScanState.Idle,
            "Stopping must cancel startup settling.");
        Check(scanner.Start(out error) && scanner.IsStartupSettling,
            "Starting a new scan after stopping must reapply startup settling.");
        scanner.Tick();
        now += 500;
        Check(scanner.Start(out error) && scanner.IsStartupSettling,
            "Replanning during startup must keep the original settling period.");
        now += 500;
        scanner.Tick();
        SetField(pipeline, "_buffersProcessed", 16);
        scanner.Tick();
        Check(!scanner.IsStartupSettling, "Replanning during startup must not restart the one-second timer.");
        scanner.Stop();
        settings.SettleMs = 1500;
        now = 0;
        Check(scanner.Start(out error), "Scanner should start with a longer per-hop settling time.");
        scanner.Tick();
        now = 1000;
        scanner.Tick();
        SetField(pipeline, "_buffersProcessed", 18);
        now = 1499;
        scanner.Tick();
        Check(scanner.State == ScanState.Scanning, "Startup must not bypass a longer per-hop settling time.");
        now = 1500;
        scanner.Tick();
        Check(scanner.State == ScanState.Locked, "Detection should resume after the longer per-hop settling time.");
        scanner.Stop();
    }

    private static void SetField(object instance, string name, object value) =>
        instance.GetType().GetField(name, PrivateInstance)!.SetValue(instance, value);

    private static void CheckPresetModes(Form1 form, ComboBox presets, CheckBox auto, ScanSettings settings)
    {
        var pipeline = new ReceiverPipeline(2_400_000);
        typeof(Form1).GetField("_pipeline", PrivateInstance)!.SetValue(form, pipeline);
        try
        {
            var cases = new (int Index, DemodulationMode Mode)[]
            {
                (1, DemodulationMode.WideFm), (2, DemodulationMode.Am),
                (3, DemodulationMode.NarrowFm), (4, DemodulationMode.NarrowFm),
                (5, DemodulationMode.NarrowFm), (6, DemodulationMode.Am),
                (7, DemodulationMode.NarrowFm), (8, DemodulationMode.NarrowFm),
                (9, DemodulationMode.NarrowFm), (10, DemodulationMode.NarrowFm)
            };
            foreach (var (index, mode) in cases)
            {
                auto.Checked = true;
                presets.SelectedIndex = 0;
                Field<RadioButton>(form, mode == DemodulationMode.Am ? "rdoDemodWideFm" : "rdoDemodAm").Checked = true;
                presets.SelectedIndex = index;
                Check(pipeline.Demodulator.Mode == mode, $"Preset {index} must apply {mode} to the demodulator immediately.");
                double start = settings.StartMHz;
                double stop = settings.StopMHz;
                settings.LogDetections = false;
                Call(form, "OnScanLocked", start * 1_000_000, 10d);
                Check(pipeline.Demodulator.Mode == mode, $"Preset {index} start must retain {mode} on lock.");
                Call(form, "OnScanLocked", stop * 1_000_000, 10d);
                Check(pipeline.Demodulator.Mode == mode, $"Preset {index} endpoint must retain {mode} on lock.");
                Call(form, "LoadScanSettingsToUi");
                Check(presets.SelectedIndex == index, $"Preset {index} must survive a settings reload.");
                Call(form, "OnScanLocked", stop * 1_000_000, 10d);
                Check(pipeline.Demodulator.Mode == mode, $"Restored preset {index} must retain {mode} on lock.");
            }

            auto.Checked = false;
            Field<RadioButton>(form, "rdoDemodAm").Checked = true;
            presets.SelectedIndex = 1;
            Check(pipeline.Demodulator.Mode == DemodulationMode.Am && !auto.Checked,
                "Selecting a normal preset with automatic mode off must preserve manual demodulation.");
            Call(form, "OnScanLocked", 100_000_000d, 10d);
            Check(pipeline.Demodulator.Mode == DemodulationMode.Am,
                "Automatic mode off must preserve the manual mode when a signal locks.");

            presets.SelectedIndex = 0;
            auto.Checked = true;
            Call(form, "OnScanLocked", 100_000_000d, 10d);
            Check(pipeline.Demodulator.Mode == DemodulationMode.WideFm, "Custom FM broadcast channels must use wide FM.");
            Call(form, "OnScanLocked", 120_000_000d, 10d);
            Check(pipeline.Demodulator.Mode == DemodulationMode.Am, "Custom airband channels must use AM.");
            Call(form, "OnScanLocked", 145_000_000d, 10d);
            Check(pipeline.Demodulator.Mode == DemodulationMode.NarrowFm, "Custom 2 m channels must use narrow FM.");
            Call(form, "OnScanLocked", 300_000_000d, 10d);
            Check(pipeline.Demodulator.Mode == DemodulationMode.NarrowFm,
                "Unknown custom bands must preserve the current mode rather than guess.");
        }
        finally
        {
            typeof(Form1).GetField("_pipeline", PrivateInstance)!.SetValue(form, null);
            pipeline.Dispose();
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
