// Form1 - gain: tuner AGC/manual gain, RTL AGC, ADC overload indicator, signal meter.
namespace RandySDR
{
    public partial class Form1
    {
        private void chkTunerAGC_CheckedChanged(object sender, EventArgs e)
        {
            InvalidateSquelchCalibration();
            _sdr?.SetTunerAgc(chkTunerAGC.Checked);
            trackTunerGain.Enabled = btnClose.Enabled && !chkTunerAGC.Checked;
            lblTunerGain.Text = chkTunerAGC.Checked
                ? "Tuner Gain: AUTO"
                : $"Tuner Gain: {trackTunerGain.Value / 10.0:F1} dB";

            // When switching to manual gain, immediately apply the current slider value.
            if (_sdr != null && !chkTunerAGC.Checked)
                _sdr.SetTunerGain(trackTunerGain.Value);
        }

        private void chkRtlAGC_CheckedChanged(object sender, EventArgs e)
        {
            InvalidateSquelchCalibration();
            _sdr?.SetRtlAgc(chkRtlAGC.Checked);
        }

        private void trackTunerGain_Scroll(object sender, EventArgs e)
        {
            InvalidateSquelchCalibration();
            int snapped = SnapGain(trackTunerGain.Value);
            if (trackTunerGain.Value != snapped)
                trackTunerGain.Value = snapped;

            if (_sdr != null && !chkTunerAGC.Checked)
            {
                _sdr.SetTunerGain(snapped);
                lblTunerGain.Text = $"Tuner Gain: {snapped / 10.0:F1} dB";
            }
        }

        private void timerSignalLevel_Tick(object sender, EventArgs e)
        {
            if (_demodulator != null)
            {
                int signalPercent = FmDemodulator.DbToSquelchPercent(_demodulator.ChannelDb);
                progressSignal.Value = signalPercent;
                lblSignalStrength.Text = _demodulator.ChannelDb > -119f ? $"Signal: {_demodulator.ChannelDb:F0} dB" : "Signal:";

                UpdateOverloadIndicator();
            }
        }

        // Fraction of I/Q bytes pinned at the 8-bit ADC rails indicates RF/ADC overload.
        private void UpdateOverloadIndicator()
        {
            var iq = _lastIqData;
            if (iq == null || iq.Length == 0)
                return;

            int clipped = 0;
            for (int i = 0; i < iq.Length; i++)
                if (iq[i] <= 1 || iq[i] >= 254) clipped++;

            double pct = 100.0 * clipped / iq.Length;
            if (pct > 1.0)
            {
                lblClip.Text = $"ADC OVERLOAD {pct:F1}% - lower gain";
                lblClip.ForeColor = Color.Red;
            }
            else
            {
                lblClip.Text = $"ADC clip: {pct:F2}%";
                lblClip.ForeColor = pct > 0.1 ? Color.DarkOrange : Color.Green;
            }
        }

        private int SnapGain(int requested)
        {
            if (_supportedGains.Length == 0) return requested;
            return _supportedGains.OrderBy(g => Math.Abs(g - requested)).First();
        }

        private void LoadGains()
        {
            var gains = _sdr.GetTunerGains();
            if (gains != null && gains.Length > 0)
            {
                _supportedGains = gains.OrderBy(g => g).ToArray();
                trackTunerGain.Minimum = _supportedGains.First();
                trackTunerGain.Maximum = _supportedGains.Last();
                trackTunerGain.TickFrequency = Math.Max(1, (trackTunerGain.Maximum - trackTunerGain.Minimum) / 10);
                trackTunerGain.Value = _supportedGains[_supportedGains.Length / 2];
                lblTunerGain.Text = $"Tuner Gain: {trackTunerGain.Value / 10.0:F1} dB";

                // Ensure manual gain mode actually applies a gain value on open.
                if (!chkTunerAGC.Checked)
                {
                    _sdr.SetTunerGain(trackTunerGain.Value);
                }
            }
        }
    }
}
