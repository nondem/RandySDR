// Form1 - audio/demodulation: volume, squelch, de-emphasis, mode, I/Q options.
namespace RandySDR
{
    public partial class Form1
    {
        private void trackVolume_ValueChanged(object? sender, EventArgs e)
        {
            lblVolume.Text = $"Volume: {trackVolume.Value}%";
            UpdateDemodulatorSettings();
        }

        private void trackSquelch_Scroll(object sender, EventArgs e)
        {
            _manualSquelchValue = trackSquelch.Value;
            UpdateDemodulatorSettings();
            UpdateSquelchUi();
        }

        private Panel _squelchMarker = null!;
        private Button _btnSquelchAuto = null!;
        private Button _btnMute = null!;
        private bool _userMuted = true;
        private bool _squelchAutoSampling;

        private void UpdateSquelchUi()
        {
            int v = trackSquelch.Value;
            float? threshold = CurrentSquelchThreshold();
            lblSquelch.Text = threshold is float db ? $"Squelch: {db:F0} dB" : "Squelch: Off";
            if (_squelchMarker != null)
            {
                _squelchMarker.Visible = v > 0;
                _squelchMarker.Left = Math.Max(0, Math.Min(progressSignal.Width * v / 100 - 1, progressSignal.Width - 2));
            }
        }

        /// <summary>Samples the in-channel level for ~1 s (tune to a quiet spot first) and sets the squelch just above it.</summary>
        private async void btnSquelchAuto_Click(object? sender, EventArgs e)
        {
            var pipeline = _pipeline;
            var demodulator = _demodulator;
            if (pipeline == null || demodulator == null || !pipeline.IsRunning || SquelchLinked
                || (_scanner != null && _scanner.State != ScanState.Idle)) return;
            long retuneTick = _lastRetuneTick;
            int calibrationVersion = _squelchCalibrationVersion;
            _squelchAutoSampling = true;
            ApplyLinkedSquelch();
            float peak = -120f;
            for (int i = 0; i < 20; i++)
            {
                await Task.Delay(50);
                if (IsDisposed) return;
                if (_pipeline != pipeline || _demodulator != demodulator || !pipeline.IsRunning || SquelchLinked
                    || (_scanner != null && _scanner.State != ScanState.Idle) || _lastRetuneTick != retuneTick
                    || _squelchCalibrationVersion != calibrationVersion)
                {
                    _squelchAutoSampling = false;
                    ApplyLinkedSquelch();
                    lblStatus.Text = "Auto squelch canceled: receiver settings changed.";
                    return;
                }
                peak = Math.Max(peak, demodulator.ChannelDb);
            }
            if (peak > -119f)
                _manualSquelchValue = Math.Clamp(FmDemodulator.DbToSquelchPercent(peak + 4f), 1, 100);
            else
                lblStatus.Text = "Auto squelch could not measure a signal level.";
            _squelchAutoSampling = false;
            ApplyLinkedSquelch();
        }

        /// <summary>Rebuilds the Audio group as a table: Volume / Squelch (+Auto) / Signal meter with squelch marker.</summary>
        private void BuildAudioPanel()
        {
            var table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 3, Margin = Padding.Empty };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 85));
            for (int r = 0; r < 3; r++) table.RowStyles.Add(new RowStyle(SizeType.Percent, 33.3f));

            groupBoxAudio.Controls.Clear();
            foreach (var l in new[] { lblVolume, lblSquelch, lblSignalStrength })
            {
                l.AutoSize = false;
                l.Dock = DockStyle.Fill;
                l.TextAlign = ContentAlignment.MiddleLeft;
            }
            lblSignalStrength.Text = "Signal:";
            foreach (var t in new[] { trackVolume, trackSquelch })
            {
                t.Dock = DockStyle.Fill;
                t.Margin = Padding.Empty;
            }
            progressSignal.Dock = DockStyle.Fill;
            progressSignal.Margin = new Padding(4, 8, 4, 8);
            progressSignal.Minimum = 0;
            progressSignal.Maximum = 100;

            _squelchMarker = new Panel { BackColor = Color.FromArgb(255, 183, 77), Width = 3, Dock = DockStyle.None, Height = progressSignal.Height };
            progressSignal.Controls.Add(_squelchMarker);
            progressSignal.Resize += (_, _) => { _squelchMarker.Height = progressSignal.Height; UpdateSquelchUi(); };

            _btnMute = new Button { Text = _userMuted ? "Unmute" : "Mute", Dock = DockStyle.Fill, Margin = new Padding(4, 6, 0, 6) };
            _btnMute.Click += btnMute_Click;
            var muteTip = new ToolTip();
            muteTip.SetToolTip(_btnMute, "Mute or unmute audio without changing the volume.");

            _btnSquelchAuto = new Button { Text = "Auto", Dock = DockStyle.Fill, Margin = new Padding(4, 6, 0, 6) };
            _btnSquelchAuto.Click += btnSquelchAuto_Click;
            new ToolTip().SetToolTip(_btnSquelchAuto, "While receiving, tune to a quiet spot and click to set manual squelch 4 dB above the measured peak. Unavailable during scanning or linked mode.");
            new ToolTip().SetToolTip(trackSquelch, "0 = off. Squelch opens when the in-channel signal exceeds the orange marker on the Signal bar.");

            table.Controls.Add(lblVolume, 0, 0);
            table.Controls.Add(trackVolume, 1, 0);
            table.Controls.Add(_btnMute, 2, 0);
            table.Controls.Add(lblSquelch, 0, 1);
            table.Controls.Add(trackSquelch, 1, 1);
            table.Controls.Add(_btnSquelchAuto, 2, 1);
            table.Controls.Add(lblSignalStrength, 0, 2);
            table.Controls.Add(progressSignal, 1, 2);
            table.SetColumnSpan(progressSignal, 2);
            groupBoxAudio.Controls.Add(table);
            UpdateSquelchUi();
        }

        private void chkDeEmphasis_CheckedChanged(object sender, EventArgs e)
        {
            UpdateDemodulatorSettings();
        }

        private void chkSwapIQ_CheckedChanged(object sender, EventArgs e)
        {
            if (_demodulator != null)
            {
                _demodulator.SwapIQ = chkSwapIQ.Checked;
                lblStatus.Text = chkSwapIQ.Checked ? "I/Q Swapped" : "I/Q Normal";
                lblStatus.ForeColor = Color.Blue;
                System.Diagnostics.Debug.WriteLine($"I/Q Swap: {(chkSwapIQ.Checked ? "ON" : "OFF")}");
            }
        }

        private void chkInvertDemod_CheckedChanged(object sender, EventArgs e)
        {
            if (_demodulator != null)
            {
                _demodulator.InvertDemod = chkInvertDemod.Checked;
                lblStatus.Text = chkInvertDemod.Checked ? "Demod Inverted" : "Demod Normal";
                lblStatus.ForeColor = Color.Blue;
                System.Diagnostics.Debug.WriteLine($"Demod Invert: {(chkInvertDemod.Checked ? "ON" : "OFF")}");
            }
        }

        private void btnMute_Click(object? sender, EventArgs e)
        {
            _userMuted = !_userMuted;
            _btnMute.Text = _userMuted ? "Unmute" : "Mute";
            UpdateDemodulatorSettings();
        }

        private void UpdateDemodulatorSettings()
        {
            if (_pipeline != null)
            {
                _pipeline.AudioVolume = trackVolume.Value / 50f;
                _pipeline.AudioMuted = _userMuted || _scanMuted;
            }

            if (_demodulator == null)
                return;

            _demodulator.Volume = 1f;
            _demodulator.SquelchThresholdDb = CurrentSquelchThreshold();
            _demodulator.DeEmphasisEnabled = chkDeEmphasis.Checked;
            _demodulator.Mode = GetDemodulationMode();
        }

        private int[] _supportedGains = Array.Empty<int>();

        private void rdoDemodMode_CheckedChanged(object sender, EventArgs e)
        {
            if (sender is RadioButton rb && !rb.Checked)
                return;

            UpdateDemodulatorSettings();

            if (_demodulator != null)
            {
                lblStatus.Text = $"Demod: {GetDemodulationMode()}";
                lblStatus.ForeColor = Color.Blue;
            }
        }
    }
}
