namespace RandySDR
{
    internal sealed class CardResizeGrip : Control
    {
        private Point _startScreen;
        private Size _startSize;
        private bool _resizing;

        internal event Action? ResizeStarted;
        internal event Action<Size, bool, bool>? ResizeRequested;

        internal CardResizeGrip()
        {
            Size = new Size(20, 20);
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            Cursor = Cursors.SizeNWSE;
            SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent?.BackColor ?? Color.Black);
            using var pen = new Pen(CardGroupBox.AccentColor, 2);
            for (int offset = 5; offset <= 13; offset += 4)
                e.Graphics.DrawLine(pen, Width - offset, Height - 3, Width - 3, Height - offset);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left || Parent == null) return;
            _resizing = true;
            _startScreen = PointToScreen(e.Location);
            _startSize = Parent.Size;
            ResizeStarted?.Invoke();
            Capture = true;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!_resizing) return;
            var point = PointToScreen(e.Location);
            ResizeRequested?.Invoke(new Size(_startSize.Width + point.X - _startScreen.X,
                _startSize.Height + point.Y - _startScreen.Y), false, false);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (!_resizing || e.Button != MouseButtons.Left) return;
            var point = PointToScreen(e.Location);
            _resizing = false;
            Capture = false;
            ResizeRequested?.Invoke(new Size(_startSize.Width + point.X - _startScreen.X,
                _startSize.Height + point.Y - _startScreen.Y), true, false);
        }

        protected override void OnMouseCaptureChanged(EventArgs e)
        {
            base.OnMouseCaptureChanged(e);
            if (!_resizing || Capture) return;
            _resizing = false;
            ResizeRequested?.Invoke(_startSize, true, true);
        }
    }
}
