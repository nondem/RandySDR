namespace RandySDR
{
    public sealed class ClipDetailsForm : Form
    {
        public ClipDetailsForm(ClipRow clip)
        {
            Text = "Clip details";
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            ClientSize = new Size(720, 480);
            MinimumSize = new Size(440, 320);

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 6, Padding = new Padding(12)
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var metadata = new Label
            {
                AutoSize = true, Dock = DockStyle.Fill,
                Text = $"{clip.StartUtc.ToLocalTime():g}  |  {clip.Hz / 1e6:0.0000} MHz  |  {clip.Mode}  |  {clip.Class}  |  {clip.DurationSec:0.0} s\n" +
                       $"Status: {clip.Status}  |  Category: {(string.IsNullOrWhiteSpace(clip.Category) ? "not classified" : clip.Category)}" +
                       (clip.EndReason.StartsWith("screened-", StringComparison.Ordinal) ? $"  |  ScreenerBot: {clip.EndReason[9..]} (partial recording)" : ""),
                Margin = new Padding(0, 0, 0, 8)
            };
            var summary = new TextBox
            {
                Name = "clipSummary", AccessibleName = "Classification summary",
                Multiline = true, ReadOnly = true, MaxLength = 0, ScrollBars = ScrollBars.Vertical,
                Dock = DockStyle.Fill, Text = clip.Summary
            };
            var transcript = new TextBox
            {
                Name = "clipTranscript", AccessibleName = "Full transcript",
                Multiline = true, ReadOnly = true, MaxLength = 0, ScrollBars = ScrollBars.Vertical,
                Dock = DockStyle.Fill, Text = clip.Transcript
            };
            var close = new Button { Text = "Close", AutoSize = true, Anchor = AnchorStyles.Right, DialogResult = DialogResult.Cancel };
            layout.Controls.Add(metadata, 0, 0);
            layout.Controls.Add(new Label { Text = "Summary", AutoSize = true }, 0, 1);
            layout.Controls.Add(summary, 0, 2);
            layout.Controls.Add(new Label
            {
                Text = string.IsNullOrWhiteSpace(clip.Transcript) ? "Transcript (not available)" : "Transcript",
                AutoSize = true, Margin = new Padding(0, 8, 0, 0)
            }, 0, 3);
            layout.Controls.Add(transcript, 0, 4);
            layout.Controls.Add(close, 0, 5);
            Controls.Add(layout);
            CancelButton = close;
        }
    }
}
