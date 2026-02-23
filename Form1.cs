namespace RandySDR
{
    public partial class Form1 : Form
    {
        private RtlSdrWrapper _sdr;
        private List<DeviceInfo> _devices = new List<DeviceInfo>();
        private FmDemodulator? _demodulator;
        private AudioPlayer? _audioPlayer;
        private SpectrumAnalyzer? _spectrum;
        private int _sampleCount = 0;
        private bool _isScanning = false;
        private byte[]? _lastIqData = null;

        public Form1()
        {
            InitializeComponent();
            _sdr = new RtlSdrWrapper();
            LoadConnectedDevices();
            InitializeControls();
        }

        private void InitializeControls()
        {
            numFrequency.Value = 98.5m;
            numSampleRate.Value = 2.4m; // 2.4 MSPS is optimal for FM broadcast
            numPpm.Value = 0;
            trackVolume.Value = 50;
            trackSquelch.Value = 0;
            chkDeEmphasis.Checked = true;
            chkTunerAGC.Checked = true;
            chkRtlAGC.Checked = false;
            UpdateControlsState(false);
        }

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
                
                System.Diagnostics.Debug.WriteLine($"=== OPENING DEVICE WITH PPM: {numPpm.Value} ===");
                
                // Set PPM correction FIRST before anything else
                _sdr.SetPpmCorrection((int)numPpm.Value);
                
                uint sampleRate = (uint)(numSampleRate.Value * 1_000_000);
                _sdr.SetSampleRate(sampleRate);
                
                // Initialize spectrum analyzer
                _spectrum = new SpectrumAnalyzer(2048);
                
                // Set frequency
                _sdr.SetFrequency((uint)(numFrequency.Value * 1_000_000));
                
                // Set gains
                _sdr.SetTunerAgc(chkTunerAGC.Checked);
                _sdr.SetRtlAgc(chkRtlAGC.Checked);

                _demodulator = new FmDemodulator((int)sampleRate, 48000);
                UpdateDemodulatorSettings();
                _audioPlayer = new AudioPlayer(48000);

                LoadGains();
                UpdateControlsState(true);
                lblStatus.Text = $"Connected (PPM: {numPpm.Value})";
                lblStatus.ForeColor = Color.Green;
                
                System.Diagnostics.Debug.WriteLine($"Device opened successfully");
                System.Diagnostics.Debug.WriteLine($"  Frequency: {numFrequency.Value:F3} MHz");
                System.Diagnostics.Debug.WriteLine($"  Sample Rate: {sampleRate / 1e6:F2} MSPS");
                System.Diagnostics.Debug.WriteLine($"  PPM: {numPpm.Value}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening device: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnClose_Click(object sender, EventArgs e)
        {
            _isScanning = false;
            timerSignalLevel.Stop();
            _audioPlayer?.Stop();
            _audioPlayer?.Dispose();
            _audioPlayer = null;
            _demodulator = null;
            _spectrum = null;
            _sdr.CloseDevice();
            UpdateControlsState(false);
            lblStatus.Text = "Disconnected";
            lblStatus.ForeColor = Color.Gray;
            progressSignal.Value = 0;
            lblFreqOffset.Text = "Offset: -.--- kHz";
        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            try
            {
                _audioPlayer?.Start();
                _audioPlayer?.GenerateTestTone(440);
                _sdr.Start(OnDataReceived);
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
                
                _sampleCount = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error starting: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            _isScanning = false;
            timerSignalLevel.Stop();
            _sdr.Stop();
            _audioPlayer?.Stop();
            btnStart.Enabled = true;
            btnStop.Enabled = false;
            lblStatus.Text = "Stopped";
            lblStatus.ForeColor = Color.Orange;
            progressSignal.Value = 0;
            lblFreqOffset.Text = "Offset: -.--- kHz";
        }

        private void OnDataReceived(byte[] iqData)
        {
            if (_demodulator == null || _audioPlayer == null)
                return;

            _sampleCount++;
            
            // Store last IQ data for diagnostics
            _lastIqData = iqData;
            
            // Demodulate and play audio
            float[] audioSamples = _demodulator.Demodulate(iqData);
            
            if (audioSamples.Length > 0)
                _audioPlayer.AddSamples(audioSamples);
        }

        private void btnDiagnostics_Click(object sender, EventArgs e)
        {
            if (_demodulator == null)
            {
                MessageBox.Show("Please OPEN and START the device first!", "Diagnostics", 
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string diagnostics = FrequencyDiagnostics.DiagnoseFrequencyIssue(
                _demodulator.SignalLevel, 
                (int)numPpm.Value, 
                (double)numFrequency.Value,
                (double)numSampleRate.Value);

            // Add spectrum info if available
            if (_spectrum != null && _lastIqData != null)
            {
                var spectrum = _spectrum.ProcessIQ(_lastIqData);
                float offset = _spectrum.GetFrequencyOffset((int)(numSampleRate.Value * 1_000_000));
                
                diagnostics += $"\n=== SPECTRUM ANALYSIS ===\n";
                diagnostics += $"Peak frequency offset: {offset / 1000:F1} kHz\n";
                diagnostics += $"\nIf offset is NOT near 0 kHz:";
                diagnostics += $"\n• Signal may be at {(double)numFrequency.Value + offset / 1_000_000:F3} MHz";
                diagnostics += $"\n• Try tuning to {(double)numFrequency.Value + offset / 1_000_000:F3} MHz";
                diagnostics += $"\n• Or adjust PPM to bring offset closer to 0";
            }

            diagnostics += $"\n\n=== CURRENT SETTINGS ===\n";
            diagnostics += $"Sample Rate: {numSampleRate.Value:F1} MSPS";
            if (numSampleRate.Value != 2.4m)
                diagnostics += " ⚠️ (Use 2.4 MSPS!)";
            diagnostics += $"\nTuner AGC: {(chkTunerAGC.Checked ? "ON" : "OFF")}";
            if (!chkTunerAGC.Checked)
                diagnostics += $" (Gain: {trackTunerGain.Value / 10.0:F1} dB)";
            diagnostics += $"\nRTL AGC: {(chkRtlAGC.Checked ? "ON ⚠️ (try OFF)" : "OFF ✓")}";
            diagnostics += $"\nVolume: {trackVolume.Value}%";
            diagnostics += $"\nSquelch: {trackSquelch.Value}%";
            diagnostics += $"\nDe-emphasis: {(chkDeEmphasis.Checked ? "ON ✓" : "OFF")}";

            MessageBox.Show(diagnostics, "Frequency Diagnostics", 
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void numFrequency_ValueChanged(object sender, EventArgs e)
        {
            _sdr?.SetFrequency((uint)(numFrequency.Value * 1_000_000));
            lblFreqDisplay.Text = $"{numFrequency.Value:F3} MHz";
            
            if (_sdr != null && !_sdr.IsTestMode && _sdr.IsRunning)
            {
                lblStatus.Text = $"✓ Tuned to {numFrequency.Value:F3} MHz";
                System.Diagnostics.Debug.WriteLine($"Frequency changed to {numFrequency.Value:F3} MHz");
            }
        }

        private void numSampleRate_ValueChanged(object sender, EventArgs e)
        {
            if (_sdr != null)
            {
                uint sampleRate = (uint)(numSampleRate.Value * 1_000_000);
                _sdr.SetSampleRate(sampleRate);
                
                if (_demodulator != null)
                {
                    // Recreate demodulator with new sample rate
                    _demodulator = new FmDemodulator((int)sampleRate, 48000);
                    UpdateDemodulatorSettings();
                    
                    System.Diagnostics.Debug.WriteLine($"Demodulator recreated for {sampleRate / 1e6:F2} MSPS");
                    
                    // Warn if sample rate is non-standard
                    if (numSampleRate.Value != 2.4m && numSampleRate.Value != 2.0m && numSampleRate.Value != 1.8m)
                    {
                        lblStatus.Text = $"⚠ Non-standard sample rate! Recommended: 2.4 MSPS";
                        lblStatus.ForeColor = Color.Orange;
                    }
                }
                
                if (_spectrum != null)
                {
                    _spectrum = new SpectrumAnalyzer(2048);
                }
            }
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

        private async void btnScan_Click(object sender, EventArgs e)
        {
            if (_isScanning)
            {
                _isScanning = false;
                btnScan.Text = "Scan FM";
                return;
            }

            if (!_sdr.IsRunning)
            {
                MessageBox.Show("Please START receiving first!", "Scan", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _isScanning = true;
            btnScan.Text = "Stop";
            var foundStations = new List<decimal>();
            
            // Scan FM band 88-108 MHz in 0.1 MHz steps
            for (decimal freq = 88.0m; freq <= 108.0m && _isScanning; freq += 0.1m)
            {
                numFrequency.Value = freq;
                await Task.Delay(300); // Wait 300ms per frequency
                
                // Check signal strength
                if (_demodulator != null && _demodulator.SignalLevel > 0.25f)
                {
                    foundStations.Add(freq);
                    System.Diagnostics.Debug.WriteLine($"Station found at {freq:F1} MHz (Signal: {_demodulator.SignalLevel * 100:F0}%)");
                }
            }
            
            _isScanning = false;
            btnScan.Text = "Scan FM";
            
            if (foundStations.Count > 0)
            {
                string stations = string.Join(", ", foundStations.Select(f => $"{f:F1}"));
                var result = MessageBox.Show($"Found {foundStations.Count} station(s):\n{stations}\n\nTune to first station?", 
                    "Scan Complete", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                
                if (result == DialogResult.Yes)
                {
                    numFrequency.Value = foundStations[0];
                }
            }
            else
            {
                MessageBox.Show("No stations found!\n\nTry:\n• Adjusting PPM correction\n• Checking antenna\n• Turning OFF RTL AGC\n• Increasing tuner gain", 
                    "Scan Complete", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void chkTunerAGC_CheckedChanged(object sender, EventArgs e)
        {
            _sdr?.SetTunerAgc(chkTunerAGC.Checked);
            trackTunerGain.Enabled = !chkTunerAGC.Checked;
        }

        private void chkRtlAGC_CheckedChanged(object sender, EventArgs e)
        {
            _sdr?.SetRtlAgc(chkRtlAGC.Checked);
        }

        private void trackTunerGain_Scroll(object sender, EventArgs e)
        {
            if (_sdr != null && !chkTunerAGC.Checked)
            {
                _sdr.SetTunerGain(trackTunerGain.Value);
                lblTunerGain.Text = $"Tuner Gain: {trackTunerGain.Value / 10.0:F1} dB";
            }
        }

        private void trackVolume_Scroll(object sender, EventArgs e)
        {
            lblVolume.Text = $"Volume: {trackVolume.Value}%";
            UpdateDemodulatorSettings();
        }

        private void trackSquelch_Scroll(object sender, EventArgs e)
        {
            lblSquelch.Text = $"Squelch: {trackSquelch.Value}%";
            UpdateDemodulatorSettings();
        }

        private void chkDeEmphasis_CheckedChanged(object sender, EventArgs e)
        {
            UpdateDemodulatorSettings();
        }

        private void timerSignalLevel_Tick(object sender, EventArgs e)
        {
            if (_demodulator != null)
            {
                int signalPercent = (int)(_demodulator.SignalLevel * 100);
                signalPercent = Math.Clamp(signalPercent, 0, 100);
                progressSignal.Value = signalPercent;
                
                // Update frequency offset if spectrum analyzer is available
                if (_spectrum != null && _lastIqData != null && _lastIqData.Length >= 4096)
                {
                    try
                    {
                        var spectrum = _spectrum.ProcessIQ(_lastIqData);
                        float offsetHz = _spectrum.GetFrequencyOffset((int)(numSampleRate.Value * 1_000_000));
                        lblFreqOffset.Text = $"Offset: {offsetHz / 1000:+0.0;-0.0;0.0} kHz";
                        
                        if (Math.Abs(offsetHz) > 10000) // More than 10 kHz off
                        {
                            lblFreqOffset.ForeColor = Color.Red;
                        }
                        else if (Math.Abs(offsetHz) > 2000) // More than 2 kHz off
                        {
                            lblFreqOffset.ForeColor = Color.Orange;
                        }
                        else
                        {
                            lblFreqOffset.ForeColor = Color.Green;
                        }
                    }
                    catch { }
                }
            }
        }

        private void UpdateDemodulatorSettings()
        {
            if (_demodulator == null)
                return;

            _demodulator.Volume = trackVolume.Value / 50.0f;
            _demodulator.SquelchLevel = trackSquelch.Value / 100.0f;
            _demodulator.DeEmphasisEnabled = chkDeEmphasis.Checked;
        }

        private void LoadGains()
        {
            var gains = _sdr.GetTunerGains();
            if (gains != null && gains.Length > 0)
            {
                trackTunerGain.Minimum = gains.Min();
                trackTunerGain.Maximum = gains.Max();
                trackTunerGain.Value = gains[gains.Length / 2];
                lblTunerGain.Text = $"Tuner Gain: {trackTunerGain.Value / 10.0:F1} dB";
            }
        }

        private void UpdateControlsState(bool deviceOpen)
        {
            btnOpen.Enabled = !deviceOpen;
            btnClose.Enabled = deviceOpen;
            btnStart.Enabled = deviceOpen;
            btnStop.Enabled = false;
            btnScan.Enabled = deviceOpen;
            btnDiagnostics.Enabled = deviceOpen;
            
            numFrequency.Enabled = deviceOpen;
            numSampleRate.Enabled = deviceOpen;
            numPpm.Enabled = deviceOpen;
            btnPpm0.Enabled = deviceOpen;
            btnPpmMinus50.Enabled = deviceOpen;
            btnPpmPlus50.Enabled = deviceOpen;
            chkTunerAGC.Enabled = deviceOpen;
            chkRtlAGC.Enabled = deviceOpen;
            trackTunerGain.Enabled = deviceOpen && !chkTunerAGC.Checked;
            trackVolume.Enabled = deviceOpen;
            trackSquelch.Enabled = deviceOpen;
            chkDeEmphasis.Enabled = deviceOpen;
            cmbDevices.Enabled = !deviceOpen;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _isScanning = false;
            timerSignalLevel.Stop();
            _audioPlayer?.Stop();
            _audioPlayer?.Dispose();
            _sdr?.CloseDevice();
            base.OnFormClosing(e);
        }

        public List<DeviceInfo> GetDevices()
        {
            return RtlSdrWrapper.GetConnectedDevices();
        }
    }
}
