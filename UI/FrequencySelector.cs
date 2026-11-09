using System;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace RandySDR
{
    public class FrequencySelector : UserControl, IMessageFilter
    {
        private const int DigitCount = 10; // 1 GHz ... 1 Hz
        private const long HzPerMhz = 1_000_000;
        private const int MouseWheelMessage = 0x020A;
        private const int WheelDelta = 120;
        private static readonly Color DisplayBackColor = Color.FromArgb(15, 19, 27);
        private static readonly Color DisplayForeColor = Color.FromArgb(180, 236, 255);
        private static readonly Color DisplayDisabledForeColor = Color.FromArgb(90, 110, 120);
        private static readonly Color UnitLabelColor = Color.FromArgb(125, 165, 185);
        private static readonly Color UnitLabelDisabledColor = Color.FromArgb(90, 100, 110);

        private TextBox[] digitBoxes = null!;
        private Button[] upButtons = null!;
        private Button[] downButtons = null!;
        private Label[] groupSeparators = null!;
        private Label[] unitLabels = null!;
        private readonly ToolTip _digitToolTip = new();
        private int _wheelRemainder;
        private int _wheelDigit = -1;
        private decimal _value = 98.5m;
        private decimal _minimum = 0.000001m; // 1 Hz
        private decimal _maximum = 9000m;     // 9 GHz
        private bool _updating = false;

        public event EventHandler? ValueChanged;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public decimal Value
        {
            get => _value;
            set
            {
                if (value >= _minimum && value <= _maximum && _value != value)
                {
                    _value = value;
                    _updating = true;
                    UpdateDisplay();
                    _updating = false;
                    ValueChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public decimal Minimum
        {
            get => _minimum;
            set => _minimum = value;
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public decimal Maximum
        {
            get => _maximum;
            set => _maximum = value;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Application.AddMessageFilter(this);
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            Application.RemoveMessageFilter(this);
            base.OnHandleDestroyed(e);
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            base.OnEnabledChanged(e);
            _wheelDigit = -1;
            _wheelRemainder = 0;
            ApplyEnabledState();
        }

        public FrequencySelector()
        {
            InitializeControl();
        }

        private void InitializeControl()
        {
            BackColor = CardGroupBox.ActiveCardColor;
            DoubleBuffered = true;

            digitBoxes = new TextBox[DigitCount];
            upButtons = new Button[DigitCount];
            downButtons = new Button[DigitCount];
            groupSeparators = new Label[3];
            unitLabels = new Label[4];

            int startX = 10;
            int digitWidth = 32;
            using var digitMeasureBox = new TextBox { Font = new Font("Consolas", 16, FontStyle.Bold), BorderStyle = BorderStyle.FixedSingle };
            int digitHeight = digitMeasureBox.PreferredHeight;
            int buttonHeight = 19;
            int spacing = 3;
            int groupGap = 12;
            int topMargin = 2;
            int topGap = 2;
            int bottomGap = 2;

            for (int i = 0; i < DigitCount; i++)
            {
                int xPos = startX + (i * (digitWidth + spacing));
                if (i >= 1) xPos += groupGap;
                if (i >= 4) xPos += groupGap;
                if (i >= 7) xPos += groupGap;

                upButtons[i] = new Button
                {
                    Location = new Point(xPos, topMargin),
                    Size = new Size(digitWidth, buttonHeight),
                    Text = "▲",
                    Font = new Font("Segoe UI Symbol", 7f, FontStyle.Regular),
                    TextAlign = ContentAlignment.MiddleCenter,
                    TabStop = false,
                    FlatStyle = FlatStyle.Standard,
                    AccessibleName = $"Increase {GetMultiplier(i) * HzPerMhz:0} Hz digit",
                    Tag = i
                };
                upButtons[i].UseCompatibleTextRendering = true;
                upButtons[i].Click += UpButton_Click;
                Controls.Add(upButtons[i]);

                digitBoxes[i] = new TextBox
                {
                    Location = new Point(xPos, topMargin + buttonHeight + topGap),
                    Size = new Size(digitWidth, digitHeight),
                    Font = new Font("Consolas", 16, FontStyle.Bold),
                    TextAlign = HorizontalAlignment.Center,
                    MaxLength = 1,
                    Text = "0",
                    Tag = i,
                    BorderStyle = BorderStyle.FixedSingle,
                    BackColor = DisplayBackColor,
                    ForeColor = DisplayForeColor,
                    ShortcutsEnabled = false
                };
                digitBoxes[i].AccessibleName = $"{GetMultiplier(i) * HzPerMhz:0} Hz digit";
                digitBoxes[i].AccessibleDescription = "Scroll or use the arrow keys to tune this digit.";
                _digitToolTip.SetToolTip(digitBoxes[i], "Scroll or use the arrow keys to tune this digit");
                digitBoxes[i].KeyPress += DigitBox_KeyPress;
                digitBoxes[i].TextChanged += DigitBox_TextChanged;
                digitBoxes[i].Enter += DigitBox_Enter;
                digitBoxes[i].KeyDown += DigitBox_KeyDown;
                Controls.Add(digitBoxes[i]);

                downButtons[i] = new Button
                {
                    Location = new Point(xPos, topMargin + buttonHeight + topGap + digitHeight + bottomGap),
                    Size = new Size(digitWidth, buttonHeight),
                    Text = "▼",
                    Font = new Font("Segoe UI Symbol", 7f, FontStyle.Regular),
                    TextAlign = ContentAlignment.MiddleCenter,
                    TabStop = false,
                    FlatStyle = FlatStyle.Standard,
                    AccessibleName = $"Decrease {GetMultiplier(i) * HzPerMhz:0} Hz digit",
                    Tag = i
                };
                downButtons[i].UseCompatibleTextRendering = true;
                downButtons[i].Click += DownButton_Click;
                Controls.Add(downButtons[i]);
            }

            int[] sepAfter = { 0, 3, 6 };
            for (int s = 0; s < sepAfter.Length; s++)
            {
                int d = sepAfter[s];
                groupSeparators[s] = new Label
                {
                    AutoSize = false,
                    Size = new Size(groupGap, digitHeight),
                    Location = new Point(digitBoxes[d].Right + 1, topMargin + buttonHeight + topGap),
                    Text = ".",
                    Font = new Font("Consolas", 16, FontStyle.Bold),
                    TextAlign = ContentAlignment.MiddleCenter,
                    ForeColor = DisplayForeColor,
                    BackColor = Color.Transparent
                };
                Controls.Add(groupSeparators[s]);
            }

            string[] units = { "GHz", "MHz", "kHz", "Hz" };
            int[] firstDigits = { 0, 1, 4, 7 };
            int[] lastDigits = { 0, 3, 6, 9 };
            for (int i = 0; i < units.Length; i++)
            {
                unitLabels[i] = new Label
                {
                    Text = units[i],
                    Font = new Font("Segoe UI", 8f, FontStyle.Bold),
                    TextAlign = ContentAlignment.MiddleCenter,
                    ForeColor = UnitLabelColor,
                    BackColor = Color.Transparent
                };
                int groupWidth = digitBoxes[lastDigits[i]].Right - digitBoxes[firstDigits[i]].Left;
                int labelWidth = Math.Max(groupWidth, unitLabels[i].GetPreferredSize(Size.Empty).Width);
                unitLabels[i].Location = new Point(digitBoxes[firstDigits[i]].Left + (groupWidth - labelWidth) / 2,
                    downButtons[0].Bottom + 2);
                unitLabels[i].Size = new Size(labelWidth, 18);
                Controls.Add(unitLabels[i]);
            }

            MinimumSize = new Size(digitBoxes[DigitCount - 1].Right + 4, unitLabels[0].Bottom + 2);
            Size = MinimumSize;

            ApplyEnabledState();
            UpdateDisplay();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _digitToolTip.Dispose();
            base.Dispose(disposing);
        }

        private void ApplyEnabledState()
        {
            if (digitBoxes == null)
                return;

            foreach (var box in digitBoxes)
            {
                box.Enabled = base.Enabled;
                box.BackColor = DisplayBackColor;
                box.ForeColor = base.Enabled ? DisplayForeColor : DisplayDisabledForeColor;
            }

            foreach (var btn in upButtons)
            {
                btn.Enabled = base.Enabled;
                btn.Invalidate();
            }

            foreach (var btn in downButtons)
            {
                btn.Enabled = base.Enabled;
                btn.Invalidate();
            }

            foreach (var separator in groupSeparators)
                separator.ForeColor = base.Enabled ? DisplayForeColor : DisplayDisabledForeColor;

            foreach (var label in unitLabels)
                label.ForeColor = base.Enabled ? UnitLabelColor : UnitLabelDisabledColor;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr WindowFromPoint(Point point);

        bool IMessageFilter.PreFilterMessage(ref Message m)
        {
            if (m.Msg != MouseWheelMessage || !Enabled || !Visible) return false;

            Point screenPoint = Cursor.Position;
            Control? hovered = Control.FromChildHandle(WindowFromPoint(screenPoint));
            if (hovered == null || (hovered != this && !Contains(hovered)))
            {
                _wheelDigit = -1;
                _wheelRemainder = 0;
                return false;
            }

            Point point = PointToClient(screenPoint);
            int index = Array.FindIndex(digitBoxes, box => point.X >= box.Left && point.X < box.Right
                && point.Y >= upButtons[0].Top && point.Y < downButtons[0].Bottom);
            if (index < 0)
            {
                _wheelDigit = -1;
                _wheelRemainder = 0;
                return false;
            }

            if (_wheelDigit != index)
            {
                _wheelDigit = index;
                _wheelRemainder = 0;
            }
            _wheelRemainder += (short)(((long)m.WParam >> 16) & 0xffff);
            while (_wheelRemainder >= WheelDelta)
            {
                IncrementDigit(index);
                _wheelRemainder -= WheelDelta;
            }
            while (_wheelRemainder <= -WheelDelta)
            {
                DecrementDigit(index);
                _wheelRemainder += WheelDelta;
            }
            return true;
        }

        private void UpButton_Click(object? sender, EventArgs e)
        {
            if (sender is Button btn && btn.Tag is int index)
            {
                IncrementDigit(index);
            }
        }

        private void DownButton_Click(object? sender, EventArgs e)
        {
            if (sender is Button btn && btn.Tag is int index)
            {
                DecrementDigit(index);
            }
        }

        private void IncrementDigit(int index)
        {
            decimal multiplier = GetMultiplier(index);
            decimal newValue = _value + multiplier;
            
            if (newValue <= _maximum)
            {
                Value = newValue;
            }
        }

        private void DecrementDigit(int index)
        {
            decimal multiplier = GetMultiplier(index);
            decimal newValue = _value - multiplier;
            
            if (newValue >= _minimum)
            {
                Value = newValue;
            }
        }

        private decimal GetMultiplier(int index)
        {
            // index 0..9 => 1e9..1 Hz (converted to MHz step)
            int power = DigitCount - 1 - index;
            decimal hzStep = 1m;
            for (int i = 0; i < power; i++)
                hzStep *= 10m;

            return hzStep / HzPerMhz;
        }

        private void UpdateDisplay()
        {
            long hz = (long)Math.Round(_value * HzPerMhz, MidpointRounding.AwayFromZero);
            if (hz < 0) hz = 0;

            string freqStr = hz.ToString("D10");

            bool wasUpdating = _updating;
            _updating = true;
            try
            {
                for (int i = 0; i < DigitCount; i++)
                {
                    digitBoxes[i].Text = freqStr[i].ToString();
                }
            }
            finally
            {
                _updating = wasUpdating;
            }
        }

        private void DigitBox_KeyPress(object? sender, KeyPressEventArgs e)
        {
            // Only allow digits
            if (!char.IsDigit(e.KeyChar) && !char.IsControl(e.KeyChar))
            {
                e.Handled = true;
            }
        }

        private void DigitBox_TextChanged(object? sender, EventArgs e)
        {
            if (_updating) return;

            if (sender is TextBox box && box.Tag is int index)
            {
                if (box.Text.Length > 0 && char.IsDigit(box.Text[0]))
                {
                    UpdateValueFromDigits();

                    // Auto-advance only for active user typing, not for programmatic updates.
                    if (box.Focused && box.Text.Length == 1 && index < DigitCount - 1)
                    {
                        digitBoxes[index + 1].Focus();
                        digitBoxes[index + 1].SelectAll();
                    }
                }
            }
        }

        private void DigitBox_Enter(object? sender, EventArgs e)
        {
            if (sender is TextBox box)
            {
                box.SelectAll();
            }
        }

        private void DigitBox_KeyDown(object? sender, KeyEventArgs e)
        {
            if (sender is TextBox box && box.Tag is int index)
            {
                switch (e.KeyCode)
                {
                    case Keys.Up:
                        IncrementDigit(index);
                        e.Handled = true;
                        break;
                    case Keys.Down:
                        DecrementDigit(index);
                        e.Handled = true;
                        break;
                    case Keys.Left:
                        if (index > 0)
                        {
                            digitBoxes[index - 1].Focus();
                            digitBoxes[index - 1].SelectAll();
                        }
                        e.Handled = true;
                        break;
                    case Keys.Right:
                        if (index < DigitCount - 1)
                        {
                            digitBoxes[index + 1].Focus();
                            digitBoxes[index + 1].SelectAll();
                        }
                        e.Handled = true;
                        break;
                }
            }
        }

        private void UpdateValueFromDigits()
        {
            try
            {
                string freqStr = "";
                for (int i = 0; i < DigitCount; i++)
                {
                    freqStr += digitBoxes[i].Text.Length > 0 ? digitBoxes[i].Text : "0";
                }

                long hz = long.Parse(freqStr);
                decimal newValue = hz / (decimal)HzPerMhz;
                if (newValue >= _minimum && newValue <= _maximum)
                {
                    _value = newValue;
                    _updating = true;
                    ValueChanged?.Invoke(this, EventArgs.Empty);
                    _updating = false;
                }
            }
            catch
            {
                // Invalid value, revert to current
                UpdateDisplay();
            }
        }
    }
}
