// Form1 - device lifecycle: open, close, start, stop.
namespace RandySDR
{
    public partial class Form1
    {
        private void LoadConnectedDevices()
        {
            try
            {
                _devices = RtlSdrWrapper.GetConnectedDevices();
                cmbDevices.Items.Clear();

                if (_devices.Count == 0)
                {
                    cmbDevices.Items.Add("No devices found");
                    cmbDevices.SelectedIndex = 0;
                    btnOpen.Enabled = false;
                }
                else
                {
                    foreach (var device in _devices)
                        cmbDevices.Items.Add(device.ToString());
                    cmbDevices.SelectedIndex = 0;
                    btnOpen.Enabled = true;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading devices: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                cmbDevices.Items.Add("Error loading devices");
                cmbDevices.SelectedIndex = 0;
                btnOpen.Enabled = false;
            }
        }

        private void btnOpen_Click(object sender, EventArgs e)
        {
            try
            {
                if (cmbDevices.SelectedIndex < 0 || cmbDevices.SelectedIndex >= _devices.Count)
                    return;

                var device = _devices[cmbDevices.SelectedIndex];
                _sdr.OpenDevice(device.Index);

                uint sampleRate = (uint)(numSampleRate.Value * 1_000_000);
                uint centerFrequencyHz = (uint)(numFrequency.Value * 1_000_000);
                uint tunerBandwidthHz = (uint)numTunerBandwidth.Value * 1000;
                int ppm = (int)numPpm.Value;
                bool tunerAgc = chkTunerAGC.Checked;
                bool rtlAgc = chkRtlAGC.Checked;
                int manualGainTenthdB = trackTunerGain.Value;
                bool biasTeeEnabled = false; // enable if using active antenna/LNA
                int samplingMode = GetSamplingMode();

                System.Diagnostics.Debug.WriteLine("=== OPENING DEVICE (RTL-SDR V4 SEQUENCE) ===");
                System.Diagnostics.Debug.WriteLine($"  Frequency: {numFrequency.Value:F6} MHz");
                System.Diagnostics.Debug.WriteLine($"  Sample Rate: {sampleRate / 1e6:F3} MSPS");
                System.Diagnostics.Debug.WriteLine($"  Tuner BW: {numTunerBandwidth.Value} kHz");
                System.Diagnostics.Debug.WriteLine($"  Tuner AGC: {tunerAgc}");
                System.Diagnostics.Debug.WriteLine($"  RTL AGC: {rtlAgc}");
                System.Diagnostics.Debug.WriteLine($"  Manual Gain: {manualGainTenthdB / 10.0:F1} dB");
                System.Diagnostics.Debug.WriteLine($"  PPM: {ppm}");
                System.Diagnostics.Debug.WriteLine($"  Bias-Tee: {biasTeeEnabled}");
                System.Diagnostics.Debug.WriteLine($"  Sampling Mode: {samplingMode}");

                _sdr.ConfigureReceiveChain(
                    sampleRate,
                    centerFrequencyHz,
                    tunerAgc,
                    rtlAgc,
                    manualGainTenthdB,
                    ppm,
                    tunerBandwidthHz,
                    chkOffsetTuning.Checked,
                    biasTeeEnabled,
                    samplingMode);

                // Initialize spectrum analyzer
                _spectrum = chkSpectrumEnabled.Checked ? new SpectrumAnalyzer(4096) : null;
                if (_spectrum != null)
                    _spectrumTimer.Start();

                _pipeline = new ReceiverPipeline((int)sampleRate);
                UpdateDemodulatorSettings();

                LoadGains();
                UpdateControlsState(true);
                lblStatus.Text = $"Connected (PPM: {numPpm.Value})";
                lblStatus.ForeColor = Color.Green;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening device: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnClose_Click(object sender, EventArgs e)
        {
            StopScan();
            timerSignalLevel.Stop();
            
            _pipeline?.Dispose();
            _pipeline = null;
            _spectrum = null;
            _spectrumTimer.Stop();
            picSpectrum.Clear();
            _sdr.CloseDevice();
            UpdateControlsState(false);
            lblStatus.Text = "Disconnected";
            lblStatus.ForeColor = Color.Gray;
            progressSignal.Value = 0;
            lblFreqOffset.Text = "Offset: -.--- kHz";
            lblClip.Text = "ADC clip: -.--%";
            lblClip.ForeColor = Color.Gray;
        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            try
            {
                uint sampleRate = (uint)(numSampleRate.Value * 1_000_000);
                uint centerFrequencyHz = (uint)(numFrequency.Value * 1_000_000);

                // CRITICAL FIX #1 & #2: Configure tuner with manual gain and RTL AGC
                // Apply UI gain slider (manual) and RTL AGC checkbox rather than hardcoded values
                if (!_sdr.ConfigureTunerForManualGain(sampleRate, centerFrequencyHz, trackTunerGain.Value, chkRtlAGC.Checked, chkTunerAGC.Checked))
                {
                    MessageBox.Show("Failed to configure tuner for manual gain mode!", "Error", 
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                if (_pipeline == null || !_pipeline.Start(_sdr.GetDeviceHandle(), sampleRate))
                {
                    MessageBox.Show("Failed to start USB read thread!", "Error", 
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                timerSignalLevel.Start();

                btnStart.Enabled = false;
                btnStop.Enabled = true;

                if (_sdr.IsTestMode)
                {
                    lblStatus.Text = "⚠ TEST MODE - No RTL-SDR hardware!";
                    lblStatus.ForeColor = Color.Red;
                }
                else
                {
                    lblStatus.Text = $"✓ RX @ {numFrequency.Value:F3} MHz (PPM: {numPpm.Value})";
                    lblStatus.ForeColor = Color.Blue;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error starting: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            StopScan();
            timerSignalLevel.Stop();
            
            _pipeline?.Stop();
            btnStart.Enabled = true;
            btnStop.Enabled = false;
            lblStatus.Text = "Stopped";
            lblStatus.ForeColor = Color.Orange;
            progressSignal.Value = 0;
            lblFreqOffset.Text = "Offset: -.--- kHz";
        }
    }
}
