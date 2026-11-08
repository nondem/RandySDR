using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace RandySDR
{
    internal sealed class SignalMeter : Control
    {
        private int _minimum;
        private int _maximum = 100;
        private int _value;

        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        internal int Minimum
        {
            get => _minimum;
            set
            {
                _minimum = value;
                if (_maximum < value) _maximum = value;
                Value = _value;
            }
        }

        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        internal int Maximum
        {
            get => _maximum;
            set
            {
                _maximum = Math.Max(_minimum, value);
                Value = _value;
            }
        }

        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        internal int Value
        {
            get => _value;
            set
            {
                int next = Math.Clamp(value, _minimum, _maximum);
                if (_value == next) return;
                _value = next;
                Invalidate();
            }
        }

        internal SignalMeter()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint
                | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = CardGroupBox.ActiveCardColor;
            MinimumSize = new Size(40, 16);
            TabStop = false;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (ClientSize.Width < 4 || ClientSize.Height < 4) return;

            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var bounds = new Rectangle(0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
            var track = Rectangle.Inflate(bounds, -1, -1);
            int radius = Math.Min(6, Math.Max(2, track.Height / 2));
            using var trackPath = RoundedRectangle(track, radius);
            using (var background = new SolidBrush(Color.FromArgb(15, 19, 27)))
                g.FillPath(background, trackPath);

            float range = Math.Max(1, _maximum - _minimum);
            float percent = Math.Clamp((_value - _minimum) / range, 0f, 1f);
            int fillWidth = (int)Math.Round(track.Width * percent);
            if (fillWidth > 0)
            {
                var fillBounds = new Rectangle(track.X, track.Y, fillWidth, track.Height);
                using var fillPath = RoundedRectangle(fillBounds, radius);
                var state = g.Save();
                g.SetClip(fillBounds);
                using (var gradient = new LinearGradientBrush(
                    track, Color.FromArgb(0, 174, 218), Color.FromArgb(255, 135, 95), LinearGradientMode.Horizontal))
                {
                    gradient.InterpolationColors = new ColorBlend
                    {
                        Positions = new[] { 0f, 0.58f, 0.82f, 1f },
                        Colors = new[]
                        {
                            Color.FromArgb(0, 174, 218),
                            Color.FromArgb(52, 211, 153),
                            Color.FromArgb(250, 204, 90),
                            Color.FromArgb(255, 135, 95)
                        }
                    };
                    g.FillPath(gradient, fillPath);
                }

                int shineHeight = Math.Max(2, track.Height / 4);
                using (var shine = new LinearGradientBrush(
                    new Rectangle(track.X, track.Y, track.Width, shineHeight),
                    Color.FromArgb(74, Color.White), Color.FromArgb(0, Color.White), LinearGradientMode.Vertical))
                    g.FillRectangle(shine, track.X, track.Y, track.Width, shineHeight);
                g.Restore(state);
            }

            using (var ticks = new Pen(Color.FromArgb(55, 220, 232, 247), 1f))
            {
                for (int i = 1; i < 10; i++)
                {
                    int x = track.X + track.Width * i / 10;
                    int tickHeight = i % 5 == 0 ? Math.Max(3, track.Height / 3) : Math.Max(2, track.Height / 5);
                    g.DrawLine(ticks, x, track.Bottom - tickHeight, x, track.Bottom - 1);
                }
            }

            using (var border = new Pen(Color.FromArgb(95, 137, 157, 181), 1f))
                g.DrawPath(border, trackPath);
            using (var topEdge = new Pen(Color.FromArgb(28, Color.White), 1f))
                g.DrawLine(topEdge, track.X + radius, track.Y + 1, track.Right - radius, track.Y + 1);
        }

        private static GraphicsPath RoundedRectangle(Rectangle rectangle, int radius)
        {
            int diameter = Math.Min(radius * 2, Math.Min(rectangle.Width, rectangle.Height));
            var path = new GraphicsPath();
            path.AddArc(rectangle.X, rectangle.Y, diameter, diameter, 180, 90);
            path.AddArc(rectangle.Right - diameter, rectangle.Y, diameter, diameter, 270, 90);
            path.AddArc(rectangle.Right - diameter, rectangle.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(rectangle.X, rectangle.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
