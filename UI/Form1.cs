// Form1: constructor, control initialisation, enable/disable state, shutdown.
namespace RandySDR
{
    public partial class Form1 : Form
    {
        private RtlSdrWrapper _sdr;
        private List<DeviceInfo> _devices = new List<DeviceInfo>();
        private ReceiverPipeline? _pipeline; // USB read -> DSP -> audio; exists while a device is open
        private FmDemodulator? _demodulator => _pipeline?.Demodulator;
        private byte[]? _lastIqData => _pipeline?.LastIqData;
        private SpectrumAnalyzer? _spectrum;
        private readonly System.Windows.Forms.Timer _spectrumTimer = new() { Interval = 50 };

        public Form1()
        {
            InitializeComponent();
            _sdr = new RtlSdrWrapper();
            LoadConnectedDevices();
            InitializeControls();
            BuildMainLayout();
            BuildScannerPanel();
            ApplyVisualPolish();
        }

        private int GetSamplingMode()
        {
            if (rdoDirectSamplingI.Checked) return 1;
            if (rdoDirectSamplingQ.Checked) return 2;
            return 0; // Quadrature
        }

        private DemodulationMode GetDemodulationMode()
        {
            if (rdoDemodUsb.Checked) return DemodulationMode.Usb;
            if (rdoDemodLsb.Checked) return DemodulationMode.Lsb;
            if (rdoDemodRaw.Checked) return DemodulationMode.Raw;
            if (rdoDemodNarrowFm.Checked) return DemodulationMode.NarrowFm;
            if (rdoDemodAm.Checked) return DemodulationMode.Am;
            if (rdoDemodCw.Checked) return DemodulationMode.Cw;
            return DemodulationMode.WideFm;
        }

        private void InitializeControls()
        {
            // Default to 103.1 FM for easy testing
            numFrequency.Value = 103.1m;
            numSampleRate.Value = 2.4m; // 2.4 MSPS is optimal for FM broadcast
            
            // Normalize numPpm TrackBar range to include -100 to 100
            if (numPpm.Maximum < 100) numPpm.Maximum = 100;
            if (numPpm.Minimum > -100) numPpm.Minimum = -100;
            numPpm.Value = 0;
            numTunerBandwidth.Minimum = 1;
            numTunerBandwidth.Maximum = 200;
            numTunerBandwidth.Value = 200;
            trackRxBandwidth.Minimum = 1;
            trackRxBandwidth.Maximum = 200;
            trackRxBandwidth.Value = (int)numTunerBandwidth.Value;
            lblRxBandwidth.Text = $"RX BW: {numTunerBandwidth.Value} kHz";
            chkOffsetTuning.Checked = false;

            // Some designer states can leave TrackBars with default ranges.
            // Normalize ranges before assigning startup values.
            if (trackVolume.Maximum < 100) trackVolume.Maximum = 100;
            if (trackVolume.Minimum > 0) trackVolume.Minimum = 0;
            if (trackSquelch.Maximum < 100) trackSquelch.Maximum = 100;
            if (trackSquelch.Minimum > 0) trackSquelch.Minimum = 0;

            trackVolume.Value = Math.Clamp(50, trackVolume.Minimum, trackVolume.Maximum);
            trackSquelch.Value = Math.Clamp(0, trackSquelch.Minimum, trackSquelch.Maximum);

            chkDeEmphasis.Checked = true;
            chkTunerAGC.Checked = false;
            chkRtlAGC.Checked = false;
            rdoQuadrature.Checked = true;
            rdoDemodWideFm.Checked = true;
            // Spectrum analyzer OFF by default for easier testing
            chkSpectrumEnabled.Checked = false;
            _spectrumTimer.Tick += SpectrumTimer_Tick;
            UpdateControlsState(false);
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
            numTunerBandwidth.Enabled = deviceOpen;
            chkOffsetTuning.Enabled = deviceOpen;
            btnPpm0.Enabled = deviceOpen;
            btnPpmMinus50.Enabled = deviceOpen;
            btnPpmPlus50.Enabled = deviceOpen;
            chkTunerAGC.Enabled = deviceOpen;
            chkRtlAGC.Enabled = deviceOpen;
            trackTunerGain.Enabled = deviceOpen && !chkTunerAGC.Checked;
            trackVolume.Enabled = deviceOpen;
            trackSquelch.Enabled = deviceOpen;
            chkDeEmphasis.Enabled = deviceOpen;
            chkSpectrumEnabled.Enabled = deviceOpen;
            trackRxBandwidth.Enabled = deviceOpen;
            rdoDemodWideFm.Enabled = deviceOpen;
            rdoDemodNarrowFm.Enabled = deviceOpen;
            rdoDemodAm.Enabled = deviceOpen;
            rdoDemodUsb.Enabled = deviceOpen;
            rdoDemodLsb.Enabled = deviceOpen;
            rdoDemodRaw.Enabled = deviceOpen;
            rdoDemodCw.Enabled = deviceOpen;
            rdoQuadrature.Enabled = deviceOpen;
            rdoDirectSamplingI.Enabled = deviceOpen;
            rdoDirectSamplingQ.Enabled = deviceOpen;
            chkSwapIQ.Enabled = deviceOpen;
            chkInvertDemod.Enabled = deviceOpen;
            cmbDevices.Enabled = !deviceOpen;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            StopScan();
            _scanSettings.Save();
            timerSignalLevel.Stop();
            _pipeline?.Dispose();
            _pipeline = null;
            _sdr?.CloseDevice();
            base.OnFormClosing(e);
        }

        public List<DeviceInfo> GetDevices()
        {
            return RtlSdrWrapper.GetConnectedDevices();
        }

        private void Form1_Load(object sender, EventArgs e) => FitToScreen();
    }
}
