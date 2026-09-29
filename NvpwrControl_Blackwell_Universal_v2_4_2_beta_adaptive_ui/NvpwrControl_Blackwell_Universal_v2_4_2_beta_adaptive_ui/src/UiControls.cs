using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace NvpwrControlBlackwell
{
    internal static class UiDrawing
    {
        public static GraphicsPath RoundedRect(Rectangle r, int radius)
        {
            GraphicsPath p = new GraphicsPath();
            int d = Math.Max(2, radius * 2);
            if (r.Width <= d || r.Height <= d)
            {
                p.AddRectangle(r);
                p.CloseFigure();
                return p;
            }
            p.AddArc(r.Left, r.Top, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Top, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }
    }

    internal sealed class CardPanel : Panel
    {
        public Color BorderColor = Color.FromArgb(45, 55, 68);
        public Color AccentColor = Color.FromArgb(124, 58, 237);
        public bool AccentLine;
        public int CornerRadius = 9;

        public CardPanel()
        {
            DoubleBuffered = true;
            Padding = new Padding(14);
            Margin = new Padding(7);
            ResizeRedraw = true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(0, 0, Math.Max(1, Width - 1), Math.Max(1, Height - 1));
            using (GraphicsPath path = UiDrawing.RoundedRect(r, CornerRadius))
            using (Pen p = new Pen(BorderColor))
            {
                e.Graphics.DrawPath(p, path);
            }
            if (AccentLine)
            {
                using (SolidBrush b = new SolidBrush(AccentColor))
                    e.Graphics.FillRectangle(b, 0, CornerRadius, 3, Math.Max(0, Height - CornerRadius * 2));
            }
            base.OnPaint(e);
        }
    }

    internal sealed class NavButton : Button
    {
        public bool Active;
        public Color AccentColor = Color.FromArgb(124, 58, 237);
        public Color HoverColor = Color.FromArgb(20, 25, 31);
        private bool _hover;

        public NavButton()
        {
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            Height = 36;
            Cursor = Cursors.Hand;
            TabStop = false;
            SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs pevent)
        {
            pevent.Graphics.Clear(_hover && !Active ? HoverColor : BackColor);
            TextRenderer.DrawText(pevent.Graphics, Text, Font, ClientRectangle, ForeColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            if (Active)
            {
                using (SolidBrush b = new SolidBrush(AccentColor))
                    pevent.Graphics.FillRectangle(b, 10, Height - 3, Math.Max(0, Width - 20), 3);
            }
        }
    }

    internal sealed class ThemedButton : Button
    {
        public bool Primary;
        public Color AccentColor = Color.FromArgb(124, 58, 237);
        public Color SurfaceColor = Color.FromArgb(14, 18, 23);
        public Color BorderColor = Color.FromArgb(43, 55, 68);
        public Color TextColor = Color.White;
        public Color MutedTextColor = Color.FromArgb(120, 130, 142);
        public int CornerRadius = 7;
        private bool _hover;
        private bool _pressed;

        public ThemedButton()
        {
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            Cursor = Cursors.Hand;
            TabStop = false;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; _pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs mevent) { if (mevent.Button == MouseButtons.Left) _pressed = true; Invalidate(); base.OnMouseDown(mevent); }
        protected override void OnMouseUp(MouseEventArgs mevent) { _pressed = false; Invalidate(); base.OnMouseUp(mevent); }

        private static Color Blend(Color a, Color b, int pctB)
        {
            int p = Math.Max(0, Math.Min(100, pctB));
            int q = 100 - p;
            return Color.FromArgb((a.R * q + b.R * p) / 100, (a.G * q + b.G * p) / 100, (a.B * q + b.B * p) / 100);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Color baseColor = Primary ? AccentColor : SurfaceColor;
            if (!Enabled) baseColor = Blend(baseColor, Color.Gray, 48);
            else if (_pressed) baseColor = Blend(baseColor, Color.Black, 20);
            else if (_hover) baseColor = Blend(baseColor, Color.White, Primary ? 10 : 6);

            Rectangle r = new Rectangle(0, 0, Math.Max(1, Width - 1), Math.Max(1, Height - 1));
            using (GraphicsPath path = UiDrawing.RoundedRect(r, CornerRadius))
            using (SolidBrush b = new SolidBrush(baseColor))
            using (Pen p = new Pen(Primary ? baseColor : BorderColor))
            {
                e.Graphics.FillPath(b, path);
                e.Graphics.DrawPath(p, path);
            }

            Color tc = Enabled ? (Primary ? Color.White : TextColor) : MutedTextColor;
            TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, tc,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }

    internal sealed class PowerSlider : Control
    {
        private int _minimum = 100;
        private int _maximum = 200;
        private int _step = 5;
        private int _value = 100;
        private bool _dragging;

        public Color TrackColor = Color.FromArgb(48, 59, 72);
        public Color FillColor = Color.FromArgb(124, 58, 237);
        public Color KnobColor = Color.White;
        public Color TickColor = Color.FromArgb(100, 112, 126);
        public Color LabelColor = Color.FromArgb(145, 157, 171);
        public string UnitSuffix = "W";

        public event EventHandler ValueChanged;

        public int Minimum { get { return _minimum; } set { SetRange(value, _maximum, _step); } }
        public int Maximum { get { return _maximum; } set { SetRange(_minimum, value, _step); } }
        public int Step { get { return _step; } set { SetRange(_minimum, _maximum, value); } }
        public int Value
        {
            get { return _value; }
            set
            {
                int v = Snap(value);
                if (v == _value) return;
                _value = v;
                Invalidate();
                if (ValueChanged != null) ValueChanged(this, EventArgs.Empty);
            }
        }

        public PowerSlider()
        {
            Height = 74;
            MinimumSize = new Size(180, 70);
            Cursor = Cursors.Hand;
            TabStop = true;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        public void SetRange(int min, int max, int step)
        {
            if (max < min) { int t = min; min = max; max = t; }
            _minimum = min;
            _maximum = Math.Max(min + 1, max);
            _step = Math.Max(1, step);
            _value = Snap(_value);
            Invalidate();
        }

        private int Snap(int raw)
        {
            int clamped = Math.Max(_minimum, Math.Min(_maximum, raw));
            int n = (int)Math.Round((clamped - _minimum) / (double)_step);
            int v = _minimum + n * _step;
            if (v > _maximum) v = _maximum;
            if (v < _minimum) v = _minimum;
            return v;
        }

        private Rectangle TrackRect()
        {
            int left = 14;
            int right = Math.Max(left + 20, Width - 14);
            int y = 22;
            return new Rectangle(left, y, right - left, 8);
        }

        private int XForValue(int v)
        {
            Rectangle tr = TrackRect();
            double f = (_maximum == _minimum) ? 0.0 : (v - _minimum) / (double)(_maximum - _minimum);
            return tr.Left + (int)Math.Round(tr.Width * f);
        }

        private int ValueForX(int x)
        {
            Rectangle tr = TrackRect();
            double f = (x - tr.Left) / (double)Math.Max(1, tr.Width);
            f = Math.Max(0.0, Math.Min(1.0, f));
            return Snap(_minimum + (int)Math.Round((_maximum - _minimum) * f));
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                Focus();
                _dragging = true;
                Value = ValueForX(e.X);
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (_dragging) Value = ValueForX(e.X);
            base.OnMouseMove(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            _dragging = false;
            base.OnMouseUp(e);
        }

        protected override bool IsInputKey(Keys keyData)
        {
            Keys k = keyData & Keys.KeyCode;
            if (k == Keys.Left || k == Keys.Right || k == Keys.Home || k == Keys.End) return true;
            return base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Left) { Value -= _step; e.Handled = true; }
            else if (e.KeyCode == Keys.Right) { Value += _step; e.Handled = true; }
            else if (e.KeyCode == Keys.Home) { Value = _minimum; e.Handled = true; }
            else if (e.KeyCode == Keys.End) { Value = _maximum; e.Handled = true; }
            base.OnKeyDown(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            Rectangle tr = TrackRect();
            int knobX = XForValue(_value);

            using (SolidBrush b = new SolidBrush(TrackColor))
                e.Graphics.FillRectangle(b, tr);
            using (SolidBrush b = new SolidBrush(FillColor))
                e.Graphics.FillRectangle(b, tr.Left, tr.Top, Math.Max(0, knobX - tr.Left), tr.Height);

            int count = Math.Max(1, (_maximum - _minimum) / Math.Max(1, _step));
            int drawEvery = count <= 24 ? 1 : (int)Math.Ceiling(count / 24.0);
            using (Pen p = new Pen(TickColor))
            {
                for (int i = 0; i <= count; i += drawEvery)
                {
                    int v = Math.Min(_maximum, _minimum + i * _step);
                    int x = XForValue(v);
                    e.Graphics.DrawLine(p, x, tr.Bottom + 4, x, tr.Bottom + 8);
                }
                if ((_maximum - _minimum) % _step != 0)
                {
                    int x = XForValue(_maximum);
                    e.Graphics.DrawLine(p, x, tr.Bottom + 4, x, tr.Bottom + 8);
                }
            }

            using (SolidBrush shadow = new SolidBrush(Color.FromArgb(80, Color.Black)))
                e.Graphics.FillEllipse(shadow, knobX - 9, tr.Top - 6, 20, 20);
            using (SolidBrush b = new SolidBrush(KnobColor))
                e.Graphics.FillEllipse(b, knobX - 8, tr.Top - 7, 18, 18);
            using (Pen p = new Pen(FillColor, 2f))
                e.Graphics.DrawEllipse(p, knobX - 8, tr.Top - 7, 18, 18);

            string current = _value.ToString() + " " + UnitSuffix;
            Size currentSize = TextRenderer.MeasureText(e.Graphics, current, Font, new Size(200, 20), TextFormatFlags.NoPadding);
            int currentWidth = Math.Max(52, currentSize.Width + 6);
            int currentX = Math.Max(0, Math.Min(Width - currentWidth, knobX - currentWidth / 2));
            Rectangle currentRect = new Rectangle(currentX, tr.Bottom + 10, currentWidth, 18);
            TextRenderer.DrawText(e.Graphics, current, Font, currentRect, FillColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

            using (Pen p = new Pen(Color.FromArgb(140, FillColor)))
                e.Graphics.DrawLine(p, knobX, tr.Bottom + 2, knobX, currentRect.Top - 2);

            string left = _minimum.ToString() + " " + UnitSuffix;
            string right = _maximum.ToString() + " " + UnitSuffix;
            Rectangle lr = new Rectangle(2, Height - 20, Width / 2 - 4, 18);
            Rectangle rr = new Rectangle(Width / 2, Height - 20, Width / 2 - 2, 18);
            TextRenderer.DrawText(e.Graphics, left, Font, lr, LabelColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            TextRenderer.DrawText(e.Graphics, right, Font, rr, LabelColor, TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

            if (Focused)
            {
                Rectangle fr = ClientRectangle; fr.Inflate(-1, -1);
                using (Pen p = new Pen(Color.FromArgb(90, FillColor)))
                    e.Graphics.DrawRectangle(p, fr);
            }
        }
    }
}
