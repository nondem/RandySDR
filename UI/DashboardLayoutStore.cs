using System.Text.Json;

namespace RandySDR
{
    internal sealed class DashboardLayoutStore
    {
        private readonly string _path;
        private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

        internal DashboardLayoutStore(string directory) => _path = Path.Combine(directory, "dashboard-layout.json");
        internal bool Exists => File.Exists(_path);

        internal DashboardLayout Load()
        {
            var layout = JsonSerializer.Deserialize<DashboardLayout>(File.ReadAllText(_path), Options)
                ?? throw new JsonException("The layout is empty.");
            if (layout.Version != 1 || layout.Panels == null ||
                layout.Panels.Any(p => p == null || string.IsNullOrWhiteSpace(p.Id) ||
                    p.Width <= 0 || p.Height <= 0 || p.Width > 20000 || p.Height > 20000) ||
                layout.Panels.Select(p => p.Id).Distinct(StringComparer.Ordinal).Count() != layout.Panels.Count)
                throw new JsonException("The layout contains invalid panel data.");
            return layout;
        }

        internal void Save(DashboardLayout layout)
        {
            string directory = Path.GetDirectoryName(_path)!;
            Directory.CreateDirectory(directory);
            string temporary = Path.Combine(directory, $".dashboard-layout-{Guid.NewGuid():N}.tmp");
            try
            {
                File.WriteAllText(temporary, JsonSerializer.Serialize(layout, Options));
                File.Move(temporary, _path, true);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }
    }

    internal sealed class DashboardLayout
    {
        public int Version { get; set; } = 1;
        public string Name { get; set; } = "Custom 1";
        public int ViewportWidth { get; set; }
        public int ViewportHeight { get; set; }
        public List<DashboardPanelLayout> Panels { get; set; } = new();
    }

    internal sealed class DashboardPanelLayout
    {
        public string Id { get; set; } = "";
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public bool Visible { get; set; } = true;
        public bool Collapsed { get; set; }
        public int Z { get; set; }
    }
}
