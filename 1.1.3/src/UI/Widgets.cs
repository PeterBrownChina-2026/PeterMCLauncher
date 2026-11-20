using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace PeterLauncher.UI
{
    /// <summary>自绘按钮：主按钮（草绿渐变）/次按钮（描边）。</summary>
    public class UiButton : Control
    {
        public bool Primary = true;
        public bool Compact = false;

        private bool hovered;
        private bool pressed;

        public UiButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint
                   | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Cursor = Cursors.Hand;
            Font = Theme.BodyFont;
            ForeColor = Theme.Text;
            Height = Compact ? 30 : 40;
        }

        protected override void OnMouseEnter(EventArgs e) { hovered = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hovered = false; pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { pressed = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Theme.EnableSmoothing(g);

            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            Color textColor = Enabled ? Theme.Text : Theme.TextDim;

            if (Primary)
            {
                Color top = Theme.Accent;
                Color bottom = Theme.AccentDown;
                if (!Enabled) { top = Color.FromArgb(52, 62, 58); bottom = Color.FromArgb(42, 50, 48); }
                else if (pressed) { top = Theme.AccentDown; bottom = Theme.AccentDown; }
                else if (hovered) { top = Theme.AccentHover; bottom = Theme.Accent; }
                Theme.FillRoundGradient(g, rect, 10, top, bottom);
                textColor = Enabled ? Color.FromArgb(12, 20, 14) : Theme.TextDim;
            }
            else
            {
                Color fill = Theme.Card;
                if (!Enabled) fill = Color.FromArgb(26, 31, 39);
                else if (pressed) fill = Color.FromArgb(35, 42, 53);
                else if (hovered) fill = Theme.CardHover;
                Theme.FillRound(g, rect, 10, fill);
                Theme.StrokeRound(g, rect, 10, hovered && Enabled ? Theme.Accent : Theme.Border, 1f);
            }

            TextRenderer.DrawText(g, Text, Font, rect, textColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }

    /// <summary>圆角卡片容器，可在顶部显示标题。</summary>
    public class CardPanel : Panel
    {
        public string Title = "";
        public int Radius = 14;
        private int titleHeight = 0;

        public CardPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint
                   | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Background;
            ForeColor = Theme.Text;
        }

        public void SetTitle(string title)
        {
            Title = title;
            titleHeight = string.IsNullOrEmpty(title) ? 0 : 34;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Theme.EnableSmoothing(g);
            Rectangle bounds = new Rectangle(0, 0, Width - 1, Height - 1);
            Theme.FillRound(g, bounds, Radius, Theme.Card);
            Theme.StrokeRound(g, bounds, Radius, Theme.Border, 1f);

            if (!string.IsNullOrEmpty(Title))
            {
                using (Font font = Theme.H2Font)
                    TextRenderer.DrawText(g, Title, font, new Rectangle(18, 0, Width - 36, titleHeight),
                        Theme.Text, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
            }
        }

        public int TitleHeight { get { return titleHeight; } }
    }

    /// <summary>左侧导航项。</summary>
    public class NavButton : Control
    {
        private bool hovered;
        public bool Active = false;

        public NavButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint
                   | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Cursor = Cursors.Hand;
            Font = Theme.Font(10f, false);
            Height = 44;
        }

        protected override void OnMouseEnter(EventArgs e) { hovered = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hovered = false; Invalidate(); base.OnMouseLeave(e); }

        public void SetActive(bool active)
        {
            Active = active;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Theme.EnableSmoothing(g);
            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);

            if (Active) Theme.FillRound(g, rect, 9, Color.FromArgb(32, 46, 36));
            else if (hovered) Theme.FillRound(g, rect, 9, Theme.Card);

            if (Active)
            {
                Rectangle bar = new Rectangle(0, 8, 3, Height - 16);
                using (SolidBrush brush = new SolidBrush(Theme.Accent))
                    g.FillRectangle(brush, bar);
            }

            Rectangle dot = new Rectangle(18, (Height - 7) / 2, 7, 7);
            Theme.FillRound(g, dot, 4, Active ? Theme.Accent : (hovered ? Theme.TextDim : Color.FromArgb(72, 82, 98)));

            Color color = Active ? Theme.Accent : (hovered ? Theme.Text : Theme.TextDim);
            using (Font font = Active ? Theme.Font(10f, true) : Font)
            {
                TextRenderer.DrawText(g, Text, font,
                    new Rectangle(36, 0, Width - 46, Height), color,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
            }
        }
    }

    /// <summary>圆角输入框（内部套一个无边框 TextBox）。</summary>
    public class TextField : Control
    {
        private readonly TextBox box;
        private bool focused;
        public event EventHandler TextValueChanged;

        public TextField()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint
                   | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Background;

            box = new TextBox();
            box.BorderStyle = BorderStyle.None;
            box.BackColor = Theme.Field;
            box.ForeColor = Theme.Text;
            box.Font = Theme.BodyFont;
            box.GotFocus += delegate { focused = true; Invalidate(); };
            box.LostFocus += delegate { focused = false; Invalidate(); };
            box.TextChanged += delegate { if (TextValueChanged != null) TextValueChanged(this, EventArgs.Empty); };
            Controls.Add(box);
            Height = 38;
        }

        public override string Text
        {
            get { return box.Text; }
            set { box.Text = value; }
        }

        public bool ReadOnly
        {
            get { return box.ReadOnly; }
            set { box.ReadOnly = value; box.ForeColor = value ? Theme.TextDim : Theme.Text; }
        }

        public char PasswordChar
        {
            get { return box.PasswordChar; }
            set { box.PasswordChar = value; }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (box == null) return;
            int top = (Height - box.PreferredHeight) / 2;
            box.SetBounds(12, Math.Max(2, top), Math.Max(10, Width - 24), box.PreferredHeight);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Theme.EnableSmoothing(g);
            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            Theme.FillRound(g, rect, 9, Theme.Field);
            Theme.StrokeRound(g, rect, 9, focused ? Theme.Accent : Theme.Border, 1f);
            base.OnPaint(e);
        }
    }

    /// <summary>自绘下拉框：深色圆角 + 深色右键菜单，替代系统白底下拉框。</summary>
    public class UiSelect : Control
    {
        private readonly List<string> items = new List<string>();
        private int index = -1;
        private bool hovered;

        public event EventHandler SelectedIndexChanged;

        public UiSelect()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint
                   | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Height = 32;
            Cursor = Cursors.Hand;
            Font = Theme.BodyFont;
            BackColor = Theme.Background;
            ForeColor = Theme.Text;
        }

        public int Count { get { return items.Count; } }

        public string SelectedText
        {
            get { return (index >= 0 && index < items.Count) ? items[index] : ""; }
        }

        public int SelectedIndex
        {
            get { return index; }
            set
            {
                int next = value;
                if (next < -1) next = -1;
                if (next >= items.Count) next = items.Count - 1;
                if (next == index) return;
                index = next;
                Invalidate();
                if (SelectedIndexChanged != null) SelectedIndexChanged(this, EventArgs.Empty);
            }
        }

        public void Clear()
        {
            items.Clear();
            index = -1;
            Invalidate();
        }

        public void AddItem(string text)
        {
            items.Add(text);
        }

        public void SetItems(IEnumerable<string> values)
        {
            items.Clear();
            foreach (string value in values) items.Add(value);
            index = items.Count > 0 ? 0 : -1;
            Invalidate();
        }

        protected override void OnMouseEnter(EventArgs e) { hovered = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hovered = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            if (items.Count == 0) return;

            ContextMenuStrip menu = new ContextMenuStrip();
            menu.BackColor = Theme.Card;
            menu.ForeColor = Theme.Text;
            menu.Font = Theme.BodyFont;
            menu.ShowImageMargin = false;
            menu.Renderer = new DarkMenuRenderer();
            menu.Padding = new Padding(4);
            for (int i = 0; i < items.Count; i++)
            {
                ToolStripMenuItem item = new ToolStripMenuItem(items[i]);
                item.BackColor = Theme.Card;
                item.ForeColor = Theme.Text;
                item.Font = Theme.BodyFont;
                int target = i;
                item.Click += delegate { SelectedIndex = target; };
                menu.Items.Add(item);
            }
            menu.Show(this, new Point(0, Height + 2));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Theme.EnableSmoothing(g);
            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            Theme.FillRound(g, rect, 9, Theme.Field);
            Theme.StrokeRound(g, rect, 9, hovered ? Theme.Accent : Theme.Border, 1f);

            TextRenderer.DrawText(g, SelectedText, Font, new Rectangle(12, 0, Width - 40, Height),
                Theme.Text, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);

            using (Pen pen = new Pen(Theme.TextDim, 1.4f))
            {
                int cx = Width - 18;
                int cy = Height / 2;
                g.DrawLine(pen, cx - 4, cy - 2, cx, cy + 2);
                g.DrawLine(pen, cx, cy + 2, cx + 4, cy - 2);
            }
        }
    }

    /// <summary>深色下拉菜单渲染器。</summary>
    public class DarkMenuRenderer : ToolStripProfessionalRenderer
    {
        public DarkMenuRenderer() : base(new DarkColorTable()) { }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = Theme.Text;
            base.OnRenderItemText(e);
        }
    }

    public class DarkColorTable : ProfessionalColorTable
    {
        public override Color MenuItemSelected { get { return Theme.CardHover; } }
        public override Color MenuItemSelectedGradientBegin { get { return Theme.CardHover; } }
        public override Color MenuItemSelectedGradientEnd { get { return Theme.CardHover; } }
        public override Color MenuItemBorder { get { return Theme.Border; } }
        public override Color MenuBorder { get { return Theme.Border; } }
        public override Color ToolStripDropDownBackground { get { return Theme.Card; } }
        public override Color ImageMarginGradientBegin { get { return Theme.Card; } }
        public override Color ImageMarginGradientMiddle { get { return Theme.Card; } }
        public override Color ImageMarginGradientEnd { get { return Theme.Card; } }
    }

    /// <summary>自绘滑块（用于内存分配）。</summary>
    public class UiSlider : Control
    {
        private int minimum = 0;
        private int maximum = 100;
        private int value = 0;
        private bool dragging;
        public event EventHandler ValueChanged;

        public UiSlider()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint
                   | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Height = 28;
            Cursor = Cursors.Hand;
            BackColor = Theme.Background;
        }

        public int Minimum { get { return minimum; } set { minimum = value; Clamp(); Invalidate(); } }
        public int Maximum { get { return maximum; } set { maximum = value; Clamp(); Invalidate(); } }

        public int Value
        {
            get { return value; }
            set
            {
                int clamped = Math.Max(minimum, Math.Min(maximum, value));
                if (clamped != this.value)
                {
                    this.value = clamped;
                    Invalidate();
                    if (ValueChanged != null) ValueChanged(this, EventArgs.Empty);
                }
            }
        }

        private void Clamp()
        {
            if (value < minimum) value = minimum;
            if (value > maximum) value = maximum;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            dragging = true;
            SetFromMouse(e.X);
            base.OnMouseDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (dragging) SetFromMouse(e.X);
            base.OnMouseMove(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            dragging = false;
            base.OnMouseUp(e);
        }

        private void SetFromMouse(int x)
        {
            int pad = 8;
            int width = Width - pad * 2;
            if (width <= 0) return;
            double ratio = (double)(x - pad) / width;
            ratio = Math.Max(0.0, Math.Min(1.0, ratio));
            Value = minimum + (int)Math.Round(ratio * (maximum - minimum));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Theme.EnableSmoothing(g);
            int pad = 8;
            int width = Width - pad * 2;
            int centerY = Height / 2;
            Rectangle track = new Rectangle(pad, centerY - 3, width, 6);
            Theme.FillRound(g, track, 3, Color.FromArgb(40, 47, 58));

            double ratio = maximum == minimum ? 0 : (double)(value - minimum) / (maximum - minimum);
            int filled = (int)(width * ratio);
            if (filled > 4) Theme.FillRound(g, new Rectangle(pad, centerY - 3, filled, 6), 3, Theme.Accent);

            int thumbX = pad + filled - 5;
            thumbX = Math.Max(pad - 5, Math.Min(Width - pad - 5, thumbX));
            Rectangle thumb = new Rectangle(thumbX, centerY - 6, 12, 12);
            Theme.FillRound(g, thumb, 6, Theme.AccentHover);
        }
    }

    /// <summary>自绘复选框。</summary>
    public class UiCheck : Control
    {
        private bool isChecked;
        public event EventHandler CheckedChanged;

        public UiCheck()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint
                   | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw
                   | ControlStyles.SupportsTransparentBackColor, true);
            Height = 24;
            Cursor = Cursors.Hand;
            BackColor = Color.Transparent;
            Font = Theme.BodyFont;
        }

        public bool Checked
        {
            get { return isChecked; }
            set { isChecked = value; Invalidate(); }
        }

        protected override void OnClick(EventArgs e)
        {
            isChecked = !isChecked;
            Invalidate();
            if (CheckedChanged != null) CheckedChanged(this, EventArgs.Empty);
            base.OnClick(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Theme.EnableSmoothing(g);
            Rectangle boxRect = new Rectangle(0, (Height - 18) / 2, 18, 18);
            Theme.FillRound(g, boxRect, 6, isChecked ? Theme.Accent : Theme.Field);
            Theme.StrokeRound(g, boxRect, 6, isChecked ? Theme.Accent : Theme.Border, 1f);
            if (isChecked)
            {
                using (Pen pen = new Pen(Color.FromArgb(16, 24, 18), 2f))
                {
                    Point[] points = new Point[]
                    {
                        new Point(boxRect.X + 4, boxRect.Y + 9),
                        new Point(boxRect.X + 8, boxRect.Y + 13),
                        new Point(boxRect.X + 14, boxRect.Y + 6)
                    };
                    g.DrawLines(pen, points);
                }
            }
            TextRenderer.DrawText(g, Text, Font, new Rectangle(26, 0, Width - 26, Height),
                isChecked ? Theme.Text : Theme.TextDim, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
        }
    }

    /// <summary>自绘进度条（带文字）。</summary>
    public class UiProgress : Control
    {
        private int percent;
        private string caption = "";

        public UiProgress()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint
                   | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw
                   | ControlStyles.SupportsTransparentBackColor, true);
            Height = 46;
            BackColor = Color.Transparent;
        }

        public void SetState(int value, string text)
        {
            percent = Math.Max(0, Math.Min(100, value));
            caption = text;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Theme.EnableSmoothing(g);
            Rectangle bar = new Rectangle(0, Height - 10, Width, 6);
            Theme.FillRound(g, bar, 3, Color.FromArgb(38, 45, 56));
            int filled = (int)(Width * (double)percent / 100.0);
            if (filled > 4) Theme.FillRound(g, new Rectangle(0, Height - 10, filled, 6), 3, Theme.Accent);

            if (caption.Length > 0)
            {
                using (Font font = Theme.SmallFont)
                    TextRenderer.DrawText(g, caption + "  " + percent + "%", font,
                        new Rectangle(0, 0, Width, Height - 12), Theme.TextDim,
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }
        }
    }
}
