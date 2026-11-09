namespace RandySDR
{
    internal sealed class DashboardResizeHandle : Control
    {
        private readonly bool _horizontal;
        private readonly bool _vertical;
        private Point _start;
        private Size _size;
        private bool _dragging;

        internal event Action<Size, bool, bool>? ResizeRequested;

        internal DashboardResizeHandle(bool horizontal, bool vertical)
        {
            _horizontal = horizontal;
            _vertical = vertical;
            Cursor = horizontal && vertical ? Cursors.SizeNWSE : horizontal ? Cursors.SizeWE : Cursors.SizeNS;
            BackColor = CardGroupBox.BorderColor;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left || Parent == null) return;
            _start = PointToScreen(e.Location);
            _size = Parent.Size;
            _dragging = true;
            Capture = true;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_dragging) Request(e.Location, false);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (!_dragging || e.Button != MouseButtons.Left) return;
            Request(e.Location, true);
            _dragging = false;
            Capture = false;
        }

        protected override void OnMouseCaptureChanged(EventArgs e)
        {
            base.OnMouseCaptureChanged(e);
            if (!_dragging || Capture) return;
            _dragging = false;
            ResizeRequested?.Invoke(_size, true, true);
        }

        private void Request(Point location, bool finished)
        {
            Point current = PointToScreen(location);
            ResizeRequested?.Invoke(new Size(_size.Width + (_horizontal ? current.X - _start.X : 0),
                _size.Height + (_vertical ? current.Y - _start.Y : 0)), finished, false);
        }
    }
}
