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
        private bool _loadingRobotUi;
        private volatile bool _robotDirty = true;
        private DateTime _lastRobotLockoutRefresh = DateTime.MinValue;

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
            _cmbRobotLockout = NewCombo(new[] { "Off", "Suggest only", "Auto lockout" });
            _cmbRobotLockout.Width = 140; // Ensure full text is visible
            _lblRobotStatus = new Label { Text = "Robot idle", AutoSize = true, ForeColor = Color.Gray, Margin = new Padding(0, 4, 0, 4) };
            var flags = new FlowLayoutPanel { AutoSize = true, WrapContents = true, Dock = DockStyle.Fill };
            flags.Controls.AddRange(new Control[] { _chkRobotRecord, _chkRobotTranscribe, _chkRobotClassify, _chkRobotEarlySkip, _cmbRobotLockout });

            _lstRobotClips = NewRobotList(true, "Delete", 35, "Time", 70, "MHz", 80, "Class", 70, "Sec", 40, "Category", 90, "Transcript", 400);
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
            var btnMarkDelete = NewButton("Mark delete");
            var btnUnmarkDelete = NewButton("Unmark delete");
            var btnClearAllMarks = NewButton("Clear all marks");

            var layout = new TableLayoutPanel { ColumnCount = 1, RowCount = 4, Margin = Padding.Empty };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 60));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 40));
            var clipsSection = ListSection(null, _lstRobotClips, btnPlay, btnStar, btnWrong, btnRedo, btnDelete, btnDeleteJunk, btnOpen);
            clipsSection.Controls.RemoveAt(0);
            var clipHeader = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = false, Margin = Padding.Empty };
            clipHeader.Controls.Add(new Label { Text = "Recent clips", AutoSize = true, Margin = new Padding(0, 7, 8, 0) });
            var btnDetails = new Button { Text = "View details", AutoSize = true, Enabled = false };
            clipHeader.Controls.Add(btnDetails);
            clipsSection.Controls.Add(clipHeader, 0, 0);
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
                c.LockoutMode = (AutoLockoutMode)_cmbRobotLockout.SelectedIndex;
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
            void ShowClipDetails(long id)
            {
                var clip = _robot?.Store.GetClip(id);
                if (clip == null || clip.Deleted)
                {
                    MessageBox.Show(this, "This clip is no longer available.", "Clip details", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                using var details = new ClipDetailsForm(clip);
                details.ShowDialog(this);
            }
            _lstRobotClips.SelectedIndexChanged += (_, _) => btnDetails.Enabled = _lstRobotClips.SelectedItems.Count == 1;
            btnDetails.Click += (_, _) => { if (_lstRobotClips.SelectedItems.Count == 1 && SelClip() is long id) ShowClipDetails(id); };
            _lstRobotClips.MouseDoubleClick += (_, e) =>
            {
                if (e.Button == MouseButtons.Left && _lstRobotClips.GetItemAt(e.X, e.Y)?.Tag is long id) ShowClipDetails(id);
            };
            _lstRobotClips.KeyDown += (_, e) =>
            {
                if (e.KeyCode == Keys.Enter && _lstRobotClips.SelectedItems.Count == 1 && SelClip() is long id)
                {
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    ShowClipDetails(id);
                }
            };
            void DeleteIds(List<long> ids, string what)
            {
                if (ids.Count == 0 || _robot == null) return;
                if (ids.Count > 1 && MessageBox.Show(this, $"Delete {ids.Count} {what}?") != DialogResult.Yes) return;
                foreach (var id in ids)
                {
                    try { _robot.DeleteClip(id); }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                    {
                        MessageBox.Show(this, $"Could not delete clip {id}: {ex.Message}", "Delete clips", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                RefreshRobotLists();
            }
            
            void DeleteMarkedClips()
            {
                var ids = _lstRobotClips.Items.Cast<ListViewItem>()
                    .Where(i => i.Checked)
                    .Select(i => (long)i.Tag!).ToList();
                
                if (ids.Count == 0 || _robot == null) return;
                if (MessageBox.Show(this, $"Delete {ids.Count} marked clips?") != DialogResult.Yes) return;
                
                foreach (var id in ids)
                {
                    try { _robot.DeleteClip(id); }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                    {
                        MessageBox.Show(this, $"Could not delete clip {id}: {ex.Message}", "Delete clips", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                
                // Unmark all checked items after deletion
                foreach (ListViewItem item in _lstRobotClips.Items)
                {
                    if (item.Checked) item.Checked = false;
                }
                
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
            var btnDeleteMarked = NewButton("Delete marked");
            clipHeader.Controls.Add(btnMarkDelete);
            clipHeader.Controls.Add(btnUnmarkDelete);
            clipHeader.Controls.Add(btnClearAllMarks);
            clipHeader.Controls.Add(btnDeleteMarked);
            
            btnDeleteMarked.Click += (_, _) => DeleteMarkedClips();
            
            btnMarkDelete.Click += (_, _) =>
            {
                foreach (var id in SelClips())
                {
                    var item = _lstRobotClips.Items.Cast<ListViewItem>().FirstOrDefault(i => (long)i.Tag! == id);
                    if (item != null) item.Checked = true;
                }
            };
            
            btnUnmarkDelete.Click += (_, _) =>
            {
                foreach (var id in SelClips())
                {
                    var item = _lstRobotClips.Items.Cast<ListViewItem>().FirstOrDefault(i => (long)i.Tag! == id);
                    if (item != null) item.Checked = false;
                }
            };
            
            btnClearAllMarks.Click += (_, _) =>
            {
                foreach (ListViewItem item in _lstRobotClips.Items) item.Checked = false;
            };
            // Add sorting on column click (skip delete checkbox column)
            _lstRobotClips.ColumnClick += (_, e) =>
            {
                var col = e.Column;
                if (col == 0) return; // Skip delete checkbox column
                
                // Toggle sort direction: if same column, reverse; otherwise start ascending
                var currentSort = _lstRobotClips.Tag as (int Column, SortOrder Order)?;
                SortOrder newOrder;
                if (currentSort.HasValue && currentSort.Value.Column == col)
                {
                    newOrder = currentSort.Value.Order == SortOrder.Ascending ? SortOrder.Descending : SortOrder.Ascending;
                }
                else
                {
                    newOrder = SortOrder.Ascending;
                }
                
                _lstRobotClips.ListViewItemSorter = new ListViewItemComparer(col, newOrder);
                _lstRobotClips.Sort();
                _lstRobotClips.Tag = (col, newOrder); // Store current sort column and order
            };

            _lstRobotClips.KeyDown += (_, e) =>
            {
                if (e.KeyCode == Keys.Delete) { DeleteIds(SelClips(), "selected clips"); e.Handled = true; }
                else if (e.Control && e.KeyCode == Keys.A)
                {
                    foreach (ListViewItem i in _lstRobotClips.Items) i.Selected = true;
                    e.Handled = true;
                }
            };

            // Track last clicked item for Shift+Click range selection
            int? _lastClickedIndex = null;

            // Update delete checkbox text based on checked state
            _lstRobotClips.ItemChecked += (_, e) =>
            {
                if (e.Item.Index >= 0 && e.Item.Index < _lstRobotClips.Items.Count)
                {
                    var item = _lstRobotClips.Items[e.Item.Index];
                    // Update the checkbox column text based on checked state
                    item.SubItems[0].Text = item.Checked ? "✓" : "X";
                }
            };

            // Handle Shift+Click for range selection
            _lstRobotClips.MouseDown += (_, e) =>
            {
                if (e.Button == MouseButtons.Left && _lstRobotClips.GetItemAt(e.X, e.Y) is ListViewItem clickedItem)
                {
                    var clickedIndex = clickedItem.Index;
                    
                    // If Shift is pressed and there's a previously clicked item, select range
                    if (Control.ModifierKeys.HasFlag(Keys.Shift) && _lastClickedIndex.HasValue)
                    {
                        int start = Math.Min(_lastClickedIndex.Value, clickedIndex);
                        int end = Math.Max(_lastClickedIndex.Value, clickedIndex);
                        
                        // Preserve checked state of items in range
                        var checkedStates = new Dictionary<int, bool>();
                        for (int i = 0; i < _lstRobotClips.Items.Count; i++)
                        {
                            if (_lstRobotClips.Items[i].Selected)
                            {
                                checkedStates[i] = _lstRobotClips.Items[i].Checked;
                            }
                        }
                        
                        // Select all items in range
                        for (int i = start; i <= end; i++)
                        {
                            _lstRobotClips.Items[i].Selected = true;
                            // Restore checked state if it was previously selected
                            if (checkedStates.ContainsKey(i))
                            {
                                _lstRobotClips.Items[i].Checked = checkedStates[i];
                            }
                        }
                    }
                    
                    _lastClickedIndex = clickedIndex;
                }
            };

            // Reset last clicked index when clicking elsewhere or on header
            _lstRobotClips.MouseClick += (_, e) =>
            {
                if (e.Button == MouseButtons.Left && _lstRobotClips.GetItemAt(e.X, e.Y) == null)
                {
                    _lastClickedIndex = null;
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
            var lv = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, GridLines = true, MultiSelect = multi, HideSelection = false, Margin = new Padding(0, 4, 0, 4), CheckBoxes = true };
            for (int i = 0; i < cols.Length; i += 2) lv.Columns.Add((string)cols[i], (int)cols[i + 1]);
            return lv;
        }

        private void RobotTick()
        {
            if (_robot == null) return;
            if (DateTime.UtcNow - _lastRobotLockoutRefresh >= TimeSpan.FromMinutes(1))
            {
                _lastRobotLockoutRefresh = DateTime.UtcNow;
                _robot.RefreshLockoutCache();
                _robotDirty = true;
            }
            if (_robotScanActive && _robot.EarlySkipDue && _scanner is { State: ScanState.Locked })
            {
                _robot.EndRecording("early-skip");
                _scanner.Resume();
            }

            var r = _robot.Recorder;
            string state = r.IsWriting ? $"REC {r.LockOpenSeconds:F0}s" : r.IsRecording ? "waiting for audio" : "idle";
            string disk = "";
            try
            {
                var root = Path.GetPathRoot(Path.GetFullPath(_robot.Config.ClipFolder));
                if (root != null) disk = $" | disk {new DriveInfo(root).AvailableFreeSpace / 1e9:F0} GB free";
            }
            catch { }
            string whisperStatus = _robot.Whisper.Status;
            string aiStatus = whisperStatus.StartsWith("Downloading ", StringComparison.Ordinal) ||
                              whisperStatus is "Loading Whisper model..." or "Transcribing..."
                ? whisperStatus : _robot.AiStatus;
            string ai = string.IsNullOrEmpty(aiStatus) ? "" : " | " + aiStatus;
            string err = r.LastError is { Length: > 0 } e ? " | " + e : "";
            string lost = _robot.LostAnalysisClips > 0 ? $" | unindexed clips {_robot.LostAnalysisClips}" : "";
            _lblRobotStatus.Text = $"{state} | dropped {r.DroppedBlocks} | AI queue {_robot.AiBacklog}{lost}{disk}{ai}{err}";
            _lblRobotStatus.ForeColor = r.IsWriting ? Color.Firebrick : Color.Gray;

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
                        "X", c.StartUtc.ToLocalTime().ToString("HH:mm:ss"), (c.Hz / 1e6).ToString("0.0000"),
                        (c.Starred ? "* " : "") + c.Class, c.DurationSec.ToString("0"), c.Category ?? "",
                        string.IsNullOrEmpty(c.Transcript) ? c.Status ?? "" : c.Transcript
                    }) { Tag = c.Id, Selected = selClips.Contains(c.Id), Checked = false };
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

        // Custom comparer for ListView sorting
        private class ListViewItemComparer : System.Collections.IComparer
        {
            private readonly int _column;
            private readonly SortOrder _order;

            public ListViewItemComparer(int column, SortOrder order)
            {
                _column = column;
                _order = order;
            }

            public int Compare(object? x, object? y)
            {
                if (x is ListViewItem lx && y is ListViewItem ly)
                {
                    string text1 = _column > 0 ? lx.SubItems[_column].Text : lx.Text;
                    string text2 = _column > 0 ? ly.SubItems[_column].Text : ly.Text;

                    // Try parsing as double for numeric sorting (frequency, duration)
                    if (double.TryParse(text1, out double val1) && double.TryParse(text2, out double val2))
                    {
                        return _order == SortOrder.Ascending ? val1.CompareTo(val2) : val2.CompareTo(val1);
                    }

                    // Otherwise sort as strings
                    var cmp = string.Compare(text1, text2, StringComparison.Ordinal);
                    return _order == SortOrder.Ascending ? cmp : -cmp;
                }
                return 0;
            }
        }
    }
}
