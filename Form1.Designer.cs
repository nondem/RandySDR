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
            groupBox1 = new GroupBox();
            cmbDevices = new ComboBox();
            btnOpen = new Button();
            btnClose = new Button();
            groupBox2 = new GroupBox();
            numFrequency = new NumericUpDown();
            label1 = new Label();
            numSampleRate = new NumericUpDown();
            label2 = new Label();
            numPpm = new NumericUpDown();
            label3 = new Label();
            btnPpmHelp = new Button();
            btnPpm0 = new Button();
            btnScan = new Button();
            btnPpmMinus50 = new Button();
            btnPpmPlus50 = new Button();
            btnDiagnostics = new Button();
            lblFreqDisplay = new Label();
            groupBox3 = new GroupBox();
            chkTunerAGC = new CheckBox();
            trackTunerGain = new TrackBar();
            lblTunerGain = new Label();
            chkRtlAGC = new CheckBox();
            groupBox4 = new GroupBox();
            chkDeEmphasis = new CheckBox();
            lblVolume = new Label();
            trackVolume = new TrackBar();
            lblSquelch = new Label();
            trackSquelch = new TrackBar();
            lblSignalStrength = new Label();
            progressSignal = new ProgressBar();
            lblFreqOffset = new Label();
            btnStart = new Button();
            btnStop = new Button();
            lblStatus = new Label();
            timerSignalLevel = new System.Windows.Forms.Timer(components);
            groupBox1.SuspendLayout();
            groupBox2.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)numFrequency).BeginInit();
            ((System.ComponentModel.ISupportInitialize)numSampleRate).BeginInit();
            ((System.ComponentModel.ISupportInitialize)numPpm).BeginInit();
            groupBox3.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)trackTunerGain).BeginInit();
            groupBox4.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)trackVolume).BeginInit();
            ((System.ComponentModel.ISupportInitialize)trackSquelch).BeginInit();
            SuspendLayout();
            // 
            // groupBox1
            // 
            groupBox1.Controls.Add(cmbDevices);
            groupBox1.Controls.Add(btnOpen);
            groupBox1.Controls.Add(btnClose);
            groupBox1.Location = new Point(15, 15);
            groupBox1.Margin = new Padding(4);
            groupBox1.Name = "groupBox1";
            groupBox1.Padding = new Padding(4);
            groupBox1.Size = new Size(950, 88);
            groupBox1.TabIndex = 0;
            groupBox1.TabStop = false;
            groupBox1.Text = "Device";
            // 
            // cmbDevices
            // 
            cmbDevices.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbDevices.FormattingEnabled = true;
            cmbDevices.Location = new Point(19, 31);
            cmbDevices.Margin = new Padding(4);
            cmbDevices.Name = "cmbDevices";
            cmbDevices.Size = new Size(686, 33);
            cmbDevices.TabIndex = 0;
            // 
            // btnOpen
            // 
            btnOpen.Location = new Point(719, 29);
            btnOpen.Margin = new Padding(4);
            btnOpen.Name = "btnOpen";
            btnOpen.Size = new Size(106, 40);
            btnOpen.TabIndex = 1;
            btnOpen.Text = "Open";
            btnOpen.UseVisualStyleBackColor = true;
            btnOpen.Click += btnOpen_Click;
            // 
            // btnClose
            // 
            btnClose.Location = new Point(832, 29);
            btnClose.Margin = new Padding(4);
            btnClose.Name = "btnClose";
            btnClose.Size = new Size(106, 40);
            btnClose.TabIndex = 2;
            btnClose.Text = "Close";
            btnClose.UseVisualStyleBackColor = true;
            btnClose.Click += btnClose_Click;
            // 
            // groupBox2
            // 
            groupBox2.Controls.Add(numFrequency);
            groupBox2.Controls.Add(label1);
            groupBox2.Controls.Add(numSampleRate);
            groupBox2.Controls.Add(label2);
            groupBox2.Controls.Add(numPpm);
            groupBox2.Controls.Add(label3);
            groupBox2.Controls.Add(btnPpmHelp);
            groupBox2.Controls.Add(btnPpm0);
            groupBox2.Controls.Add(btnScan);
            groupBox2.Controls.Add(btnPpmMinus50);
            groupBox2.Controls.Add(btnPpmPlus50);
            groupBox2.Controls.Add(btnDiagnostics);
            groupBox2.Location = new Point(15, 110);
            groupBox2.Margin = new Padding(4);
            groupBox2.Name = "groupBox2";
            groupBox2.Padding = new Padding(4);
            groupBox2.Size = new Size(950, 125);
            groupBox2.TabIndex = 1;
            groupBox2.TabStop = false;
            groupBox2.Text = "Tuning";
            // 
            // numFrequency
            // 
            numFrequency.DecimalPlaces = 3;
            numFrequency.Increment = new decimal(new int[] { 1, 0, 0, 131072 });
            numFrequency.Location = new Point(19, 66);
            numFrequency.Margin = new Padding(4);
            numFrequency.Maximum = new decimal(new int[] { 1766, 0, 0, 0 });
            numFrequency.Minimum = new decimal(new int[] { 24, 0, 0, 0 });
            numFrequency.Name = "numFrequency";
            numFrequency.Size = new Size(162, 31);
            numFrequency.TabIndex = 1;
            numFrequency.Value = new decimal(new int[] { 985, 0, 0, 65536 });
            numFrequency.ValueChanged += numFrequency_ValueChanged;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(19, 38);
            label1.Margin = new Padding(4, 0, 4, 0);
            label1.Name = "label1";
            label1.Size = new Size(149, 25);
            label1.TabIndex = 0;
            label1.Text = "Frequency (MHz):";
            // 
            // numSampleRate
            // 
            numSampleRate.DecimalPlaces = 1;
            numSampleRate.Increment = new decimal(new int[] { 1, 0, 0, 65536 });
            numSampleRate.Location = new Point(206, 66);
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
            label2.Location = new Point(206, 38);
            label2.Margin = new Padding(4, 0, 4, 0);
            label2.Name = "label2";
            label2.Size = new Size(176, 25);
            label2.TabIndex = 2;
            label2.Text = "Sample Rate (MSPS):";
            // 
            // numPpm
            // 
            numPpm.Location = new Point(381, 66);
            numPpm.Margin = new Padding(4);
            numPpm.Maximum = new decimal(new int[] { 200, 0, 0, 0 });
            numPpm.Minimum = new decimal(new int[] { 200, 0, 0, int.MinValue });
            numPpm.Name = "numPpm";
            numPpm.Size = new Size(100, 31);
            numPpm.TabIndex = 5;
            numPpm.ValueChanged += numPpm_ValueChanged;
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.Location = new Point(381, 38);
            label3.Margin = new Padding(4, 0, 4, 0);
            label3.Name = "label3";
            label3.Size = new Size(139, 25);
            label3.TabIndex = 4;
            label3.Text = "PPM Correction:";
            // 
            // btnPpmHelp
            // 
            btnPpmHelp.Location = new Point(838, 19);
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
            btnPpm0.Location = new Point(488, 65);
            btnPpm0.Margin = new Padding(4);
            btnPpm0.Name = "btnPpm0";
            btnPpm0.Size = new Size(44, 35);
            btnPpm0.TabIndex = 6;
            btnPpm0.Text = "0";
            btnPpm0.UseVisualStyleBackColor = true;
            btnPpm0.Click += btnPpm0_Click;
            // 
            // btnScan
            // 
            btnScan.Location = new Point(676, 63);
            btnScan.Margin = new Padding(4);
            btnScan.Name = "btnScan";
            btnScan.Size = new Size(88, 38);
            btnScan.TabIndex = 10;
            btnScan.Text = "Scan FM";
            btnScan.UseVisualStyleBackColor = true;
            btnScan.Click += btnScan_Click;
            // 
            // btnPpmMinus50
            // 
            btnPpmMinus50.Location = new Point(538, 65);
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
            btnPpmPlus50.Location = new Point(594, 65);
            btnPpmPlus50.Margin = new Padding(4);
            btnPpmPlus50.Name = "btnPpmPlus50";
            btnPpmPlus50.Size = new Size(50, 35);
            btnPpmPlus50.TabIndex = 8;
            btnPpmPlus50.Text = "+50";
            btnPpmPlus50.UseVisualStyleBackColor = true;
            btnPpmPlus50.Click += btnPpmPlus50_Click;
            // 
            // btnDiagnostics
            // 
            btnDiagnostics.BackColor = Color.LightYellow;
            btnDiagnostics.Location = new Point(838, 61);
            btnDiagnostics.Margin = new Padding(4);
            btnDiagnostics.Name = "btnDiagnostics";
            btnDiagnostics.Size = new Size(100, 36);
            btnDiagnostics.TabIndex = 11;
            btnDiagnostics.Text = "Diag";
            btnDiagnostics.UseVisualStyleBackColor = false;
            btnDiagnostics.Click += btnDiagnostics_Click;
            // 
            // lblFreqDisplay
            // 
            lblFreqDisplay.AutoSize = true;
            lblFreqDisplay.Font = new Font("Segoe UI", 24F, FontStyle.Bold);
            lblFreqDisplay.ForeColor = Color.DodgerBlue;
            lblFreqDisplay.Location = new Point(34, 386);
            lblFreqDisplay.Margin = new Padding(4, 0, 4, 0);
            lblFreqDisplay.Name = "lblFreqDisplay";
            lblFreqDisplay.Size = new Size(272, 65);
            lblFreqDisplay.TabIndex = 6;
            lblFreqDisplay.Text = "98.50 MHz";
            // 
            // groupBox3
            // 
            groupBox3.Controls.Add(chkTunerAGC);
            groupBox3.Controls.Add(trackTunerGain);
            groupBox3.Controls.Add(lblTunerGain);
            groupBox3.Controls.Add(chkRtlAGC);
            groupBox3.Location = new Point(15, 242);
            groupBox3.Margin = new Padding(4);
            groupBox3.Name = "groupBox3";
            groupBox3.Padding = new Padding(4);
            groupBox3.Size = new Size(462, 169);
            groupBox3.TabIndex = 2;
            groupBox3.TabStop = false;
            groupBox3.Text = "Gain Control";
            // 
            // chkTunerAGC
            // 
            chkTunerAGC.AutoSize = true;
            chkTunerAGC.Checked = true;
            chkTunerAGC.CheckState = CheckState.Checked;
            chkTunerAGC.Location = new Point(19, 38);
            chkTunerAGC.Margin = new Padding(4);
            chkTunerAGC.Name = "chkTunerAGC";
            chkTunerAGC.Size = new Size(122, 29);
            chkTunerAGC.TabIndex = 0;
            chkTunerAGC.Text = "Tuner AGC";
            chkTunerAGC.UseVisualStyleBackColor = true;
            chkTunerAGC.CheckedChanged += chkTunerAGC_CheckedChanged;
            // 
            // trackTunerGain
            // 
            trackTunerGain.Location = new Point(19, 106);
            trackTunerGain.Margin = new Padding(4);
            trackTunerGain.Maximum = 500;
            trackTunerGain.Name = "trackTunerGain";
            trackTunerGain.Size = new Size(425, 69);
            trackTunerGain.TabIndex = 3;
            trackTunerGain.TickFrequency = 50;
            trackTunerGain.Value = 250;
            trackTunerGain.Scroll += trackTunerGain_Scroll;
            // 
            // lblTunerGain
            // 
            lblTunerGain.AutoSize = true;
            lblTunerGain.Location = new Point(19, 81);
            lblTunerGain.Margin = new Padding(4, 0, 4, 0);
            lblTunerGain.Name = "lblTunerGain";
            lblTunerGain.Size = new Size(155, 25);
            lblTunerGain.TabIndex = 2;
            lblTunerGain.Text = "Tuner Gain: 0.0 dB";
            // 
            // chkRtlAGC
            // 
            chkRtlAGC.AutoSize = true;
            chkRtlAGC.Location = new Point(250, 38);
            chkRtlAGC.Margin = new Padding(4);
            chkRtlAGC.Name = "chkRtlAGC";
            chkRtlAGC.Size = new Size(105, 29);
            chkRtlAGC.TabIndex = 1;
            chkRtlAGC.Text = "RTL AGC";
            chkRtlAGC.UseVisualStyleBackColor = true;
            chkRtlAGC.CheckedChanged += chkRtlAGC_CheckedChanged;
            // 
            // groupBox4
            // 
            groupBox4.Controls.Add(chkDeEmphasis);
            groupBox4.Controls.Add(lblVolume);
            groupBox4.Controls.Add(trackVolume);
            groupBox4.Controls.Add(lblSquelch);
            groupBox4.Controls.Add(trackSquelch);
            groupBox4.Controls.Add(lblSignalStrength);
            groupBox4.Controls.Add(progressSignal);
            groupBox4.Controls.Add(lblFreqOffset);
            groupBox4.Location = new Point(502, 242);
            groupBox4.Margin = new Padding(4);
            groupBox4.Name = "groupBox4";
            groupBox4.Padding = new Padding(4);
            groupBox4.Size = new Size(462, 225);
            groupBox4.TabIndex = 3;
            groupBox4.TabStop = false;
            groupBox4.Text = "Audio";
            // 
            // chkDeEmphasis
            // 
            chkDeEmphasis.AutoSize = true;
            chkDeEmphasis.Checked = true;
            chkDeEmphasis.CheckState = CheckState.Checked;
            chkDeEmphasis.Location = new Point(19, 38);
            chkDeEmphasis.Margin = new Padding(4);
            chkDeEmphasis.Name = "chkDeEmphasis";
            chkDeEmphasis.Size = new Size(195, 29);
            chkDeEmphasis.TabIndex = 0;
            chkDeEmphasis.Text = "De-emphasis (75µs)";
            chkDeEmphasis.UseVisualStyleBackColor = true;
            chkDeEmphasis.CheckedChanged += chkDeEmphasis_CheckedChanged;
            // 
            // lblVolume
            // 
            lblVolume.AutoSize = true;
            lblVolume.Location = new Point(19, 75);
            lblVolume.Margin = new Padding(4, 0, 4, 0);
            lblVolume.Name = "lblVolume";
            lblVolume.Size = new Size(116, 25);
            lblVolume.TabIndex = 1;
            lblVolume.Text = "Volume: 50%";
            // 
            // trackVolume
            // 
            trackVolume.Location = new Point(150, 69);
            trackVolume.Margin = new Padding(4);
            trackVolume.Maximum = 100;
            trackVolume.Name = "trackVolume";
            trackVolume.Size = new Size(294, 69);
            trackVolume.TabIndex = 2;
            trackVolume.TickFrequency = 10;
            trackVolume.Value = 50;
            trackVolume.Scroll += trackVolume_Scroll;
            // 
            // lblSquelch
            // 
            lblSquelch.AutoSize = true;
            lblSquelch.Location = new Point(19, 119);
            lblSquelch.Margin = new Padding(4, 0, 4, 0);
            lblSquelch.Name = "lblSquelch";
            lblSquelch.Size = new Size(108, 25);
            lblSquelch.TabIndex = 3;
            lblSquelch.Text = "Squelch: 0%";
            // 
            // trackSquelch
            // 
            trackSquelch.Location = new Point(150, 112);
            trackSquelch.Margin = new Padding(4);
            trackSquelch.Maximum = 100;
            trackSquelch.Name = "trackSquelch";
            trackSquelch.Size = new Size(294, 69);
            trackSquelch.TabIndex = 4;
            trackSquelch.TickFrequency = 10;
            trackSquelch.Scroll += trackSquelch_Scroll;
            // 
            // lblSignalStrength
            // 
            lblSignalStrength.AutoSize = true;
            lblSignalStrength.Location = new Point(19, 162);
            lblSignalStrength.Margin = new Padding(4, 0, 4, 0);
            lblSignalStrength.Name = "lblSignalStrength";
            lblSignalStrength.Size = new Size(64, 25);
            lblSignalStrength.TabIndex = 5;
            lblSignalStrength.Text = "Signal:";
            // 
            // progressSignal
            // 
            progressSignal.Location = new Point(150, 162);
            progressSignal.Margin = new Padding(4);
            progressSignal.Name = "progressSignal";
            progressSignal.Size = new Size(294, 29);
            progressSignal.Style = ProgressBarStyle.Continuous;
            progressSignal.TabIndex = 6;
            // 
            // lblFreqOffset
            // 
            lblFreqOffset.AutoSize = true;
            lblFreqOffset.Font = new Font("Consolas", 9F);
            lblFreqOffset.ForeColor = Color.DarkBlue;
            lblFreqOffset.Location = new Point(19, 198);
            lblFreqOffset.Margin = new Padding(4, 0, 4, 0);
            lblFreqOffset.Name = "lblFreqOffset";
            lblFreqOffset.Size = new Size(180, 22);
            lblFreqOffset.TabIndex = 7;
            lblFreqOffset.Text = "Offset: -.--- kHz";
            // 
            // btnStart
            // 
            btnStart.BackColor = Color.LightGreen;
            btnStart.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
            btnStart.Location = new Point(15, 480);
            btnStart.Margin = new Padding(4);
            btnStart.Name = "btnStart";
            btnStart.Size = new Size(462, 62);
            btnStart.TabIndex = 4;
            btnStart.Text = "▶ START";
            btnStart.UseVisualStyleBackColor = false;
            btnStart.Click += btnStart_Click;
            // 
            // btnStop
            // 
            btnStop.BackColor = Color.LightCoral;
            btnStop.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
            btnStop.Location = new Point(502, 480);
            btnStop.Margin = new Padding(4);
            btnStop.Name = "btnStop";
            btnStop.Size = new Size(462, 62);
            btnStop.TabIndex = 5;
            btnStop.Text = "■ STOP";
            btnStop.UseVisualStyleBackColor = false;
            btnStop.Click += btnStop_Click;
            // 
            // lblStatus
            // 
            lblStatus.AutoSize = true;
            lblStatus.Font = new Font("Segoe UI", 10F);
            lblStatus.ForeColor = Color.Gray;
            lblStatus.Location = new Point(15, 559);
            lblStatus.Margin = new Padding(4, 0, 4, 0);
            lblStatus.Name = "lblStatus";
            lblStatus.Size = new Size(130, 28);
            lblStatus.TabIndex = 6;
            lblStatus.Text = "Disconnected";
            // 
            // timerSignalLevel
            // 
            timerSignalLevel.Tick += timerSignalLevel_Tick;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(10F, 25F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(983, 647);
            Controls.Add(lblFreqDisplay);
            Controls.Add(lblStatus);
            Controls.Add(btnStop);
            Controls.Add(btnStart);
            Controls.Add(groupBox4);
            Controls.Add(groupBox3);
            Controls.Add(groupBox2);
            Controls.Add(groupBox1);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            Margin = new Padding(4);
            MaximizeBox = false;
            Name = "Form1";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "RandySDR - FM Receiver";
            groupBox1.ResumeLayout(false);
            groupBox2.ResumeLayout(false);
            groupBox2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)numFrequency).EndInit();
            ((System.ComponentModel.ISupportInitialize)numSampleRate).EndInit();
            ((System.ComponentModel.ISupportInitialize)numPpm).EndInit();
            groupBox3.ResumeLayout(false);
            groupBox3.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)trackTunerGain).EndInit();
            groupBox4.ResumeLayout(false);
            groupBox4.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)trackVolume).EndInit();
            ((System.ComponentModel.ISupportInitialize)trackSquelch).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private GroupBox groupBox1;
        private ComboBox cmbDevices;
        private Button btnOpen;
        private Button btnClose;
        private GroupBox groupBox2;
        private Label lblFreqDisplay;
        private NumericUpDown numFrequency;
        private Label label1;
        private NumericUpDown numSampleRate;
        private Label label2;
        private NumericUpDown numPpm;
        private Label label3;
        private Button btnPpmHelp;
        private Button btnPpm0;
        private Button btnPpmMinus50;
        private Button btnPpmPlus50;
        private Button btnScan;
        private Button btnDiagnostics;
        private GroupBox groupBox3;
        private CheckBox chkTunerAGC;
        private TrackBar trackTunerGain;
        private Label lblTunerGain;
        private CheckBox chkRtlAGC;
        private GroupBox groupBox4;
        private TrackBar trackVolume;
        private Label lblVolume;
        private TrackBar trackSquelch;
        private Label lblSquelch;
        private CheckBox chkDeEmphasis;
        private ProgressBar progressSignal;
        private Label lblSignalStrength;
        private Label lblFreqOffset;
        private Button btnStart;
        private Button btnStop;
        private Label lblStatus;
        private System.Windows.Forms.Timer timerSignalLevel;
    }
}
