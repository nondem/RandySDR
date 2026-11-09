using System.Diagnostics;

namespace RandySDR
{
    // Scan Robot card: recording / transcription / classification toggles, recent clips and lockout suggestions.
    public partial class Form1
    {
        private RobotController? _robot;
        private CardGroupBox _cardRobot = null!;
        private CheckBox _chkRobotRecord = null!, _chkRobotTranscribe = null!, _chkRobotClassify = null!, _chkRobotEarlySkip = null!;
        private ComboBox _cmbRobotLockout = null!;
        private Label _lblRobotStatus = null!;
        private ListView _lstRobotClips = null!, _lstRobotLockouts = null!;
        private readonly System.Windows.Forms.Timer _robotTimer = new() { Interval = 500 };
        private bool _loadingRobotUi, _robotDirty = true;

        private void BuildRobotCard()
        {
            try
            {
                var config = RobotConfig.Load();
                _robot = new RobotController(config, new RobotStore(Path.Combine(LayoutDirectory, "robot.db")),
                    () => _scanSettings.StepKHz * 1000.0);
                _robot.SetModeProvider(() => GetDemodulationMode().ToString());
                _robot.Changed += () => _robotDirty = true;
            }
            catch (Exception ex)
            {
                _robot = null;
                Debug.WriteLine("Robot disabled: " + ex.Message);
            }

            _chkRobotRecord = new CheckBox { Text = "Record signals", AutoSize = true };
            _chkRobotTranscribe = new CheckBox { Text = "Transcribe (Whisper)", AutoSize = true };
            _chkRobotClassify = new CheckBox { Text = "Classify (Ollama)", AutoSize = true };
            _chkRobotEarlySkip = new CheckBox { Text = "Early-skip dead carriers", AutoSize = true };
            _cmbRobotLockout = NewCombo(new[] { "Auto-lockout off", "Suggest lockouts", "Auto lockout" });
            _lblRobotStatus = new Label { Text = "Robot idle", AutoSize = true, ForeColor = Color.Gray, Margin = new Padding(0, 4, 0, 4) };
            var flags = new FlowLayoutPanel { AutoSize = true, WrapContents = true, Dock = DockStyle.Fill };
            flags.Controls.AddRange(new Control[] { _chkRobotRecord, _chkRobotTranscribe, _chkRobotClassify, _chkRobotEarlySkip, _cmbRobotLockout });

            _lstRobotClips = NewRobotList(true, "Time", 70, "MHz", 80, "Class", 70, "Sec", 40, "Category", 90, "Transcript", 400);
            _lstRobotLockouts = NewRobotList(false, "MHz", 80, "Status", 80, "Reason", 400);
            var btnPlay = NewButton("Play");
            var btnStar = NewButton("Star");
            var btnWrong = NewButton("Wrong");
            var btnRedo = NewButton("Re-run AI");
            var btnDelete = NewButton("Delete");
            var btnDeleteJunk = NewButton("Delete junk");
            var btnOpen = NewButton("Open folder");
            var btnAccept = NewButton("Accept");
            var btnRestore = NewButton("Restore");

            var layout = new TableLayoutPanel { ColumnCount = 1, RowCount = 4, Margin = Padding.Empty };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 60));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 40));
            var clipsSection = ListSection("Recent clips", _lstRobotClips, btnPlay, btnStar, btnWrong, btnRedo, btnDelete, btnDeleteJunk, btnOpen);
            var lockSection = ListSection("Lockout suggestions / auto-lockouts", _lstRobotLockouts, btnAccept, btnRestore);
            foreach (var c in new Control[] { flags, _lblRobotStatus, clipsSection, lockSection })
            {
                c.Dock = DockStyle.Fill;
                layout.Controls.Add(c);
            }
            _cardRobot = AddScanCard("Scan Robot", layout, 50);
            _wideCards.Add(_cardRobot);

            _loadingRobotUi = true;
            if (_robot != null)
            {
                var c = _robot.Config;
                _chkRobotRecord.Checked = c.RecordClips;
                _chkRobotTranscribe.Checked = c.Transcribe;
                _chkRobotClassify.Checked = c.Classify;
                _chkRobotEarlySkip.Checked = c.EarlySkip;
                _cmbRobotLockout.SelectedIndex = (int)c.LockoutMode;
            }
            else
            {
                _cardRobot.Enabled = false;
                _cmbRobotLockout.SelectedIndex = 0;
            }
            _loadingRobotUi = false;

            void Changed(object? s, EventArgs e)
            {
                if (_loadingRobotUi || _robot == null) return;
                var c = _robot.Config;
                c.RecordClips = _chkRobotRecord.Checked;
                c.Transcribe = _chkRobotTranscribe.Checked;
                c.Classify = _chkRobotClassify.Checked;
                c.EarlySkip = _chkRobotEarlySkip.Checked;
                c.LockoutMode = (AutoLockoutMode)Math.Max(0, _cmbRobotLockout.SelectedIndex);
                c.Save();
                _robot.ApplyConfig();
                _robot.RefreshLockoutCache();
            }
            _chkRobotRecord.CheckedChanged += Changed;
            _chkRobotTranscribe.CheckedChanged += Changed;
            _chkRobotClassify.CheckedChanged += Changed;
            _chkRobotEarlySkip.CheckedChanged += Changed;
            _cmbRobotLockout.SelectedIndexChanged += Changed;

            long? SelClip() => _lstRobotClips.SelectedItems.Count > 0 ? (long)_lstRobotClips.SelectedItems[0].Tag! : null;
            List<long> SelClips() => _lstRobotClips.SelectedItems.Cast<ListViewItem>().Select(i => (long)i.Tag!).ToList();
            long? SelLock() => _lstRobotLockouts.SelectedItems.Count > 0 ? (long)_lstRobotLockouts.SelectedItems[0].Tag! : null;
            void DeleteIds(List<long> ids, string what)
            {
                if (ids.Count == 0 || _robot == null) return;
                if (ids.Count > 1 && MessageBox.Show(this, $"Delete {ids.Count} {what} and their audio files?", "Delete clips",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                foreach (var id in ids) _robot.DeleteClip(id);
                RefreshRobotLists();
            }
            btnPlay.Click += (_, _) =>
            {
                if (SelClip() is long id && _robot?.Store.GetClip(id) is { } clip && File.Exists(clip.Path))
                    try { Process.Start(new ProcessStartInfo(clip.Path) { UseShellExecute = true }); } catch { }
            };
            btnStar.Click += (_, _) => { foreach (var id in SelClips()) _robot?.SetStar(id, true); };
            btnWrong.Click += (_, _) => { foreach (var id in SelClips()) _robot?.MarkWrong(id); };
            btnRedo.Click += (_, _) => { foreach (var id in SelClips()) _robot?.Reprocess(id); };
            btnDelete.Click += (_, _) => DeleteIds(SelClips(), "selected clips");
            btnDeleteJunk.Click += (_, _) =>
                DeleteIds(_lstRobotClips.Items.Cast<ListViewItem>()
                    .Where(i => !i.SubItems[2].Text.StartsWith('*') && i.SubItems[2].Text.ToUpperInvariant() is "CARRIER" or "TONE" or "NOISE")
                    .Select(i => (long)i.Tag!).ToList(), "carrier/tone/noise clips");
            _lstRobotClips.KeyDown += (_, e) =>
            {
                if (e.KeyCode == Keys.Delete) { DeleteIds(SelClips(), "selected clips"); e.Handled = true; }
                else if (e.Control && e.KeyCode == Keys.A)
                {
                    foreach (ListViewItem i in _lstRobotClips.Items) i.Selected = true;
                    e.Handled = true;
                }
            };
            btnOpen.Click += (_, _) =>
            {
                if (_robot == null) return;
                try { Directory.CreateDirectory(_robot.Config.ClipFolder); Process.Start("explorer.exe", _robot.Config.ClipFolder); } catch { }
            };
            btnAccept.Click += (_, _) => { if (SelLock() is long id) _robot?.AcceptSuggestion(id); };
            btnRestore.Click += (_, _) => { if (SelLock() is long id) _robot?.RestoreLockout(id); };

            _robotTimer.Tick += (_, _) => RobotTick();
            _robotTimer.Start();
        }

        private void OpenRobotSettings()
        {
            if (_robot == null)
            {
                MessageBox.Show(this, "The scan robot is unavailable, so there are no settings to edit.", "Settings", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            using var dlg = new RobotSettingsForm(_robot);
            dlg.ShowDialog(this);
        }

        private static ListView NewRobotList(bool multi, params object[] cols)
        {
            var lv = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, GridLines = true, MultiSelect = multi, HideSelection = false, Margin = new Padding(0, 4, 0, 4) };
            for (int i = 0; i < cols.Length; i += 2) lv.Columns.Add((string)cols[i], (int)cols[i + 1]);
            return lv;
        }

        private void RobotTick()
        {
            if (_robot == null) return;
            if (_robot.EarlySkipDue && _scanner is { State: ScanState.Locked })
            {
                _robot.EndRecording("early-skip");
                _scanner.Resume();
            }

            var r = _robot.Recorder;
            string state = r.IsRecording ? $"REC {r.LockOpenSeconds:F0}s" : "idle";
            string disk = "";
            try
            {
                var root = Path.GetPathRoot(Path.GetFullPath(_robot.Config.ClipFolder));
                if (root != null) disk = $" | disk {new DriveInfo(root).AvailableFreeSpace / 1e9:F0} GB free";
            }
            catch { }
            string ai = string.IsNullOrEmpty(_robot.AiStatus) ? "" : " | " + _robot.AiStatus;
            string err = r.LastError is { Length: > 0 } e ? " | " + e : "";
            _lblRobotStatus.Text = $"{state} | dropped {r.DroppedBlocks} | AI queue {_robot.AiBacklog}{disk}{ai}{err}";
            _lblRobotStatus.ForeColor = r.IsRecording ? Color.Firebrick : Color.Gray;

            if (!_robotDirty) return;
            _robotDirty = false;
            RefreshRobotLists();
        }

        private void RefreshRobotLists()
        {
            if (_robot == null) return;
            try
            {
                var selClips = _lstRobotClips.SelectedItems.Cast<ListViewItem>().Select(i => (long)i.Tag!).ToHashSet();
                _lstRobotClips.BeginUpdate();
                _lstRobotClips.Items.Clear();
                foreach (var c in _robot.Store.RecentClips(100))
                {
                    var item = new ListViewItem(new[]
                    {
                        c.StartUtc.ToLocalTime().ToString("HH:mm:ss"), (c.Hz / 1e6).ToString("0.0000"),
                        (c.Starred ? "* " : "") + c.Class, c.DurationSec.ToString("0"), c.Category ?? "",
                        string.IsNullOrEmpty(c.Transcript) ? c.Status ?? "" : c.Transcript
                    }) { Tag = c.Id, Selected = selClips.Contains(c.Id) };
                    _lstRobotClips.Items.Add(item);
                }
                _lstRobotClips.EndUpdate();

                long? selLock = _lstRobotLockouts.SelectedItems.Count > 0 ? (long)_lstRobotLockouts.SelectedItems[0].Tag! : null;
                _lstRobotLockouts.BeginUpdate();
                _lstRobotLockouts.Items.Clear();
                foreach (var l in _robot.Store.Lockouts("suggested", "active"))
                    _lstRobotLockouts.Items.Add(new ListViewItem(new[] { (l.Hz / 1e6).ToString("0.0000"), l.Status, l.Reason })
                    { Tag = l.Id, Selected = l.Id == selLock });
                _lstRobotLockouts.EndUpdate();
            }
            catch (Exception ex) { Debug.WriteLine("Robot list refresh failed: " + ex.Message); }
        }
    }
}
