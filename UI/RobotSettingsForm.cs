// RobotSettingsForm - modal dialog for Ollama, Whisper, recording/retention and early-skip settings.
using System.Diagnostics;
using Whisper.net.Ggml;

namespace RandySDR
{
    /// <summary>
    /// Extension methods for Control to safely update UI from background threads.
    /// </summary>
    public static class ControlExtensions
    {
        public static void InvokeOnUIThread<T>(this T? control, Action<T> action) where T : Control
        {
            if (control is null) return;
            if (control.InvokeRequired)
                control.BeginInvoke(action);
            else
                action(control);
        }
    }

    public sealed class RobotSettingsForm : Form
    {
        private readonly RobotController _robot;
        private readonly TextBox _url = new(), _folder = new(), _whisperPrompt = new(), _extraPrompt = new();
        private readonly ComboBox _model = new(), _whisperModel = new(), _keepAlive = new();
        private readonly TextBox _language = new();
        private readonly NumericUpDown _temp = Num(2, 0, 2, 0.1m), _ctx = Num(0, 512, 131072, 512), _timeout = Num(0, 5, 600, 5);
        private readonly NumericUpDown _noSpeech = Num(2, 0, 1, 0.05m), _threads = Num(0, 0, 64, 1);
        private readonly NumericUpDown _voiceDays = Num(0, 1, 3650, 1), _junkDays = Num(0, 1, 3650, 1);
        private readonly NumericUpDown _maxClip = Num(0, 10, 3600, 10), _minFree = Num(1, 0, 10000, 1), _earlySkip = Num(1, 1, 60, 0.5m);
        private readonly NumericUpDown _speechSkipSeconds = Num(0, 6, 120, 1);
        private readonly CheckBox _speechSkip = new() { Text = "Enable experimental speech-based early skip", AutoSize = true };
        private readonly NumericUpDown _triageSeconds = Num(2, 0.1m, 10, 0.1m), _triageVoice = Num(2, 0.01m, 1, 0.01m), _triageCarrier = Num(2, 0, 1, 0.01m);
        private readonly CheckBox _think = new() { Text = "Let the model \"think\" first (slower; for reasoning models)", AutoSize = true };
        private readonly Label _ollamaStatus = new() { AutoSize = true, ForeColor = Color.Gray };
        private CancellationTokenSource? _refreshCts;
        
        // Log viewer controls
        private TextBox? _logViewer;
        private ComboBox? _logFilter;
        private ComboBox? _logSource;
        private Label? _logStatus;
        private Label? _startupWarning;

        public RobotSettingsForm(RobotController robot)
        {
            _robot = robot;
            Text = "Settings";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimizeBox = true;
            ShowInTaskbar = true;
            MinimumSize = new Size(500, 400);
            Font = new Font("Segoe UI", 9f);

            var tabs = new TabControl { Dock = DockStyle.Fill };
            tabs.TabPages.Add(OllamaPage());
            tabs.TabPages.Add(WhisperPage());
            tabs.TabPages.Add(RecordingPage());
            tabs.TabPages.Add(TriagePage());
            tabs.TabPages.Add(LogsPage());

            var ok = new Button { Text = "OK", Width = 90, Height = 32 };
            var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Width = 90, Height = 32 };
            ok.Click += (_, _) => { if (Apply()) { DialogResult = DialogResult.OK; Close(); } };
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, Height = 48, Padding = new Padding(8) };
            buttons.Controls.AddRange(new Control[] { cancel, ok });
            Controls.Add(tabs);
            Controls.Add(buttons);
            AcceptButton = ok;
            CancelButton = cancel;

            LoadValues(robot.Config);
            Shown += async (_, _) => await RefreshModelsAsync();
            FormClosed += (_, _) => _refreshCts?.Cancel();
        }

        private static NumericUpDown Num(int dec, decimal min, decimal max, decimal inc) =>
            new() { DecimalPlaces = dec, Minimum = min, Maximum = max, Increment = inc, Width = 120 };

        private static TableLayoutPanel Grid()
        {
            var t = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, AutoScroll = true, Padding = new Padding(12) };
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            return t;
        }

        private static void Row(TableLayoutPanel t, string label, Control c, string? hint = null)
        {
            int r = t.RowCount++;
            t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            t.Controls.Add(new Label { Text = label, AutoSize = true, Margin = new Padding(0, 8, 8, 0) }, 0, r);
            if (c is TextBox or ComboBox) c.Dock = DockStyle.Fill;
            c.Margin = new Padding(0, 4, 0, 4);
            if (hint == null) { t.Controls.Add(c, 1, r); return; }
            var p = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Dock = DockStyle.Fill, Margin = Padding.Empty };
            p.Controls.Add(c);
            p.Controls.Add(new Label { Text = hint, AutoSize = true, ForeColor = Color.Gray, MaximumSize = new Size(400, 0), Margin = new Padding(0, 0, 0, 4) });
            t.Controls.Add(p, 1, r);
        }

        private TabPage OllamaPage()
        {
            var page = new TabPage("Ollama (classification)");
            var g = Grid();
            Row(g, "Server URL", _url);
            
            var checkBtn = new Button { Text = "Check Server", AutoSize = true };
            checkBtn.Click += async (s, e) =>
            {
                checkBtn.Enabled = false;
                checkBtn.Text = "Checking...";
                var statusMsg = await OllamaClient.CheckServerStatusAsync(_url.Text, CancellationToken.None);
                MessageBox.Show(this, statusMsg, "Ollama Server Status", MessageBoxButtons.OK, 
                    statusMsg.Contains("running") ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
                checkBtn.Enabled = true;
                checkBtn.Text = "Check Server";
            };
            
            var refresh = new Button { Text = "Refresh models", AutoSize = true };
            refresh.Click += async (_, _) => await RefreshModelsAsync();
            _model.DropDownStyle = ComboBoxStyle.DropDown;
            Row(g, "Model", _model, "Pick an installed model or type a name.");
            
            var row1 = new FlowLayoutPanel { AutoSize = true, Margin = Padding.Empty };
            row1.Controls.AddRange(new Control[] { checkBtn, refresh });
            Row(g, "", row1);
            
            var row2 = new FlowLayoutPanel { AutoSize = true, Margin = Padding.Empty };
            row2.Controls.Add(_ollamaStatus);
            _ollamaStatus.Margin = new Padding(8, 8, 0, 0);
            Row(g, "Status", row2);
            Row(g, "Temperature", _temp, "0 gives the most repeatable classifications.");
            Row(g, "Context (tokens)", _ctx, "Larger values use more VRAM.");
            Row(g, "Request timeout (s)", _timeout);
            _keepAlive.DropDownStyle = ComboBoxStyle.DropDown;
            _keepAlive.Items.AddRange(new object[] { "0", "5m", "30m", "2h", "-1" });
            Row(g, "Keep model loaded", _keepAlive, "How long Ollama keeps the model in VRAM after a request (-1 = forever, 0 = unload at once).");
            Row(g, "", _think);
            _extraPrompt.Multiline = true;
            _extraPrompt.Height = 90;
            _extraPrompt.ScrollBars = ScrollBars.Vertical;
            Row(g, "Extra guidance", _extraPrompt, "Optional text added to the prompt, e.g. local agency names or jargon.");
            page.Controls.Add(g);
            return page;
        }

        private TabPage WhisperPage()
        {
            var page = new TabPage("Whisper (transcription)");
            var g = Grid();
            _whisperModel.DropDownStyle = ComboBoxStyle.DropDownList;
            _whisperModel.Items.AddRange(Enum.GetNames<GgmlType>().Cast<object>().ToArray());
            Row(g, "Model", _whisperModel, "Downloaded on first use to %AppData%\\RandySDR\\models. LargeV3Turbo is best on an RTX GPU; Base/Small are faster and smaller.");
            Row(g, "Language", _language, "ISO code such as en, es, de; leave blank for automatic detection.");
            Row(g, "No-speech threshold", _noSpeech, "Whisper's decoder threshold; a separate 0.7 segment filter also applies.");
            Row(g, "CPU threads (0 = auto)", _threads);
            _whisperPrompt.Multiline = true;
            _whisperPrompt.Height = 90;
            _whisperPrompt.ScrollBars = ScrollBars.Vertical;
            Row(g, "Vocabulary hint", _whisperPrompt, "Optional call signs, place names, or 10-codes to bias recognition.");
            page.Controls.Add(g);
            return page;
        }

        private TabPage RecordingPage()
        {
            var page = new TabPage("Recording && retention");
            var g = Grid();
            var browse = new Button { Text = "Browse...", AutoSize = true };
            browse.Click += (_, _) =>
            {
                using var d = new FolderBrowserDialog { SelectedPath = _folder.Text, UseDescriptionForTitle = true, Description = "Clip folder" };
                if (d.ShowDialog(this) == DialogResult.OK) _folder.Text = d.SelectedPath;
            };
            var folderRow = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Dock = DockStyle.Fill, Margin = Padding.Empty };
            folderRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            folderRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            _folder.Dock = DockStyle.Fill;
            folderRow.Controls.Add(_folder, 0, 0);
            folderRow.Controls.Add(browse, 1, 0);
            Row(g, "Clip folder", folderRow);
            Row(g, "Keep voice clips (days)", _voiceDays, "Starred clips are never purged.");
            Row(g, "Keep carrier/noise clips (days)", _junkDays);
            Row(g, "Max clip length (s)", _maxClip, "Long transmissions are split at this length.");
            Row(g, "Stop recording below (GB free)", _minFree);
            Row(g, "Squelch early-skip (s)", _earlySkip, "Time before skipping a lock that has almost no open-squelch audio; enable it on the Scan Robot card.");
            Row(g, "", _speechSkip, "Experimental: may miss weak or delayed speech. Stops evaluating a lock after speech-like or uncertain audio or dropped blocks.");
            Row(g, "No-speech screening (s)", _speechSkipSeconds, "Minimum captured audio screened before skipping. Detection is heuristic, not a guarantee of no voice.");
            page.Controls.Add(g);
            return page;
        }

        private TabPage TriagePage()
        {
            var page = new TabPage("Audio triage");
            var g = Grid();
            Row(g, "Minimum voice seconds", _triageSeconds, "Minimum speech-like audio to label a clip voice.");
            Row(g, "Minimum voice ratio", _triageVoice, "Fraction of the clip that must sound speech-like.");
            Row(g, "Maximum carrier ratio", _triageCarrier, "Below this speech-like fraction, clips may be labeled carrier or noise. Higher values can miss voice.");
            Row(g, "", new Label { Text = "These heuristics are not verified speech detection. Changing them can affect automatic lockouts; use suggested lockouts until tested with real recordings.", AutoSize = true, MaximumSize = new Size(390, 0) });
            page.Controls.Add(g);
            return page;
        }

        private TabPage LogsPage()
        {
            var page = new TabPage("Logs");
            
            // Use TableLayoutPanel for proper layout control
            var table = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(8),
                ColumnCount = 1,
                RowCount = 4,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink
            };
            
            // Row 0: Log source selector and controls - fixed height
            var controlsRow = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 50,
                Padding = new Padding(0, 0, 0, 8),
                FlowDirection = FlowDirection.LeftToRight,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink
            };
            
            var logSource = _logSource = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 150,
                Margin = new Padding(0, 4, 8, 0)
            };
            logSource.Items.AddRange(new[] { "Robot Workflow Logs", "Startup Errors", "All Logs" });
            logSource.SelectedIndex = 0;
            
            var filterLevel = _logFilter = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 120,
                Margin = new Padding(0, 4, 8, 0)
            };
            filterLevel.Items.AddRange(new[] { "All", "DEBUG", "INFO", "WARN", "ERROR" });
            filterLevel.SelectedIndex = 0;
            
            var btnRefresh = new Button { Text = "Refresh", Width = 80, Height = 28, Margin = new Padding(0, 4, 8, 0) };
            var btnClear = new Button { Text = "Clear", Width = 60, Height = 28, Margin = new Padding(0, 4, 8, 0) };
            var btnSave = new Button { Text = "Save to File", Width = 100, Height = 28, Margin = new Padding(0, 4, 8, 0) };
            
            controlsRow.Controls.AddRange(new Control[] { logSource, filterLevel, btnRefresh, btnClear, btnSave });
            
            // Row 1: Status label - fixed height
            _logStatus = new Label
            {
                Dock = DockStyle.Top,
                Height = 25,
                Padding = new Padding(0, 0, 0, 8),
                ForeColor = Color.Gray,
                AutoSize = true,
                Text = "Ready"
            };
            
            // Row 2: Warning label for startup errors - fixed height
            _startupWarning = new Label
            {
                Dock = DockStyle.Top,
                Height = 30,
                Padding = new Padding(0, 0, 0, 8),
                ForeColor = Color.OrangeRed,
                AutoSize = true,
                Text = "",
                Font = new Font("Segoe UI", 9f, FontStyle.Bold)
            };
            
            // Row 3: Log viewer - takes remaining space
            _logViewer = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Both,
                Font = new Font("Consolas", 9f),
                BackColor = Color.Black,
                ForeColor = Color.LightGreen,
                AcceptsTab = true
            };
            
            table.Controls.Add(controlsRow, 0, 0);
            table.Controls.Add(_logStatus, 0, 1);
            table.Controls.Add(_startupWarning, 0, 2);
            table.Controls.Add(_logViewer, 0, 3);
            
            // Set column to fill width, rows to auto/fill
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize, 0f));
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize, 0f));
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize, 0f));
            table.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            
            page.Controls.Add(table);
            
            // Log source change handler
            logSource.SelectedIndexChanged += (s, e) =>
            {
                var source = logSource.SelectedItem?.ToString() ?? "Robot Workflow Logs";
                if (source == "Startup Errors")
                {
                    _logFilter.Enabled = false;
                    btnClear.Enabled = false;
                    _startupWarning.Text = "Startup errors are read-only. Use 'All Logs' or 'Robot Workflow Logs' to clear.";
                }
                else
                {
                    _logFilter.Enabled = true;
                    btnClear.Enabled = true;
                    _startupWarning.Text = "";
                }
                btnRefresh.PerformClick();
            };
            
            // Refresh button handler
            btnRefresh.Click += (s, e) =>
            {
                var source = logSource.SelectedItem?.ToString() ?? "Robot Workflow Logs";
                var levelFilter = filterLevel.SelectedItem?.ToString() ?? "All";
                
                if (source == "Startup Errors")
                {
                    LoadStartupErrors();
                }
                else if (source == "All Logs")
                {
                    LoadAllLogs(levelFilter);
                }
                else
                {
                    var entries = _robot.Logger.GetLast(500);
                    
                    var filtered = levelFilter == "All" 
                        ? entries 
                        : entries.Where(e => e.Level == levelFilter).ToList();
                    
                    _logViewer!.Text = FormatLogEntries(filtered);
                    _logStatus!.Text = $"Showing {filtered.Count} of {entries.Count} robot workflow entries";
                }
            };
            
            // Clear button handler
            btnClear.Click += (s, e) =>
            {
                var source = logSource.SelectedItem?.ToString() ?? "Robot Workflow Logs";
                if (source == "Startup Errors")
                {
                    MessageBox.Show(this, "Cannot clear startup errors. They are read-only system logs.", 
                        "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                
                _robot.Logger.Clear();
                _logViewer!.Text = "Log buffer cleared. Click Refresh to reload.";
                _logStatus!.Text = "Buffer cleared";
            };
            
            // Save button handler
            btnSave.Click += async (s, e) =>
            {
                var source = logSource.SelectedItem?.ToString() ?? "Robot Workflow Logs";
                var levelFilter = filterLevel.SelectedItem?.ToString() ?? "All";
                
                string content;
                string fileName;
                
                if (source == "Startup Errors")
                {
                    content = GetStartupErrorContent();
                    fileName = $"startup-errors-{DateTime.UtcNow:yyyyMMdd-HHmmss}.txt";
                }
                else if (source == "All Logs")
                {
                    content = FormatAllLogs(levelFilter);
                    fileName = $"all-logs-{DateTime.UtcNow:yyyyMMdd-HHmmss}.txt";
                }
                else
                {
                    var entries = _robot.Logger.GetLast(1000);
                    var filtered = levelFilter == "All" 
                        ? entries 
                        : entries.Where(e => e.Level == levelFilter).ToList();
                    
                    content = FormatLogEntries(filtered);
                    fileName = $"robot-logs-{DateTime.UtcNow:yyyyMMdd-HHmmss}.txt";
                }
                
                using var dlg = new SaveFileDialog
                {
                    Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
                    FileName = fileName,
                    DefaultExt = "txt"
                };
                
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    try
                    {
                        await File.WriteAllTextAsync(dlg.FileName, content);
                        MessageBox.Show(this, $"Log saved to {dlg.FileName}", "Success", 
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(this, $"Failed to save log: {ex.Message}", "Error", 
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            };
            
            // Initial load
            btnRefresh.PerformClick();
            
            return page;
        }
        
        private void LoadStartupErrors()
        {
            var errorLog = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "startup-error.log");
            if (!File.Exists(errorLog))
            {
                _logViewer?.InvokeOnUIThread(c => c.Text = "No startup errors found.");
                _logStatus?.InvokeOnUIThread(c => c.Text = "No errors recorded");
                _startupWarning?.InvokeOnUIThread(c => c.Text = "");
                return;
            }
            
            var content = File.ReadAllText(errorLog);
            if (string.IsNullOrWhiteSpace(content))
            {
                _logViewer?.InvokeOnUIThread(c => c.Text = "Startup error log is empty.");
                _logStatus?.InvokeOnUIThread(c => c.Text = "No errors recorded");
                _startupWarning?.InvokeOnUIThread(c => c.Text = "");
                return;
            }
            
            _logViewer?.InvokeOnUIThread(c => c.Text = content.Trim());
            var lineCount = content.Split('\n').Length;
            _logStatus?.InvokeOnUIThread(c => c.Text = $"Startup error log loaded ({lineCount} lines)");
            _startupWarning?.InvokeOnUIThread(c => c.Text = "⚠️ Startup errors detected. Review the stack trace above for diagnostics.");
        }
        
        private string GetStartupErrorContent()
        {
            var errorLog = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "startup-error.log");
            if (!File.Exists(errorLog))
                return "No startup errors found.\n\n";
            
            var content = File.ReadAllText(errorLog);
            if (string.IsNullOrWhiteSpace(content))
                return "Startup error log is empty.\n\n";
            
            return $"=== Startup Error Log ===\n\n{content.Trim()}\n\n";
        }
        
        private void LoadAllLogs(string levelFilter)
        {
            var robotEntries = _robot.Logger.GetLast(1000);
            var filteredRobot = levelFilter == "All" 
                ? robotEntries 
                : robotEntries.Where(e => e.Level == levelFilter).ToList();
            
            var startupErrorContent = GetStartupErrorContent();
            var hasErrors = !startupErrorContent.Contains("No startup errors") && 
                           !startupErrorContent.Contains("empty");
            
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== ALL LOGS ===\n");
            sb.AppendLine($"Robot Workflow Logs - {filteredRobot.Count} entries\n");
            sb.AppendLine(new string('=', 80));
            sb.AppendLine();
            
            foreach (var entry in filteredRobot)
            {
                var timestamp = entry.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff");
                sb.AppendLine($"[{timestamp}] [{entry.Level,-5}] [{entry.Source,-18}] {entry.Message}");
                if (entry.Details != null)
                {
                    foreach (var line in entry.Details.Split('\n'))
                    {
                        sb.AppendLine($"  {line}");
                    }
                }
                sb.AppendLine();
            }
            
            if (hasErrors)
            {
                sb.AppendLine("\n" + new string('=', 80));
                sb.AppendLine("\n=== STARTUP ERRORS ===\n");
                sb.AppendLine(startupErrorContent);
            }
            
            var finalText = sb.ToString();
            _logViewer?.InvokeOnUIThread(c => c.Text = finalText);
            var statusText = $"Robot: {filteredRobot.Count} entries";
            if (hasErrors) statusText += " | Startup errors present";
            _logStatus?.InvokeOnUIThread(c => c.Text = statusText);
        }
        
        private string FormatAllLogs(string levelFilter)
        {
            var robotEntries = _robot.Logger.GetLast(1000);
            var filteredRobot = levelFilter == "All" 
                ? robotEntries 
                : robotEntries.Where(e => e.Level == levelFilter).ToList();
            
            var startupErrorContent = GetStartupErrorContent();
            var hasErrors = !startupErrorContent.Contains("No startup errors") && 
                           !startupErrorContent.Contains("empty");
            
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== ALL LOGS ===\n");
            sb.AppendLine($"Robot Workflow Logs - {filteredRobot.Count} entries\n");
            sb.AppendLine(new string('=', 80));
            sb.AppendLine();
            
            foreach (var entry in filteredRobot)
            {
                var timestamp = entry.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff");
                sb.AppendLine($"[{timestamp}] [{entry.Level,-5}] [{entry.Source,-18}] {entry.Message}");
                if (entry.Details != null)
                {
                    foreach (var line in entry.Details.Split('\n'))
                    {
                        sb.AppendLine($"  {line}");
                    }
                }
                sb.AppendLine();
            }
            
            if (hasErrors)
            {
                sb.AppendLine("\n" + new string('=', 80));
                sb.AppendLine("\n=== STARTUP ERRORS ===\n");
                sb.AppendLine(startupErrorContent);
            }
            
            return sb.ToString();
        }

        private static string FormatLogEntries(List<RobotLogEntry> entries)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Robot Workflow Logs - {entries.Count} entries");
            sb.AppendLine(new string('=', 80));
            sb.AppendLine();
            
            foreach (var entry in entries)
            {
                var timestamp = entry.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff");
                sb.AppendLine($"[{timestamp}] [{entry.Level,-5}] [{entry.Source,-18}] {entry.Message}");
                if (entry.Details != null)
                {
                    foreach (var line in entry.Details.Split('\n'))
                    {
                        sb.AppendLine($"  {line}");
                    }
                }
                sb.AppendLine();
            }
            
            return sb.ToString();
        }

        private void LoadValues(RobotConfig c)
        {
            _url.Text = c.OllamaUrl;
            _model.Text = c.OllamaModel;
            _temp.Value = Clamp(_temp, (decimal)c.OllamaTemperature);
            _ctx.Value = Clamp(_ctx, c.OllamaContextTokens);
            _timeout.Value = Clamp(_timeout, c.OllamaTimeoutSeconds);
            _keepAlive.Text = c.OllamaKeepAlive;
            _think.Checked = c.OllamaThink;
            _extraPrompt.Text = c.OllamaExtraPrompt;
            int wi = _whisperModel.FindStringExact(c.WhisperModel);
            _whisperModel.SelectedIndex = wi >= 0 ? wi : _whisperModel.FindStringExact("LargeV3Turbo");
            _language.Text = c.WhisperLanguage;
            _noSpeech.Value = Clamp(_noSpeech, (decimal)c.WhisperNoSpeechThreshold);
            _threads.Value = Clamp(_threads, c.WhisperThreads);
            _whisperPrompt.Text = c.WhisperPrompt;
            _folder.Text = c.ClipFolder;
            _voiceDays.Value = Clamp(_voiceDays, c.VoiceRetentionDays);
            _junkDays.Value = Clamp(_junkDays, c.CarrierRetentionDays);
            _maxClip.Value = Clamp(_maxClip, c.MaxClipSeconds);
            _minFree.Value = Clamp(_minFree, (decimal)c.MinFreeGb);
            _earlySkip.Value = Clamp(_earlySkip, (decimal)c.EarlySkipSeconds);
            _speechSkip.Checked = c.SpeechEarlySkip;
            _speechSkipSeconds.Value = Clamp(_speechSkipSeconds, (decimal)c.SpeechEarlySkipSeconds);
            _triageSeconds.Value = Clamp(_triageSeconds, (decimal)c.TriageMinSpeechSeconds);
            _triageVoice.Value = Clamp(_triageVoice, (decimal)c.TriageMinSpeechRatio);
            _triageCarrier.Value = Clamp(_triageCarrier, (decimal)c.TriageMaxCarrierRatio);
        }

        private static decimal Clamp(NumericUpDown n, decimal v) => Math.Clamp(v, n.Minimum, n.Maximum);

        private async Task RefreshModelsAsync()
        {
            _refreshCts?.Cancel();
            var cts = _refreshCts = new CancellationTokenSource();
            _ollamaStatus.Text = "Checking Ollama...";
            var models = await _robot.Ollama.ListModelsAsync(_url.Text, cts.Token);
            if (cts.IsCancellationRequested || IsDisposed) return;
            if (models == null) { _ollamaStatus.Text = "Ollama not reachable at this URL"; return; }
            string current = _model.Text;
            _model.Items.Clear();
            _model.Items.AddRange(models.Cast<object>().ToArray());
            _model.Text = current;
            _ollamaStatus.Text = $"{models.Count} model(s) installed" + (models.Contains(current) ? "" : " - selected model not found");
        }

        private bool Apply()
        {
            if (_triageCarrier.Value > _triageVoice.Value)
            {
                MessageBox.Show(this, "Maximum carrier ratio cannot exceed minimum voice ratio.", "Settings", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            var c = _robot.Config;
            c.OllamaUrl = _url.Text.Trim().Length > 0 ? _url.Text.Trim() : "http://localhost:11434";
            c.OllamaModel = _model.Text.Trim();
            c.OllamaTemperature = (double)_temp.Value;
            c.OllamaContextTokens = (int)_ctx.Value;
            c.OllamaTimeoutSeconds = (int)_timeout.Value;
            c.OllamaKeepAlive = _keepAlive.Text.Trim().Length > 0 ? _keepAlive.Text.Trim() : "5m";
            c.OllamaThink = _think.Checked;
            c.OllamaExtraPrompt = _extraPrompt.Text;
            c.WhisperModel = _whisperModel.Text;
            c.WhisperLanguage = _language.Text.Trim();
            c.WhisperNoSpeechThreshold = (double)_noSpeech.Value;
            c.WhisperThreads = (int)_threads.Value;
            c.WhisperPrompt = _whisperPrompt.Text;
            if (_folder.Text.Trim().Length > 0) c.ClipFolder = _folder.Text.Trim();
            c.VoiceRetentionDays = (int)_voiceDays.Value;
            c.CarrierRetentionDays = (int)_junkDays.Value;
            c.MaxClipSeconds = (int)_maxClip.Value;
            c.MinFreeGb = (double)_minFree.Value;
            c.EarlySkipSeconds = (double)_earlySkip.Value;
            c.SpeechEarlySkip = _speechSkip.Checked;
            c.SpeechEarlySkipSeconds = (double)_speechSkipSeconds.Value;
            c.TriageMinSpeechSeconds = (double)_triageSeconds.Value;
            c.TriageMinSpeechRatio = (double)_triageVoice.Value;
            c.TriageMaxCarrierRatio = (double)_triageCarrier.Value;
            c.Save();
            _robot.ApplyConfig();
            return true;
        }
    }
}
