using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using RandySDR;

internal static class Program
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    [STAThread]
    private static void Main()
    {
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
                var scanViewport = scan.Controls.OfType<Panel>().Single();
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
