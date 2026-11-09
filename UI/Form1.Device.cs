// Form1 - device lifecycle: open, close, start, stop.
namespace RandySDR
{
    public partial class Form1
    {
        private bool _deviceOpening;
        private bool _closeAfterOpen;
        private Func<Func<int[]>, Task<int[]>> _runDeviceOpen = operation => Task.Run(operation);

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

        private async void btnOpen_Click(object sender, EventArgs e)
        {
            if (_deviceOpening || cmbDevices.SelectedIndex < 0 || cmbDevices.SelectedIndex >= _devices.Count)
                return;

            var device = _devices[cmbDevices.SelectedIndex];
            uint sampleRate = (uint)(numSampleRate.Value * 1_000_000);
            uint centerFrequencyHz = (uint)(numFrequency.Value * 1_000_000);
            uint tunerBandwidthHz = (uint)numTunerBandwidth.Value * 1000;
            int ppm = (int)numPpm.Value;
            bool tunerAgc = chkTunerAGC.Checked;
            bool rtlAgc = chkRtlAGC.Checked;
            int manualGainTenthdB = trackTunerGain.Value;
            bool offsetTuning = chkOffsetTuning.Checked;
            int samplingMode = GetSamplingMode();
            var openingDevice = new RtlSdrWrapper();
            bool handedOff = false;
            Exception? openingError = null;
            _deviceOpening = true;
            btnOpen.Enabled = false;
            cmbDevices.Enabled = false;
            lblStatus.Text = $"Opening device: {device.Name}...";
            lblStatus.ForeColor = Color.Orange;
            UpdateCardStates();

            try
            {
                int[] gains = await _runDeviceOpen(() =>
                {
                    if (!openingDevice.OpenDevice(device.Index))
                        throw new InvalidOperationException("The SDR device could not be opened.");
                    if (!openingDevice.ConfigureReceiveChain(sampleRate, centerFrequencyHz, tunerAgc, rtlAgc,
                        manualGainTenthdB, ppm, tunerBandwidthHz, offsetTuning, false, samplingMode, out string error))
                        throw new InvalidOperationException(error);
                    int[] availableGains = openingDevice.GetTunerGains();
                    if (!tunerAgc && availableGains.Length > 0)
                    {
                        Array.Sort(availableGains);
                        openingDevice.SetTunerGain(availableGains[availableGains.Length / 2]);
                    }
                    return availableGains;
                });

                if (_closeAfterOpen) return;

                // Initialize spectrum analyzer
                _spectrum = chkSpectrumEnabled.Checked ? new SpectrumAnalyzer(4096) : null;
                if (_spectrum != null)
                    _spectrumTimer.Start();

                _pipeline = new ReceiverPipeline((int)sampleRate);
                _robot?.AttachPipeline(_pipeline);
                UpdateDemodulatorSettings();

                _sdr = openingDevice;
                handedOff = true;
                LoadGains(gains);
                UpdateControlsState(true);
                lblStatus.Text = $"Connected (PPM: {numPpm.Value})";
                lblStatus.ForeColor = Color.Green;
            }
            catch (Exception ex)
            {
                _robot?.AttachPipeline(null);
                _pipeline?.Dispose();
                _pipeline = null;
                _spectrumTimer.Stop();
                _spectrum = null;
                handedOff = false;
                _sdr = new RtlSdrWrapper();
                openingError = ex;
            }
            finally
            {
                if (!handedOff)
                    await Task.Run(openingDevice.CloseDevice);
                _deviceOpening = false;
                if (_closeAfterOpen)
                {
                    Close();
                }
                else
                {
                    UpdateControlsState(handedOff);
                    UpdateCardStates();
                }
            }
            if (openingError != null && !_closeAfterOpen)
            {
                lblStatus.Text = "Device opening failed";
                lblStatus.ForeColor = Color.Red;
                MessageBox.Show($"Error opening device: {openingError.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnClose_Click(object sender, EventArgs e)
        {
            StopScan();
            timerSignalLevel.Stop();
            
            _robot?.AttachPipeline(null);
            _pipeline?.Dispose();
            _pipeline = null;
            _noiseFloorDb = float.NaN;
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
                ApplyLinkedSquelch();

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
            ApplyLinkedSquelch();
            lblStatus.Text = "Stopped";
            lblStatus.ForeColor = Color.Orange;
            progressSignal.Value = 0;
            lblFreqOffset.Text = "Offset: -.--- kHz";
        }
    }
}
