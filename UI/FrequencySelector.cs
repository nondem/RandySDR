using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace RandySDR
{
    public class FrequencySelector : UserControl
    {
        private const int DigitCount = 10; // 1 GHz ... 1 Hz
        private const long HzPerMhz = 1_000_000;
        private static readonly Color DisplayBackColor = Color.Black;
        private static readonly Color DisplayForeColor = Color.FromArgb(0, 255, 80);
        private static readonly Color DisplayDisabledForeColor = Color.FromArgb(0, 110, 35);
        private static readonly Color LedLabelColor = Color.FromArgb(0, 150, 40);
        private static readonly Color LedLabelDisabledColor = Color.FromArgb(90, 120, 90);
        private static readonly Color ButtonBackColor = Color.FromArgb(22, 22, 22);
        private static readonly Color ButtonBorderColor = Color.FromArgb(55, 140, 55);

        private TextBox[] digitBoxes;
        private Button[] upButtons;
        private Button[] downButtons;
        private Label[] groupSeparators;
        private Label dotLabel;
        private Label mhzLabel;
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

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public new bool Enabled
        {
            get => base.Enabled;
            set
            {
                base.Enabled = value;
                ApplyEnabledState();
            }
        }

        public FrequencySelector()
        {
            InitializeControl();
        }

        private void InitializeControl()
        {
            Size = new Size(700, 114);
            MinimumSize = new Size(640, 110);
            BackColor = SystemColors.Control;
            DoubleBuffered = true;

            digitBoxes = new TextBox[DigitCount];
            upButtons = new Button[DigitCount];
            downButtons = new Button[DigitCount];
            groupSeparators = new Label[3];

            int startX = 10;
            int digitWidth = 38;
            using var digitMeasureBox = new TextBox { Font = new Font("Consolas", 17, FontStyle.Bold), BorderStyle = BorderStyle.FixedSingle };
            int digitHeight = digitMeasureBox.PreferredHeight;
            int buttonHeight = 24;
            int spacing = 3;
            int groupGap = 16;
            int topMargin = 4;
            int topGap = 4;
            int bottomGap = 8;

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
                    Font = new Font("Segoe UI Symbol", 9f, FontStyle.Regular),
                    TextAlign = ContentAlignment.MiddleCenter,
                    TabStop = false,
                    FlatStyle = FlatStyle.Standard,
                    Tag = i
                };
                upButtons[i].UseCompatibleTextRendering = true;
                upButtons[i].Click += UpButton_Click;
                Controls.Add(upButtons[i]);

                digitBoxes[i] = new TextBox
                {
                    Location = new Point(xPos, topMargin + buttonHeight + topGap),
                    Size = new Size(digitWidth, digitHeight),
                    Font = new Font("Consolas", 17, FontStyle.Bold),
                    TextAlign = HorizontalAlignment.Center,
                    MaxLength = 1,
                    Text = "0",
                    Tag = i,
                    BorderStyle = BorderStyle.FixedSingle,
                    BackColor = DisplayBackColor,
                    ForeColor = DisplayForeColor,
                    ShortcutsEnabled = false
                };
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
                    Font = new Font("Segoe UI Symbol", 9f, FontStyle.Regular),
                    TextAlign = ContentAlignment.MiddleCenter,
                    TabStop = false,
                    FlatStyle = FlatStyle.Standard,
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
                    Size = new Size(12, digitHeight),
                    Location = new Point(digitBoxes[d].Right + 2, topMargin + buttonHeight + topGap),
                    Text = ".",
                    Font = new Font("Consolas", 18, FontStyle.Bold),
                    TextAlign = ContentAlignment.MiddleCenter,
                    ForeColor = DisplayForeColor,
                    BackColor = Color.Transparent
                };
                Controls.Add(groupSeparators[s]);
            }

            dotLabel = new Label
            {
                Location = new Point(startX, topMargin + buttonHeight + topGap + digitHeight + bottomGap + buttonHeight + 4),
                Size = new Size(560, 18),
                Text = "GHz                 MHz                 kHz                 Hz",
                Font = new Font("Consolas", 9f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = LedLabelColor,
                BackColor = Color.Transparent
            };
            Controls.Add(dotLabel);

            mhzLabel = new Label
            {
                Location = new Point(575, topMargin + buttonHeight + topGap + digitHeight + 6),
                Size = new Size(110, 22),
                Text = "MHz tuning",
                Font = new Font("Consolas", 9f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = LedLabelColor,
                BackColor = Color.Transparent
            };
            Controls.Add(mhzLabel);

            ApplyEnabledState();
            UpdateDisplay();
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

            var labelColor = base.Enabled ? LedLabelColor : LedLabelDisabledColor;
            dotLabel.ForeColor = labelColor;
            mhzLabel.ForeColor = labelColor;
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
