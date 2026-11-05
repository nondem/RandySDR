// Form1 - audio/demodulation: volume, squelch, de-emphasis, mode, I/Q options.
namespace RandySDR
{
    public partial class Form1
    {
        private void trackVolume_Scroll(object sender, EventArgs e)
        {
            lblVolume.Text = $"Volume: {trackVolume.Value}%";
            UpdateDemodulatorSettings();
        }

        private void trackSquelch_Scroll(object sender, EventArgs e)
        {
            UpdateDemodulatorSettings();
            UpdateSquelchUi();
        }

        private Panel _squelchMarker = null!;
        private Button _btnSquelchAuto = null!;

        private void UpdateSquelchUi()
        {
            int v = trackSquelch.Value;
            lblSquelch.Text = v == 0 ? "Squelch: Off" : $"Squelch: {FmDemodulator.SquelchPercentToDb(v):F0} dB";
            if (_squelchMarker != null)
            {
                _squelchMarker.Visible = v > 0;
                _squelchMarker.Left = Math.Max(0, Math.Min(progressSignal.Width * v / 100 - 1, progressSignal.Width - 2));
            }
        }

        /// <summary>Samples the in-channel level for ~1 s (tune to a quiet spot first) and sets the squelch just above it.</summary>
        private async void btnSquelchAuto_Click(object? sender, EventArgs e)
        {
            if (_demodulator == null || _pipeline == null) return;
            _btnSquelchAuto.Enabled = false;
            float peak = -120f;
            for (int i = 0; i < 20; i++)
            {
                await Task.Delay(50);
                if (_demodulator == null) break;
                peak = Math.Max(peak, _demodulator.ChannelDb);
            }
            if (peak > -119f)
                trackSquelch.Value = Math.Clamp(FmDemodulator.DbToSquelchPercent(peak + 4f), 1, 100);
            _btnSquelchAuto.Enabled = true;
            UpdateDemodulatorSettings();
            UpdateSquelchUi();
        }

        /// <summary>Rebuilds the Audio group as a table: Volume / Squelch (+Auto) / Signal meter with squelch marker.</summary>
        private void BuildAudioPanel()
        {
            var table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 3, Margin = Padding.Empty };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));
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

            _squelchMarker = new Panel { BackColor = Color.OrangeRed, Width = 3, Dock = DockStyle.None, Height = progressSignal.Height };
            progressSignal.Controls.Add(_squelchMarker);
            progressSignal.Resize += (_, _) => { _squelchMarker.Height = progressSignal.Height; UpdateSquelchUi(); };

            _btnSquelchAuto = new Button { Text = "Auto", Dock = DockStyle.Fill, Margin = new Padding(4, 6, 0, 6) };
            _btnSquelchAuto.Click += btnSquelchAuto_Click;
            new ToolTip().SetToolTip(_btnSquelchAuto, "Tune to a quiet spot (no signal), then click: sets the squelch just above the noise.");
            new ToolTip().SetToolTip(trackSquelch, "0 = off. Squelch opens when the in-channel signal exceeds the orange marker on the Signal bar.");

            table.Controls.Add(lblVolume, 0, 0);
            table.Controls.Add(trackVolume, 1, 0);
            table.SetColumnSpan(trackVolume, 2);
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

        private void UpdateDemodulatorSettings()
        {
            if (_demodulator == null)
                return;

            _demodulator.Volume = _scanMuted ? 0f : trackVolume.Value / 50.0f;
            _demodulator.SquelchThresholdDb = trackSquelch.Value == 0 ? null : FmDemodulator.SquelchPercentToDb(trackSquelch.Value);
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
