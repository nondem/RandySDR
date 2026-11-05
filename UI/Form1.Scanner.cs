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
        private decimal _scanOriginalMHz;

        private sealed record LogEntry(DateTime Time, double MHz, double Db, string Mode);
        private readonly List<LogEntry> _scanLog = new();

        private CardGroupBox _grpScan = null!;
        private ComboBox _cmbPreset = null!, _cmbMethod = null!, _cmbHold = null!;
        private NumericUpDown _numStart = null!, _numStop = null!, _numStep = null!, _numHold = null!, _numSettle = null!;
        private TrackBar _trackThreshold = null!;
        private Label _lblThreshold = null!, _lblScanStatus = null!;
        private CheckBox _chkAutoMode = null!, _chkLog = null!;
        private Button _btnScanToggle = null!, _btnResume = null!, _btnLockout = null!;
        private ScanGraph _scanGraph = null!;
        private ListView _lstLog = null!;
        private ListBox _lstLockouts = null!;

        private static readonly (string Name, double Start, double Stop, double Step)[] ScanPresets =
        {
            ("Custom", 0, 0, 0),
            ("FM broadcast 88.1-107.9 / 200 kHz", 88.1, 107.9, 200),
            ("Airband 118-137 / 25 kHz", 118, 137, 25),
            ("NOAA weather 162.400-162.550 / 25 kHz", 162.4, 162.55, 25),
            ("Marine VHF 156-162.6 / 25 kHz", 156, 162.6, 25),
            ("2 m ham 144-148 / 12.5 kHz", 144, 148, 12.5),
            ("70 cm ham 430-450 / 25 kHz", 430, 450, 25),
            ("FRS/GMRS 462.5-467.75 / 12.5 kHz", 462.5, 467.75, 12.5),
        };

        private void BuildScannerPanel()
        {
            _grpScan = new CardGroupBox
            {
                Text = "Signal Scanner / Monitor",
                Dock = DockStyle.Fill,
                Padding = new Padding(12, 8, 12, 12),
                MinimumSize = new Size(560, 880)
            };
            _rightPanel.Controls.Add(_grpScan);

            _cmbPreset = NewCombo(ScanPresets.Select(p => p.Name).ToArray());
            BuildSquelchLinkControls();
            _numStart = NewNumeric(3, 0.5m, 1766m, 0.1m);
            _numStop = NewNumeric(3, 0.5m, 1766m, 0.1m);
            _numStep = NewNumeric(2, 1m, 5000m, 5m);
            _cmbMethod = NewCombo(new[] { "Wideband FFT (fast)", "Tune each channel (slow)" });
            _cmbHold = NewCombo(new[] { "Stay until signal drops", "Stay until I press Resume", "Stay for fixed time" });
            _lblThreshold = new Label { AutoSize = true, Margin = new Padding(0, 6, 0, 0) };
            _trackThreshold = new TrackBar { Dock = DockStyle.Fill, Minimum = 3, Maximum = 40, TickFrequency = 3, Margin = new Padding(0, 0, 0, 6) };
            _numHold = NewNumeric(1, 0.5m, 120m, 0.5m);
            _numSettle = NewNumeric(0, 40m, 1000m, 10m);
            _chkAutoMode = new CheckBox { Text = "Auto-select mode by band", AutoSize = true, Margin = new Padding(0, 0, 0, 6) };
            _chkLog = new CheckBox { Text = "Log detections", AutoSize = true };
            var checks = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 4, 0, 0) };
            checks.Controls.Add(_chkAutoMode);
            checks.Controls.Add(_chkLog);

            _btnScanToggle = NewButton("Start scan");
            _btnResume = NewButton("Resume");
            _btnLockout = NewButton("Lock out signal");
            _lblScanStatus = new Label
            {
                Text = "Idle", ForeColor = Color.Silver, Dock = DockStyle.Fill, AutoSize = false,
                Font = new Font("Segoe UI Semibold", 12f), TextAlign = ContentAlignment.MiddleLeft, MinimumSize = new Size(0, 60)
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

            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 0 };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            void AddRow(Control c, SizeType type = SizeType.AutoSize, float size = 0)
            {
                root.RowStyles.Add(new RowStyle(type, size));
                c.Dock = DockStyle.Fill;
                root.Controls.Add(c, 0, root.RowCount++);
            }

            AddRow(FieldRow(("Band preset", _cmbPreset)));
            AddRow(FieldRow(("Start (MHz)", _numStart), ("Stop (MHz)", _numStop), ("Step (kHz)", _numStep)));
            AddRow(FieldRow(("Scan method", _cmbMethod), ("When a signal is found", _cmbHold)));
            AddRow(FieldRow((_lblThreshold, _trackThreshold)));
            AddRow(FieldRow((null, _chkLinkSquelch), ("Squelch offset (dB)", _numSquelchOffset)));
            AddRow(FieldRow(("Hold / drop time (s)", _numHold), ("Settle time (ms)", _numSettle), (null, checks)));
            AddRow(ButtonRow(_btnScanToggle, _btnResume, _btnLockout));
            AddRow(_lblScanStatus);
            AddRow(_scanGraph, SizeType.Percent, 40);
            AddRow(ListSection("Detection log", _lstLog, btnClearLog, btnSaveLog), SizeType.Percent, 34);
            AddRow(ListSection("Locked-out frequencies (skipped by the scanner)", _lstLockouts, btnRemoveLockout, btnClearLockouts), SizeType.Percent, 26);
            _grpScan.Controls.Add(root);

            LoadScanSettingsToUi();

            _cmbPreset.SelectedIndexChanged += PresetChanged;
            foreach (var n in new[] { _numStart, _numStop, _numStep, _numHold, _numSettle })
                n.ValueChanged += (_, _) => ReadScanSettingsFromUi();
            _cmbMethod.SelectedIndexChanged += (_, _) => ReadScanSettingsFromUi();
            _cmbHold.SelectedIndexChanged += (_, _) => ReadScanSettingsFromUi();
            _trackThreshold.ValueChanged += (_, _) => ReadScanSettingsFromUi();
            _chkAutoMode.CheckedChanged += (_, _) => ReadScanSettingsFromUi();
            _chkLog.CheckedChanged += (_, _) => ReadScanSettingsFromUi();

            _btnScanToggle.Click += (_, _) => ToggleScan();
            _btnResume.Click += (_, _) => _scanner?.Resume();
            _btnLockout.Click += (_, _) => LockOutCurrent();
            btnClearLog.Click += (_, _) => { _scanLog.Clear(); _lstLog.Items.Clear(); };
            btnSaveLog.Click += (_, _) => SaveScanLog();
            btnRemoveLockout.Click += (_, _) => RemoveSelectedLockout();
            btnClearLockouts.Click += (_, _) => { _scanSettings.LockoutsMHz.Clear(); RefreshLockoutList(); };

            _scanTimer.Tick += (_, _) => _scanner?.Tick();
            UpdateScanUi();
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
            new() { Text = text, Dock = DockStyle.Fill, MinimumSize = new Size(0, 44), Margin = new Padding(0, 4, 10, 4) };

        /// <summary>One row of equally wide columns, each a caption above its control.</summary>
        private static TableLayoutPanel FieldRow(params (object? Caption, Control Control)[] fields)
        {
            var t = new TableLayoutPanel { AutoSize = true, ColumnCount = fields.Length, RowCount = 2, Margin = new Padding(0, 0, 0, 8) };
            t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            for (int i = 0; i < fields.Length; i++)
            {
                t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / fields.Length));
                var caption = fields[i].Caption switch
                {
                    Label l => l,
                    string s => new Label { Text = s, AutoSize = true, Margin = new Padding(0, 6, 0, 2) },
                    _ => null
                };
                if (caption != null) t.Controls.Add(caption, i, 0);
                fields[i].Control.Dock = DockStyle.Fill;
                t.Controls.Add(fields[i].Control, i, 1);
            }
            return t;
        }

        private static TableLayoutPanel ButtonRow(params Button[] buttons)
        {
            var t = new TableLayoutPanel { AutoSize = true, ColumnCount = buttons.Length, RowCount = 1, Margin = new Padding(0, 4, 0, 4) };
            t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            for (int i = 0; i < buttons.Length; i++)
            {
                t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / buttons.Length));
                t.Controls.Add(buttons[i], i, 0);
            }
            return t;
        }

        /// <summary>Caption, a stretching list, and a row of buttons underneath.</summary>
        private static TableLayoutPanel ListSection(string caption, Control list, params Button[] buttons)
        {
            var t = new TableLayoutPanel { ColumnCount = 1, RowCount = 3, Margin = new Padding(0, 6, 0, 0) };
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            t.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            t.Controls.Add(new Label { Text = caption, AutoSize = true, Font = new Font("Segoe UI Semibold", 9.5f) }, 0, 0);
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
            _chkLinkSquelch.Checked = s.LinkSquelch;
            _numSquelchOffset.Value = Math.Clamp(s.SquelchOffsetDb, (int)_numSquelchOffset.Minimum, (int)_numSquelchOffset.Maximum);
            _loadingScanUi = false;
            RefreshLockoutList();
            RefreshThresholdLabel();
        }

        private void ReadScanSettingsFromUi()
        {
            if (_loadingScanUi) return;
            var s = _scanSettings;
            s.StartMHz = (double)_numStart.Value;
            s.StopMHz = (double)_numStop.Value;
            s.StepKHz = (double)_numStep.Value;
            s.Method = (ScanMethod)Math.Max(0, _cmbMethod.SelectedIndex);
            s.Hold = (HoldMode)Math.Max(0, _cmbHold.SelectedIndex);
            s.ThresholdDb = _trackThreshold.Value;
            s.HoldSeconds = (double)_numHold.Value;
            s.SettleMs = (int)_numSettle.Value;
            s.AutoSelectMode = _chkAutoMode.Checked;
            s.LogDetections = _chkLog.Checked;
            s.LinkSquelch = _chkLinkSquelch.Checked;
            s.SquelchOffsetDb = (int)_numSquelchOffset.Value;
            RefreshThresholdLabel();
            _numHold.Enabled = s.Hold != HoldMode.UntilResumed;
        }

        private void RefreshThresholdLabel()
        {
            _lblThreshold.Text = $"Detection threshold: {_trackThreshold.Value} dB above noise floor";
            _scanGraph.ThresholdDb = _trackThreshold.Value;
            ApplyLinkedSquelch();
        }

        private void PresetChanged(object? sender, EventArgs e)
        {
            int i = _cmbPreset.SelectedIndex;
            if (i <= 0) return;
            var p = ScanPresets[i];
            _loadingScanUi = true;
            _numStart.Value = (decimal)p.Start;
            _numStop.Value = (decimal)p.Stop;
            _numStep.Value = (decimal)p.Step;
            _loadingScanUi = false;
            ReadScanSettingsFromUi();
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
            return sc;
        }

        private void StopScan()
        {
            if (_scanner == null || _scanner.State == ScanState.Idle)
                return;

            bool wasLocked = _scanner.State == ScanState.Locked;
            _scanner.Stop();
            _scanTimer.Stop();
            RefreshScanMute();

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

            lblStatus.Text = $"Scanner locked: {mhz:F3} MHz (+{db:F0} dB)";
            lblStatus.ForeColor = Color.Green;

            if (_scanSettings.LogDetections)
            {
                var entry = new LogEntry(DateTime.Now, (double)mhz, db, GetDemodulationMode().ToString());
                _scanLog.Add(entry);
                var item = new ListViewItem(new[] { entry.Time.ToString("HH:mm:ss"), entry.MHz.ToString("F3"), $"+{entry.Db:F0}", entry.Mode });
                _lstLog.Items.Insert(0, item);
            }
        }

        private void OnScanUpdated()
        {
            if (_scanner == null) return;
            RefreshScanMute();
            UpdateScanUi();
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

            _btnScanToggle.Text = running ? "Stop scan" : "Start scan";
            _btnResume.Enabled = state == ScanState.Locked;
            _btnLockout.Enabled = true;
            foreach (Control c in new Control[] { _numStart, _numStop, _numStep, _cmbMethod, _cmbPreset })
                c.Enabled = !running;

            if (sc == null || !running)
            {
                _lblScanStatus.Text = "Idle";
                _lblScanStatus.ForeColor = Color.DimGray;
                if (sc != null) _scanGraph.SetData(sc.Strength, _scanSettings.StartMHz, _scanSettings.StopMHz, -1);
                return;
            }

            int marker = -1;
            if (state == ScanState.Locked)
                marker = (int)Math.Round((sc.LockedHz - _scanSettings.StartMHz * 1e6) / (_scanSettings.StepKHz * 1000));

            _scanGraph.SetData(sc.Strength, _scanSettings.StartMHz, _scanSettings.StopMHz, marker);
            _lblScanStatus.Text = state == ScanState.Locked
                ? $"LOCKED {sc.LockedHz / 1e6:F3} MHz  signal +{sc.LockedStrengthDb:F1} dB  (floor {sc.NoiseFloorDb:F1} dBFS)"
                : $"Scanning {sc.CurrentHz / 1e6:F3} MHz   floor {sc.NoiseFloorDb:F1} dBFS   sweeps: {sc.SweepCount}";
            _lblScanStatus.ForeColor = state == ScanState.Locked ? Color.Green : Color.RoyalBlue;
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
                _lstLockouts.Items.Add($"{mhz:F3} MHz");
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
            lines.AddRange(_scanLog.Select(e => $"{e.Time:yyyy-MM-dd HH:mm:ss},{e.MHz:F3},{e.Db:F1},{e.Mode}"));
            File.WriteAllLines(dlg.FileName, lines);
        }

        private void SelectModeForBand(double mhz)
        {
            RadioButton? target = mhz switch
            {
                >= 87.5 and <= 108 => rdoDemodWideFm,
                > 108 and <= 137 => rdoDemodAm,
                >= 25 and <= 30 => rdoDemodAm,
                >= 137 and <= 174 => rdoDemodNarrowFm,
                >= 430 and <= 470 => rdoDemodNarrowFm,
                _ => null
            };
            if (target != null) target.Checked = true;
        }
    }
}
