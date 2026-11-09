namespace RandySDR
{
    public partial class Form1
    {
        private bool _dashboardReady;
        private bool _dashboardLocked;
        private bool _changingDashboard;
        private bool _dashboardSnap = true;
        private ComboBox _layoutChoice = null!;
        private Button _lockLayoutButton = null!;
        private ToolStripDropDownButton _panelMenu = null!;
        private DashboardLayout _defaultDashboard = null!;
        private DashboardLayout? _pendingDashboard;
        private readonly Dictionary<CardGroupBox, bool> _panelVisibility = new();
        private readonly Dictionary<CardGroupBox, List<DashboardResizeHandle>> _dashboardHandles = new();
        private readonly Dictionary<CardGroupBox, Size> _dashboardMinimum = new();
        private CardGroupBox? _dashboardDrag;
        private Point _dashboardDragStart;
        private Point _dashboardCardStart;
        private bool _dashboardMoved;
        private const int DashboardGrid = 10;

        private FlowLayoutPanel CreateWorkspaceToolbar()
        {
            var toolbar = new FlowLayoutPanel
            {
                Dock = DockStyle.Top, AutoSize = true, WrapContents = true, Padding = new Padding(6)
            };
            var menu = new ToolStrip { Dock = DockStyle.None, AutoSize = true, GripStyle = ToolStripGripStyle.Hidden };
            _panelMenu = new ToolStripDropDownButton("Panels");
            menu.Items.Add(_panelMenu);
            _layoutChoice = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 120 };
            _layoutChoice.Items.AddRange(new object[] { "Default", "Compact", "Custom 1" });
            _layoutChoice.SelectedIndex = 0;
            _layoutChoice.SelectedIndexChanged += (_, _) => SelectDashboardLayout();
            var save = new Button { Text = "Save Layout", AutoSize = true };
            save.Click += (_, _) => SaveDashboard();
            var reset = new Button { Text = "Reset to Default", AutoSize = true };
            reset.Click += (_, _) => SelectDashboardDefault();
            _lockLayoutButton = new Button { Text = "Lock Layout", AutoSize = true };
            _lockLayoutButton.Click += (_, _) => SetDashboardLocked(!_dashboardLocked);
            var snap = new CheckBox { Text = "Snap to grid", AutoSize = true, Checked = true, Margin = new Padding(8) };
            snap.CheckedChanged += (_, _) => _dashboardSnap = snap.Checked;
            toolbar.Controls.AddRange(new Control[] { menu, _layoutChoice, save, reset, _lockLayoutButton, snap });
            return toolbar;
        }

        private void InitializeDashboard()
        {
            _defaultDashboard = CreateBuiltInDashboard(false);
            foreach (var card in _defaultCards)
            {
                _panelVisibility.Add(card, true);
                _dashboardMinimum.Add(card, new Size(240, card.HeaderHeight + 70));
                WrapDashboardContent(card);
                AddDashboardHandles(card);
                RegisterDashboardFocus(card, card);
                card.MouseDown += (_, e) => BeginDashboardDrag(card, e);
                card.MouseMove += (_, e) => MoveDashboardDrag(card, e);
                card.MouseUp += (_, e) => EndDashboardDrag(card, e);
                card.MouseCaptureChanged += (_, _) =>
                {
                    if (_dashboardDrag == card && !card.Capture) _dashboardDrag = null;
                };
                var item = new ToolStripMenuItem(card.Text) { CheckOnClick = true, Checked = true, Tag = card };
                item.CheckedChanged += (_, _) =>
                {
                    if (_changingDashboard) return;
                    _panelVisibility[card] = item.Checked;
                    card.Visible = item.Checked;
                    if (item.Checked) FocusDashboardCard(card);
                    UpdateDashboardExtent();
                };
                _panelMenu.DropDownItems.Add(item);
            }
            _dashboardReady = true;
            var store = new DashboardLayoutStore(LayoutDirectory);
            if (store.Exists)
            {
                try
                {
                    _pendingDashboard = store.Load();
                    SetDashboardChoice(2);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
                {
                    MessageBox.Show(this, $"Could not load saved dashboard: {ex.Message}\nThe previous layout will be used.",
                        "Dashboard layout", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            UpdateDashboardExtent();
        }

        private void WrapDashboardContent(CardGroupBox card)
        {
            Control[] children = card.Controls.Cast<Control>().ToArray();
            int minimumWidth = card == groupBox2
                ? 360
                : Math.Max(360, card.ClientSize.Width - card.Padding.Horizontal);
            int minimumHeight = Math.Max(60, _minimumCardHeights[card] - card.HeaderHeight - card.Padding.Vertical);
            foreach (var child in children.Where(c => c.Dock == DockStyle.None))
            {
                minimumWidth = Math.Max(minimumWidth, child.Right + child.Margin.Right);
                minimumHeight = Math.Max(minimumHeight, child.Bottom + child.Margin.Bottom);
            }
            var viewport = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Margin = Padding.Empty };
            var content = new Panel { Location = Point.Empty, BackColor = card.BackColor };
            card.Controls.Clear();
            foreach (var child in children)
            {
                if (card == _cardRange || card == _cardDetect || card == _cardBehave || card == _cardControl)
                    child.Enabled = true;
                content.Controls.Add(child);
            }
            card.BackColorChanged += (_, _) => content.BackColor = card.BackColor;
            viewport.Controls.Add(content);
            card.Controls.Add(viewport);
            void ResizeContent()
            {
                content.Size = new Size(Math.Max(minimumWidth, viewport.ClientSize.Width),
                    Math.Max(minimumHeight, viewport.ClientSize.Height));
            }
            viewport.ClientSizeChanged += (_, _) => ResizeContent();
            ResizeContent();
        }

        private void AddDashboardHandles(CardGroupBox card)
        {
            var handles = new List<DashboardResizeHandle>();
            foreach (var (horizontal, vertical) in new[] { (true, false), (false, true), (true, true) })
            {
                var handle = new DashboardResizeHandle(horizontal, vertical);
                card.Controls.Add(handle);
                handles.Add(handle);
                Size original = Size.Empty;
                handle.MouseDown += (_, _) => { original = card.Size; FocusDashboardCard(card); };
                handle.ResizeRequested += (size, _, cancelled) =>
                {
                    if (_dashboardLocked || card.Collapsed) return;
                    Size min = _dashboardMinimum[card];
                    int width = cancelled || !horizontal ? original.Width : Math.Clamp(SnapDashboard(size.Width), min.Width, 4000);
                    int height = cancelled || !vertical ? original.Height : Math.Clamp(SnapDashboard(size.Height), min.Height, 4000);
                    card.Size = new Size(width, height);
                    _cardHeights[card] = height;
                    UpdateDashboardExtent();
                };
            }
            _dashboardHandles.Add(card, handles);
            card.SizeChanged += (_, _) => PositionDashboardHandles(card);
            PositionDashboardHandles(card);
        }

        private void PositionDashboardHandles(CardGroupBox card)
        {
            if (!_dashboardHandles.TryGetValue(card, out var h)) return;
            h[0].Bounds = new Rectangle(Math.Max(0, card.Width - 6), card.HeaderHeight, 6, Math.Max(0, card.Height - card.HeaderHeight - 14));
            h[1].Bounds = new Rectangle(8, Math.Max(0, card.Height - 6), Math.Max(0, card.Width - 22), 6);
            h[2].Bounds = new Rectangle(Math.Max(0, card.Width - 14), Math.Max(0, card.Height - 14), 14, 14);
            foreach (var handle in h)
            {
                handle.Visible = !_dashboardLocked && !card.Collapsed;
                handle.BringToFront();
            }
        }

        private void RegisterDashboardFocus(Control control, CardGroupBox card)
        {
            control.MouseDown += (_, _) => FocusDashboardCard(card);
            control.Enter += (_, _) => FocusDashboardCard(card);
            foreach (Control child in control.Controls) RegisterDashboardFocus(child, card);
        }

        private void FocusDashboardCard(CardGroupBox card)
        {
            foreach (var other in _defaultCards) other.WorkspaceFocused = other == card;
            card.BringToFront();
        }

        private void BeginDashboardDrag(CardGroupBox card, MouseEventArgs e)
        {
            if (_dashboardLocked || e.Button != MouseButtons.Left || e.Y > card.HeaderHeight) return;
            _dashboardDrag = card;
            _dashboardDragStart = card.PointToScreen(e.Location);
            _dashboardCardStart = card.Location;
            _dashboardMoved = false;
            card.Capture = true;
        }

        private void MoveDashboardDrag(CardGroupBox card, MouseEventArgs e)
        {
            if (_dashboardDrag != card || e.Button != MouseButtons.Left) return;
            Point current = card.PointToScreen(e.Location);
            int dx = current.X - _dashboardDragStart.X, dy = current.Y - _dashboardDragStart.Y;
            if (!_dashboardMoved && Math.Abs(dx) < SystemInformation.DragSize.Width && Math.Abs(dy) < SystemInformation.DragSize.Height) return;
            _dashboardMoved = true;
            card.CancelHeaderClick();
            card.Location = new Point(Math.Max(0, SnapDashboard(_dashboardCardStart.X + dx)),
                Math.Max(0, SnapDashboard(_dashboardCardStart.Y + dy)));
            UpdateDashboardExtent();
        }

        private void EndDashboardDrag(CardGroupBox card, MouseEventArgs e)
        {
            if (_dashboardDrag != card || e.Button != MouseButtons.Left) return;
            _dashboardDrag = null;
            card.Capture = false;
        }

        private int SnapDashboard(int value) => _dashboardSnap
            ? (int)Math.Round(value / (double)DashboardGrid, MidpointRounding.AwayFromZero) * DashboardGrid : value;

        private void SetDashboardLocked(bool locked)
        {
            _dashboardLocked = locked;
            _lockLayoutButton.Text = locked ? "Unlock Layout" : "Lock Layout";
            foreach (var card in _defaultCards) PositionDashboardHandles(card);
        }

        private void DashboardCollapsed(CardGroupBox card)
        {
            card.Height = card.Collapsed ? card.HeaderHeight : _cardHeights[card];
            PositionDashboardHandles(card);
            UpdateDashboardExtent();
        }

        private void UpdateDashboardExtent()
        {
            if (!_dashboardReady) return;
            var cards = _defaultCards.Where(c => _panelVisibility[c]).ToArray();
            int right = cards.Length == 0 ? 0 : cards.Max(c => c.Right + 12);
            int bottom = cards.Length == 0 ? 0 : cards.Max(c => c.Bottom + 12);
            _workspace.Size = new Size(Math.Max(_workspaceScroll.ClientSize.Width - 12, right),
                Math.Max(_workspaceScroll.ClientSize.Height - 12, bottom));
        }

        private DashboardLayout CaptureDashboard()
        {
            return new DashboardLayout
            {
                ViewportWidth = _workspaceScroll.ClientSize.Width,
                ViewportHeight = _workspaceScroll.ClientSize.Height,
                Panels = _defaultCards.Select(card => new DashboardPanelLayout
                {
                    Id = card.Name, X = card.Left, Y = card.Top, Width = card.Width,
                    Height = _cardHeights[card], Visible = _panelVisibility[card], Collapsed = card.Collapsed,
                    Z = _workspace.Controls.GetChildIndex(card)
                }).ToList()
            };
        }

        private DashboardLayout CreateBuiltInDashboard(bool compact)
        {
            int available = Math.Max(620, _workspaceScroll.ClientSize.Width - 30);
            int columns = Math.Max(1, available / (compact ? 450 : 550));
            int width = Math.Max(420, available / columns - 10);
            var heights = new int[columns];
            var layout = new DashboardLayout { Name = compact ? "Compact" : "Default" };
            foreach (var card in _defaultCards)
            {
                int column = Array.IndexOf(heights, heights.Min());
                int height = compact ? Math.Max(card.HeaderHeight + 90, Math.Min(_minimumCardHeights[card], 200))
                    : _minimumCardHeights[card];
                layout.Panels.Add(new DashboardPanelLayout
                {
                    Id = card.Name, X = column * (width + 10), Y = heights[column],
                    Width = width, Height = height, Z = layout.Panels.Count
                });
                heights[column] += height + 10;
            }
            return layout;
        }

        private void ApplyDashboard(DashboardLayout layout)
        {
            _changingDashboard = true;
            try
            {
                var entries = _defaultDashboard.Panels.ToDictionary(p => p.Id, StringComparer.Ordinal);
                foreach (var panel in layout.Panels) entries[panel.Id] = panel;
                foreach (var card in _defaultCards)
                {
                    if (!entries.TryGetValue(card.Name, out var state)) continue;
                    Size min = _dashboardMinimum[card];
                    int width = Math.Clamp(state.Width, min.Width, 4000);
                    int height = Math.Clamp(state.Height, min.Height, 4000);
                    int x = Math.Clamp(state.X, 0, 20000);
                    int y = Math.Clamp(state.Y, 0, 20000);
                    if (layout.ViewportWidth > _workspaceScroll.ClientSize.Width && x < layout.ViewportWidth
                        && x >= _workspaceScroll.ClientSize.Width)
                        x = Math.Max(0, _workspaceScroll.ClientSize.Width - Math.Min(width, 120));
                    if (layout.ViewportHeight > _workspaceScroll.ClientSize.Height && y < layout.ViewportHeight
                        && y >= _workspaceScroll.ClientSize.Height)
                        y = Math.Max(0, _workspaceScroll.ClientSize.Height - card.HeaderHeight);
                    card.Location = new Point(x, y);
                    _cardHeights[card] = height;
                    card.SetCollapsed(state.Collapsed);
                    card.Size = new Size(width, card.Collapsed ? card.HeaderHeight : height);
                    _panelVisibility[card] = state.Visible;
                    card.Visible = state.Visible;
                    var item = _panelMenu.DropDownItems.OfType<ToolStripMenuItem>().Single(i => i.Tag == card);
                    item.Checked = state.Visible;
                    PositionDashboardHandles(card);
                }
                foreach (var state in layout.Panels.OrderByDescending(p => p.Z))
                {
                    var card = _defaultCards.Find(c => c.Name == state.Id);
                    card?.BringToFront();
                }
            }
            finally { _changingDashboard = false; }
            UpdateDashboardExtent();
        }

        private void SetDashboardChoice(int index)
        {
            _changingDashboard = true;
            try { _layoutChoice.SelectedIndex = index; }
            finally { _changingDashboard = false; }
        }

        private void SelectDashboardDefault()
        {
            if (!_dashboardReady) return;
            ApplyDashboard(_defaultDashboard);
            SetDashboardChoice(0);
        }

        private void RestorePendingDashboard()
        {
            if (_pendingDashboard == null) return;
            var layout = _pendingDashboard;
            _pendingDashboard = null;
            ApplyDashboard(layout);
        }

        private void SelectDashboardLayout()
        {
            if (!_dashboardReady || _changingDashboard) return;
            if (_layoutChoice.SelectedIndex == 0) ApplyDashboard(_defaultDashboard);
            else if (_layoutChoice.SelectedIndex == 1) ApplyDashboard(CreateBuiltInDashboard(true));
            else
            {
                try { ApplyDashboard(new DashboardLayoutStore(LayoutDirectory).Load()); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
                {
                    MessageBox.Show(this, $"Could not load Custom 1: {ex.Message}", "Dashboard layout",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    ApplyDashboard(_defaultDashboard);
                    SetDashboardChoice(0);
                }
            }
        }

        private void SaveDashboard()
        {
            if (!_dashboardReady) return;
            try
            {
                new DashboardLayoutStore(LayoutDirectory).Save(CaptureDashboard());
                SetDashboardChoice(2);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
            {
                MessageBox.Show(this, $"Could not save dashboard: {ex.Message}", "Dashboard layout",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}
