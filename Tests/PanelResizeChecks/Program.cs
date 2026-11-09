using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using RandySDR;

internal static class Program
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Contains("--scan-mode-only"))
        {
            CheckLiveScanRange();
            Console.WriteLine("Live scan range and mode checks passed.");
            return;
        }
        CheckFrequencyWheel();
        CheckDriverConfigurationResults();
        CheckDeviceOpening();
        CheckLiveScanRange();
        if (Environment.GetEnvironmentVariable("RANDYSDR_HARDWARE_CHECK") == "1")
            CheckRealDeviceOpening();
        string directory = Path.Combine(Path.GetTempPath(), "RandySDR-dashboard-check-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(Path.Combine(directory, "panel-sizes.json"), "device:999");
            Size savedSize;
            Point savedLocation;
            using (var form = CreateForm(directory))
            {
                var device = FindCard(form, "device");
                var audio = FindCard(form, "audio");
                var scan = FindCard(form, "scan-scan-range");
                var tuning = FindCard(form, "tuning");
                var tuningContent = tuning.Controls.OfType<Panel>().Single().Controls[0];
                var frequencySelector = tuningContent.Controls.OfType<FrequencySelector>().Single();
                var scanViewport = scan.Controls.OfType<Panel>().Single();
                Check(tuningContent.Controls.Count == 1 && tuning.Height == frequencySelector.Bottom + 10,
                    "The default tuning card should contain only the compact frequency selector, with minimal height.");
                var tuningViewport = tuning.Controls.OfType<Panel>().Single();
                tuning.Width = Math.Max(650, frequencySelector.Right + 60);
                Application.DoEvents();
                Check(tuningContent.Width == tuningViewport.ClientSize.Width,
                    "The compact tuning card must not retain its old oversized content width.");
                scanViewport.Enabled = true;
                Check(scanViewport.Controls[0].Controls.Cast<Control>().All(c => c.Enabled),
                    "Reparented scanner content must become usable when its viewport is enabled.");
                Check(device.Height == 999, "Legacy panel height should be migrated.");
                var handles = device.Controls.Cast<Control>().Where(c => c.GetType().Name == "DashboardResizeHandle").ToArray();
                Check(handles.Length == 3 && handles.All(h => h.Visible), "Unlocked panels need three resize handles.");
                var corner = handles.Single(c => c.Width == 14 && c.Height == 14);
                var origin = new Point(corner.Width / 2, corner.Height / 2);
                var screen = corner.PointToScreen(origin);
                int oldWidth = device.Width, oldHeight = device.Height;
                Mouse(corner, "OnMouseDown", origin);
                Mouse(corner, "OnMouseMove", corner.PointToClient(new Point(screen.X + 80, screen.Y + 60)));
                Mouse(corner, "OnMouseUp", corner.PointToClient(new Point(screen.X + 80, screen.Y + 60)));
                Check(device.Width > oldWidth && device.Height > oldHeight, "Corner must resize both dimensions.");
                var right = handles.Single(h => h.Width == 6);
                var rightStart = new Point(2, 8);
                screen = right.PointToScreen(rightStart);
                int heightBefore = device.Height;
                int widthBeforeRight = device.Width;
                Mouse(right, "OnMouseDown", rightStart);
                Mouse(right, "OnMouseMove", right.PointToClient(new Point(screen.X + 50, screen.Y)));
                Mouse(right, "OnMouseUp", right.PointToClient(new Point(screen.X + 50, screen.Y)));
                Check(device.Height == heightBefore && device.Width > widthBeforeRight, "Horizontal resize must change only width.");
                var bottom = handles.Single(h => h.Height == 6);
                var bottomStart = new Point(8, 2);
                screen = bottom.PointToScreen(bottomStart);
                int widthBefore = device.Width;
                Mouse(bottom, "OnMouseDown", bottomStart);
                Mouse(bottom, "OnMouseMove", bottom.PointToClient(new Point(screen.X, screen.Y + 50)));
                Mouse(bottom, "OnMouseUp", bottom.PointToClient(new Point(screen.X, screen.Y + 50)));
                Check(device.Width == widthBefore && device.Height > heightBefore, "Vertical resize must not change width.");
                var sizeBeforeCancel = device.Size;
                screen = right.PointToScreen(rightStart);
                Mouse(right, "OnMouseDown", rightStart);
                Mouse(right, "OnMouseMove", right.PointToClient(new Point(screen.X + 40, screen.Y)));
                right.Capture = false;
                Check(device.Size == sizeBeforeCancel, "Losing capture must cancel resizing.");
                screen = right.PointToScreen(rightStart);
                Mouse(right, "OnMouseDown", rightStart);
                Mouse(right, "OnMouseMove", right.PointToClient(new Point(screen.X - 10000, screen.Y)));
                Mouse(right, "OnMouseUp", right.PointToClient(new Point(screen.X - 10000, screen.Y)));
                Check(device.Width == 240 && device.Height == sizeBeforeCancel.Height,
                    "Horizontal resizing must respect the minimum width without changing height.");
                screen = right.PointToScreen(rightStart);
                Mouse(right, "OnMouseDown", rightStart);
                Mouse(right, "OnMouseMove", right.PointToClient(new Point(screen.X + sizeBeforeCancel.Width - device.Width, screen.Y)));
                Mouse(right, "OnMouseUp", right.PointToClient(new Point(screen.X + sizeBeforeCancel.Width - 240, screen.Y)));
                savedSize = device.Size;

                Point start = new(50, Math.Max(2, device.HeaderHeight / 2));
                screen = device.PointToScreen(start);
                Mouse(device, "OnMouseDown", start);
                Mouse(device, "OnMouseMove", device.PointToClient(new Point(screen.X + 100, screen.Y + 80)));
                Mouse(device, "OnMouseUp", device.PointToClient(new Point(screen.X + 100, screen.Y + 80)));
                Check(!device.Collapsed, "Dragging the header must not collapse the panel.");
                savedLocation = device.Location;

                var menu = Field<ToolStripDropDownButton>(form, "_panelMenu");
                var audioItem = menu.DropDownItems.OfType<ToolStripMenuItem>().Single(i => i.Tag == audio);
                audioItem.Checked = false;
                Check(!audio.Visible, "Panel menu should hide the panel.");
                Call(form, "FocusDashboardCard", device);
                var workspace = Field<Panel>(form, "_workspace");
                Check(workspace.Controls.GetChildIndex(device) == 0 && device.WorkspaceFocused,
                    "Focused panel should be raised and highlighted.");
                Call(form, "SaveDashboard");
                Check(File.Exists(Path.Combine(directory, "dashboard-layout.json")), "Save should write a layout file.");
                Call(form, "SetDashboardLocked", true);
                Check(handles.All(h => !h.Visible), "Locked layout must hide handles.");
                Point lockedLocation = device.Location;
                Mouse(device, "OnMouseDown", start);
                Mouse(device, "OnMouseMove", new Point(start.X + 100, start.Y + 100));
                Mouse(device, "OnMouseUp", new Point(start.X + 100, start.Y + 100));
                Check(device.Location == lockedLocation, "Locked panels must not move.");
                Call(form, "SetDashboardLocked", false);
                device.SetCollapsed(true);
                Check(handles.All(h => !h.Visible), "Collapsed panel must hide handles.");
                device.SetCollapsed(false);
                Check(device.Size == savedSize, "Expanding must restore custom size.");
            }
            string path = Path.Combine(directory, "dashboard-layout.json");
            var json = JsonNode.Parse(File.ReadAllText(path))!;
            json["Panels"]!.AsArray().Add(new JsonObject
            {
                ["Id"] = "obsolete-panel", ["X"] = 0, ["Y"] = 0, ["Width"] = 300,
                ["Height"] = 200, ["Visible"] = true, ["Z"] = 100
            });
            File.WriteAllText(path, json.ToJsonString());
            using (var form = CreateForm(directory))
            {
                var device = FindCard(form, "device");
                Check(device.Size == savedSize && device.Location == savedLocation,
                    "Saved panel position and size must survive restart.");
                Check(!FindCard(form, "audio").Visible, "Saved visibility must survive restart.");
                Check(Field<Panel>(form, "_workspace").Controls.GetChildIndex(device) == 0,
                    "Saved z-order must survive restart.");
                var viewport = device.Controls.OfType<Panel>().Single();
                device.Size = new Size(240, device.HeaderHeight + 70);
                Application.DoEvents();
                Check(viewport.AutoScroll && viewport.Controls[0].Width >= viewport.ClientSize.Width
                    && viewport.Controls[0].Height >= viewport.ClientSize.Height,
                    "Small panels must retain scrollable content.");
                Call(form, "SelectDashboardDefault");
                Check(FindCard(form, "audio").Visible, "Reset must show the default panels.");
                var choice = Field<ComboBox>(form, "_layoutChoice");
                choice.SelectedIndex = 1;
                Check(choice.SelectedIndex == 1 && FindCard(form, "audio").Visible, "Compact preset must be selectable.");
                Call(form, "SelectDashboardDefault");
                var tuning = FindCard(form, "tuning");
                var tuningContent = tuning.Controls.OfType<Panel>().Single().Controls[0];
                Check(tuningContent.Controls.Cast<Control>().Where(c => c.Dock == DockStyle.None)
                    .All(c => c.Right <= tuningContent.Width && c.Bottom <= tuningContent.Height),
                    "Fixed-position controls must remain reachable inside their scrolling content.");
            }
            var panels = json["Panels"]!.AsArray();
            var audioState = panels.First(p => (string?)p!["Id"] == "audio");
            panels.Remove(audioState);
            var deviceState = panels.First(p => (string?)p!["Id"] == "device")!;
            deviceState["X"] = 19000;
            deviceState["Y"] = 19000;
            json["ViewportWidth"] = 20000;
            json["ViewportHeight"] = 20000;
            File.WriteAllText(path, json.ToJsonString());
            using (var form = CreateForm(directory))
            {
                var workspaceScroll = Field<Panel>(form, "_workspaceScroll");
                var device = FindCard(form, "device");
                Check(device.Left < workspaceScroll.ClientSize.Width && device.Top < workspaceScroll.ClientSize.Height,
                    "Panels saved within a larger viewport must remain reachable on a smaller viewport.");
                Check(FindCard(form, "audio").Visible, "Missing saved panels must use their defaults.");
            }
            using (var document = JsonDocument.Parse(File.ReadAllText(path)))
            {
                Check(document.RootElement.GetProperty("Panels").GetArrayLength() > 0, "Saved layout must contain panel state.");
            }
            var storeType = typeof(Form1).Assembly.GetType("RandySDR.DashboardLayoutStore")!;
            var store = Activator.CreateInstance(storeType, PrivateInstance, null, new object[] { directory }, null)!;
            var load = storeType.GetMethod("Load", PrivateInstance)!;
            panels.Add(JsonNode.Parse(deviceState.ToJsonString()));
            File.WriteAllText(path, json.ToJsonString());
            ExpectInvalidLayout(store, load, "Duplicate panel IDs must be rejected.");
            File.WriteAllText(path, "{");
            ExpectInvalidLayout(store, load, "Malformed JSON must be rejected.");
            Console.WriteLine("Dashboard migration, movement, resizing, visibility, z-order and persistence checks passed.");
        }
        finally
        {
            foreach (string file in Directory.GetFiles(directory)) File.Delete(file);
            Directory.Delete(directory);
        }
    }

    private static void CheckRealDeviceOpening()
    {
        string directory = Path.Combine(Path.GetTempPath(), "RandySDR-hardware-check-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        try
        {
            using var form = CreateForm(directory);
            Check(Field<List<DeviceInfo>>(form, "_devices").Count > 0, "Hardware check requires an SDR device.");
            string? error = null;
            using var dialogTimer = new System.Windows.Forms.Timer { Interval = 20 };
            dialogTimer.Tick += (_, _) =>
            {
                IntPtr window = FindWindow(null, "Error");
                if (window == IntPtr.Zero) return;
                GetWindowThreadProcessId(window, out uint processId);
                if (processId != (uint)Environment.ProcessId) return;
                error = Field<Label>(form, "lblStatus").Text;
                PostMessage(window, 0x0010, IntPtr.Zero, IntPtr.Zero);
            };
            dialogTimer.Start();
            Call(form, "btnOpen_Click", form, EventArgs.Empty);
            var timeout = System.Diagnostics.Stopwatch.StartNew();
            while (Field<bool>(form, "_deviceOpening") && timeout.ElapsedMilliseconds < 30000)
            {
                Application.DoEvents();
                Thread.Sleep(10);
            }
            Check(!Field<bool>(form, "_deviceOpening") && error == null && Field<Button>(form, "btnClose").Enabled,
                $"The physical SDR should open successfully: {error ?? Field<Label>(form, "lblStatus").Text}");
            Call(form, "btnClose_Click", form, EventArgs.Empty);
        }
        finally
        {
            foreach (string file in Directory.GetFiles(directory)) File.Delete(file);
            Directory.Delete(directory);
        }
    }

    private static void CheckDriverConfigurationResults()
    {
        var type = typeof(NativeRtlSdr);
        var ppm = type.GetMethod("PpmCorrectionSucceeded", BindingFlags.Static | BindingFlags.NonPublic)!;
        var offset = type.GetMethod("OffsetTuningSucceeded", BindingFlags.Static | BindingFlags.NonPublic)!;
        bool Ppm(int result, int requested, int actual) =>
            (bool)ppm.Invoke(null, new object[] { result, requested, (Func<int>)(() => actual) })!;
        bool Offset(int result, bool requested, int actual) =>
            (bool)offset.Invoke(null, new object[] { result, requested, (Func<int>)(() => actual) })!;

        Check(Ppm(0, 0, 1) && Ppm(-2, 0, 0) && Ppm(-2, 17, 17),
            "An unchanged PPM correction should succeed only when the requested correction is present.");
        Check(!Ppm(-2, 17, 0) && !Ppm(-1, 0, 0) && !Ppm(-3, 0, 0),
            "A mismatched correction or other driver failure must remain an error.");
        Check(Offset(0, true, 0) && Offset(-2, false, 0),
            "Unsupported offset tuning should not prevent opening when it is already disabled.");
        Check(!Offset(-2, false, 1) && !Offset(-2, false, -1) && !Offset(-2, true, 0)
            && !Offset(-3, false, 0),
            "Failed offset changes and other driver errors must not be reported as successful.");

        var wrapper = new RtlSdrWrapper();
        Check(!wrapper.ConfigureReceiveChain(2_400_000, 103_100_000, false, false, 250, 0,
            200_000, false, false, 0, out string error) && error.Contains("not open"),
            "Configuration errors must explain why the device could not be configured.");
    }

    private static void CheckDeviceOpening()
    {
        string directory = Path.Combine(Path.GetTempPath(), "RandySDR-open-check-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        try
        {
            using (var form = CreateForm(directory))
            using (var gate = new ManualResetEventSlim())
            {
                var devices = Field<List<DeviceInfo>>(form, "_devices");
                devices.Clear();
                devices.Add(new DeviceInfo { Index = 0, Name = "Test SDR", Serial = "" });
                var list = Field<ComboBox>(form, "cmbDevices");
                list.Items.Clear();
                list.Items.Add("Test SDR");
                list.SelectedIndex = 0;
                Call(form, "UpdateControlsState", false);
                int workerThread = 0;
                int calls = 0;
                Func<Func<int[]>, Task<int[]>> runner = _ => Task.Run(() =>
                {
                    Interlocked.Increment(ref calls);
                    workerThread = Environment.CurrentManagedThreadId;
                    gate.Wait();
                    return new[] { 0, 100, 200 };
                });
                SetField(form, "_runDeviceOpen", runner);
                int uiThread = Environment.CurrentManagedThreadId;
                Call(form, "btnOpen_Click", form, EventArgs.Empty);
                WaitFor(() => calls == 1, "The device-open worker did not start.");
                var open = Field<Button>(form, "btnOpen");
                var status = Field<Label>(form, "lblStatus");
                Check(!open.Enabled && !list.Enabled && status.Text.Contains("Opening device"),
                    "Opening should be visible and conflicting controls disabled.");
                Check(workerThread != uiThread, "Device opening must run away from the UI thread.");
                Call(form, "btnOpen_Click", form, EventArgs.Empty);
                Check(calls == 1, "Repeated open requests must not start another worker.");
                bool ticked = false;
                using var timer = new System.Windows.Forms.Timer { Interval = 20 };
                timer.Tick += (_, _) => ticked = true;
                timer.Start();
                WaitFor(() => ticked, "The UI message loop must remain responsive while opening.");
                gate.Set();
                WaitFor(() => !Field<bool>(form, "_deviceOpening"), "Opening did not complete.");
                Check(status.Text.Contains("Connected") && !list.Enabled,
                    $"Successful opening must enable the connected state (status: {status.Text}, list enabled: {list.Enabled}).");
                Call(form, "btnClose_Click", form, EventArgs.Empty);
                Check(open.Enabled && list.Enabled, "Closing must restore the disconnected controls.");
            }

            using (var form = CreateForm(directory))
            using (var gate = new ManualResetEventSlim())
            {
                var devices = Field<List<DeviceInfo>>(form, "_devices");
                devices.Clear();
                devices.Add(new DeviceInfo { Index = 0, Name = "Test SDR", Serial = "" });
                var list = Field<ComboBox>(form, "cmbDevices");
                list.Items.Clear();
                list.Items.Add("Test SDR");
                list.SelectedIndex = 0;
                Call(form, "UpdateControlsState", false);
                SetField(form, "_runDeviceOpen", (Func<Func<int[]>, Task<int[]>>)(_ => Task.Run(() =>
                {
                    gate.Wait();
                    return Array.Empty<int>();
                })));
                Call(form, "btnOpen_Click", form, EventArgs.Empty);
                form.Close();
                Check(!form.IsDisposed && Field<Label>(form, "lblStatus").Text.Contains("Waiting"),
                    "Closing during opening must wait without destroying the form.");
                gate.Set();
                WaitFor(() => form.IsDisposed, "The form must close after the pending open completes.");
            }

            using (var form = CreateForm(directory))
            {
                var devices = Field<List<DeviceInfo>>(form, "_devices");
                devices.Clear();
                devices.Add(new DeviceInfo { Index = 0, Name = "Test SDR", Serial = "" });
                var list = Field<ComboBox>(form, "cmbDevices");
                list.Items.Clear();
                list.Items.Add("Test SDR");
                list.SelectedIndex = 0;
                Call(form, "UpdateControlsState", false);
                var completion = new TaskCompletionSource<int[]>();
                SetField(form, "_runDeviceOpen", (Func<Func<int[]>, Task<int[]>>)(_ => completion.Task));
                bool sawError = false;
                using var dialogTimer = new System.Windows.Forms.Timer { Interval = 20 };
                dialogTimer.Tick += (_, _) =>
                {
                    IntPtr window = FindWindow(null, "Error");
                    if (window == IntPtr.Zero) return;
                    GetWindowThreadProcessId(window, out uint processId);
                    if (processId != (uint)Environment.ProcessId) return;
                    sawError = true;
                    PostMessage(window, 0x0010, IntPtr.Zero, IntPtr.Zero);
                };
                dialogTimer.Start();
                Call(form, "btnOpen_Click", form, EventArgs.Empty);
                completion.SetException(new InvalidOperationException("Simulated connection failure"));
                WaitFor(() => sawError, "Opening failures must be reported to the user.");
                Check(!Field<bool>(form, "_deviceOpening") && Field<Button>(form, "btnOpen").Enabled
                    && list.Enabled && Field<Label>(form, "lblStatus").Text == "Device opening failed",
                    "A failed open must restore the disconnected state.");
            }
        }
        finally
        {
            foreach (string file in Directory.GetFiles(directory)) File.Delete(file);
            Directory.Delete(directory);
        }
    }

    private static void CheckLiveScanRange()
    {
        string directory = Path.Combine(Path.GetTempPath(), "RandySDR-live-scan-check-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        try
        {
            using var form = CreateForm(directory);
            SetField(form, "_pipeline", new ReceiverPipeline(2_400_000));
            Call(form, "UpdateControlsState", true);
            Field<Button>(form, "btnStop").Enabled = true;
            var pipeline = Field<ReceiverPipeline>(form, "_pipeline");
            var automatic = Field<CheckBox>(form, "_chkAutoMode");
            var am = Field<RadioButton>(form, "rdoDemodAm");
            var wideFm = Field<RadioButton>(form, "rdoDemodWideFm");
            var narrowFm = Field<RadioButton>(form, "rdoDemodNarrowFm");
            automatic.Checked = true;
            var start = Field<NumericUpDown>(form, "_numStart");
            var stop = Field<NumericUpDown>(form, "_numStop");
            var step = Field<NumericUpDown>(form, "_numStep");
            var preset = Field<ComboBox>(form, "_cmbPreset");
            preset.SelectedIndex = 1;
            am.Checked = true;
            Call(form, "ToggleScan");
            var scanner = Field<SignalScanner>(form, "_scanner");
            var range = FindCard(form, "scan-scan-range");
            Check(scanner.State == ScanState.Scanning && range.Active && start.Enabled && stop.Enabled && step.Enabled && preset.Enabled,
                "The scan range must remain active and editable while scanning.");
            Check(scanner.IsStartupSettling && Field<Label>(form, "_lblScanStatus").Text.Contains("Settling"),
                "The scan status must indicate the initial radio settling period.");
            var clock = Field<System.Diagnostics.Stopwatch>(scanner, "_clock");
            SetField(scanner, "_startupStartedMs", clock.ElapsedMilliseconds - 1000);
            scanner.Tick();
            SetField(pipeline, "_buffersProcessed", pipeline.BuffersProcessed + 2);
            scanner.Tick();
            Check(!scanner.IsStartupSettling && Field<Label>(form, "_lblScanStatus").Text.StartsWith("Scanning"),
                "The scan status must update when initial settling finishes.");
            Check(pipeline.Demodulator.Mode == DemodulationMode.WideFm,
                "Starting a scan must apply the selected preset's mode.");
            start.Value = 90m;
            Check(scanner.State == ScanState.Scanning && scanner.ChannelHz[0] == 90_000_000,
                "A valid range edit must immediately rebuild the active scan plan.");
            stop.Value = 89m;
            Check(scanner.State == ScanState.Scanning && scanner.ChannelHz[0] == 90_000_000
                && Field<Label>(form, "_lblScanStatus").Text.Contains("Range not applied"),
                "An invalid range must leave the previous scan running and show why it was not applied.");
            stop.Value = 92m;
            Check(scanner.ChannelHz[^1] == 92_000_000
                && !Field<Label>(form, "_lblScanStatus").Text.Contains("Range not applied"),
                "Correcting the range must apply it and clear the warning.");
            step.Value = 100m;
            Check(scanner.ChannelHz[1] - scanner.ChannelHz[0] == 100_000,
                "A step edit must rebuild the active scan grid.");
            Call(scanner, "Lock", 0, 15d);
            Check(scanner.State == ScanState.Locked && range.Active && start.Enabled,
                "The scan range must remain editable while locked on a signal.");
            step.Value = 200m;
            Check(scanner.State == ScanState.Scanning && scanner.ChannelHz[1] - scanner.ChannelHz[0] == 200_000,
                "Editing the range while locked must resume scanning with the new plan.");
            preset.SelectedIndex = 3;
            Check(scanner.ChannelHz[0] == 162_400_000 && scanner.ChannelHz.Count == 7
                && pipeline.Demodulator.Mode == DemodulationMode.NarrowFm,
                "A preset change must rebuild the active scan plan and apply its mode.");
            automatic.Checked = false;
            wideFm.Checked = true;
            preset.SelectedIndex = 2;
            Check(pipeline.Demodulator.Mode == DemodulationMode.WideFm,
                "Changing presets while scanning must preserve manual mode when auto is off.");
            automatic.Checked = true;
            Check(pipeline.Demodulator.Mode == DemodulationMode.Am,
                "Enabling auto while scanning must apply the active scan's mode immediately.");
            Call(scanner, "Lock", 0, 15d);
            automatic.Checked = false;
            narrowFm.Checked = true;
            automatic.Checked = true;
            Check(pipeline.Demodulator.Mode == DemodulationMode.Am,
                "Enabling auto on a locked signal must apply that signal's mode.");
            preset.SelectedIndex = 3;
            Field<ComboBox>(form, "_cmbMethod").SelectedIndex = 1;
            Check(scanner.State == ScanState.Scanning && scanner.ChannelHz.Count == 7,
                "Changing scan method must keep scanning with the new plan.");
            preset.SelectedIndex = 6;
            Check(scanner.State == ScanState.Scanning && scanner.ChannelHz.Count == 40
                && scanner.ChannelHz[0] == 26_965_000,
                "The CB preset must apply its channel plan during scanning.");
            Call(form, "StopScan");
            Check(scanner.State == ScanState.Idle, "Stopping must end scanning after a range change.");
        }
        finally
        {
            foreach (string file in Directory.GetFiles(directory)) File.Delete(file);
            Directory.Delete(directory);
        }
    }

    private static void WaitFor(Func<bool> condition, string message)
    {
        var timeout = System.Diagnostics.Stopwatch.StartNew();
        while (!condition() && timeout.ElapsedMilliseconds < 5000)
        {
            Application.DoEvents();
            Thread.Sleep(10);
        }
        Check(condition(), message);
    }

    private static void SetField(object instance, string name, object value) =>
        instance.GetType().GetField(name, PrivateInstance)!.SetValue(instance, value);

    private static void CheckFrequencyWheel()
    {
        Point originalCursor = Cursor.Position;
        using var form = new Form { Size = new Size(700, 300), TopMost = true, ShowInTaskbar = false };
        using var selector = new FrequencySelector { Location = new Point(10, 10) };
        var other = new TextBox { Location = new Point(10, 180) };
        form.Controls.Add(selector);
        form.Controls.Add(other);
        try
        {
            form.Show();
            form.Activate();
            Application.DoEvents();
            other.Focus();
            var digits = Field<TextBox[]>(selector, "digitBoxes");
            foreach (var label in Field<Label[]>(selector, "unitLabels"))
                Check(label.Width >= label.GetPreferredSize(Size.Empty).Width,
                    $"The {label.Text} unit label must not be clipped.");
            int changes = 0;
            selector.ValueChanged += (_, _) => changes++;

            for (int i = 0; i < digits.Length; i++)
            {
                selector.Value = 1000m;
                decimal step = 1000m;
                for (int j = 0; j < i; j++) step /= 10m;
                Cursor.Position = digits[i].PointToScreen(new Point(digits[i].Width / 2, digits[i].Height / 2));
                Check(Wheel(other, 120), $"Wheel up should be consumed over digit {i}.");
                Check(selector.Value == 1000m + step, $"Wheel up should adjust digit {i}'s place value.");
                Check(Wheel(other, -120), $"Wheel down should be consumed over digit {i}.");
                Check(selector.Value == 1000m, $"Wheel down should restore digit {i}'s place value.");
            }
            Check(other.Focused, "Wheel tuning must not steal keyboard focus.");

            Cursor.Position = digits[9].PointToScreen(new Point(digits[9].Width / 2, digits[9].Height / 2));
            selector.Value = 1000m;
            Check(PostMessage(other.Handle, 0x020A, (IntPtr)(120 << 16), IntPtr.Zero),
                "Posting a wheel message to the focused control should succeed.");
            Application.DoEvents();
            Check(selector.Value == 1000.000001m && other.Focused,
                "The WinForms message loop must route wheel input to the hovered digit.");
            selector.Value = 1000m;
            var upButtons = Field<Button[]>(selector, "upButtons");
            upButtons[9].PerformClick();
            Check(selector.Value == 1000.000001m, "Digit buttons must still adjust the corresponding place.");
            selector.Value = 1000m;
            int beforeChanges = changes;
            Check(Wheel(other, 60) && selector.Value == 1000m, "Partial wheel deltas should accumulate.");
            Check(Wheel(other, 60) && selector.Value == 1000.000001m && changes == beforeChanges + 1,
                "A full accumulated wheel notch should raise one value change.");
            Check(Wheel(other, -240) && selector.Value == 999.999999m,
                "Multiple negative wheel notches should apply the correct number of steps.");
            selector.Value = selector.Minimum;
            beforeChanges = changes;
            Check(Wheel(other, -120) && selector.Value == selector.Minimum && changes == beforeChanges,
                "Wheel tuning must respect the minimum without raising a value change.");
            selector.Value = selector.Maximum;
            beforeChanges = changes;
            Check(Wheel(other, 120) && selector.Value == selector.Maximum && changes == beforeChanges,
                "Wheel tuning must respect the maximum without raising a value change.");

            Cursor.Position = other.PointToScreen(new Point(5, 5));
            Check(!Wheel(other, 120), "Wheel events outside the selector must not be consumed.");
            Cursor.Position = digits[9].PointToScreen(new Point(5, 5));
            selector.Value = 1000m;
            Check(Wheel(other, 60), "Partial wheel input over a digit should be consumed.");
            Cursor.Position = other.PointToScreen(new Point(5, 5));
            Check(!Wheel(other, 120), "Wheel input outside the selector should pass through.");
            Cursor.Position = digits[9].PointToScreen(new Point(5, 5));
            Check(Wheel(other, 60) && selector.Value == 1000m,
                "Partial wheel input should reset after the pointer leaves the selector.");
            selector.Enabled = false;
            Check(!Wheel(other, 120), "Disabled selectors must not consume wheel input.");
            selector.Enabled = true;
            Check(Wheel(other, 60) && selector.Value == 1000m,
                "Disabling the selector must clear accumulated wheel input.");
        }
        finally
        {
            Cursor.Position = originalCursor;
        }
    }

    private static bool Wheel(Control target, int delta)
    {
        var message = Message.Create(target.Handle, 0x020A, (IntPtr)((long)(ushort)(short)delta << 16), IntPtr.Zero);
        return Application.FilterMessage(ref message);
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool PostMessage(IntPtr hWnd, int message, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindow(string? className, string windowName);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    private static Form1 CreateForm(string directory)
    {
        var ctor = typeof(Form1).GetConstructor(PrivateInstance, null, new[] { typeof(string) }, null)!;
        var form = (Form1)ctor.Invoke(new object[] { directory });
        form.ShowInTaskbar = false;
        form.Show();
        Application.DoEvents();
        return form;
    }

    private static CardGroupBox FindCard(Form1 form, string name) =>
        Field<Panel>(form, "_workspace").Controls.OfType<CardGroupBox>().Single(c => c.Name == name);

    private static T Field<T>(object instance, string name) =>
        (T)instance.GetType().GetField(name, PrivateInstance)!.GetValue(instance)!;

    private static void Mouse(Control control, string method, Point point) =>
        control.GetType().GetMethod(method, PrivateInstance)!.Invoke(control,
            new object[] { new MouseEventArgs(MouseButtons.Left, 1, point.X, point.Y, 0) });

    private static void Call(object instance, string method, params object[] args) =>
        instance.GetType().GetMethod(method, PrivateInstance)!.Invoke(instance, args);

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void ExpectInvalidLayout(object store, MethodInfo load, string message)
    {
        try { load.Invoke(store, null); }
        catch (TargetInvocationException ex) when (ex.InnerException is JsonException) { return; }
        throw new InvalidOperationException(message);
    }
}
