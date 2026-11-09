namespace RandySDR
{
    partial class Form1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            if (disposing)
            {
                _robotTimer.Stop();
                _robot?.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            groupBox1 = new CardGroupBox();
            cmbDevices = new ComboBox();
            btnOpen = new Button();
            btnClose = new Button();
            btnStart = new Button();
            btnStop = new Button();
            groupBox2 = new CardGroupBox();
            label1 = new Label();
            numFrequency = new FrequencySelector();
            numSampleRate = new NumericUpDown();
            label2 = new Label();
            numPpm = new NumericUpDown();
            label3 = new Label();
            labelBandwidth = new Label();
            numTunerBandwidth = new NumericUpDown();
            btnPpmHelp = new Button();
            btnPpm0 = new Button();
            btnPpmMinus50 = new Button();
            btnPpmPlus50 = new Button();
            btnScan = new Button();
            btnDiagnostics = new Button();
            chkOffsetTuning = new CheckBox();
            lblFreqDisplay = new Label();
            chkTunerAGC = new CheckBox();
            chkRtlAGC = new CheckBox();
            groupBox3 = new CardGroupBox();
            lblTunerGain = new Label();
            trackTunerGain = new TrackBar();
            lblClip = new Label();
            lblRxBandwidth = new Label();
            trackRxBandwidth = new TrackBar();
            rdoQuadrature = new RadioButton();
            rdoDirectSamplingI = new RadioButton();
            rdoDirectSamplingQ = new RadioButton();
            chkSpecAuto = new CheckBox();
            lblSpecZoom = new Label();
            trackSpecZoom = new TrackBar();
            lblSpecRange = new Label();
            trackSpecRange = new TrackBar();
            lblSpecRef = new Label();
            trackSpecRef = new TrackBar();
            chkDeEmphasis = new CheckBox();
            groupBox4 = new CardGroupBox();
            rdoDemodCw = new RadioButton();
            rdoDemodRaw = new RadioButton();
            rdoDemodLsb = new RadioButton();
            rdoDemodUsb = new RadioButton();
            rdoDemodNarrowFm = new RadioButton();
            rdoDemodWideFm = new RadioButton();
            rdoDemodAm = new RadioButton();
            lblFreqOffset = new Label();
            groupBoxAudio = new CardGroupBox();
            lblVolume = new Label();
            trackVolume = new TrackBar();
            lblSquelch = new Label();
            trackSquelch = new TrackBar();
            lblSignalStrength = new Label();
            groupBox6 = new CardGroupBox();
            chkSpectrumEnabled = new CheckBox();
            picSpectrum = new SpectrumDisplay();
            lblStatus = new Label();
            timerSignalLevel = new System.Windows.Forms.Timer(components);
            chkSwapIQ = new CheckBox();
            chkInvertDemod = new CheckBox();
            progressSignal = new SignalMeter();
            groupBox1.SuspendLayout();
            groupBox2.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)numSampleRate).BeginInit();
            ((System.ComponentModel.ISupportInitialize)numPpm).BeginInit();
            ((System.ComponentModel.ISupportInitialize)numTunerBandwidth).BeginInit();
            groupBox3.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)trackTunerGain).BeginInit();
            ((System.ComponentModel.ISupportInitialize)trackRxBandwidth).BeginInit();
            ((System.ComponentModel.ISupportInitialize)trackSpecZoom).BeginInit();
            ((System.ComponentModel.ISupportInitialize)trackSpecRange).BeginInit();
            ((System.ComponentModel.ISupportInitialize)trackSpecRef).BeginInit();
            groupBox4.SuspendLayout();
            groupBoxAudio.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)trackVolume).BeginInit();
            ((System.ComponentModel.ISupportInitialize)trackSquelch).BeginInit();
            groupBox6.SuspendLayout();
            SuspendLayout();
            // 
            // groupBox1
            // 
            groupBox1.BackColor = Color.FromArgb(34, 42, 58);
            groupBox1.Controls.Add(cmbDevices);
            groupBox1.Controls.Add(btnOpen);
            groupBox1.Controls.Add(btnClose);
            groupBox1.Controls.Add(btnStart);
            groupBox1.Controls.Add(btnStop);
            groupBox1.Font = new Font("Segoe UI Semibold", 9.5F);
            groupBox1.Location = new Point(12, 10);
            groupBox1.Margin = new Padding(4);
            groupBox1.Name = "groupBox1";
            groupBox1.Padding = new Padding(4);
            groupBox1.Size = new Size(976, 70);
            groupBox1.TabIndex = 0;
            groupBox1.TabStop = false;
            groupBox1.Text = "Device && Control";
            // 
            // cmbDevices
            // 
            cmbDevices.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbDevices.FormattingEnabled = true;
            cmbDevices.Location = new Point(19, 27);
            cmbDevices.Margin = new Padding(4);
            cmbDevices.Name = "cmbDevices";
            cmbDevices.Size = new Size(500, 33);
            cmbDevices.TabIndex = 0;
            // 
            // btnOpen
            // 
            btnOpen.Location = new Point(530, 24);
            btnOpen.Margin = new Padding(4);
            btnOpen.Name = "btnOpen";
            btnOpen.Size = new Size(100, 36);
            btnOpen.TabIndex = 1;
            btnOpen.Text = "Open";
            btnOpen.UseVisualStyleBackColor = true;
            btnOpen.Click += btnOpen_Click;
            // 
            // btnClose
            // 
            btnClose.Location = new Point(640, 24);
            btnClose.Margin = new Padding(4);
            btnClose.Name = "btnClose";
            btnClose.Size = new Size(100, 36);
            btnClose.TabIndex = 2;
            btnClose.Text = "Close";
            btnClose.UseVisualStyleBackColor = true;
            btnClose.Click += btnClose_Click;
            // 
            // btnStart
            // 
            btnStart.Location = new Point(760, 24);
            btnStart.Margin = new Padding(4);
            btnStart.Name = "btnStart";
            btnStart.Size = new Size(100, 36);
            btnStart.TabIndex = 13;
            btnStart.Text = "Start";
            btnStart.UseVisualStyleBackColor = true;
            btnStart.Click += btnStart_Click;
            // 
            // btnStop
            // 
            btnStop.Location = new Point(868, 24);
            btnStop.Margin = new Padding(4);
            btnStop.Name = "btnStop";
            btnStop.Size = new Size(100, 36);
            btnStop.TabIndex = 14;
            btnStop.Text = "Stop";
            btnStop.UseVisualStyleBackColor = true;
            btnStop.Click += btnStop_Click;
            // 
            // groupBox2
            // 
            groupBox2.BackColor = Color.FromArgb(34, 42, 58);
            groupBox2.Controls.Add(label1);
            groupBox2.Controls.Add(numFrequency);
            groupBox2.Font = new Font("Segoe UI Semibold", 9.5F);
            groupBox2.Location = new Point(12, 86);
            groupBox2.Margin = new Padding(4);
            groupBox2.Name = "groupBox2";
            groupBox2.Padding = new Padding(4);
            groupBox2.Size = new Size(976, 143);
            groupBox2.TabIndex = 1;
            groupBox2.TabStop = false;
            groupBox2.Text = "Tuning";
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(19, 67);
            label1.Margin = new Padding(4, 0, 4, 0);
            label1.Name = "label1";
            label1.Size = new Size(58, 25);
            label1.TabIndex = 0;
            label1.Text = "MHz:";
            // 
            // numFrequency
            // 
            numFrequency.BackColor = CardGroupBox.ActiveCardColor;
            numFrequency.Location = new Point(190, 9);
            numFrequency.Margin = new Padding(4);
            numFrequency.Name = "numFrequency";
            numFrequency.TabIndex = 1;
            numFrequency.ValueChanged += numFrequency_ValueChanged;
            numFrequency.Load += numFrequency_Load;
            // 
            // numSampleRate
            // 
            numSampleRate.DecimalPlaces = 1;
            numSampleRate.Increment = new decimal(new int[] { 1, 0, 0, 65536 });
            numSampleRate.Location = new Point(35, 451);
            numSampleRate.Margin = new Padding(4);
            numSampleRate.Maximum = new decimal(new int[] { 32, 0, 0, 65536 });
            numSampleRate.Minimum = new decimal(new int[] { 9, 0, 0, 65536 });
            numSampleRate.Name = "numSampleRate";
            numSampleRate.Size = new Size(150, 31);
            numSampleRate.TabIndex = 3;
            numSampleRate.Value = new decimal(new int[] { 24, 0, 0, 65536 });
            numSampleRate.ValueChanged += numSampleRate_ValueChanged;
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new Point(35, 422);
            label2.Margin = new Padding(4, 0, 4, 0);
            label2.Name = "label2";
            label2.Size = new Size(117, 25);
            label2.TabIndex = 2;
            label2.Text = "Sample Rate:";
            // 
            // numPpm
            // 
            numPpm.Location = new Point(179, 387);
            numPpm.Margin = new Padding(4);
            numPpm.Maximum = new decimal(new int[] { 200, 0, 0, 0 });
            numPpm.Minimum = new decimal(new int[] { 200, 0, 0, int.MinValue });
            numPpm.Name = "numPpm";
            numPpm.Size = new Size(69, 31);
            numPpm.TabIndex = 5;
            numPpm.ValueChanged += numPpm_ValueChanged;
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.Location = new Point(32, 387);
            label3.Margin = new Padding(4, 0, 4, 0);
            label3.Name = "label3";
            label3.Size = new Size(139, 25);
            label3.TabIndex = 4;
            label3.Text = "PPM Correction:";
            // 
            // labelBandwidth
            // 
            labelBandwidth.AutoSize = true;
            labelBandwidth.Location = new Point(34, 350);
            labelBandwidth.Margin = new Padding(4, 0, 4, 0);
            labelBandwidth.Name = "labelBandwidth";
            labelBandwidth.Size = new Size(137, 25);
            labelBandwidth.TabIndex = 6;
            labelBandwidth.Text = "Tuner BW:";
            // 
            // numTunerBandwidth
            // 
            numTunerBandwidth.Location = new Point(179, 348);
            numTunerBandwidth.Margin = new Padding(4);
            numTunerBandwidth.Maximum = new decimal(new int[] { 400, 0, 0, 0 });
            numTunerBandwidth.Minimum = new decimal(new int[] { 50, 0, 0, 0 });
            numTunerBandwidth.Name = "numTunerBandwidth";
            numTunerBandwidth.Size = new Size(100, 31);
            numTunerBandwidth.TabIndex = 7;
            numTunerBandwidth.Value = new decimal(new int[] { 200, 0, 0, 0 });
            numTunerBandwidth.ValueChanged += numTunerBandwidth_ValueChanged;
            // 
            // btnPpmHelp
            // 
            btnPpmHelp.Location = new Point(256, 504);
            btnPpmHelp.Margin = new Padding(4);
            btnPpmHelp.Name = "btnPpmHelp";
            btnPpmHelp.Size = new Size(100, 35);
            btnPpmHelp.TabIndex = 9;
            btnPpmHelp.Text = "PPM Help";
            btnPpmHelp.UseVisualStyleBackColor = true;
            btnPpmHelp.Click += btnPpmHelp_Click;
            // 
            // btnPpm0
            // 
            btnPpm0.Location = new Point(247, 384);
            btnPpm0.Margin = new Padding(4);
            btnPpm0.Name = "btnPpm0";
            btnPpm0.Size = new Size(44, 35);
            btnPpm0.TabIndex = 6;
            btnPpm0.Text = "0";
            btnPpm0.UseVisualStyleBackColor = true;
            btnPpm0.Click += btnPpm0_Click;
            // 
            // btnPpmMinus50
            // 
            btnPpmMinus50.Location = new Point(284, 384);
            btnPpmMinus50.Margin = new Padding(4);
            btnPpmMinus50.Name = "btnPpmMinus50";
            btnPpmMinus50.Size = new Size(50, 35);
            btnPpmMinus50.TabIndex = 7;
            btnPpmMinus50.Text = "-50";
            btnPpmMinus50.UseVisualStyleBackColor = true;
            btnPpmMinus50.Click += btnPpmMinus50_Click;
            // 
            // btnPpmPlus50
            // 
            btnPpmPlus50.Location = new Point(337, 384);
            btnPpmPlus50.Margin = new Padding(4);
            btnPpmPlus50.Name = "btnPpmPlus50";
            btnPpmPlus50.Size = new Size(50, 35);
            btnPpmPlus50.TabIndex = 8;
            btnPpmPlus50.Text = "+50";
            btnPpmPlus50.UseVisualStyleBackColor = true;
            btnPpmPlus50.Click += btnPpmPlus50_Click;
            // 
            // btnScan
            // 
            btnScan.Location = new Point(148, 503);
            btnScan.Margin = new Padding(4);
            btnScan.Name = "btnScan";
            btnScan.Size = new Size(100, 36);
            btnScan.TabIndex = 10;
            btnScan.Text = "Scan FM";
            btnScan.UseVisualStyleBackColor = true;
            btnScan.Click += btnScan_Click;
            // 
            // btnDiagnostics
            // 
            btnDiagnostics.BackColor = Color.LightYellow;
            btnDiagnostics.Location = new Point(40, 503);
            btnDiagnostics.Margin = new Padding(4);
            btnDiagnostics.Name = "btnDiagnostics";
            btnDiagnostics.Size = new Size(100, 36);
            btnDiagnostics.TabIndex = 11;
            btnDiagnostics.Text = "Diag";
            btnDiagnostics.UseVisualStyleBackColor = false;
            btnDiagnostics.Click += btnDiagnostics_Click;
            // 
            // chkOffsetTuning
            // 
            chkOffsetTuning.AutoSize = true;
            chkOffsetTuning.Location = new Point(150, 70);
            chkOffsetTuning.Margin = new Padding(4);
            chkOffsetTuning.Name = "chkOffsetTuning";
            chkOffsetTuning.Size = new Size(154, 29);
            chkOffsetTuning.TabIndex = 4;
            chkOffsetTuning.Text = "Offset Tuning";
            chkOffsetTuning.UseVisualStyleBackColor = true;
            chkOffsetTuning.CheckedChanged += chkOffsetTuning_CheckedChanged;
            // 
            // lblFreqDisplay
            // 
            lblFreqDisplay.AutoSize = true;
            lblFreqDisplay.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            lblFreqDisplay.Location = new Point(19, 45);
            lblFreqDisplay.Margin = new Padding(4, 0, 4, 0);
            lblFreqDisplay.Name = "lblFreqDisplay";
            lblFreqDisplay.Size = new Size(100, 25);
            lblFreqDisplay.TabIndex = 12;
            lblFreqDisplay.Text = "98.500";
            // 
            // chkTunerAGC
            // 
            chkTunerAGC.AutoSize = true;
            chkTunerAGC.Checked = true;
            chkTunerAGC.CheckState = CheckState.Checked;
            chkTunerAGC.Location = new Point(19, 32);
            chkTunerAGC.Margin = new Padding(4);
            chkTunerAGC.Name = "chkTunerAGC";
            chkTunerAGC.Size = new Size(129, 29);
            chkTunerAGC.TabIndex = 0;
            chkTunerAGC.Text = "Tuner AGC";
            chkTunerAGC.UseVisualStyleBackColor = true;
            chkTunerAGC.CheckedChanged += chkTunerAGC_CheckedChanged;
            // 
            // chkRtlAGC
            // 
            chkRtlAGC.AutoSize = true;
            chkRtlAGC.Location = new Point(19, 70);
            chkRtlAGC.Margin = new Padding(4);
            chkRtlAGC.Name = "chkRtlAGC";
            chkRtlAGC.Size = new Size(112, 29);
            chkRtlAGC.TabIndex = 5;
            chkRtlAGC.Text = "RTL AGC";
            chkRtlAGC.UseVisualStyleBackColor = true;
            chkRtlAGC.CheckedChanged += chkRtlAGC_CheckedChanged;
            // 
            // groupBox3
            // 
            groupBox3.BackColor = Color.FromArgb(34, 42, 58);
            groupBox3.Controls.Add(chkTunerAGC);
            groupBox3.Controls.Add(lblTunerGain);
            groupBox3.Controls.Add(trackTunerGain);
            groupBox3.Controls.Add(chkOffsetTuning);
            groupBox3.Controls.Add(chkRtlAGC);
            groupBox3.Controls.Add(lblClip);
            groupBox3.Controls.Add(lblRxBandwidth);
            groupBox3.Controls.Add(trackRxBandwidth);
            groupBox3.Controls.Add(rdoQuadrature);
            groupBox3.Controls.Add(rdoDirectSamplingI);
            groupBox3.Controls.Add(rdoDirectSamplingQ);
            groupBox3.Font = new Font("Segoe UI Semibold", 9.5F);
            groupBox3.Location = new Point(12, 394);
            groupBox3.Margin = new Padding(4);
            groupBox3.Name = "groupBox3";
            groupBox3.Padding = new Padding(4);
            groupBox3.Size = new Size(976, 150);
            groupBox3.TabIndex = 4;
            groupBox3.TabStop = false;
            groupBox3.Text = "Gain && Input";
            // 
            // lblTunerGain
            // 
            lblTunerGain.AutoSize = true;
            lblTunerGain.Location = new Point(590, 33);
            lblTunerGain.Margin = new Padding(4, 0, 4, 0);
            lblTunerGain.Name = "lblTunerGain";
            lblTunerGain.Size = new Size(168, 25);
            lblTunerGain.TabIndex = 2;
            lblTunerGain.Text = "Tuner Gain: 0.0 dB";
            // 
            // trackTunerGain
            // 
            trackTunerGain.AutoSize = false;
            trackTunerGain.Location = new Point(150, 26);
            trackTunerGain.Margin = new Padding(4);
            trackTunerGain.Maximum = 500;
            trackTunerGain.Name = "trackTunerGain";
            trackTunerGain.Size = new Size(430, 40);
            trackTunerGain.TabIndex = 3;
            trackTunerGain.TickFrequency = 50;
            trackTunerGain.Value = 250;
            trackTunerGain.Scroll += trackTunerGain_Scroll;
            // 
            // lblClip
            // 
            lblClip.AutoSize = true;
            lblClip.ForeColor = Color.Gray;
            lblClip.Location = new Point(330, 72);
            lblClip.Name = "lblClip";
            lblClip.Size = new Size(141, 25);
            lblClip.TabIndex = 9;
            lblClip.Text = "ADC clip: -.--%";
            // 
            // lblRxBandwidth
            // 
            lblRxBandwidth.AutoSize = true;
            lblRxBandwidth.ForeColor = Color.White;
            lblRxBandwidth.Location = new Point(560, 72);
            lblRxBandwidth.Margin = new Padding(4, 0, 4, 0);
            lblRxBandwidth.Name = "lblRxBandwidth";
            lblRxBandwidth.Size = new Size(151, 25);
            lblRxBandwidth.TabIndex = 2;
            lblRxBandwidth.Text = "RX BW: 200 kHz";
            // 
            // trackRxBandwidth
            // 
            trackRxBandwidth.AutoSize = false;
            trackRxBandwidth.Location = new Point(690, 62);
            trackRxBandwidth.Margin = new Padding(4);
            trackRxBandwidth.Maximum = 200;
            trackRxBandwidth.Minimum = 1;
            trackRxBandwidth.Name = "trackRxBandwidth";
            trackRxBandwidth.Size = new Size(270, 40);
            trackRxBandwidth.TabIndex = 3;
            trackRxBandwidth.TickFrequency = 10;
            trackRxBandwidth.Value = 200;
            trackRxBandwidth.Scroll += trackRxBandwidth_Scroll;
            // 
            // rdoQuadrature
            // 
            rdoQuadrature.AutoSize = true;
            rdoQuadrature.Checked = true;
            rdoQuadrature.Location = new Point(19, 108);
            rdoQuadrature.Margin = new Padding(4);
            rdoQuadrature.Name = "rdoQuadrature";
            rdoQuadrature.Size = new Size(135, 29);
            rdoQuadrature.TabIndex = 2;
            rdoQuadrature.TabStop = true;
            rdoQuadrature.Text = "Quadrature";
            rdoQuadrature.UseVisualStyleBackColor = true;
            rdoQuadrature.CheckedChanged += rdoSamplingMode_CheckedChanged;
            // 
            // rdoDirectSamplingI
            // 
            rdoDirectSamplingI.AutoSize = true;
            rdoDirectSamplingI.Location = new Point(170, 108);
            rdoDirectSamplingI.Margin = new Padding(4);
            rdoDirectSamplingI.Name = "rdoDirectSamplingI";
            rdoDirectSamplingI.Size = new Size(185, 29);
            rdoDirectSamplingI.TabIndex = 3;
            rdoDirectSamplingI.Text = "Direct Sampling I";
            rdoDirectSamplingI.UseVisualStyleBackColor = true;
            rdoDirectSamplingI.CheckedChanged += rdoSamplingMode_CheckedChanged;
            // 
            // rdoDirectSamplingQ
            // 
            rdoDirectSamplingQ.AutoSize = true;
            rdoDirectSamplingQ.Location = new Point(345, 108);
            rdoDirectSamplingQ.Margin = new Padding(4);
            rdoDirectSamplingQ.Name = "rdoDirectSamplingQ";
            rdoDirectSamplingQ.Size = new Size(193, 29);
            rdoDirectSamplingQ.TabIndex = 4;
            rdoDirectSamplingQ.Text = "Direct Sampling Q";
            rdoDirectSamplingQ.UseVisualStyleBackColor = true;
            rdoDirectSamplingQ.CheckedChanged += rdoSamplingMode_CheckedChanged;
            // 
            // chkSpecAuto
            // 
            chkSpecAuto.AutoSize = true;
            chkSpecAuto.Checked = true;
            chkSpecAuto.CheckState = CheckState.Checked;
            chkSpecAuto.Location = new Point(210, 30);
            chkSpecAuto.Name = "chkSpecAuto";
            chkSpecAuto.Size = new Size(134, 29);
            chkSpecAuto.TabIndex = 6;
            chkSpecAuto.Text = "Auto range";
            chkSpecAuto.UseVisualStyleBackColor = true;
            chkSpecAuto.CheckedChanged += chkSpecAuto_CheckedChanged;
            // 
            // lblSpecZoom
            // 
            lblSpecZoom.AutoSize = true;
            lblSpecZoom.Location = new Point(19, 80);
            lblSpecZoom.Name = "lblSpecZoom";
            lblSpecZoom.Size = new Size(106, 25);
            lblSpecZoom.TabIndex = 7;
            lblSpecZoom.Text = "Zoom: 1.0x";
            // 
            // trackSpecZoom
            // 
            trackSpecZoom.AutoSize = false;
            trackSpecZoom.Location = new Point(120, 72);
            trackSpecZoom.Maximum = 50;
            trackSpecZoom.Name = "trackSpecZoom";
            trackSpecZoom.Size = new Size(190, 40);
            trackSpecZoom.TabIndex = 7;
            trackSpecZoom.TickFrequency = 10;
            trackSpecZoom.Scroll += trackSpecZoom_Scroll;
            // 
            // lblSpecRange
            // 
            lblSpecRange.AutoSize = true;
            lblSpecRange.Location = new Point(330, 80);
            lblSpecRange.Name = "lblSpecRange";
            lblSpecRange.Size = new Size(125, 25);
            lblSpecRange.TabIndex = 8;
            lblSpecRange.Text = "Range: 80 dB";
            // 
            // trackSpecRange
            // 
            trackSpecRange.AutoSize = false;
            trackSpecRange.Enabled = false;
            trackSpecRange.Location = new Point(445, 72);
            trackSpecRange.Maximum = 120;
            trackSpecRange.Minimum = 20;
            trackSpecRange.Name = "trackSpecRange";
            trackSpecRange.Size = new Size(170, 40);
            trackSpecRange.TabIndex = 8;
            trackSpecRange.TickFrequency = 20;
            trackSpecRange.Value = 80;
            trackSpecRange.Scroll += trackSpecRange_Scroll;
            // 
            // lblSpecRef
            // 
            lblSpecRef.AutoSize = true;
            lblSpecRef.Location = new Point(635, 80);
            lblSpecRef.Name = "lblSpecRef";
            lblSpecRef.Size = new Size(108, 25);
            lblSpecRef.TabIndex = 9;
            lblSpecRef.Text = "Ref: -20 dB";
            // 
            // trackSpecRef
            // 
            trackSpecRef.AutoSize = false;
            trackSpecRef.Enabled = false;
            trackSpecRef.Location = new Point(740, 72);
            trackSpecRef.Maximum = 0;
            trackSpecRef.Minimum = -100;
            trackSpecRef.Name = "trackSpecRef";
            trackSpecRef.Size = new Size(220, 40);
            trackSpecRef.TabIndex = 9;
            trackSpecRef.TickFrequency = 10;
            trackSpecRef.Value = -20;
            trackSpecRef.Scroll += trackSpecRef_Scroll;
            // 
            // chkDeEmphasis
            // 
            chkDeEmphasis.AutoSize = true;
            chkDeEmphasis.Location = new Point(19, 106);
            chkDeEmphasis.Margin = new Padding(4);
            chkDeEmphasis.Name = "chkDeEmphasis";
            chkDeEmphasis.Size = new Size(150, 29);
            chkDeEmphasis.TabIndex = 17;
            chkDeEmphasis.Text = "De-emphasis";
            chkDeEmphasis.UseVisualStyleBackColor = true;
            chkDeEmphasis.CheckedChanged += chkDeEmphasis_CheckedChanged;
            // 
            // groupBox4
            // 
            groupBox4.BackColor = Color.FromArgb(34, 42, 58);
            groupBox4.Controls.Add(rdoDemodCw);
            groupBox4.Controls.Add(rdoDemodRaw);
            groupBox4.Controls.Add(rdoDemodLsb);
            groupBox4.Controls.Add(rdoDemodUsb);
            groupBox4.Controls.Add(rdoDemodNarrowFm);
            groupBox4.Controls.Add(rdoDemodWideFm);
            groupBox4.Controls.Add(rdoDemodAm);
            groupBox4.Controls.Add(lblFreqOffset);
            groupBox4.Controls.Add(chkDeEmphasis);
            groupBox4.Font = new Font("Segoe UI Semibold", 9.5F);
            groupBox4.Location = new Point(12, 236);
            groupBox4.Margin = new Padding(4);
            groupBox4.Name = "groupBox4";
            groupBox4.Padding = new Padding(4);
            groupBox4.Size = new Size(470, 150);
            groupBox4.TabIndex = 2;
            groupBox4.TabStop = false;
            groupBox4.Text = "Demodulation Mode";
            // 
            // rdoDemodCw
            // 
            rdoDemodCw.AutoSize = true;
            rdoDemodCw.Location = new Point(180, 68);
            rdoDemodCw.Margin = new Padding(4);
            rdoDemodCw.Name = "rdoDemodCw";
            rdoDemodCw.Size = new Size(67, 29);
            rdoDemodCw.TabIndex = 13;
            rdoDemodCw.Text = "CW";
            rdoDemodCw.UseVisualStyleBackColor = true;
            rdoDemodCw.CheckedChanged += rdoDemodMode_CheckedChanged;
            // 
            // rdoDemodRaw
            // 
            rdoDemodRaw.AutoSize = true;
            rdoDemodRaw.Location = new Point(255, 68);
            rdoDemodRaw.Margin = new Padding(4);
            rdoDemodRaw.Name = "rdoDemodRaw";
            rdoDemodRaw.Size = new Size(79, 29);
            rdoDemodRaw.TabIndex = 12;
            rdoDemodRaw.Text = "RAW";
            rdoDemodRaw.UseVisualStyleBackColor = true;
            rdoDemodRaw.CheckedChanged += rdoDemodMode_CheckedChanged;
            // 
            // rdoDemodLsb
            // 
            rdoDemodLsb.AutoSize = true;
            rdoDemodLsb.Location = new Point(100, 68);
            rdoDemodLsb.Margin = new Padding(4);
            rdoDemodLsb.Name = "rdoDemodLsb";
            rdoDemodLsb.Size = new Size(67, 29);
            rdoDemodLsb.TabIndex = 11;
            rdoDemodLsb.Text = "LSB";
            rdoDemodLsb.UseVisualStyleBackColor = true;
            rdoDemodLsb.CheckedChanged += rdoDemodMode_CheckedChanged;
            // 
            // rdoDemodUsb
            // 
            rdoDemodUsb.AutoSize = true;
            rdoDemodUsb.Location = new Point(19, 68);
            rdoDemodUsb.Margin = new Padding(4);
            rdoDemodUsb.Name = "rdoDemodUsb";
            rdoDemodUsb.Size = new Size(71, 29);
            rdoDemodUsb.TabIndex = 10;
            rdoDemodUsb.Text = "USB";
            rdoDemodUsb.UseVisualStyleBackColor = true;
            rdoDemodUsb.CheckedChanged += rdoDemodMode_CheckedChanged;
            // 
            // rdoDemodNarrowFm
            // 
            rdoDemodNarrowFm.AutoSize = true;
            rdoDemodNarrowFm.Location = new Point(140, 32);
            rdoDemodNarrowFm.Margin = new Padding(4);
            rdoDemodNarrowFm.Name = "rdoDemodNarrowFm";
            rdoDemodNarrowFm.Size = new Size(134, 29);
            rdoDemodNarrowFm.TabIndex = 9;
            rdoDemodNarrowFm.Text = "Narrow FM";
            rdoDemodNarrowFm.UseVisualStyleBackColor = true;
            rdoDemodNarrowFm.CheckedChanged += rdoDemodMode_CheckedChanged;
            // 
            // rdoDemodWideFm
            // 
            rdoDemodWideFm.AutoSize = true;
            rdoDemodWideFm.Checked = true;
            rdoDemodWideFm.Location = new Point(19, 32);
            rdoDemodWideFm.Margin = new Padding(4);
            rdoDemodWideFm.Name = "rdoDemodWideFm";
            rdoDemodWideFm.Size = new Size(114, 29);
            rdoDemodWideFm.TabIndex = 8;
            rdoDemodWideFm.TabStop = true;
            rdoDemodWideFm.Text = "Wide FM";
            rdoDemodWideFm.UseVisualStyleBackColor = true;
            rdoDemodWideFm.CheckedChanged += rdoDemodMode_CheckedChanged;
            // 
            // rdoDemodAm
            // 
            rdoDemodAm.AutoSize = true;
            rdoDemodAm.Location = new Point(295, 32);
            rdoDemodAm.Margin = new Padding(4);
            rdoDemodAm.Name = "rdoDemodAm";
            rdoDemodAm.Size = new Size(68, 29);
            rdoDemodAm.TabIndex = 15;
            rdoDemodAm.Text = "AM";
            rdoDemodAm.UseVisualStyleBackColor = true;
            rdoDemodAm.CheckedChanged += rdoDemodMode_CheckedChanged;
            // 
            // lblFreqOffset
            // 
            lblFreqOffset.AutoSize = true;
            lblFreqOffset.Font = new Font("Consolas", 9F);
            lblFreqOffset.ForeColor = Color.DarkBlue;
            lblFreqOffset.Location = new Point(200, 108);
            lblFreqOffset.Margin = new Padding(4, 0, 4, 0);
            lblFreqOffset.Name = "lblFreqOffset";
            lblFreqOffset.Size = new Size(180, 22);
            lblFreqOffset.TabIndex = 14;
            lblFreqOffset.Text = "Offset: -.--- kHz";
            // 
            // groupBoxAudio
            // 
            groupBoxAudio.BackColor = Color.FromArgb(34, 42, 58);
            groupBoxAudio.Controls.Add(lblVolume);
            groupBoxAudio.Controls.Add(trackVolume);
            groupBoxAudio.Controls.Add(lblSquelch);
            groupBoxAudio.Controls.Add(trackSquelch);
            groupBoxAudio.Controls.Add(lblSignalStrength);
            groupBoxAudio.Controls.Add(progressSignal);
            groupBoxAudio.Font = new Font("Segoe UI Semibold", 9.5F);
            groupBoxAudio.Location = new Point(494, 236);
            groupBoxAudio.Name = "groupBoxAudio";
            groupBoxAudio.Size = new Size(494, 150);
            groupBoxAudio.TabIndex = 3;
            groupBoxAudio.TabStop = false;
            groupBoxAudio.Text = "Audio && Signal";
            // 
            // lblVolume
            // 
            lblVolume.AutoSize = true;
            lblVolume.Location = new Point(15, 34);
            lblVolume.Margin = new Padding(4, 0, 4, 0);
            lblVolume.Name = "lblVolume";
            lblVolume.Size = new Size(111, 25);
            lblVolume.TabIndex = 18;
            lblVolume.Text = "Volume: 1%";
            // 
            // trackVolume
            // 
            trackVolume.AutoSize = false;
            trackVolume.Location = new Point(140, 26);
            trackVolume.Margin = new Padding(4);
            trackVolume.Maximum = 100;
            trackVolume.Name = "trackVolume";
            trackVolume.Size = new Size(335, 40);
            trackVolume.TabIndex = 19;
            trackVolume.TickFrequency = 10;
            trackVolume.Value = 1;
            trackVolume.Scroll += trackVolume_Scroll;
            // 
            // lblSquelch
            // 
            lblSquelch.AutoSize = true;
            lblSquelch.Location = new Point(15, 74);
            lblSquelch.Margin = new Padding(4, 0, 4, 0);
            lblSquelch.Name = "lblSquelch";
            lblSquelch.Size = new Size(116, 25);
            lblSquelch.TabIndex = 20;
            lblSquelch.Text = "Squelch: 0%";
            // 
            // trackSquelch
            // 
            trackSquelch.AutoSize = false;
            trackSquelch.Location = new Point(140, 66);
            trackSquelch.Margin = new Padding(4);
            trackSquelch.Maximum = 100;
            trackSquelch.Name = "trackSquelch";
            trackSquelch.Size = new Size(335, 40);
            trackSquelch.TabIndex = 21;
            trackSquelch.TickFrequency = 10;
            trackSquelch.Scroll += trackSquelch_Scroll;
            // 
            // lblSignalStrength
            // 
            lblSignalStrength.AutoSize = true;
            lblSignalStrength.Location = new Point(15, 112);
            lblSignalStrength.Margin = new Padding(4, 0, 4, 0);
            lblSignalStrength.Name = "lblSignalStrength";
            lblSignalStrength.Size = new Size(69, 25);
            lblSignalStrength.TabIndex = 15;
            lblSignalStrength.Text = "Signal:";
            // 
            // progressSignal
            // 
            progressSignal.BackColor = Color.FromArgb(34, 42, 58);
            progressSignal.Location = new Point(140, 110);
            progressSignal.Margin = new Padding(4);
            progressSignal.MinimumSize = new Size(40, 16);
            progressSignal.Name = "progressSignal";
            progressSignal.Size = new Size(335, 24);
            progressSignal.TabIndex = 16;
            progressSignal.TabStop = false;
            // 
            // groupBox6
            // 
            groupBox6.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            groupBox6.BackColor = Color.FromArgb(34, 42, 58);
            groupBox6.Controls.Add(chkSpectrumEnabled);
            groupBox6.Controls.Add(chkSpecAuto);
            groupBox6.Controls.Add(lblSpecZoom);
            groupBox6.Controls.Add(trackSpecZoom);
            groupBox6.Controls.Add(lblSpecRange);
            groupBox6.Controls.Add(trackSpecRange);
            groupBox6.Controls.Add(lblSpecRef);
            groupBox6.Controls.Add(trackSpecRef);
            groupBox6.Controls.Add(picSpectrum);
            groupBox6.Font = new Font("Segoe UI Semibold", 9.5F);
            groupBox6.Location = new Point(12, 550);
            groupBox6.Margin = new Padding(4);
            groupBox6.Name = "groupBox6";
            groupBox6.Padding = new Padding(4);
            groupBox6.Size = new Size(976, 405);
            groupBox6.TabIndex = 5;
            groupBox6.TabStop = false;
            groupBox6.Text = "Spectrum";
            // 
            // chkSpectrumEnabled
            // 
            chkSpectrumEnabled.AutoSize = true;
            chkSpectrumEnabled.Location = new Point(19, 30);
            chkSpectrumEnabled.Margin = new Padding(4);
            chkSpectrumEnabled.Name = "chkSpectrumEnabled";
            chkSpectrumEnabled.Size = new Size(182, 29);
            chkSpectrumEnabled.TabIndex = 0;
            chkSpectrumEnabled.Text = "Enable Spectrum";
            chkSpectrumEnabled.UseVisualStyleBackColor = true;
            chkSpectrumEnabled.CheckedChanged += chkSpectrumEnabled_CheckedChanged;
            // 
            // picSpectrum
            // 
            picSpectrum.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            picSpectrum.BackColor = Color.Black;
            picSpectrum.Font = new Font("Segoe UI", 8F);
            picSpectrum.ForeColor = Color.FromArgb(170, 180, 195);
            picSpectrum.Location = new Point(19, 122);
            picSpectrum.Margin = new Padding(4);
            picSpectrum.Name = "picSpectrum";
            picSpectrum.Size = new Size(938, 270);
            picSpectrum.TabIndex = 1;
            // 
            // lblStatus
            // 
            lblStatus.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            lblStatus.AutoSize = true;
            lblStatus.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            lblStatus.Location = new Point(12, 962);
            lblStatus.Margin = new Padding(4, 0, 4, 0);
            lblStatus.Name = "lblStatus";
            lblStatus.Size = new Size(76, 28);
            lblStatus.TabIndex = 15;
            lblStatus.Text = "Status:";
            // 
            // timerSignalLevel
            // 
            timerSignalLevel.Tick += timerSignalLevel_Tick;
            // 
            // chkSwapIQ
            // 
            chkSwapIQ.AutoSize = true;
            chkSwapIQ.Location = new Point(19, 80);
            chkSwapIQ.Margin = new Padding(4);
            chkSwapIQ.Name = "chkSwapIQ";
            chkSwapIQ.Size = new Size(105, 29);
            chkSwapIQ.TabIndex = 6;
            chkSwapIQ.Text = "Swap I/Q";
            chkSwapIQ.UseVisualStyleBackColor = true;
            // 
            // chkInvertDemod
            // 
            chkInvertDemod.AutoSize = true;
            chkInvertDemod.Location = new Point(139, 80);
            chkInvertDemod.Margin = new Padding(4);
            chkInvertDemod.Name = "chkInvertDemod";
            chkInvertDemod.Size = new Size(149, 29);
            chkInvertDemod.TabIndex = 7;
            chkInvertDemod.Text = "Invert Demod";
            chkInvertDemod.UseVisualStyleBackColor = true;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(144F, 144F);
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(1200, 800);
            Controls.Add(lblStatus);
            Controls.Add(groupBox6);
            Controls.Add(groupBoxAudio);
            Controls.Add(groupBox4);
            Controls.Add(groupBox3);
            Controls.Add(groupBox2);
            Controls.Add(groupBox1);
            Font = new Font("Segoe UI", 9.5F);
            Margin = new Padding(4);
            MinimumSize = new Size(400, 300);
            Name = "Form1";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "RandySDR";
            Load += Form1_Load;
            groupBox1.ResumeLayout(false);
            groupBox2.ResumeLayout(false);
            groupBox2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)numSampleRate).EndInit();
            ((System.ComponentModel.ISupportInitialize)numPpm).EndInit();
            ((System.ComponentModel.ISupportInitialize)numTunerBandwidth).EndInit();
            groupBox3.ResumeLayout(false);
            groupBox3.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)trackTunerGain).EndInit();
            ((System.ComponentModel.ISupportInitialize)trackRxBandwidth).EndInit();
            ((System.ComponentModel.ISupportInitialize)trackSpecZoom).EndInit();
            ((System.ComponentModel.ISupportInitialize)trackSpecRange).EndInit();
            ((System.ComponentModel.ISupportInitialize)trackSpecRef).EndInit();
            groupBox4.ResumeLayout(false);
            groupBox4.PerformLayout();
            groupBoxAudio.ResumeLayout(false);
            groupBoxAudio.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)trackVolume).EndInit();
            ((System.ComponentModel.ISupportInitialize)trackSquelch).EndInit();
            groupBox6.ResumeLayout(false);
            groupBox6.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private CardGroupBox groupBox1;
        private CheckBox chkSpecAuto;
        private Label lblSpecZoom;
        private TrackBar trackSpecZoom;
        private Label lblSpecRange;
        private TrackBar trackSpecRange;
        private Label lblSpecRef;
        private TrackBar trackSpecRef;
        private ComboBox cmbDevices;
        private Button btnOpen;
        private Button btnClose;
        private CardGroupBox groupBox2;
        private FrequencySelector numFrequency;
        private Label label1;
        private NumericUpDown numSampleRate;
        private Label label2;
        private NumericUpDown numPpm;
        private Label label3;
        private Label labelBandwidth;
        private NumericUpDown numTunerBandwidth;
        private Button btnPpmHelp;
        private Button btnPpm0;
        private Button btnPpmMinus50;
        private Button btnPpmPlus50;
        private Button btnScan;
        private Button btnDiagnostics;
        private CheckBox chkOffsetTuning;
        private Label lblFreqDisplay;
        private CheckBox chkTunerAGC;
        private CheckBox chkRtlAGC;
        private CardGroupBox groupBox3;
        private Label lblTunerGain;
        private Label lblClip;
        private TrackBar trackTunerGain;
        private CardGroupBox groupBox4;
        private CardGroupBox groupBoxAudio;
        private RadioButton rdoDemodCw;
        private RadioButton rdoDemodRaw;
        private RadioButton rdoDemodLsb;
        private RadioButton rdoDemodUsb;
        private RadioButton rdoDemodNarrowFm;
        private RadioButton rdoDemodWideFm;
        private RadioButton rdoDemodAm;
        private Label lblFreqOffset;
        private Label lblSignalStrength;
        private CheckBox chkDeEmphasis;
        private Label lblVolume;
        private TrackBar trackVolume;
        private Label lblSquelch;
        private TrackBar trackSquelch;
        private CardGroupBox groupBox6;
        private CheckBox chkSpectrumEnabled;
        private RadioButton rdoQuadrature;
        private RadioButton rdoDirectSamplingI;
        private RadioButton rdoDirectSamplingQ;
        private SpectrumDisplay picSpectrum;
        private Label lblRxBandwidth;
        private TrackBar trackRxBandwidth;
        private Button btnStart;
        private Button btnStop;
        private Label lblStatus;
        private System.Windows.Forms.Timer timerSignalLevel;
        private CheckBox chkSwapIQ;
        private CheckBox chkInvertDemod;
        private SignalMeter progressSignal;
    }
}
