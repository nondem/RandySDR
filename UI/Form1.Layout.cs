using System.Text.Json;

namespace RandySDR
{
    public partial class Form1
    {
        private Panel _workspaceScroll = null!;
        private Panel _workspace = null!;
        private readonly Dictionary<CardGroupBox, int> _cardHeights = new();
        private readonly Dictionary<CardGroupBox, int> _minimumCardHeights = new();
        private readonly Dictionary<CardGroupBox, int> _cardSpans = new();
        private readonly Dictionary<CardGroupBox, CardResizeGrip> _resizeGrips = new();
        private readonly HashSet<CardGroupBox> _wideCards = new();
        private readonly List<CardGroupBox> _defaultCards = new();
        private CardGroupBox? _dropTarget;
        private CardGroupBox? _draggedCard;
        private Label _dragHint = null!;
        private readonly Panel[] _dropOutline = new Panel[4];
        private readonly System.Windows.Forms.Timer _dragScrollTimer = new() { Interval = 100 };
        private Point? _lastDragScreenPoint;
        private Button _btnResizeMode = null!;
        private bool _resizeModeEnabled;
        private readonly string? _layoutDirectory;
        private string LayoutDirectory => _layoutDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RandySDR");
        private string CustomSizesFilePath => Path.Combine(LayoutDirectory, "panel-sizes.json");
        private const string DefaultDragHint = "Drag a card header to move it; click to collapse or expand.";

        private string LayoutFilePath => Path.Combine(LayoutDirectory, "panel-layout.json");

        private void BuildMainLayout()
        {
            SuspendLayout();
            foreach (var card in new[] { groupBox1, groupBox2, groupBox3, groupBox4, groupBoxAudio, groupBox6 })
                Controls.Remove(card);

            var toolbar = CreateWorkspaceToolbar();
            _workspaceScroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(6) };
            _workspace = new Panel
            {
                AutoSize = false,
                AllowDrop = true,
                Margin = Padding.Empty
            };
            _workspaceScroll.Controls.Add(_workspace);
            _workspaceScroll.ClientSizeChanged += (_, _) => SizeWorkspace();

            BuildRadioCards();
            lblStatus.Dock = DockStyle.Bottom;
            lblStatus.AutoSize = false;
            lblStatus.Height = Math.Max(lblStatus.Height, Font.Height + 12);
            Controls.Add(_workspaceScroll);
            Controls.Add(toolbar);
            Controls.Add(lblStatus);
            ResumeLayout(true);
        }

        private void BuildRadioCards()
        {
            foreach (var card in new[] { groupBox2, groupBox4 })
            {
                int firstTop = card.Controls.Cast<Control>().Min(c => c.Top);
                int shift = Math.Max(0, card.HeaderHeight + 2 - firstTop);
                foreach (Control control in card.Controls) control.Top += shift;
            }

            var device = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 5, RowCount = 1 };
            device.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            groupBox1.Controls.Clear();
            groupBox1.Padding = new Padding(10, 4, 10, 6);
            device.Controls.Add(cmbDevices, 0, 0);
            cmbDevices.Dock = DockStyle.Fill;
            var buttons = new[] { btnOpen, btnClose, btnStart, btnStop };
            for (int i = 0; i < buttons.Length; i++)
            {
                device.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                buttons[i].AutoSize = true;
                device.Controls.Add(buttons[i], i + 1, 0);
            }
            groupBox1.Controls.Add(device);

            label1.Location = new Point(12, groupBox2.HeaderHeight + 4);
            numFrequency.Location = new Point(12, label1.Bottom + 2);
            numFrequency.Width = Math.Max(numFrequency.MinimumSize.Width,
                numFrequency.Controls.Cast<Control>().Max(control => control.Right) + 4);
            groupBox2.Height = numFrequency.Bottom + 10;

            var gain = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
            gain.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
            gain.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
            gain.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
            var tuner = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3 };
            tuner.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            tuner.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            tuner.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            trackTunerGain.Dock = DockStyle.Fill;
            tuner.Controls.Add(chkTunerAGC, 0, 0);
            tuner.Controls.Add(trackTunerGain, 1, 0);
            tuner.Controls.Add(lblTunerGain, 2, 0);
            var input = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 5 };
            for (int i = 0; i < 4; i++) input.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            input.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            trackRxBandwidth.Dock = DockStyle.Fill;
            input.Controls.Add(chkRtlAGC, 0, 0);
            input.Controls.Add(chkOffsetTuning, 1, 0);
            input.Controls.Add(lblClip, 2, 0);
            input.Controls.Add(lblRxBandwidth, 3, 0);
            input.Controls.Add(trackRxBandwidth, 4, 0);
            var sampling = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
            sampling.Controls.AddRange(new Control[] { rdoQuadrature, rdoDirectSamplingI, rdoDirectSamplingQ });
            groupBox3.Controls.Clear();
            groupBox3.Padding = new Padding(10, 4, 10, 6);
            gain.Controls.Add(tuner, 0, 0);
            gain.Controls.Add(input, 0, 1);
            gain.Controls.Add(sampling, 0, 2);
            groupBox3.Controls.Add(gain);
            groupBox3.Height = groupBox3.HeaderHeight + 170;

            BuildAudioPanel();
            groupBoxAudio.Height = Math.Max(groupBoxAudio.Height, groupBoxAudio.HeaderHeight + 140);
            BuildSpectrumCard();

            AddWorkspaceCard(groupBox1, "device", Math.Max(groupBox1.Height, groupBox1.HeaderHeight + 50));
            AddWorkspaceCard(groupBox2, "tuning", groupBox2.Height);
            AddWorkspaceCard(groupBox4, "demodulation", Math.Max(groupBox4.Height, groupBox4.Controls.Cast<Control>().Max(c => c.Bottom) + 10));
            AddWorkspaceCard(groupBoxAudio, "audio", groupBoxAudio.Height);
            AddWorkspaceCard(groupBox3, "gain", groupBox3.Height);
            AddWorkspaceCard(groupBox6, "spectrum", Math.Max(groupBox6.Height, 350));
            AddAdvancedCard();
            _wideCards.UnionWith(new[] { groupBox2, groupBox3, groupBox6 });

        }

        private void BtnResizeMode_Click(object? sender, EventArgs e)
        {
            _resizeModeEnabled = !_resizeModeEnabled;
            _btnResizeMode.Text = _resizeModeEnabled ? "Custom resize: ON" : "Custom resize: OFF";
            _btnResizeMode.BackColor = _resizeModeEnabled ? Color.FromArgb(30, 120, 70) : Color.FromArgb(50, 70, 90);

            foreach (var pair in _resizeGrips)
            {
                pair.Value.Visible = _resizeModeEnabled && !pair.Key.Collapsed;
                pair.Value.BringToFront();
            }
            UpdateDragHint();
        }

        private void UpdateDragHint()
        {
            _dragHint.Text = _resizeModeEnabled
                ? "Drag a panel's lower-right grip to resize; widths snap to columns. Click headers to collapse."
                : DefaultDragHint;
        }

        private void AddAdvancedCard()
        {
            var card = new CardGroupBox { Text = "Advanced Tuning", Padding = new Padding(10, 4, 10, 6) };
            var rows = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
            for (int i = 0; i < 3; i++) rows.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
            var rate = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
            rate.Controls.AddRange(new Control[] { label2, numSampleRate, labelBandwidth, numTunerBandwidth });
            var ppm = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
            ppm.Controls.AddRange(new Control[] { label3, numPpm, btnPpm0, btnPpmMinus50, btnPpmPlus50, btnPpmHelp });
            var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
            actions.Controls.AddRange(new Control[] { btnDiagnostics, btnScan, chkSwapIQ, chkInvertDemod });
            rows.Controls.Add(rate, 0, 0);
            rows.Controls.Add(ppm, 0, 1);
            rows.Controls.Add(actions, 0, 2);
            card.Controls.Add(rows);
            AddWorkspaceCard(card, "advanced-tuning", card.HeaderHeight + 170);
            _wideCards.Add(card);
            HookCardCollapse(card);
        }

        private void BuildSpectrumCard()
        {
            var content = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var options = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true };
            options.Controls.AddRange(new Control[] { chkSpectrumEnabled, chkSpecAuto });
            var sliders = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 6, AutoSize = true };
            for (int i = 0; i < 3; i++)
            {
                sliders.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                sliders.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 3));
            }
            Control[] controls = { lblSpecZoom, trackSpecZoom, lblSpecRange, trackSpecRange, lblSpecRef, trackSpecRef };
            for (int i = 0; i < controls.Length; i++)
            {
                if (controls[i] is TrackBar) controls[i].Dock = DockStyle.Fill;
                sliders.Controls.Add(controls[i], i, 0);
            }
            groupBox6.Controls.Clear();
            groupBox6.Padding = new Padding(10, 4, 10, 8);
            picSpectrum.Dock = DockStyle.Fill;
            content.Controls.Add(options, 0, 0);
            content.Controls.Add(sliders, 0, 1);
            content.Controls.Add(picSpectrum, 0, 2);
            groupBox6.Controls.Add(content);
        }

        private void AddWorkspaceCard(CardGroupBox card, string id, int height)
        {
            card.Name = id;
            card.Dock = DockStyle.None;
            card.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            card.AutoSize = false;
            card.Margin = new Padding(4);
            _cardHeights.Add(card, height);
            _minimumCardHeights.Add(card, height);
            _defaultCards.Add(card);
            card.Height = height;
            _workspace.Controls.Add(card);
        }

        private void RegisterDropSurfaceTree(Control control)
        {
            control.AllowDrop = true;
            RegisterDropSurface(control);
            foreach (Control child in control.Controls) RegisterDropSurfaceTree(child);
        }

        private void RegisterDropSurface(Control control)
        {
            control.DragEnter += WorkspaceDragEnter;
            control.DragOver += WorkspaceDragOver;
            control.DragLeave += WorkspaceDragLeave;
            control.DragDrop += WorkspaceDragDrop;
        }

        private List<CardGroupBox> OrderedCards() => _workspace.Controls.OfType<CardGroupBox>().ToList();

        private (int Available, int Columns, int WideWidth) WorkspaceMetrics()
        {
            int minimum = Math.Max(450, groupBox4.Controls.Cast<Control>()
                .Where(control => control is not CardResizeGrip).Max(control => control.Right) + 16);
            int wideWidth = Math.Max(numFrequency.Width + 32, 620);
            int available = Math.Max(wideWidth + 8, _workspaceScroll.ClientSize.Width - _workspaceScroll.Padding.Horizontal
                - SystemInformation.VerticalScrollBarWidth);
            return (available, Math.Max(1, available / (minimum + 8)), wideWidth);
        }

        private List<PanelLayoutPlacement<CardGroupBox>> CalculatePlacements(IReadOnlyList<CardGroupBox> cards)
        {
            var (available, columns, wideWidth) = WorkspaceMetrics();
            var items = cards.Select(card => new PanelLayoutItem<CardGroupBox>(
                card, card.Height, card.Margin, _wideCards.Contains(card),
                _cardSpans.GetValueOrDefault(card))).ToList();
            return PanelLayoutEngine.Calculate(items, available, columns, wideWidth);
        }

        private void SizeWorkspace()
        {
            if (_workspace == null || _defaultCards.Count == 0) return;
            if (_dashboardReady)
            {
                UpdateDashboardExtent();
                return;
            }
            var (available, _, _) = WorkspaceMetrics();
            _workspace.SuspendLayout();
            _workspace.Width = available;
            var placements = CalculatePlacements(OrderedCards());
            foreach (var placement in placements)
            {
                placement.Card.Bounds = placement.Bounds;
            }
            _workspace.Height = placements.Max(p => p.Bounds.Bottom + p.Card.Margin.Bottom);
            _workspace.ResumeLayout(true);
        }

        private void DragCard(CardGroupBox card)
        {
            _draggedCard = card;
            _dragHint.Text = "Drag over a panel: gold marks before/after; blue shows the final position. Esc cancels.";
            try
            {
                card.DoDragDrop(card, DragDropEffects.Move);
            }
            finally
            {
                _draggedCard = null;
                _dragScrollTimer.Stop();
                _lastDragScreenPoint = null;
                ClearDropPreview();
            }
        }

        private void WorkspaceDragEnter(object? sender, DragEventArgs e)
        {
            e.Effect = e.Data?.GetData(typeof(CardGroupBox)) is CardGroupBox card && card == _draggedCard
                ? DragDropEffects.Move : DragDropEffects.None;
        }

        private void WorkspaceDragOver(object? sender, DragEventArgs e)
        {
            WorkspaceDragEnter(sender, e);
            if (e.Effect == DragDropEffects.Move)
            {
                _lastDragScreenPoint = new Point(e.X, e.Y);
                _dragScrollTimer.Start();
                ShowDropPreview(_workspace.PointToClient(_lastDragScreenPoint.Value));
            }
            else
                ClearDropPreview();
        }

        private void WorkspaceDragLeave(object? sender, EventArgs e)
        {
            if (!_workspaceScroll.ClientRectangle.Contains(_workspaceScroll.PointToClient(Cursor.Position)))
            {
                _dragScrollTimer.Stop();
                _lastDragScreenPoint = null;
                ClearDropPreview();
            }
        }

        private bool ScrollDuringDrag(Point screenPoint)
        {
            var point = _workspaceScroll.PointToClient(screenPoint);
            if (!_workspaceScroll.ClientRectangle.Contains(point)) return false;
            int current = -_workspaceScroll.AutoScrollPosition.Y;
            int step = Math.Max(20, Font.Height * 2);
            int next = point.Y < 35 ? Math.Max(0, current - step)
                : point.Y > _workspaceScroll.ClientSize.Height - 35 ? current + step : current;
            if (next != current)
                _workspaceScroll.AutoScrollPosition = new Point(-_workspaceScroll.AutoScrollPosition.X, next);
            return -_workspaceScroll.AutoScrollPosition.Y != current;
        }

        private void ShowDropPreview(Point point)
        {
            if (_draggedCard == null) return;
            var order = OrderedCards();
            var current = order.Select(card => new PanelLayoutPlacement<CardGroupBox>(card, card.Bounds)).ToList();
            var (target, before) = PanelLayoutEngine.FindInsertion(current, point);
            var proposed = PanelLayoutEngine.Move(order, _draggedCard, target, before);
            if (_dropTarget != null && _dropTarget != target) _dropTarget.DropBefore = null;
            _dropTarget = target;
            target.DropBefore = before;
            bool unchanged = order.SequenceEqual(proposed);
            _dragHint.Text = unchanged ? "Already in this position. Esc cancels."
                : $"Drop {(before ? "before" : "after")} {target.Text}. Blue outline shows the final position; Esc cancels.";
            if (unchanged)
            {
                foreach (var edge in _dropOutline) edge.Visible = false;
                _workspace.Height = current.Max(p => p.Bounds.Bottom + p.Card.Margin.Bottom);
                return;
            }

            var placements = CalculatePlacements(proposed);
            _workspace.Height = Math.Max(current.Max(p => p.Bounds.Bottom + p.Card.Margin.Bottom),
                placements.Max(p => p.Bounds.Bottom + p.Card.Margin.Bottom));
            var bounds = placements.First(p => p.Card == _draggedCard).Bounds;
            const int thickness = 4;
            _dropOutline[0].Bounds = new Rectangle(bounds.Left, bounds.Top, bounds.Width, thickness);
            _dropOutline[1].Bounds = new Rectangle(bounds.Left, bounds.Bottom - thickness, bounds.Width, thickness);
            _dropOutline[2].Bounds = new Rectangle(bounds.Left, bounds.Top, thickness, bounds.Height);
            _dropOutline[3].Bounds = new Rectangle(bounds.Right - thickness, bounds.Top, thickness, bounds.Height);
            foreach (var edge in _dropOutline)
            {
                edge.Visible = true;
                edge.BringToFront();
            }
        }

        private void ClearDropPreview()
        {
            if (_dropTarget != null) _dropTarget.DropBefore = null;
            _dropTarget = null;
            foreach (var edge in _dropOutline)
                if (edge != null) edge.Visible = false;
            if (_workspace != null && _defaultCards.Count > 0)
                _workspace.Height = OrderedCards().Max(card => card.Bottom + card.Margin.Bottom);
            if (_dragHint != null) UpdateDragHint();
        }

        private void WorkspaceDragDrop(object? sender, DragEventArgs e)
        {
            if (e.Data?.GetData(typeof(CardGroupBox)) is not CardGroupBox card || card != _draggedCard) return;
            var ordered = OrderedCards();
            var current = ordered.Select(c => new PanelLayoutPlacement<CardGroupBox>(c, c.Bounds)).ToList();
            var (target, before) = PanelLayoutEngine.FindInsertion(current, _workspace.PointToClient(new Point(e.X, e.Y)));
            var proposed = PanelLayoutEngine.Move(ordered, card, target, before);
            ClearDropPreview();
            if (!ordered.SequenceEqual(proposed))
            {
                ApplyPanelOrder(proposed);
                SavePanelOrder();
            }
        }

        private void ApplyPanelOrder(IReadOnlyList<CardGroupBox> cards)
        {
            _workspace.SuspendLayout();
            for (int i = 0; i < cards.Count; i++) _workspace.Controls.SetChildIndex(cards[i], i);
            _workspace.ResumeLayout(true);
            SizeWorkspace();
        }

        private void ResetPanelOrder()
        {
            ApplyPanelOrder(_defaultCards);
            SavePanelOrder();
        }

        private void LoadPanelOrder()
        {
            try
            {
                if (File.Exists(LayoutFilePath))
                {
                    var ids = JsonSerializer.Deserialize<List<string>>(File.ReadAllText(LayoutFilePath))
                        ?? throw new JsonException("The saved panel order is empty.");
                    var cards = new List<CardGroupBox>();
                    foreach (string id in ids)
                    {
                        var card = _defaultCards.Find(c => c.Name == id);
                        if (card != null && !cards.Contains(card)) cards.Add(card);
                    }
                    cards.AddRange(_defaultCards.Where(c => !cards.Contains(c)));
                    ApplyPanelOrder(cards);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                MessageBox.Show(this, $"Could not load panel order: {ex.Message}", "Panel layout",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            LoadCustomSizes();
        }

        private sealed class SavedPanelSize
        {
            public int Height { get; set; }
            public int Span { get; set; }
        }

        private void LoadCustomSizes()
        {
            try
            {
                if (!File.Exists(CustomSizesFilePath)) return;
                var content = File.ReadAllText(CustomSizesFilePath);
                Dictionary<string, SavedPanelSize> sizes;
                
                // Check for legacy format first (plain lines like "panel-name:height")
                var trimmedContent = content.TrimStart();
                if (trimmedContent.StartsWith('{'))
                {
                    // Modern JSON object format
                    sizes = JsonSerializer.Deserialize<Dictionary<string, SavedPanelSize>>(content)
                        ?? throw new JsonException("The saved panel sizes are empty.");
                }
                else if (trimmedContent.StartsWith('['))
                {
                    // Legacy array format
                    var lines = JsonSerializer.Deserialize<List<string>>(content) ?? throw new JsonException("Empty sizes.");
                    sizes = new Dictionary<string, SavedPanelSize>();
                    foreach (string line in lines)
                    {
                        var parts = line.Split(':', 2);
                        if (parts.Length != 2 || !int.TryParse(parts[1], out int height) || height <= 0)
                            throw new JsonException("Invalid legacy panel size.");
                        sizes[parts[0]] = new SavedPanelSize { Height = height * 96 / DeviceDpi };
                    }
                }
                else
                {
                    // Legacy plain lines format (panel-name:height on each line)
                    var lines = trimmedContent.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).ToList();
                    sizes = new Dictionary<string, SavedPanelSize>();
                    foreach (string line in lines)
                    {
                        var parts = line.Split(':', 2);
                        if (parts.Length != 2 || !int.TryParse(parts[1], out int height) || height <= 0)
                            throw new JsonException("Invalid legacy panel size.");
                        sizes[parts[0]] = new SavedPanelSize { Height = height * 96 / DeviceDpi };
                    }
                }
                
                foreach (var card in OrderedCards())
                {
                    if (!sizes.TryGetValue(card.Name, out var saved)) continue;
                    if (saved == null || saved.Height <= 0 || saved.Height > 4000 || saved.Span < 0 || saved.Span > 100)
                        throw new JsonException($"Invalid size for {card.Name}.");
                }
                foreach (var card in OrderedCards())
                {
                    if (!sizes.TryGetValue(card.Name, out var saved)) continue;
                    int height = Math.Max(_minimumCardHeights[card], saved.Height * DeviceDpi / 96);
                    _cardHeights[card] = height;
                    if (saved.Span > 0) _cardSpans[card] = saved.Span;
                    if (!card.Collapsed) card.Height = height;
                }
                SizeWorkspace();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                var message = ex is JsonException 
                    ? $"Could not parse custom sizes file: {ex.Message}\n\nThe file may be in an unsupported format."
                    : $"Could not load custom sizes: {ex.Message}";
                MessageBox.Show(this, message, "Layout",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void SaveCustomSizes()
        {
            try
            {
                var sizes = OrderedCards().ToDictionary(card => card.Name, card => new SavedPanelSize
                {
                    Height = Math.Max(1, _cardHeights[card] * 96 / DeviceDpi),
                    Span = _cardSpans.GetValueOrDefault(card)
                });
                Directory.CreateDirectory(Path.GetDirectoryName(CustomSizesFilePath)!);
                File.WriteAllText(CustomSizesFilePath, JsonSerializer.Serialize(sizes));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                MessageBox.Show(this, $"Could not save custom sizes: {ex.Message}", "Layout",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void SavePanelOrder()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LayoutFilePath)!);
                File.WriteAllText(LayoutFilePath, JsonSerializer.Serialize(
                    OrderedCards().Select(c => c.Name).ToArray()));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                MessageBox.Show(this, $"Could not save panel order: {ex.Message}", "Panel layout",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void FitToScreen()
        {
            var area = Screen.FromControl(this).WorkingArea;
            Width = Math.Min(Width, area.Width);
            Height = Math.Min(Height, area.Height);
            Left = area.Left + (area.Width - Width) / 2;
            Top = area.Top + (area.Height - Height) / 2;
            RestorePendingDashboard();
            SizeWorkspace();
        }
    }
}
