namespace RandySDR
{
    public sealed class ScreenerSettingsForm : Form
    {
        private readonly RobotController _robot;
        private readonly NumericUpDown _seconds = new()
        {
            Minimum = 4, Maximum = 30, Increment = 2, DecimalPlaces = 0, Width = 90
        };

        public ScreenerSettingsForm(RobotController robot)
        {
            _robot = robot;
            Text = "ScreenerBot settings";
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(520, 265);
            MinimizeBox = MaximizeBox = false;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            double value = double.IsFinite(robot.Config.ScreenerSeconds) ? robot.Config.ScreenerSeconds : 4;
            _seconds.Value = (decimal)Math.Clamp(value, 4, 30);
            int corrections = robot.Store.ScreeningCorrections();
            var layout = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(12)
            };
            layout.Controls.Add(new Label
            {
                AutoSize = true, MaximumSize = new Size(480, 0),
                Text = "ScreenerBot works only during robot scans with Record signals enabled. Changes apply at the next lock."
            });
            var row = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
            row.Controls.Add(new Label { Text = "Minimum open-audio screening time (seconds):", AutoSize = true, Margin = new Padding(0, 7, 5, 0) });
            row.Controls.Add(_seconds);
            layout.Controls.Add(row);
            layout.Controls.Add(new Label
            {
                AutoSize = true, MaximumSize = new Size(480, 0),
                Text = $"Filtered clips marked Wrong: {corrections}. Corrections do not automatically change screening."
            });
            var suggest = new Button { Text = "Suggest safer duration", AutoSize = true, Enabled = corrections > 0 };
            suggest.Click += (_, _) =>
            {
                decimal proposed = Math.Min(_seconds.Maximum, _seconds.Value + 2);
                if (MessageBox.Show(this, $"Set the screening duration to {proposed} seconds? Longer screening can reduce premature skips but slows scanning.",
                    "Review suggestion", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    _seconds.Value = proposed;
            };
            layout.Controls.Add(suggest);
            var buttons = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
            var ok = new Button { Text = "OK", DialogResult = DialogResult.OK };
            var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel };
            ok.Click += (_, _) =>
            {
                _robot.Config.ScreenerSeconds = (double)_seconds.Value;
                _robot.Config.Save();
            };
            buttons.Controls.AddRange(new Control[] { ok, cancel });
            layout.Controls.Add(buttons);
            Controls.Add(layout);
            AcceptButton = ok;
            CancelButton = cancel;
        }
    }
}
