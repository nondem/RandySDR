using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace RandySDR
{
    /// <summary>
    /// Scanner overview: one bar per channel (dB above the noise floor) on a dark plot with dB gridlines,
    /// a MHz axis, a dashed threshold line, labels on detections and a highlighted locked channel.
    /// </summary>
    public class ScanGraph : Control
    {
        private static readonly Color Back = Color.FromArgb(10, 12, 18);
        private static readonly Color GridColor = Color.FromArgb(38, 46, 60);
        private static readonly Color TextColor = Color.FromArgb(170, 180, 195);

        private double[] _strength = Array.Empty<double>();
        private double _startMHz, _stopMHz;
        private int _thresholdDb = 10;
        private int _markerIndex = -1;

        public ScanGraph()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Back;
            ForeColor = TextColor;
            Font = new Font("Segoe UI", 9f);
        }

        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int ThresholdDb
        {
            get => _thresholdDb;
            set { _thresholdDb = value; Invalidate(); }
        }

        public void SetData(double[] strength, double startMHz, double stopMHz, int markerIndex)
        {
            _strength = strength;
            _startMHz = startMHz;
            _stopMHz = stopMHz;
            _markerIndex = markerIndex;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Back);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            float fh = Font.GetHeight(g);
            int left = (int)(fh * 3.2f), top = (int)(fh * 1.6f);
            var plot = new Rectangle(left, top, Width - left - (int)(fh * 2.2f), Height - top - (int)(fh * 2.2f));
            if (plot.Width < 40 || plot.Height < 30) return;

            double observed = 0;
            foreach (double v in _strength)
                if (!double.IsNaN(v) && v > observed) observed = v;
            double maxDb = Math.Clamp(Math.Ceiling(Math.Max(observed + 3, _thresholdDb + 8) / 10) * 10, 30, 90);

            using var gridPen = new Pen(GridColor);
            using var textBrush = new SolidBrush(TextColor);
            using var plotBack = new SolidBrush(Color.FromArgb(14, 18, 27));
            g.FillRectangle(plotBack, plot);

            int YOf(double db) => plot.Bottom - (int)(Math.Clamp(db, 0, maxDb) / maxDb * plot.Height);

            using var rightFmt = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center };
            for (double db = 0; db <= maxDb; db += 10)
            {
                int y = YOf(db);
                g.DrawLine(gridPen, plot.Left, y, plot.Right, y);
                g.DrawString($"{db:0}", Font, textBrush, new RectangleF(0, y - fh, plot.Left - 6, fh * 2), rightFmt);
            }
            g.DrawString("dB", Font, textBrush, 2, 0);

            double span = _stopMHz - _startMHz;
            int n = _strength.Length;
            double chStep = n > 1 ? span / (n - 1) : 0.1;
            double axisStart = _startMHz - chStep / 2, axisSpan = span + chStep;
            float XOf(double mhz) => plot.Left + (float)((mhz - axisStart) / axisSpan * plot.Width);

            if (axisSpan > 0 && n > 0)
            {
                double tick = NiceStep(axisSpan / Math.Max(3, plot.Width / (fh * 8)));
                using var centerFmt = new StringFormat { Alignment = StringAlignment.Center };
                for (double f = Math.Ceiling(axisStart / tick) * tick; f <= axisStart + axisSpan; f += tick)
                {
                    float x = XOf(f);
                    g.DrawLine(gridPen, x, plot.Top, x, plot.Bottom);
                    g.DrawString(f.ToString(tick < 0.1 ? "F3" : tick < 1 ? "F2" : "F1"), Font, textBrush, x, plot.Bottom + 3, centerFmt);
                }
                g.DrawString("MHz", Font, textBrush, plot.Right - g.MeasureString("MHz", Font).Width, Height - fh * 1.3f);
            }

            if (n > 0 && axisSpan > 0)
            {
                float slot = (float)plot.Width / n;
                float gap = slot >= 5 ? 1.5f : 0;
                float barW = Math.Max(1f, slot - gap);
                int columns = Math.Min(n, plot.Width);
                var hitLabels = new List<(float X, int Y, string Text)>();

                for (int c = 0; c < columns; c++)
                {
                    int i0 = (int)((long)c * n / columns), i1 = Math.Max(i0 + 1, (int)((long)(c + 1) * n / columns));
                    double best = double.NaN;
                    bool marker = false;
                    int bestIdx = i0;
                    for (int i = i0; i < Math.Min(i1, n); i++)
                    {
                        if (i == _markerIndex) marker = true;
                        if (!double.IsNaN(_strength[i]) && (double.IsNaN(best) || _strength[i] > best)) { best = _strength[i]; bestIdx = i; }
                    }
                    if (double.IsNaN(best)) continue;

                    float x = plot.Left + (float)c * plot.Width / columns;
                    float w = n <= plot.Width ? barW : Math.Max(1f, (float)plot.Width / columns);
                    int barTop = YOf(best);
                    if (plot.Bottom - barTop < 2) barTop = plot.Bottom - 2;
                    var bar = new RectangleF(x + gap / 2, barTop, w, plot.Bottom - barTop);

                    bool hit = best >= _thresholdDb;
                    Color c1 = marker ? Color.FromArgb(255, 70, 70) : hit ? Color.FromArgb(255, 190, 40) : Color.FromArgb(40, 200, 140);
                    Color c2 = Color.FromArgb(marker ? 120 : 60, c1);
                    using var br = new LinearGradientBrush(bar, c1, c2, 90f);
                    g.FillRectangle(br, bar);

                    if (hit)
                        hitLabels.Add((x + w / 2, barTop, (_startMHz + bestIdx * chStep).ToString("F1")));
                }

                using var labelFmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Far };
                float lastRight = float.NegativeInfinity;
                foreach (var (x, y, text) in hitLabels)
                {
                    float tw = g.MeasureString(text, Font).Width;
                    if (x - tw / 2 < lastRight + 2) continue;
                    g.DrawString(text, Font, Brushes.Gold, x, Math.Max(fh, y - 2), labelFmt);
                    lastRight = x + tw / 2;
                }
            }

            int ty = YOf(_thresholdDb);
            using var thr = new Pen(Color.FromArgb(255, 80, 80), 1.5f) { DashStyle = DashStyle.Dash };
            g.DrawLine(thr, plot.Left, ty, plot.Right, ty);
            string tl = $"Threshold +{_thresholdDb} dB";
            var tsz = g.MeasureString(tl, Font);
            g.DrawString(tl, Font, Brushes.OrangeRed, plot.Right - tsz.Width - 4, Math.Max(plot.Top, ty - tsz.Height));

            using var border = new Pen(Color.FromArgb(70, 84, 104));
            g.DrawRectangle(border, plot);

            if (n == 0)
            {
                using var big = new Font(Font.FontFamily, Font.Size * 1.3f);
                using var middle = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString("Start a scan to see signal strength per channel", big, textBrush, plot, middle);
            }
        }

        private static double NiceStep(double raw)
        {
            if (raw <= 0) return 1;
            double mag = Math.Pow(10, Math.Floor(Math.Log10(raw)));
            double norm = raw / mag;
            double nice = norm <= 1 ? 1 : norm <= 2 ? 2 : norm <= 5 ? 5 : 10;
            return nice * mag;
        }
    }
}
