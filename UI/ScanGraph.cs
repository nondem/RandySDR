using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace RandySDR
{
    /// <summary>Latest measured signal strength per scanner channel, relative to the measured noise floor.</summary>
    public class ScanGraph : Control
    {
        private static readonly Color Background = Color.FromArgb(10, 12, 18);
        private static readonly Color Grid = Color.FromArgb(42, 52, 67);
        private static readonly Color TextColor = Color.FromArgb(185, 198, 214);
        private static readonly Color Measured = Color.FromArgb(48, 198, 151);
        private static readonly Color AboveThreshold = Color.FromArgb(255, 190, 64);
        private static readonly Color Locked = Color.FromArgb(75, 193, 255);
        private readonly ToolTip _tip = new() { InitialDelay = 300, ReshowDelay = 100 };
        private double[] _strength = Array.Empty<double>();
        private IReadOnlyList<double> _channelHz = Array.Empty<double>();
        private double _thresholdDb = 10;
        private int _markerIndex = -1;
        private bool _running;
        private double _axisMin = 0, _axisMax = 30;
        private long _lastRangeExpansion;
        private int _hoverColumn = -1;

        public ScanGraph()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Background;
            ForeColor = TextColor;
            Font = new Font("Segoe UI", 9f);
        }

        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public double ThresholdDb
        {
            get => _thresholdDb;
            set
            {
                if (!double.IsFinite(value))
                    throw new ArgumentOutOfRangeException(nameof(value), "Threshold must be finite.");
                if (_thresholdDb == value) return;
                _thresholdDb = value;
                UpdateRange();
                Invalidate();
            }
        }

        public void SetData(double[] strength, IReadOnlyList<double> channelHz, int markerIndex, bool running)
        {
            ArgumentNullException.ThrowIfNull(strength);
            ArgumentNullException.ThrowIfNull(channelHz);
            if (strength.Length != channelHz.Count)
                throw new ArgumentException("Each strength measurement must have a channel frequency.", nameof(channelHz));

            bool newPlan = !ReferenceEquals(_channelHz, channelHz);
            _strength = strength;
            _channelHz = channelHz;
            _markerIndex = markerIndex;
            _running = running;
            UpdateRange(newPlan);
            if (newPlan) ClearTip();
            Invalidate();
        }

        private void UpdateRange(bool reset = false)
        {
            double min = Math.Min(0, _thresholdDb), max = Math.Max(0, _thresholdDb);
            foreach (double value in _strength)
            {
                if (!double.IsFinite(value)) continue;
                min = Math.Min(min, value);
                max = Math.Max(max, value);
            }

            double padding = Math.Max(2, (max - min) * 0.1);
            double step = NiceStep(Math.Max(10, max - min + 2 * padding) / 5);
            double desiredMin = Math.Floor(Math.Min(0, min - (min < 0 ? padding : 0)) / step) * step;
            double desiredMax = Math.Ceiling((max + padding) / step) * step;
            long now = Environment.TickCount64;
            if (reset || desiredMin < _axisMin || desiredMax > _axisMax)
            {
                _axisMin = reset ? desiredMin : Math.Min(_axisMin, desiredMin);
                _axisMax = reset ? desiredMax : Math.Max(_axisMax, desiredMax);
                _lastRangeExpansion = now;
            }
            else if (now - _lastRangeExpansion >= 8000 &&
                (desiredMin > _axisMin || desiredMax < _axisMax))
            {
                _axisMin = desiredMin;
                _axisMax = desiredMax;
                _lastRangeExpansion = now;
            }
        }

        private Rectangle PlotRectangle()
        {
            int fh = Font.Height;
            return new Rectangle(Math.Max(54, fh * 4), fh * 3 + 8,
                Width - Math.Max(54, fh * 4) - 18, Height - fh * 5 - 16);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.Clear(Background);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle plot = PlotRectangle();
            if (plot.Width < 40 || plot.Height < Math.Max(30, Font.Height + 4)) return;

            int count = 0, above = 0, peakIndex = -1;
            double peak = double.NegativeInfinity;
            for (int i = 0; i < _strength.Length; i++)
            {
                double value = _strength[i];
                if (!double.IsFinite(value)) continue;
                count++;
                if (value >= _thresholdDb) above++;
                if (value > peak) { peak = value; peakIndex = i; }
            }

            string summary = count == 0 ? "Waiting for channel measurements"
                : $"{(_running ? "Latest readings" : "Last scan")}  |  {count:N0}/{_strength.Length:N0} measured  |  {above:N0} above threshold  |  Peak {FormatMHz(_channelHz[peakIndex])} MHz ({peak:+0.0;-0.0;0.0} dB)";
            TextRenderer.DrawText(g, summary, Font, new Rectangle(4, 1, Width - 8, Font.Height + 2), TextColor,
                TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);

            DrawLegend(g);
            using var plotBrush = new SolidBrush(Color.FromArgb(15, 20, 30));
            using var gridPen = new Pen(Grid);
            using var textBrush = new SolidBrush(TextColor);
            g.FillRectangle(plotBrush, plot);
            int YOf(double db) => plot.Bottom - (int)Math.Round((db - _axisMin) / (_axisMax - _axisMin) * plot.Height);

            double dbStep = NiceStep((_axisMax - _axisMin) / Math.Max(2, plot.Height / (Font.Height * 2.5)));
            using var right = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center };
            for (double db = Math.Ceiling(_axisMin / dbStep) * dbStep; db <= _axisMax + dbStep * 0.001; db += dbStep)
            {
                int y = YOf(db);
                g.DrawLine(gridPen, plot.Left, y, plot.Right, y);
                g.DrawString(db.ToString("0.#"), Font, textBrush, new RectangleF(0, y - Font.Height, plot.Left - 6, Font.Height * 2), right);
            }
            TextRenderer.DrawText(g, "dB above noise", Font, new Point(plot.Left, plot.Top - Font.Height - 1), TextColor,
                TextFormatFlags.NoPadding);

            if (_channelHz.Count > 0)
            {
                double stepHz = _channelHz.Count > 1 ? _channelHz[1] - _channelHz[0] : 1000;
                double axisStart = _channelHz[0] - stepHz / 2;
                double axisEnd = _channelHz[^1] + stepHz / 2;
                double axisSpan = axisEnd - axisStart;
                float XOf(double hz) => plot.Left + (float)((hz - axisStart) / axisSpan * plot.Width);
                double tickMHz = NiceStep(axisSpan / 1e6 / Math.Max(1, plot.Width / (Font.Height * 8)));
                using var center = new StringFormat { Alignment = StringAlignment.Center };
                int decimals = Math.Clamp((int)Math.Ceiling(-Math.Log10(tickMHz)), 0, 6);
                double startMHz = axisStart / 1e6, endMHz = axisEnd / 1e6;
                for (double mhz = Math.Ceiling(startMHz / tickMHz) * tickMHz; mhz <= endMHz + tickMHz * 0.0001; mhz += tickMHz)
                {
                    float x = XOf(mhz * 1e6);
                    g.DrawLine(gridPen, x, plot.Top, x, plot.Bottom);
                    string label = mhz.ToString($"F{decimals}");
                    float labelWidth = g.MeasureString(label, Font).Width;
                    if (x - labelWidth / 2 >= 0 && x + labelWidth / 2 <= Width)
                        g.DrawString(label, Font, textBrush, x, plot.Bottom + 3, center);
                }

                int columns = Math.Min(_strength.Length, plot.Width);
                using var normalBrush = new SolidBrush(Measured);
                using var hitBrush = new SolidBrush(AboveThreshold);
                int zeroY = YOf(0);
                for (int col = 0; col < columns; col++)
                {
                    int best = BestInColumn(col, columns);
                    if (best < 0) continue;
                    float left = plot.Left + (float)col * plot.Width / columns;
                    float width = Math.Max(1, (float)plot.Width / columns - (columns < plot.Width / 4 ? 1 : 0));
                    int y = YOf(_strength[best]);
                    g.FillRectangle(_strength[best] >= _thresholdDb ? hitBrush : normalBrush,
                        left, Math.Min(y, zeroY), width, Math.Max(1, Math.Abs(zeroY - y)));
                }

                if (_markerIndex >= 0 && _markerIndex < _channelHz.Count)
                {
                    float x = XOf(_channelHz[_markerIndex]);
                    using var markerPen = new Pen(Locked, 2);
                    g.DrawLine(markerPen, x, plot.Top, x, plot.Bottom);
                }
            }

            int thresholdY = YOf(_thresholdDb);
            using var thresholdPen = new Pen(AboveThreshold, 1.5f) { DashStyle = DashStyle.Dash };
            g.DrawLine(thresholdPen, plot.Left, thresholdY, plot.Right, thresholdY);
            string thresholdText = $"Threshold {_thresholdDb:0.0} dB";
            var thresholdSize = TextRenderer.MeasureText(thresholdText, Font, Size.Empty, TextFormatFlags.NoPadding);
            var thresholdRect = new Rectangle(plot.Right - thresholdSize.Width - 5,
                Math.Clamp(thresholdY - thresholdSize.Height - 2, plot.Top, plot.Bottom - thresholdSize.Height),
                thresholdSize.Width + 3, thresholdSize.Height);
            g.FillRectangle(plotBrush, thresholdRect);
            TextRenderer.DrawText(g, thresholdText, Font, thresholdRect, AboveThreshold, TextFormatFlags.NoPadding);
            using var border = new Pen(Color.FromArgb(80, 98, 120));
            g.DrawRectangle(border, plot);
            TextRenderer.DrawText(g, "Frequency (MHz)", Font,
                new Rectangle(plot.Left, Height - Font.Height, plot.Width, Font.Height), TextColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPadding);

            if (count == 0)
            {
                using var middle = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString(_strength.Length == 0 ? "Start a scan to see channel activity" : "Scanning - waiting for measurements",
                    Font, textBrush, plot, middle);
            }
        }

        private void DrawLegend(Graphics g)
        {
            string[] labels = { "Measured", "Above threshold", "Locked" };
            Color[] colors = { Measured, AboveThreshold, Locked };
            int x = 4, y = Font.Height + 4;
            for (int i = 0; i < labels.Length; i++)
            {
                int width = TextRenderer.MeasureText(labels[i], Font, Size.Empty, TextFormatFlags.NoPadding).Width;
                if (x + width + 20 > Width) break;
                using var brush = new SolidBrush(colors[i]);
                g.FillRectangle(brush, x, y + Font.Height / 2 - 3, 10, 6);
                TextRenderer.DrawText(g, labels[i], Font, new Point(x + 14, y), TextColor, TextFormatFlags.NoPadding);
                x += width + 28;
            }
            if (x + TextRenderer.MeasureText("Hover for details", Font).Width < Width)
                TextRenderer.DrawText(g, "Hover for details", Font, new Point(x, y), TextColor, TextFormatFlags.NoPadding);
        }

        private int BestInColumn(int column, int columns)
        {
            var (first, end) = ColumnRange(column, columns);
            int best = -1;
            for (int i = first; i < end; i++)
                if (double.IsFinite(_strength[i]) && (best < 0 || _strength[i] > _strength[best])) best = i;
            return best;
        }

        private (int First, int End) ColumnRange(int column, int columns)
        {
            double step = _channelHz.Count > 1 ? _channelHz[1] - _channelHz[0] : 1000;
            double start = _channelHz[0] - step / 2;
            double span = _channelHz[^1] + step / 2 - start;
            double lower = start + span * column / columns;
            double upper = start + span * (column + 1) / columns;

            int LowerBound(double value)
            {
                int lo = 0, hi = _channelHz.Count;
                while (lo < hi)
                {
                    int mid = lo + (hi - lo) / 2;
                    if (_channelHz[mid] < value) lo = mid + 1;
                    else hi = mid;
                }
                return lo;
            }

            return (LowerBound(lower), LowerBound(upper));
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            Rectangle plot = PlotRectangle();
            if (!plot.Contains(e.Location) || _strength.Length == 0) { ClearTip(); return; }
            int columns = Math.Min(_strength.Length, plot.Width);
            int column = Math.Min(columns - 1, (int)((long)(e.X - plot.Left) * columns / plot.Width));
            if (column == _hoverColumn) return;
            _hoverColumn = column;
            int best = BestInColumn(column, columns);
            var (first, end) = ColumnRange(column, columns);
            string range = end - first > 1 ? $"Strongest of {end - first} channels in this column\n" : "";
            string message = best < 0 ? (first == end ? "No channel in this column" : "Not yet measured") :
                $"{range}{FormatMHz(_channelHz[best])} MHz\n{_strength[best]:+0.0;-0.0;0.0} dB above noise";
            _tip.SetToolTip(this, message);
        }

        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); ClearTip(); }
        private void ClearTip()
        {
            if (_hoverColumn < 0) return;
            _hoverColumn = -1;
            _tip.SetToolTip(this, null);
            _tip.Hide(this);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _tip.Dispose();
            base.Dispose(disposing);
        }

        private static string FormatMHz(double hz) => (hz / 1e6).ToString("0.000###");

        private static double NiceStep(double raw)
        {
            if (!double.IsFinite(raw) || raw <= 0) return 1;
            double magnitude = Math.Pow(10, Math.Floor(Math.Log10(raw)));
            double normalized = raw / magnitude;
            return (normalized <= 1 ? 1 : normalized <= 2 ? 2 : normalized <= 5 ? 5 : 10) * magnitude;
        }
    }
}
