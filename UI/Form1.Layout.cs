// Form1 - top-level layout: left column = radio controls + spectrum, right column = scanner.
// Designer controls are re-parented into docked/table layouts so everything resizes with the window.
// Designer units are 144-dpi pixels (AutoScaleMode.Dpi); do not apply extra DPI scaling here.
namespace RandySDR
{
    public partial class Form1
    {
        private Panel _leftPanel = null!;
        private Panel _rightPanel = null!;

        private void BuildMainLayout()
        {
            SuspendLayout();
            foreach (var c in new Control[] { groupBox1, groupBox2, groupBox3, groupBox4, groupBoxAudio, groupBox6, lblStatus })
                Controls.Remove(c);

            _rightPanel = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(6, 12, 12, 12) };
            _leftPanel = new Panel { Dock = DockStyle.Left, Width = 1250, AutoScroll = true, Padding = new Padding(12, 12, 6, 12) };
            Controls.Add(_rightPanel);
            Controls.Add(_leftPanel);

            var table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, MinimumSize = new Size(900, 1000) };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 84));
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 160));
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 170));
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 170));
            table.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));

            var modes = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
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
