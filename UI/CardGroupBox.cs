// CardGroupBox - GroupBox drawn as a filled, rounded "card" with an accent header and divider.
// Children inherit CardColor as their background so labels/checkboxes blend in.
using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace RandySDR
{
    public class CardGroupBox : GroupBox
    {
        public static readonly Color CardColor = Color.FromArgb(30, 34, 44);
        public static readonly Color BorderColor = Color.FromArgb(52, 60, 78);
        public static readonly Color AccentColor = Color.FromArgb(0, 168, 232);

        public CardGroupBox()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = CardColor;
            Font = new Font("Segoe UI Semibold", 9.5f);
        }

        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public override Color ForeColor { get => Color.FromArgb(235, 238, 245); set { } }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent?.BackColor ?? Color.Black);

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using var path = RoundedRect(rect, 10);
            using (var fill = new SolidBrush(CardColor)) g.FillPath(fill, path);
            using (var pen = new Pen(BorderColor)) g.DrawPath(pen, path);

            // accent bar at the left of the header
            using (var accent = new SolidBrush(AccentColor)) g.FillRectangle(accent, 10, 4, 4, Font.Height);
            TextRenderer.DrawText(g, Text, Font, new Point(18, 2), Color.FromArgb(235, 238, 245), TextFormatFlags.NoPadding);

            int lineY = 6 + Font.Height;
            using var divider = new Pen(BorderColor);
            g.DrawLine(divider, 12, lineY, Width - 13, lineY);
        }

        private static GraphicsPath RoundedRect(Rectangle r, int radius)
        {
            int d = radius * 2;
            var p = new GraphicsPath();
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }
    }
}
