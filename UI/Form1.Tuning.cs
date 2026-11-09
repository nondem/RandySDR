// Form1 - tuning: frequency, sample rate, PPM, tuner bandwidth, offset tuning, scan, direct sampling.
namespace RandySDR
{
    public partial class Form1
    {
        /// <summary>Retune the radio and discard stale I/Q and audio from the previous frequency.</summary>
        private void RetuneRadio(uint hz)
        {
            _sdr?.SetFrequency(hz);
            _lastRetuneTick = Environment.TickCount64;
            _pipeline?.FlushForRetune(clearAudio: true);
        }

        private void numFrequency_ValueChanged(object sender, EventArgs e)
        {
            uint newFreq = (uint)(numFrequency.Value * 1_000_000);
            RetuneRadio(newFreq);
            lblFreqDisplay.Text = $"{numFrequency.Value:F3}";

            if (_sdr != null && !_sdr.IsTestMode)
            {
                lblStatus.Text = $"✓ Tuned to {numFrequency.Value:F3}";
            }
        }

        private void numSampleRate_ValueChanged(object sender, EventArgs e)
        {
            InvalidateSquelchCalibration();
            if (_sdr != null)
            {
                uint sampleRate = (uint)(numSampleRate.Value * 1_000_000);
                _sdr.SetSampleRate(sampleRate);

                if (_pipeline != null)
                {
                    // Recreate demodulator with new sample rate
                    _pipeline.ReplaceDemodulator((int)sampleRate);
                    UpdateDemodulatorSettings();

                    System.Diagnostics.Debug.WriteLine($"Demodulator recreated for {sampleRate / 1e6:F2} MSPS");

                    // Warn if sample rate is non-standard
                    if (numSampleRate.Value != 2.4m && numSampleRate.Value != 2.0m && numSampleRate.Value != 1.8m)
                    {
                        lblStatus.Text = $"⚠ Non-standard sample rate! Recommended: 2.4 MSPS";
                        lblStatus.ForeColor = Color.Orange;
                    }
                }

                if (_spectrum != null && chkSpectrumEnabled.Checked)
                {
                    _spectrum = new SpectrumAnalyzer(4096);
                }
            }
        }

        private void numTunerBandwidth_ValueChanged(object sender, EventArgs e)
        {
            InvalidateSquelchCalibration();
            int bandwidthKHz = (int)numTunerBandwidth.Value;
            if (trackRxBandwidth.Value != bandwidthKHz)
                trackRxBandwidth.Value = bandwidthKHz;

            lblRxBandwidth.Text = $"RX BW: {bandwidthKHz} kHz";
            _sdr?.SetTunerBandwidth((uint)bandwidthKHz * 1000);
        }

        private void trackRxBandwidth_Scroll(object sender, EventArgs e)
        {
            int bandwidthKHz = trackRxBandwidth.Value;
            if (numTunerBandwidth.Value != bandwidthKHz)
                numTunerBandwidth.Value = bandwidthKHz;

            lblRxBandwidth.Text = $"RX BW: {bandwidthKHz} kHz";
        }

        private void chkOffsetTuning_CheckedChanged(object sender, EventArgs e)
        {
            _sdr?.SetOffsetTuning(chkOffsetTuning.Checked);
        }

        private void numPpm_ValueChanged(object sender, EventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"PPM changed to {numPpm.Value}");
            _sdr?.SetPpmCorrection((int)numPpm.Value);

            if (_sdr != null && !_sdr.IsTestMode)
            {
                lblStatus.Text = $"PPM = {numPpm.Value}";
                lblStatus.ForeColor = Color.Green;

                // Re-tune to apply PPM correction
                uint freq = (uint)(numFrequency.Value * 1_000_000);
                _sdr.SetFrequency(freq);
                System.Diagnostics.Debug.WriteLine($"Re-tuned to {numFrequency.Value:F3} MHz with PPM {numPpm.Value}");
            }
        }

        private void btnPpm0_Click(object sender, EventArgs e)
        {
            numPpm.Value = 0;
        }

        private void btnPpmMinus50_Click(object sender, EventArgs e)
        {
            numPpm.Value = -50;
        }

        private void btnPpmPlus50_Click(object sender, EventArgs e)
        {
            numPpm.Value = 50;
        }

        private void btnPpmHelp_Click(object sender, EventArgs e)
        {
            MessageBox.Show(PpmCalibrator.GetCalibrationHelp(), "PPM Calibration Help",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void btnScan_Click(object sender, EventArgs e) => ToggleScan();

        private void rdoSamplingMode_CheckedChanged(object sender, EventArgs e)
        {
            if (sender is RadioButton rb && !rb.Checked)
                return;

            int mode = GetSamplingMode();
            bool applied = _sdr?.SetSamplingMode(mode) ?? false;

            if (_sdr != null && _sdr.IsRunning)
            {
                // Retune and flush after changing RF path so effect is immediate.
                uint freqHz = (uint)(numFrequency.Value * 1_000_000m);
                _sdr.SetFrequency(freqHz);

                lblStatus.Text = mode switch
                {
                    1 => "Sampling: Direct I",
                    2 => "Sampling: Direct Q",
                    _ => "Sampling: Quadrature"
                };
                lblStatus.ForeColor = applied ? Color.Blue : Color.Red;

                if (!applied)
                {
                    MessageBox.Show(
                        "Sampling mode change failed.\n\n" +
                        "This usually means your loaded rtlsdr.dll does not support this API " +
                        "or the mode is unsupported by the active tuner path.",
                        "Sampling Mode",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }

                if (mode != 0 && numFrequency.Value > 30m)
                {
                    MessageBox.Show(
                        "Direct Sampling modes are for LF/HF use (typically <= 30 MHz).\n\n" +
                        "At FM broadcast frequencies, use Quadrature mode.",
                        "Sampling Mode Guidance",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
            }
        }
    }
}
