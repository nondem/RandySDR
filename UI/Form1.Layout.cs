// Form1 - top-level layout: left column = radio controls + spectrum, right column = scanner.
// Designer controls are re-parented into docked/table layouts so everything resizes with the window.
// Designer units are 144-dpi pixels (AutoScaleMode.Dpi); do not apply extra DPI scaling here.
namespace RandySDR
{
    public partial class Form1
    {
        private Panel _leftPanel = null!;
        private Panel _rightPanel = null!;
        private TableLayoutPanel _modesTable = null!;

        private void BuildMainLayout()
        {
            SuspendLayout();
            foreach (var c in new Control[] { groupBox1, groupBox2, groupBox3, groupBox4, groupBoxAudio, groupBox6, lblStatus })
                Controls.Remove(c);

            _rightPanel = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(6, 12, 12, 12) };
            _leftPanel = new Panel { Dock = DockStyle.Left, Width = 1250, AutoScroll = true, Padding = new Padding(12, 12, 6, 12) };
            Controls.Add(_rightPanel);
            Controls.Add(_leftPanel);

            PrepareCompactCards();
            var table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, MinimumSize = new Size(900, 850) };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, CardContentHeight(groupBox1)));
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, CardContentHeight(groupBox2)));
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 0));
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, CardContentHeight(groupBox3)));
            table.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));

            var modes = _modesTable = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
            modes.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            modes.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

            void Place(Control host, Control c, int col, int row, bool fill = true)
            {
                c.Dock = DockStyle.Fill;
                c.Margin = new Padding(0, 0, 0, 6);
                ((TableLayoutPanel)host).Controls.Add(c, col, row);
            }

            groupBox4.Margin = new Padding(0, 0, 6, 6);
            groupBoxAudio.Margin = new Padding(6, 0, 0, 6);
            groupBox4.Dock = DockStyle.Fill;
            BuildAudioPanel();
            groupBoxAudio.Dock = DockStyle.Fill;
            table.RowStyles[2].Height = Math.Max(CardContentHeight(groupBox4),
                groupBoxAudio.DisplayRectangle.Top + groupBoxAudio.Controls[0].GetPreferredSize(Size.Empty).Height + 12);
            modes.Controls.Add(groupBox4, 0, 0);
            modes.Controls.Add(groupBoxAudio, 1, 0);

            Place(table, groupBox1, 0, 0);
            Place(table, groupBox2, 0, 1);
            table.Controls.Add(modes, 0, 2);
            Place(table, groupBox3, 0, 3);
            Place(table, groupBox6, 0, 4);
            Place(table, lblStatus, 0, 5);
            lblStatus.AutoSize = false;
            lblStatus.TextAlign = ContentAlignment.MiddleLeft;

            _leftPanel.Controls.Add(table);
            ResumeLayout(true);
        }

        private void PrepareCompactCards()
        {
            foreach (var card in new[] { groupBox2, groupBox4, groupBox6 })
            {
                int firstTop = card.Controls.Cast<Control>().Min(c => c.Top);
                int shift = Math.Max(0, card.HeaderHeight + 2 - firstTop);
                foreach (Control control in card.Controls) control.Top += shift;
            }

            LayoutSpectrumControls();
            lblSpecZoom.TextChanged += (_, _) => LayoutSpectrumControls();
            lblSpecRange.TextChanged += (_, _) => LayoutSpectrumControls();
            lblSpecRef.TextChanged += (_, _) => LayoutSpectrumControls();

            var device = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 5, RowCount = 1, Margin = Padding.Empty };
            device.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            groupBox1.Controls.Clear();
            groupBox1.Padding = new Padding(10, 4, 10, 6);
            cmbDevices.Dock = DockStyle.Fill;
            cmbDevices.Margin = new Padding(0, 3, 8, 3);
            device.Controls.Add(cmbDevices, 0, 0);
            var deviceButtons = new[] { btnOpen, btnClose, btnStart, btnStop };
            for (int i = 0; i < deviceButtons.Length; i++)
            {
                var button = deviceButtons[i];
                device.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, Math.Max(78, button.GetPreferredSize(Size.Empty).Width + 12)));
                button.Dock = DockStyle.Fill;
                button.MinimumSize = new Size(0, 32);
                button.Margin = new Padding(0, 2, 6, 2);
                device.Controls.Add(button, i + 1, 0);
            }
            groupBox1.Controls.Add(device);

            var gain = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 1, RowCount = 3, Margin = Padding.Empty };
            gain.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (int i = 0; i < 3; i++) gain.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            groupBox3.Controls.Clear();
            groupBox3.Padding = new Padding(10, 4, 10, 6);

            var tuner = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 3, Margin = Padding.Empty };
            tuner.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            tuner.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            tuner.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            chkTunerAGC.Anchor = AnchorStyles.Left;
            lblTunerGain.Anchor = AnchorStyles.Left;
            trackTunerGain.Dock = DockStyle.Fill;
            tuner.Controls.Add(chkTunerAGC, 0, 0);
            tuner.Controls.Add(trackTunerGain, 1, 0);
            tuner.Controls.Add(lblTunerGain, 2, 0);

            var input = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 5, Margin = Padding.Empty };
            for (int i = 0; i < 4; i++) input.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            input.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            foreach (Control control in new Control[] { chkRtlAGC, chkOffsetTuning, lblClip, lblRxBandwidth })
                control.Anchor = AnchorStyles.Left;
            trackRxBandwidth.Dock = DockStyle.Fill;
            input.Controls.Add(chkRtlAGC, 0, 0);
            input.Controls.Add(chkOffsetTuning, 1, 0);
            input.Controls.Add(lblClip, 2, 0);
            input.Controls.Add(lblRxBandwidth, 3, 0);
            input.Controls.Add(trackRxBandwidth, 4, 0);

            var sampling = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = false, Margin = Padding.Empty };
            sampling.Controls.AddRange(new Control[] { rdoQuadrature, rdoDirectSamplingI, rdoDirectSamplingQ });
            gain.Controls.Add(tuner, 0, 0);
            gain.Controls.Add(input, 0, 1);
            gain.Controls.Add(sampling, 0, 2);
            groupBox3.Controls.Add(gain);
        }

        private void LayoutSpectrumControls()
        {
            trackSpecZoom.Left = lblSpecZoom.Right + 8;
            lblSpecRange.Left = trackSpecZoom.Right + 12;
            trackSpecRange.Left = lblSpecRange.Right + 8;
            lblSpecRef.Left = trackSpecRange.Right + 12;
            trackSpecRef.Left = lblSpecRef.Right + 8;
        }

        private static int CardContentHeight(CardGroupBox card)
        {
            int bottom = card.Controls.Cast<Control>().Max(c => c.Dock == DockStyle.Fill
                ? card.DisplayRectangle.Top + c.GetPreferredSize(Size.Empty).Height
                : c.Bottom);
            return bottom + card.Padding.Bottom + 8;
        }

        private void FitToScreen()
        {
            var area = Screen.FromControl(this).WorkingArea;
            Width = Math.Min(Width, area.Width);
            Height = Math.Min(Height, area.Height);
            Left = area.Left + (area.Width - Width) / 2;
            Top = area.Top + (area.Height - Height) / 2;
            ResizeColumns();
            Resize += (_, _) => ResizeColumns();
        }

        private void ResizeColumns() => _leftPanel.Width = (int)(ClientSize.Width * 0.54);
    }
}
