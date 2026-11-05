using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace RandySDR
{
    /// <summary>
    /// Dark-themed spectrum plot (filled trace, peak hold, grid, tuned-channel marker)
    /// with a scrolling waterfall underneath.
    /// </summary>
    public class SpectrumDisplay : Control
    {
        private const int WaterfallRows = 120;

        private float[] _db = Array.Empty<float>();
        private float[] _peak = Array.Empty<float>();
        private Bitmap? _waterfall;
        private Bitmap? _waterfallTmp;
        private int _wfWidth;

        private double _centerHz;
        private int _sampleRateHz = 2_400_000;
        private int _channelBwHz = 200_000;

        private float _minDb = -100f;
        private float _maxDb = -30f;
        private bool _rangeInit;

        private int _mouseX = -1;
        private static readonly Color[] Palette = BuildPalette();

        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public bool PeakHold { get; set; } = true;

        private double _zoom = 1.0;
        private bool _autoRange = true;
        private float _manualRange = 80f;
        private float _manualRef = -20f;

        /// <summary>Horizontal zoom (1 = full sample-rate span), centred on the tuned frequency.</summary>
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public double Zoom
        {
            get => _zoom;
            set { _zoom = Math.Clamp(value, 1.0, 32.0); Invalidate(); }
        }

        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public bool AutoRange
        {
            get => _autoRange;
            set { _autoRange = value; if (_db.Length > 0) { UpdateRange(); } Invalidate(); }
        }

        /// <summary>Manual dB span shown (used when AutoRange is off).</summary>
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public float RangeDb
        {
            get => _manualRange;
            set { _manualRange = Math.Clamp(value, 10f, 140f); if (!_autoRange) UpdateRangeManual(); Invalidate(); }
        }

        /// <summary>Manual top-of-scale level in dBFS (used when AutoRange is off).</summary>
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public float RefDb
        {
            get => _manualRef;
            set { _manualRef = Math.Clamp(value, -120f, 10f); if (!_autoRange) UpdateRangeManual(); Invalidate(); }
        }

        public float CurrentMaxDb => _maxDb;
        public float CurrentMinDb => _minDb;

        private void UpdateRangeManual()
        {
            _maxDb = _manualRef;
            _minDb = _manualRef - _manualRange;
            _rangeInit = true;
        }

        public SpectrumDisplay()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Color.FromArgb(10, 12, 18);
            ForeColor = Color.FromArgb(170, 180, 195);
            Font = new Font("Segoe UI", 8f);
        }

        public void SetSpectrum(float[] db, double centerHz, int sampleRateHz, int channelBwHz)
        {
            if (db.Length == 0)
                return;

            _centerHz = centerHz;
            _sampleRateHz = sampleRateHz;
            _channelBwHz = channelBwHz;

            if (_db.Length != db.Length)
            {
                _db = new float[db.Length];
                _peak = new float[db.Length];
                Array.Fill(_peak, -200f);
            }
            Array.Copy(db, _db, db.Length);

            for (int i = 0; i < _peak.Length; i++)
            {
                float decayed = _peak[i] - 0.4f;
                _peak[i] = Math.Max(_db[i], decayed);
            }

            UpdateRange();
            PushWaterfallRow();
            Invalidate();
        }

        public void Clear()
        {
            _db = Array.Empty<float>();
            _peak = Array.Empty<float>();
            _rangeInit = false;
            _waterfall?.Dispose();
            _waterfallTmp?.Dispose();
            _waterfall = _waterfallTmp = null;
            Invalidate();
        }

        private void UpdateRange()
        {
            if (!_autoRange)
            {
                UpdateRangeManual();
                return;
            }

            // Noise floor ~ 20th percentile of a decimated copy; top = strongest bin
            int step = Math.Max(1, _db.Length / 256);
            var sample = new List<float>(256);
            float max = float.MinValue;
            for (int i = 0; i < _db.Length; i += step)
            {
                sample.Add(_db[i]);
                if (_db[i] > max) max = _db[i];
            }
            sample.Sort();
            float floor = sample[sample.Count / 5];

            float targetMin = floor - 8f;
            float targetMax = Math.Max(max + 8f, targetMin + 50f);

            if (!_rangeInit)
            {
                _minDb = targetMin;
                _maxDb = targetMax;
                _rangeInit = true;
            }
            else
            {
                _minDb += (targetMin - _minDb) * 0.08f;
                _maxDb += (targetMax - _maxDb) * 0.08f;
            }
        }

        private void PushWaterfallRow()
        {
            int w = _db.Length;
            if (_waterfall == null || _wfWidth != w)
            {
                _waterfall?.Dispose();
                _waterfallTmp?.Dispose();
                _waterfall = new Bitmap(w, WaterfallRows, PixelFormat.Format32bppRgb);
                _waterfallTmp = new Bitmap(w, WaterfallRows, PixelFormat.Format32bppRgb);
                _wfWidth = w;
                using var g = Graphics.FromImage(_waterfall);
                g.Clear(Palette[0]);
            }

            // Scroll down by one row, then draw the newest row at the top
            using (var g = Graphics.FromImage(_waterfallTmp!))
            {
                g.CompositingMode = CompositingMode.SourceCopy;
                g.DrawImageUnscaled(_waterfall, 0, 1);
            }
            (_waterfall, _waterfallTmp) = (_waterfallTmp, _waterfall);

            var data = _waterfall!.LockBits(new Rectangle(0, 0, w, 1), ImageLockMode.WriteOnly, PixelFormat.Format32bppRgb);
            try
            {
                var row = new int[w];
                float span = Math.Max(1f, _maxDb - _minDb);
                for (int x = 0; x < w; x++)
                {
                    float n = Math.Clamp((_db[x] - _minDb) / span, 0f, 1f);
                    row[x] = Palette[(int)(n * (Palette.Length - 1))].ToArgb();
                }
                System.Runtime.InteropServices.Marshal.Copy(row, 0, data.Scan0, w);
            }
            finally
            {
                _waterfall.UnlockBits(data);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(BackColor);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            const int left = 44, right = 8, top = 8, axisH = 18;
            int plotW = Width - left - right;
            if (plotW < 50 || Height < 100)
                return;

            int totalH = Height - top - axisH - 4;
            int specH = (int)(totalH * 0.55);
            int gap = 6;
            int wfH = totalH - specH - gap;
            var specRect = new Rectangle(left, top, plotW, specH);
            var wfRect = new Rectangle(left, top + specH + gap, plotW, wfH);

            using var panelBrush = new SolidBrush(Color.FromArgb(14, 18, 28));
            g.FillRectangle(panelBrush, specRect);

            using var gridPen = new Pen(Color.FromArgb(40, 255, 255, 255));
            using var textBrush = new SolidBrush(ForeColor);
            double visSpan = _sampleRateHz / _zoom;
            double halfSpan = visSpan / 2.0;
            int n = _db.Length;
            // Visible bin window [vb0, vb0 + vbN) of the full-span FFT
            double vbF = n / _zoom;
            double vb0 = (n - vbF) / 2.0;

            if (_db.Length == 0)
            {
                using var f = new Font(Font.FontFamily, 10f);
                var msg = "Spectrum idle - press Start";
                var sz = g.MeasureString(msg, f);
                g.DrawString(msg, f, textBrush, specRect.X + (specRect.Width - sz.Width) / 2, specRect.Y + (specRect.Height - sz.Height) / 2);
                using var border0 = new Pen(Color.FromArgb(60, 70, 90));
                g.DrawRectangle(border0, specRect);
                g.DrawRectangle(border0, wfRect);
                return;
            }

            // Horizontal dB grid
            float span = Math.Max(1f, _maxDb - _minDb);
            float dbStep = span > 80 ? 20f : 10f;
            for (float d = MathF.Ceiling(_minDb / dbStep) * dbStep; d < _maxDb; d += dbStep)
            {
                float y = specRect.Bottom - (d - _minDb) / span * specRect.Height;
                g.DrawLine(gridPen, specRect.Left, y, specRect.Right, y);
                g.DrawString($"{d:0}", Font, textBrush, 2, y - 7);
            }

            // Vertical frequency grid + axis labels
            double startHz = _centerHz - halfSpan;
            double stepHz = ChooseStep(visSpan);
            for (double f = Math.Ceiling(startHz / stepHz) * stepHz; f <= _centerHz + halfSpan; f += stepHz)
            {
                float x = left + (float)((f - startHz) / visSpan * plotW);
                g.DrawLine(gridPen, x, specRect.Top, x, wfRect.Bottom);
                string lbl = $"{f / 1e6:0.0#}";
                var sz = g.MeasureString(lbl, Font);
                g.DrawString(lbl, Font, textBrush, x - sz.Width / 2, wfRect.Bottom + 2);
            }

            // Tuned channel passband
            float bwPx = (float)(_channelBwHz / visSpan * plotW);
            float cx = left + plotW / 2f;
            using (var bandBrush = new SolidBrush(Color.FromArgb(28, 255, 80, 80)))
            {
                g.FillRectangle(bandBrush, cx - bwPx / 2, specRect.Top, bwPx, specRect.Height);
                g.FillRectangle(bandBrush, cx - bwPx / 2, wfRect.Top, bwPx, wfRect.Height);
            }
            using (var centerPen = new Pen(Color.FromArgb(170, 255, 90, 90)) { DashStyle = DashStyle.Dash })
            {
                g.DrawLine(centerPen, cx, specRect.Top, cx, wfRect.Bottom);
            }

            // Trace: take the max bin that falls on each pixel column
            var pts = new PointF[plotW + 2];
            var peakPts = new PointF[plotW];
            for (int px = 0; px < plotW; px++)
            {
                int b0 = Math.Clamp((int)(vb0 + px * vbF / plotW), 0, n - 1);
                int b1 = Math.Max(b0 + 1, (int)(vb0 + (px + 1) * vbF / plotW));
                float m = float.MinValue, pk = float.MinValue;
                for (int b = b0; b < b1 && b < n; b++)
                {
                    if (_db[b] > m) m = _db[b];
                    if (_peak[b] > pk) pk = _peak[b];
                }
                pts[px + 1] = new PointF(left + px, DbToY(m, specRect, span));
                peakPts[px] = new PointF(left + px, DbToY(pk, specRect, span));
            }
            pts[0] = new PointF(left, specRect.Bottom);
            pts[^1] = new PointF(left + plotW - 1, specRect.Bottom);

            g.SetClip(specRect);
            using (var fill = new LinearGradientBrush(specRect, Color.FromArgb(150, 0, 220, 255), Color.FromArgb(20, 0, 40, 160), 90f))
            {
                g.FillPolygon(fill, pts);
            }
            if (PeakHold)
            {
                using var peakPen = new Pen(Color.FromArgb(200, 255, 170, 40), 1f);
                g.DrawLines(peakPen, peakPts);
            }
            using (var tracePen = new Pen(Color.FromArgb(255, 120, 240, 255), 1.5f))
            {
                g.DrawLines(tracePen, pts.AsSpan(1, plotW).ToArray());
            }
            g.ResetClip();

            // Waterfall
            if (_waterfall != null)
            {
                g.InterpolationMode = InterpolationMode.HighQualityBilinear;
                g.PixelOffsetMode = PixelOffsetMode.Half;
                var src = new RectangleF((float)vb0, 0, (float)vbF, _waterfall.Height);
                g.DrawImage(_waterfall, wfRect, src.X, src.Y, src.Width, src.Height, GraphicsUnit.Pixel);
            }

            using var border = new Pen(Color.FromArgb(70, 85, 110));
            g.DrawRectangle(border, specRect);
            g.DrawRectangle(border, wfRect);

            // Cursor readout
            if (_mouseX >= left && _mouseX < left + plotW)
            {
                double f = startHz + (double)(_mouseX - left) / plotW * visSpan;
                int bin = Math.Clamp((int)(vb0 + (double)(_mouseX - left) / plotW * vbF), 0, n - 1);
                using var curPen = new Pen(Color.FromArgb(120, 255, 255, 255));
                g.DrawLine(curPen, _mouseX, specRect.Top, _mouseX, wfRect.Bottom);
                string txt = $"{f / 1e6:0.000} MHz   {_db[bin]:0.0} dBFS";
                var sz = g.MeasureString(txt, Font);
                float tx = Math.Min(_mouseX + 8, specRect.Right - sz.Width - 4);
                using var bg = new SolidBrush(Color.FromArgb(200, 0, 0, 0));
                g.FillRectangle(bg, tx - 2, specRect.Top + 2, sz.Width + 4, sz.Height);
                using var white = new SolidBrush(Color.White);
                g.DrawString(txt, Font, white, tx, specRect.Top + 2);
            }
        }

        private float DbToY(float db, Rectangle r, float span)
        {
            float n = Math.Clamp((db - _minDb) / span, 0f, 1f);
            return r.Bottom - n * r.Height;
        }

        private static double ChooseStep(double spanHz)
        {
            double target = spanHz / 8.0;
            double[] steps = { 5_000, 10_000, 20_000, 25_000, 50_000, 100_000, 200_000, 250_000, 500_000, 1_000_000, 2_000_000 };
            foreach (var s in steps)
                if (s >= target) return s;
            return steps[^1];
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            _mouseX = e.X;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _mouseX = -1;
            Invalidate();
        }

        private static Color[] BuildPalette()
        {
            // black -> deep blue -> cyan -> green -> yellow -> red -> white
            var stops = new (float p, Color c)[]
            {
                (0.00f, Color.FromArgb(4, 6, 20)),
                (0.20f, Color.FromArgb(10, 20, 120)),
                (0.40f, Color.FromArgb(0, 170, 220)),
                (0.60f, Color.FromArgb(40, 220, 80)),
                (0.78f, Color.FromArgb(250, 230, 40)),
                (0.92f, Color.FromArgb(240, 50, 30)),
                (1.00f, Color.White)
            };

            var pal = new Color[256];
            for (int i = 0; i < 256; i++)
            {
                float t = i / 255f;
                int s = 0;
                while (s < stops.Length - 2 && t > stops[s + 1].p) s++;
                float k = (t - stops[s].p) / (stops[s + 1].p - stops[s].p);
                k = Math.Clamp(k, 0f, 1f);
                Color a = stops[s].c, b = stops[s + 1].c;
                pal[i] = Color.FromArgb(
                    (int)(a.R + (b.R - a.R) * k),
                    (int)(a.G + (b.G - a.G) * k),
                    (int)(a.B + (b.B - a.B) * k));
            }
            return pal;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _waterfall?.Dispose();
                _waterfallTmp?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
