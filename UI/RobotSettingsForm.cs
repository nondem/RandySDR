// RobotSettingsForm - modal dialog for Ollama, Whisper, recording/retention and early-skip settings.
using Whisper.net.Ggml;

namespace RandySDR
{
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
        private readonly CheckBox _think = new() { Text = "Let the model \"think\" first (slower; for reasoning models)", AutoSize = true };
        private readonly Label _ollamaStatus = new() { AutoSize = true, ForeColor = Color.Gray };
        private CancellationTokenSource? _refreshCts;

        public RobotSettingsForm(RobotController robot)
        {
            _robot = robot;
            Text = "Settings";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(640, 600);
            Font = new Font("Segoe UI", 9f);

            var tabs = new TabControl { Dock = DockStyle.Fill };
            tabs.TabPages.Add(OllamaPage());
            tabs.TabPages.Add(WhisperPage());
            tabs.TabPages.Add(RecordingPage());

            var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Width = 90, Height = 32 };
            var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Width = 90, Height = 32 };
            ok.Click += (_, _) => Apply();
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
            var refresh = new Button { Text = "Refresh models", AutoSize = true };
            refresh.Click += async (_, _) => await RefreshModelsAsync();
            _model.DropDownStyle = ComboBoxStyle.DropDown;
            Row(g, "Model", _model, "Pick an installed model or type a name.");
            var row = new FlowLayoutPanel { AutoSize = true, Margin = Padding.Empty };
            row.Controls.AddRange(new Control[] { refresh, _ollamaStatus });
            _ollamaStatus.Margin = new Padding(8, 8, 0, 0);
            Row(g, "", row);
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
            Row(g, "No-speech threshold", _noSpeech, "Higher discards more doubtful segments as silence.");
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
            Row(g, "Early-skip after (s)", _earlySkip, "Dead-carrier time before the scan resumes (needs the early-skip option on the Scan Robot card).");
            page.Controls.Add(g);
            return page;
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

        private void Apply()
        {
            var c = _robot.Config;
            string oldWhisper = c.WhisperModel;
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
            c.Save();
            if (!string.Equals(oldWhisper, c.WhisperModel, StringComparison.OrdinalIgnoreCase)) _robot.Whisper.Reload();
            _robot.ApplyConfig();
        }
    }
}
