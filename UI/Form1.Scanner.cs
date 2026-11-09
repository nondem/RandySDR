// Form1 - signal scanner panel: UI is built in code (no designer entries) and drives SignalScanner.
namespace RandySDR
{
    public partial class Form1
    {
        private readonly ScanSettings _scanSettings = ScanSettings.Load();
        private SignalScanner? _scanner;
        private readonly System.Windows.Forms.Timer _scanTimer = new() { Interval = 25 };
        private bool _scanMuted; // true while hopping between channels so the user does not hear static
        private bool _loadingScanUi;
        private string? _scanRangeError;
        private decimal _scanOriginalMHz;

        private sealed record LogEntry(DateTime Time, double MHz, double Db, string Mode);
        private readonly List<LogEntry> _scanLog = new();

        private ComboBox _cmbPreset = null!, _cmbMethod = null!, _cmbHold = null!;
        private NumericUpDown _numStart = null!, _numStop = null!, _numStep = null!, _numHold = null!, _numSettle = null!;
        private TrackBar _trackThreshold = null!;
        private Label _lblThreshold = null!, _lblScanStatus = null!;
        private CheckBox _chkAutoMode = null!, _chkLog = null!, _chkAutoThreshold = null!;
        private NumericUpDown _numAutoTrim = null!;
        private Button _btnScanToggle = null!, _btnResume = null!, _btnLockout = null!;
        private ScanGraph _scanGraph = null!;
        private ListView _lstLog = null!;
        private ListBox _lstLockouts = null!;

        private const int CbPresetIndex = 6;
        private const int LawEnforcementVhfPresetIndex = 9;
        private const int LawEnforcementUhfPresetIndex = 10;
        private static readonly (string Name, double Start, double Stop, double Step, DemodulationMode? Mode)[] ScanPresets =
        {
            ("Custom", 0, 0, 0, null),
            ("FM broadcast 88.1-107.9 / 200 kHz", 88.1, 107.9, 200, DemodulationMode.WideFm),
            ("Airband 118-137 / 25 kHz", 118, 137, 25, DemodulationMode.Am),
            ("NOAA weather 162.400-162.550 / 25 kHz", 162.4, 162.55, 25, DemodulationMode.NarrowFm),
            ("Marine VHF 156-162.6 / 25 kHz", 156, 162.6, 25, DemodulationMode.NarrowFm),
            ("2 m ham 144-148 / 5 kHz", 144, 148, 5, DemodulationMode.NarrowFm),
            ("CB US standard 40 channels (AM)", 26.965, 27.405, 10, DemodulationMode.Am),
            ("70 cm ham 430-450 / 5 kHz", 430, 450, 5, DemodulationMode.NarrowFm),
            ("FRS/GMRS 462.550-467.725 / 12.5 kHz", 462.55, 467.725, 12.5, DemodulationMode.NarrowFm),
            (ScanSettings.LawEnforcementVhfPresetName, 136, 174, 1.25, DemodulationMode.NarrowFm),
            ("Law Enforcement UHF 450-470 MHz", 450, 470, 6.25, DemodulationMode.NarrowFm),
        };

        private void BuildScannerPanel()
        {
            _cmbPreset = NewCombo(ScanPresets.Select(p => p.Name).ToArray());
            new ToolTip().SetToolTip(_cmbPreset, "Law Enforcement VHF excludes NOAA weather frequencies (162.400-162.550 MHz) and a 50 kHz guard on each side.");
            BuildSquelchLinkControls();
            _numStart = NewNumeric(3, 0.5m, 1766m, 0.1m);
            _numStop = NewNumeric(3, 0.5m, 1766m, 0.1m);
            _numStep = NewNumeric(2, 1m, 5000m, 5m);
            new ToolTip().SetToolTip(_numStep, "CB uses its standard channel list, including gaps; other presets use a regular frequency grid.");
            _cmbMethod = NewCombo(new[] { "Wideband FFT (fast)", "Tune each channel (slow)" });
            _cmbHold = NewCombo(new[] { "Stay until signal drops", "Stay until I press Resume", "Stay for fixed time" });
            _lblThreshold = new Label { AutoSize = true, Margin = new Padding(0, 2, 0, 2) };
            _chkAutoThreshold = new CheckBox { Text = "Auto-detect threshold", AutoSize = true, Margin = new Padding(0, 0, 8, 0) };
            _numAutoTrim = NewNumeric(0, -10m, 15m, 1m);
            var thresholdTip = new ToolTip();
            thresholdTip.SetToolTip(_chkAutoThreshold, "Learns the threshold from the measured noise while scanning. Uncheck to set it manually with the slider.");
            thresholdTip.SetToolTip(_numAutoTrim, "Nudges the automatic threshold. Negative = more sensitive, positive = fewer false detections.");
            _trackThreshold = new TrackBar { Dock = DockStyle.Fill, AutoSize = false, Height = 32, Minimum = 3, Maximum = 40, TickFrequency = 3, Value = 8, Margin = new Padding(0, 0, 0, 0) };
            _numHold = NewNumeric(1, 0.5m, 120m, 0.5m);
            _numSettle = NewNumeric(0, 40m, 1000m, 10m);
            _chkAutoMode = new CheckBox { Text = "Auto-select mode by band", AutoSize = true, Margin = new Padding(0, 2, 16, 2) };
            _chkLog = new CheckBox { Text = "Log detections", AutoSize = true, Margin = new Padding(0, 2, 0, 2) };
            var checks = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, WrapContents = true, AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 2, 0, 0) };
            checks.Controls.Add(_chkAutoMode);
            checks.Controls.Add(_chkLog);

            _btnScanToggle = NewButton("Start scan");
            _btnResume = NewButton("Resume");
            _btnLockout = NewButton("Lock out signal");
            _lblScanStatus = new Label
            {
                Text = "Idle", ForeColor = Color.Silver, Dock = DockStyle.Fill, AutoSize = false,
                Font = new Font("Segoe UI Semibold", 12f), TextAlign = ContentAlignment.MiddleLeft, MinimumSize = new Size(0, 36)
            };
            _scanGraph = new ScanGraph { Dock = DockStyle.Fill, Margin = new Padding(0, 6, 0, 6), MinimumSize = new Size(300, 260) };

            _lstLog = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, GridLines = true, Margin = new Padding(0, 4, 0, 4) };
            _lstLog.Columns.Add("Time", 110);
            _lstLog.Columns.Add("MHz", 130);
            _lstLog.Columns.Add("dB", 90);
            _lstLog.Columns.Add("Mode", 200);
            var btnClearLog = NewButton("Clear log");
            var btnSaveLog = NewButton("Save CSV...");

            _lstLockouts = new ListBox { Dock = DockStyle.Fill, Margin = new Padding(0, 4, 0, 4), IntegralHeight = false };
            var btnRemoveLockout = NewButton("Remove selected");
            var btnClearLockouts = NewButton("Clear all");

            // Each logical group is its own card so it can show an active / inactive state independently.
            _cardRange = AddScanCard("Scan Range", Stack(
                FieldRow(("Band preset", _cmbPreset), ("Scan method", _cmbMethod)),
                FieldRow(("Start (MHz)", _numStart), ("Stop (MHz)", _numStop), ("Step (kHz)", _numStep))));
            _cardDetect = AddScanCard("Detection", Stack(
                OptionRow(_chkAutoThreshold, "Auto trim (dB)", _numAutoTrim),
                FieldRow((_lblThreshold, _trackThreshold)),
                OptionRow(_chkLinkSquelch, "Squelch offset (dB)", _numSquelchOffset),
                _lblSquelchLinkStatus));
            _cardBehave = AddScanCard("Behaviour", Stack(
                FieldRow(("When a signal is found", _cmbHold), ("Hold / drop time (s)", _numHold), ("Settle time (ms)", _numSettle)),
                checks));
            _cardControl = AddScanCard("Scan Control", Stack(ButtonRow(_btnScanToggle, _btnResume, _btnLockout), _lblScanStatus));
            _cardGraph = AddScanCard("Activity Graph", _scanGraph, 50);
            _cardLog = AddScanCard("Detection Log", ListSection(null, _lstLog, btnClearLog, btnSaveLog), 25);
            _cardLock = AddScanCard("Locked-out Frequencies", ListSection("Skipped by the scanner", _lstLockouts, btnRemoveLockout, btnClearLockouts), 25);
            _wideCards.UnionWith(new[] { _cardRange, _cardDetect, _cardBehave });
            HookCardCollapse(groupBox1, groupBox2, groupBox3, groupBox4, groupBoxAudio, groupBox6,
                _cardRange, _cardDetect, _cardBehave, _cardControl, _cardGraph, _cardLog, _cardLock);

            LoadScanSettingsToUi();
            if (!new DashboardLayoutStore(LayoutDirectory).Exists)
                LoadPanelOrder();
            SizeWorkspace();
            InitializeDashboard();

            _cmbPreset.SelectedIndexChanged += PresetChanged;
            foreach (var n in new[] { _numStart, _numStop, _numStep })
                n.ValueChanged += (_, _) =>
                {
                    if (!_loadingScanUi) _cmbPreset.SelectedIndex = 0;
                    ReadScanSettingsFromUi();
                };
            foreach (var n in new[] { _numHold, _numSettle })
                n.ValueChanged += (_, _) => ReadScanSettingsFromUi();
            _cmbMethod.SelectedIndexChanged += (_, _) => ReadScanSettingsFromUi();
            _cmbHold.SelectedIndexChanged += (_, _) => ReadScanSettingsFromUi();
            _trackThreshold.ValueChanged += (_, _) => ReadScanSettingsFromUi();
            _chkAutoMode.CheckedChanged += (_, _) =>
            {
                if (_loadingScanUi) return;
                ReadScanSettingsFromUi();
                if (_chkAutoMode.Checked && _scanner is { State: not ScanState.Idle } scanner)
                    SelectModeForBand((scanner.State == ScanState.Locked ? scanner.LockedHz : scanner.ChannelHz[0]) / 1e6);
            };
            _chkLog.CheckedChanged += (_, _) => ReadScanSettingsFromUi();
            _chkAutoThreshold.CheckedChanged += (_, _) => ReadScanSettingsFromUi();
            _numAutoTrim.ValueChanged += (_, _) => ReadScanSettingsFromUi();

            _btnScanToggle.Click += (_, _) => ToggleScan();
            _btnResume.Click += (_, _) => _scanner?.Resume();
            _btnLockout.Click += (_, _) => LockOutCurrent();
            btnClearLog.Click += (_, _) => { _scanLog.Clear(); _lstLog.Items.Clear(); UpdateCardStates(); };
            btnSaveLog.Click += (_, _) => SaveScanLog();
            btnRemoveLockout.Click += (_, _) => RemoveSelectedLockout();
            btnClearLockouts.Click += (_, _) => { _scanSettings.LockoutsMHz.Clear(); RefreshLockoutList(); };

            // Optimize: Reduce timer interval from 25ms to 20ms for better responsiveness
            // while maintaining performance
            _scanTimer.Interval = 20;
            _scanTimer.Tick += (_, _) => _scanner?.Tick();
            UpdateScanUi();
        }

        private CardGroupBox _cardRange = null!, _cardDetect = null!, _cardBehave = null!, _cardControl = null!,
            _cardGraph = null!, _cardLog = null!, _cardLock = null!;

        private CardGroupBox AddScanCard(string title, Control content, float weight = 0)
        {
            var card = new CardGroupBox
            {
                Text = title,
                Padding = new Padding(12, 8, 12, 10),
            };
            content.Dock = DockStyle.Fill;
            card.Controls.Add(content);
            int height = weight > 0
                ? (title == "Activity Graph" ? 320 : 220)
                : content.GetPreferredSize(new Size(Math.Max(620, numFrequency.MinimumSize.Width), 0)).Height + card.HeaderHeight + card.Padding.Vertical + 12;
            AddWorkspaceCard(card, "scan-" + title.ToLowerInvariant().Replace(' ', '-'), height);
            return card;
        }

        /// <summary>Vertical stack of rows that sizes to its content.</summary>
        private static TableLayoutPanel Stack(params Control[] rows)
        {
            var t = new TableLayoutPanel { AutoSize = true, ColumnCount = 1, RowCount = rows.Length, Margin = Padding.Empty };
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            foreach (var r in rows)
            {
                t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                r.Dock = DockStyle.Fill;
                t.Controls.Add(r);
            }
            return t;
        }

        private static ComboBox NewCombo(string[] items)
        {
            var c = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, Margin = new Padding(0, 0, 10, 0) };
            c.Items.AddRange(items);
            return c;
        }

        private static NumericUpDown NewNumeric(int decimals, decimal min, decimal max, decimal inc) =>
            new() { Dock = DockStyle.Fill, DecimalPlaces = decimals, Minimum = min, Maximum = max, Increment = inc, Margin = new Padding(0, 0, 10, 0) };

        private static Button NewButton(string text) =>
            new() { Text = text, Dock = DockStyle.Fill, MinimumSize = new Size(0, 36), Margin = new Padding(0, 2, 8, 2) };

        /// <summary>One row of equally wide columns, each a caption above its control.</summary>
        private static TableLayoutPanel FieldRow(params (object? Caption, Control Control)[] fields)
        {
            var t = new TableLayoutPanel { AutoSize = true, ColumnCount = fields.Length, RowCount = 2, Margin = new Padding(0, 0, 0, 4) };
            t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            for (int i = 0; i < fields.Length; i++)
            {
                t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / fields.Length));
                var caption = fields[i].Caption switch
                {
                    Label l => l,
                    string s => new Label { Text = s, AutoSize = true, Margin = new Padding(0, 2, 0, 2) },
                    _ => null
                };
                if (caption != null) t.Controls.Add(caption, i, 0);
                fields[i].Control.Dock = DockStyle.Fill;
                t.Controls.Add(fields[i].Control, i, 1);
            }
            return t;
        }

        private static TableLayoutPanel OptionRow(CheckBox option, string caption, NumericUpDown value)
        {
            var row = new TableLayoutPanel { AutoSize = true, ColumnCount = 3, RowCount = 1, Margin = new Padding(0, 2, 0, 4) };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
            option.Anchor = AnchorStyles.Left;
            option.Dock = DockStyle.None;
            var label = new Label { Text = caption, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 0, 8, 0) };
            value.Margin = Padding.Empty;
            value.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            value.Dock = DockStyle.None;
            row.Controls.Add(option, 0, 0);
            row.Controls.Add(label, 1, 0);
            row.Controls.Add(value, 2, 0);
            return row;
        }

        private static TableLayoutPanel ButtonRow(params Button[] buttons)
        {
            var t = new TableLayoutPanel { AutoSize = true, ColumnCount = buttons.Length, RowCount = 1, Margin = new Padding(0, 2, 0, 2) };
            t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            for (int i = 0; i < buttons.Length; i++)
            {
                t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / buttons.Length));
                t.Controls.Add(buttons[i], i, 0);
            }
            return t;
        }

        /// <summary>Caption, a stretching list, and a row of buttons underneath.</summary>
        private static TableLayoutPanel ListSection(string? caption, Control list, params Button[] buttons)
        {
            var t = new TableLayoutPanel { ColumnCount = 1, RowCount = 3, Margin = new Padding(0, 6, 0, 0) };
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            t.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            t.Controls.Add(new Label { Text = caption ?? "", AutoSize = caption != null, Height = 0, Font = new Font("Segoe UI", 9f) }, 0, 0);
            t.Controls.Add(list, 0, 1);
            var row = ButtonRow(buttons);
            row.Dock = DockStyle.Fill;
            foreach (var b in buttons) b.MinimumSize = new Size(0, 36);
            t.Controls.Add(row, 0, 2);
            return t;
        }

        private void LoadScanSettingsToUi()
        {
            _loadingScanUi = true;
            var s = _scanSettings;
            _numStart.Value = Math.Clamp((decimal)s.StartMHz, _numStart.Minimum, _numStart.Maximum);
            _numStop.Value = Math.Clamp((decimal)s.StopMHz, _numStop.Minimum, _numStop.Maximum);
            _numStep.Value = Math.Clamp((decimal)s.StepKHz, _numStep.Minimum, _numStep.Maximum);
            _cmbMethod.SelectedIndex = (int)s.Method;
            _cmbHold.SelectedIndex = (int)s.Hold;
            _trackThreshold.Value = Math.Clamp(s.ThresholdDb, _trackThreshold.Minimum, _trackThreshold.Maximum);
            _numHold.Value = Math.Clamp((decimal)s.HoldSeconds, _numHold.Minimum, _numHold.Maximum);
            _numSettle.Value = Math.Clamp(s.SettleMs, (int)_numSettle.Minimum, (int)_numSettle.Maximum);
            _chkAutoMode.Checked = s.AutoSelectMode;
            _chkLog.Checked = s.LogDetections;
            _chkAutoThreshold.Checked = s.AutoThreshold;
            _numAutoTrim.Value = Math.Clamp(s.AutoTrimDb, (int)_numAutoTrim.Minimum, (int)_numAutoTrim.Maximum);
            _numSquelchOffset.Value = Math.Clamp(s.SquelchOffsetDb, (int)_numSquelchOffset.Minimum, (int)_numSquelchOffset.Maximum);
            _chkLinkSquelch.Checked = s.LinkSquelch;
            int presetIndex = Array.FindIndex(ScanPresets, p => p.Name == s.PresetName
                && p.Start == s.StartMHz && p.Stop == s.StopMHz && p.Step == s.StepKHz);
            _cmbPreset.SelectedIndex = s.UseCbChannels ? CbPresetIndex : Math.Max(0, presetIndex);
            _loadingScanUi = false;
            RefreshLockoutList();
            RefreshThresholdLabel();
        }

        private void ReadScanSettingsFromUi()
        {
            if (_loadingScanUi) return;
            var s = _scanSettings;
            double start = (double)_numStart.Value;
            double stop = (double)_numStop.Value;
            double step = (double)_numStep.Value;
            bool useCb = _cmbPreset.SelectedIndex == CbPresetIndex;
            string? preset = _cmbPreset.SelectedIndex > 0
                ? ScanPresets[_cmbPreset.SelectedIndex].Name : null;
            var method = (ScanMethod)Math.Max(0, _cmbMethod.SelectedIndex);
            bool running = _scanner is { State: not ScanState.Idle };
            bool changed = s.StartMHz != start || s.StopMHz != stop || s.StepKHz != step
                || s.UseCbChannels != useCb || s.PresetName != preset || s.Method != method;
            _scanRangeError = running && (stop <= start || step <= 0)
                ? "Range not applied: stop must be above start."
                : null;
            if (_scanRangeError == null)
            {
                s.StartMHz = start;
                s.StopMHz = stop;
                s.StepKHz = step;
                s.UseCbChannels = useCb;
                s.PresetName = preset;
                s.Method = method;
            }
            s.Hold = (HoldMode)Math.Max(0, _cmbHold.SelectedIndex);
            // The manual value is only written when auto is off, so auto mode never overwrites it.
            if (!_chkAutoThreshold.Checked) s.ThresholdDb = _trackThreshold.Value;
            s.AutoThreshold = _chkAutoThreshold.Checked;
            s.AutoTrimDb = (int)_numAutoTrim.Value;
            s.HoldSeconds = (double)_numHold.Value;
            s.SettleMs = (int)_numSettle.Value;
            s.AutoSelectMode = _chkAutoMode.Checked;
            s.LogDetections = _chkLog.Checked;
            s.LinkSquelch = _chkLinkSquelch.Checked;
            s.SquelchOffsetDb = (int)_numSquelchOffset.Value;
            RefreshThresholdLabel();
            _numHold.Enabled = s.Hold != HoldMode.UntilResumed;
            if (running && changed && _scanRangeError == null)
            {
                if (!_scanner!.Start(out string error))
                {
                    StopScan();
                    MessageBox.Show(error, "Scanner", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                _noiseFloorDb = float.NaN;
                ApplyLinkedSquelch();
                if (s.AutoSelectMode) SelectModeForBand(_scanner.ChannelHz[0] / 1e6);
            }
            if (running) UpdateScanUi();
        }

        private void RefreshThresholdLabel()
        {
            bool auto = _chkAutoThreshold.Checked;
            _trackThreshold.Enabled = !auto;
            _numAutoTrim.Enabled = auto;
            double eff = EffectiveThresholdDb;
            bool learned = auto && _scanner != null && _scanner.AutoActive;
            _lblThreshold.Text = !auto
                ? $"Detection threshold: {_trackThreshold.Value} dB above noise floor (manual)"
                : learned
                    ? $"Detection threshold: AUTO {eff:F1} dB (learned from the noise)"
                    : $"Detection threshold: AUTO (learning when the scan starts; {_trackThreshold.Value} dB until then)";
            _scanGraph.ThresholdDb = eff;
            ApplyLinkedSquelch();
        }

        private double EffectiveThresholdDb =>
            _chkAutoThreshold.Checked && _scanner != null && _scanner.AutoActive
                ? _scanner.EffectiveThresholdDb
                : _trackThreshold.Value;

        private void PresetChanged(object? sender, EventArgs e)
        {
            if (_loadingScanUi) return;
            int i = _cmbPreset.SelectedIndex;
            if (i <= 0)
            {
                ReadScanSettingsFromUi();
                return;
            }
            var p = ScanPresets[i];
            _loadingScanUi = true;
            _numStart.Value = (decimal)p.Start;
            _numStop.Value = (decimal)p.Stop;
            _numStep.Value = (decimal)p.Step;
            if (i == CbPresetIndex || IsLawEnforcementPreset(i))
            {
                _chkAutoMode.Checked = true;
                if (i == CbPresetIndex) rdoDemodAm.Checked = true;
                else rdoDemodNarrowFm.Checked = true;
            }
            _loadingScanUi = false;
            ReadScanSettingsFromUi();
            if (_scanRangeError == null && _scanSettings.AutoSelectMode)
                SelectModeForBand(p.Start);
        }

        private void ToggleScan()
        {
            if (_scanner != null && _scanner.State != ScanState.Idle)
            {
                StopScan();
                return;
            }

            if (_pipeline == null || !btnStop.Enabled)
            {
                MessageBox.Show("Open the device and press Start (receiving) before scanning.", "Scanner",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            ReadScanSettingsFromUi();
            _scanner ??= CreateScanner();
            _scanOriginalMHz = numFrequency.Value;

            if (!_scanner.Start(out string error))
            {
                MessageBox.Show(error, "Scanner", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (_scanSettings.AutoSelectMode) SelectModeForBand(_scanner.ChannelHz[0] / 1e6);
            _noiseFloorDb = float.NaN;
            ApplyLinkedSquelch();
            // Wideband hops span ~1.9 MHz; a narrow tuner IF filter (default 200 kHz) would attenuate all but the middle of each hop.
            _sdr?.SetTunerBandwidth(0);
            _scanTimer.Start();
            RefreshScanMute();
            UpdateScanUi();
        }

        private SignalScanner CreateScanner()
        {
            var sc = new SignalScanner(_scanSettings, hz => RetuneRadio(hz), () => _pipeline,
                () => (int)(numSampleRate.Value * 1_000_000m));
            sc.SignalLocked += OnScanLocked;
            sc.Updated += OnScanUpdated;
            sc.QuietChannelMeasured += OnQuietChannelMeasured;
            return sc;
        }

        private void StopScan()
        {
            if (_scanner == null || _scanner.State == ScanState.Idle)
                return;

            bool wasLocked = _scanner.State == ScanState.Locked;
            _scanner.Stop();
            _scanTimer.Stop();
            _scanRangeError = null;
            RefreshScanMute();
            _sdr?.SetTunerBandwidth((uint)numTunerBandwidth.Value * 1000);

            if (!wasLocked)
            {
                numFrequency.Value = _scanOriginalMHz;
                RetuneRadio((uint)Math.Round(_scanOriginalMHz * 1_000_000m));
            }
            UpdateScanUi();
        }

        private void OnScanLocked(double hz, double db)
        {
            decimal mhz = Math.Round((decimal)hz / 1_000_000m, 6);
            if (_scanSettings.AutoSelectMode)
                SelectModeForBand((double)mhz);

            numFrequency.Value = Math.Clamp(mhz, numFrequency.Minimum, numFrequency.Maximum);
            RetuneRadio((uint)Math.Round(hz));
            RefreshScanMute();

            lblStatus.Text = $"Scanner locked: {mhz:0.0000} MHz (+{db:F0} dB)";
            lblStatus.ForeColor = Color.Green;

            if (_scanSettings.LogDetections)
            {
                var entry = new LogEntry(DateTime.Now, (double)mhz, db, GetDemodulationMode().ToString());
                _scanLog.Add(entry);
                var item = new ListViewItem(new[] { entry.Time.ToString("HH:mm:ss"), entry.MHz.ToString("0.0000"), $"+{entry.Db:F0}", entry.Mode });
                _lstLog.Items.Insert(0, item);
                UpdateCardStates();
            }
        }

        private ScanState _lastScanUiState = ScanState.Idle;
        private bool _lastScanUiStartupSettling;
        private long _lastScanUiTick;

        private void OnScanUpdated()
        {
            if (_scanner == null) return;
            RefreshScanMute();
            // Repaint the readouts at most ~12 times a second; state changes always go through immediately.
            var state = _scanner.State;
            long now = Environment.TickCount64;
            bool settling = _scanner.IsStartupSettling;
            if (state == _lastScanUiState && settling == _lastScanUiStartupSettling && now - _lastScanUiTick < 80) return;
            _lastScanUiState = state;
            _lastScanUiStartupSettling = settling;
            _lastScanUiTick = now;
            UpdateScanUi();
            if (_chkAutoThreshold.Checked) RefreshThresholdLabel();
        }

        private void RefreshScanMute()
        {
            bool muted = _scanner != null && _scanner.State == ScanState.Scanning;
            if (muted == _scanMuted) return;
            _scanMuted = muted;
            UpdateDemodulatorSettings();
        }

        private void UpdateScanUi()
        {
            var sc = _scanner;
            var state = sc?.State ?? ScanState.Idle;
            bool running = state != ScanState.Idle;

            ApplyLinkedSquelch();
            _btnScanToggle.Text = running ? "Stop scan" : "Start scan";
            _btnResume.Enabled = state == ScanState.Locked;
            _btnLockout.Enabled = true;
            foreach (Control c in new Control[] { _numStart, _numStop, _numStep, _cmbMethod, _cmbPreset })
                c.Enabled = true;
            UpdateCardStates();

            if (sc == null || !running)
            {
                _lblScanStatus.Text = "Idle - press Start scan";
                _lblScanStatus.ForeColor = Color.FromArgb(150, 156, 170);
                if (sc != null) _scanGraph.SetData(sc.Strength, sc.ChannelHz, -1, false);
                return;
            }

            int marker = -1;
            if (state == ScanState.Locked)
                for (int i = 0; i < sc.ChannelHz.Count; i++)
                    if (sc.ChannelHz[i] == sc.LockedHz) { marker = i; break; }

            _scanGraph.SetData(sc.Strength, sc.ChannelHz, marker, true);
            _lblScanStatus.Text = sc.IsStartupSettling
                ? "Settling radio before scanning..."
                : state == ScanState.Locked
                ? $"LOCKED {sc.LockedHz / 1e6:0.0000} MHz  signal +{sc.LockedStrengthDb:F1} dB  (floor {sc.NoiseFloorDb:F1} dBFS)"
                : $"Scanning {sc.CurrentHz / 1e6:F3} MHz   floor {sc.NoiseFloorDb:F1} dBFS   sweeps: {sc.SweepCount}";
            if (_scanRangeError != null) _lblScanStatus.Text += $"  |  {_scanRangeError}";
            _lblScanStatus.ForeColor = _scanRangeError != null ? Color.Orange
                : state == ScanState.Locked ? Color.FromArgb(90, 220, 130) : Color.FromArgb(90, 190, 255);
        }

        /// <summary>Lights up cards whose controls are usable right now and dims the rest.</summary>
        private void UpdateCardStates()
        {
            if (_cardRange == null) return;
            bool open = btnClose.Enabled;
            bool receiving = btnStop.Enabled;
            var state = _scanner?.State ?? ScanState.Idle;
            bool running = state != ScanState.Idle;
            bool spectrumOn = open && chkSpectrumEnabled.Checked;
            var amber = Color.FromArgb(196, 130, 20);
            var blue = Color.FromArgb(0, 120, 190);
            var green = CardGroupBox.ActiveBadgeColor;

            static void Set(CardGroupBox c, bool active, string? badge = null, Color? badgeColor = null)
            {
                c.Active = active;
                c.BadgeText = badge;
                c.BadgeColor = badgeColor;
            }

            Set(groupBox1, true, _deviceOpening ? "OPENING DEVICE" : receiving ? "RECEIVING" : open ? "DEVICE READY" : "NO DEVICE OPEN", receiving ? green : open ? blue : amber);
            Set(groupBox2, open, open ? null : "OPEN DEVICE");
            Set(groupBox3, open, open ? null : "OPEN DEVICE");
            Set(groupBox4, open, open ? null : "OPEN DEVICE");
            Set(groupBoxAudio, open, open ? null : "OPEN DEVICE");
            Set(groupBox6, spectrumOn, spectrumOn ? null : open ? "OFF" : "OPEN DEVICE");

            bool usable = receiving || running;
            string wait = "START RECEIVING";
            Set(_cardRange, usable, running ? "LIVE" : usable ? null : wait, running ? blue : null);
            Set(_cardDetect, usable, usable ? (_chkAutoThreshold.Checked ? "AUTO" : "MANUAL") : wait, usable ? blue : null);
            Set(_cardBehave, usable, usable ? null : wait);
            Set(_cardControl, usable,
                state == ScanState.Locked ? "LOCKED ON SIGNAL" : running ? "SCANNING" : usable ? "IDLE" : wait,
                state == ScanState.Locked ? green : running ? blue : usable ? CardGroupBox.InactiveBadgeColor : null);
            Set(_cardGraph, running, running ? "LIVE" : "NOT SCANNING");
            bool hasLog = _lstLog.Items.Count > 0;
            Set(_cardLog, running || hasLog, hasLog ? $"{_lstLog.Items.Count} ENTRIES" : running ? "LISTENING" : "EMPTY");
            bool hasLock = _lstLockouts.Items.Count > 0;
            Set(_cardLock, usable || hasLock, hasLock ? $"{_lstLockouts.Items.Count} SKIPPED" : null);

            _cardRange.Enabled = true;
            foreach (var c in new[] { _cardRange, _cardDetect, _cardBehave, _cardControl })
                foreach (Control child in c.Controls)
                    if (child is not DashboardResizeHandle) child.Enabled = c.Active;
        }

        private void LockOutCurrent()
        {
            double mhz = _scanner is { State: ScanState.Locked } ? _scanner.LockedHz / 1e6 : (double)numFrequency.Value;
            mhz = Math.Round(mhz, 6);
            if (!_scanSettings.LockoutsMHz.Contains(mhz))
                _scanSettings.LockoutsMHz.Add(mhz);
            RefreshLockoutList();
            if (_scanner is { State: ScanState.Locked })
                _scanner.Resume();
        }

        private void RemoveSelectedLockout()
        {
            int i = _lstLockouts.SelectedIndex;
            if (i < 0 || i >= _scanSettings.LockoutsMHz.Count) return;
            _scanSettings.LockoutsMHz.RemoveAt(i);
            RefreshLockoutList();
        }

        private void RefreshLockoutList()
        {
            _lstLockouts.Items.Clear();
            foreach (double mhz in _scanSettings.LockoutsMHz)
                _lstLockouts.Items.Add($"{mhz:0.0000} MHz");
            UpdateCardStates();
        }

        private void SaveScanLog()
        {
            if (_scanLog.Count == 0)
            {
                MessageBox.Show("The detection log is empty.", "Scanner", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            using var dlg = new SaveFileDialog { Filter = "CSV (*.csv)|*.csv", FileName = $"scan-log-{DateTime.Now:yyyyMMdd-HHmmss}.csv" };
            if (dlg.ShowDialog() != DialogResult.OK) return;
            var lines = new List<string> { "Time,MHz,dB above floor,Mode" };
            lines.AddRange(_scanLog.Select(e => $"{e.Time:yyyy-MM-dd HH:mm:ss},{e.MHz:0.0000},{e.Db:F1},{e.Mode}"));
            File.WriteAllLines(dlg.FileName, lines);
        }

        private void SelectModeForBand(double mhz)
        {
            var preset = ScanPresets.FirstOrDefault(p => p.Name == _scanSettings.PresetName
                && p.Start == _scanSettings.StartMHz && p.Stop == _scanSettings.StopMHz && p.Step == _scanSettings.StepKHz);
            DemodulationMode? mode = preset.Mode ?? mhz switch
            {
                >= 87.5 and <= 108 => DemodulationMode.WideFm,
                > 108 and <= 137 => DemodulationMode.Am,
                >= 25 and <= 30 => DemodulationMode.Am,
                >= 137 and <= 174 => DemodulationMode.NarrowFm,
                >= 430 and <= 470 => DemodulationMode.NarrowFm,
                _ => null
            };
            RadioButton? target = mode switch
            {
                DemodulationMode.WideFm => rdoDemodWideFm,
                DemodulationMode.NarrowFm => rdoDemodNarrowFm,
                DemodulationMode.Am => rdoDemodAm,
                _ => null
            };
            if (target != null) target.Checked = true;
        }

        private static bool IsLawEnforcementPreset(int index) =>
            index == LawEnforcementVhfPresetIndex || index == LawEnforcementUhfPresetIndex;
    }
}
