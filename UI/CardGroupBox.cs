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

        // Active cards are lighter with a glowing accent outline; inactive cards sink into the window and their text dims.
        public static readonly Color ActiveCardColor = Color.FromArgb(34, 42, 58);
        public static readonly Color InactiveCardColor = Color.FromArgb(20, 22, 29);
        public static readonly Color ActiveTextColor = Color.FromArgb(240, 244, 250);
        public static readonly Color InactiveTextColor = Color.FromArgb(112, 118, 132);
        public static readonly Color ActiveBadgeColor = Color.FromArgb(38, 150, 88);
        public static readonly Color InactiveBadgeColor = Color.FromArgb(58, 64, 78);

        private bool _active = true;
        private string? _badgeText;
        private Color? _badgeColor;
        private bool _workspaceFocused;

        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool WorkspaceFocused
        {
            get => _workspaceFocused;
            set { if (_workspaceFocused != value) { _workspaceFocused = value; Invalidate(); } }
        }

        public CardGroupBox()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = ActiveCardColor;
            Font = new Font("Segoe UI Semibold", 9.5f);
        }

        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public override Color ForeColor { get => _active ? ActiveTextColor : InactiveTextColor; set { } }

        /// <summary>True when the controls in this card are usable in the current situation.</summary>
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool Active
        {
            get => _active;
            set
            {
                if (_active == value) return;
                bool wasCollapsed = Collapsed;
                _active = value;
                BackColor = value ? ActiveCardColor : InactiveCardColor;
                OnForeColorChanged(EventArgs.Empty);
                Invalidate();
            }
        }

        private bool _userCollapsed;
        private Point? _headerMouseDown;
        private bool _dragStarted;
        private bool? _dropBefore;

        public event EventHandler? HeaderDragStarted;

        internal void CancelHeaderClick()
        {
            _dragStarted = true;
            _headerMouseDown = null;
        }

        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool? DropBefore
        {
            get => _dropBefore;
            set { if (_dropBefore != value) { _dropBefore = value; Invalidate(); } }
        }

        /// <summary>Raised when the card folds to, or unfolds from, its header strip.</summary>
        public event EventHandler? CollapsedChanged;

        /// <summary>Any card can fold to its header, active or not; the user's choice persists across state changes.</summary>
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool Collapsed => _userCollapsed;

        [Browsable(false)]
        public int HeaderHeight => Font.Height + 14;

        private void ApplyCollapse()
        {
            foreach (Control c in Controls) c.Visible = !Collapsed;
            Invalidate();
            CollapsedChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Sets the fold state programmatically (used by expand/collapse-all).</summary>
        public void SetCollapsed(bool value)
        {
            if (_userCollapsed == value) return;
            _userCollapsed = value;
            ApplyCollapse();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left || e.Y > HeaderHeight) return;
            _headerMouseDown = e.Location;
            _dragStarted = false;
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button == MouseButtons.Left && _headerMouseDown.HasValue && !_dragStarted && e.Y <= HeaderHeight)
                SetCollapsed(!_userCollapsed);
            _headerMouseDown = null;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_headerMouseDown is Point start && e.Button == MouseButtons.Left && !_dragStarted)
            {
                var dragBounds = new Rectangle(start.X - SystemInformation.DragSize.Width / 2,
                    start.Y - SystemInformation.DragSize.Height / 2,
                    SystemInformation.DragSize.Width, SystemInformation.DragSize.Height);
                if (!dragBounds.Contains(e.Location))
                {
                    _dragStarted = true;
                    _headerMouseDown = null;
                    HeaderDragStarted?.Invoke(this, EventArgs.Empty);
                }
            }
            Cursor = e.Y <= HeaderHeight ? Cursors.Hand : Cursors.Default;
        }

        /// <summary>Optional pill text at the right of the header; defaults to ACTIVE / INACTIVE.</summary>
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string? BadgeText
        {
            get => _badgeText;
            set { if (_badgeText != value) { _badgeText = value; Invalidate(); } }
        }

        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color? BadgeColor
        {
            get => _badgeColor;
            set { if (_badgeColor != value) { _badgeColor = value; Invalidate(); } }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent?.BackColor ?? Color.Black);

            var rect = new Rectangle(1, 1, Width - 3, Height - 3);
            using var path = RoundedRect(rect, 10);
            using (var fill = new SolidBrush(_active ? ActiveCardColor : InactiveCardColor)) g.FillPath(fill, path);
            if (_active)
            {
                using var glow = new Pen(Color.FromArgb(60, AccentColor), 4f);
                g.DrawPath(glow, path);
            }
            using (var pen = new Pen(_workspaceFocused ? Color.White : _active ? AccentColor : Color.FromArgb(38, 42, 54), _workspaceFocused ? 2f : _active ? 1.5f : 1f)) g.DrawPath(pen, path);
            if (_dropBefore.HasValue)
            {
                using var dropPen = new Pen(Color.Gold, 4f);
                int y = _dropBefore.Value ? 4 : Height - 5;
                g.DrawLine(dropPen, 12, y, Width - 13, y);
            }

            Color barColor = _active ? AccentColor : Color.FromArgb(70, 76, 90);
            using (var bar = new SolidBrush(barColor)) g.FillRectangle(bar, 10, 5, 4, Font.Height);
            int textX = 18;
            {
                // fold chevron: right-pointing when collapsed, down-pointing when open
                int cy = 5 + Font.Height / 2;
                var tri = Collapsed
                    ? new[] { new Point(20, cy - 5), new Point(20, cy + 5), new Point(27, cy) }
                    : new[] { new Point(19, cy - 3), new Point(29, cy - 3), new Point(24, cy + 4) };
                using (var tb = new SolidBrush(_active ? AccentColor : InactiveTextColor)) g.FillPolygon(tb, tri);
                textX = 36;
            }
            TextRenderer.DrawText(g, Text, Font, new Point(textX, 3), _active ? ActiveTextColor : InactiveTextColor, TextFormatFlags.NoPadding);

            DrawBadge(g);

            int lineY = 7 + Font.Height;
            using var divider = new Pen(_active ? Color.FromArgb(70, 84, 110) : Color.FromArgb(34, 38, 48));
            g.DrawLine(divider, 12, lineY, Width - 13, lineY);
        }

        private void DrawBadge(Graphics g)
        {
            string text = _badgeText ?? (_active ? "ACTIVE" : "INACTIVE");
            using var font = new Font("Segoe UI Semibold", 7.5f);
            var size = TextRenderer.MeasureText(g, text, font, Size.Empty, TextFormatFlags.NoPadding);
            var pill = new Rectangle(Width - 16 - size.Width - 16, 4, size.Width + 16, Font.Height);
            if (pill.Left < 120) return;

            Color fill = _badgeColor ?? (_active ? ActiveBadgeColor : InactiveBadgeColor);
            using var path = RoundedRect(pill, pill.Height / 2);
            using (var b = new SolidBrush(fill)) g.FillPath(b, path);
            TextRenderer.DrawText(g, text, font, pill, _active ? Color.White : Color.FromArgb(150, 156, 170),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
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
