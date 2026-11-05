// Form1 - visual polish: deep window background, flat accent buttons, bolder status bar.
// Cards themselves are drawn by CardGroupBox.
namespace RandySDR
{
    public partial class Form1
    {
        private static readonly Color WindowColor = Color.FromArgb(18, 20, 27);
        private static readonly Color ButtonColor = Color.FromArgb(44, 52, 70);

        private void ApplyVisualPolish()
        {
            BackColor = WindowColor;
            _leftPanel.BackColor = WindowColor;
            _rightPanel.BackColor = WindowColor;
            StyleButtons(this);

            btnStart.BackColor = Color.FromArgb(30, 120, 70);
            btnStop.BackColor = Color.FromArgb(150, 50, 50);
            _btnScanToggle.BackColor = Color.FromArgb(0, 110, 170);

            lblStatus.BackColor = Color.FromArgb(26, 30, 40);
            lblStatus.ForeColor = Color.FromArgb(150, 220, 255);
            lblStatus.Padding = new Padding(10, 0, 0, 0);
        }

        private static void StyleButtons(Control parent)
        {
            foreach (Control c in parent.Controls)
            {
                if (c is Button b)
                {
                    b.FlatStyle = FlatStyle.Flat;
                    b.UseVisualStyleBackColor = false;
                    b.BackColor = ButtonColor;
                    b.ForeColor = Color.FromArgb(235, 238, 245);
                    b.FlatAppearance.BorderColor = CardGroupBox.BorderColor;
                    b.FlatAppearance.MouseOverBackColor = Color.FromArgb(60, 72, 96);
                    b.FlatAppearance.MouseDownBackColor = CardGroupBox.AccentColor;
                    b.Cursor = Cursors.Hand;
                }
                StyleButtons(c);
            }
        }
    }
}
