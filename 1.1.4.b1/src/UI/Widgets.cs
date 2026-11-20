using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace PeterLauncher.UI
{
    /// <summary>Win32 风格按钮：白底灰边框，按下凹陷。</summary>
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
            Height = Compact ? 24 : 28;
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

            Color fill = Theme.Background;
            if (!Enabled) fill = Color.FromArgb(224, 224, 224);
            else if (pressed) fill = Color.FromArgb(220, 220, 220);
            else if (hovered) fill = Color.FromArgb(235, 244, 252);

            using (SolidBrush brush = new SolidBrush(fill))
                g.FillRectangle(brush, rect);

            using (Pen pen = new Pen(Theme.Border, 1f))
                g.DrawRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height);

            if (pressed)
            {
                using (Pen p2 = new Pen(Color.FromArgb(160, 160, 160)))
                    g.DrawLine(p2, rect.Left + 1, rect.Bottom - 1, rect.Right - 1, rect.Bottom - 1);
            }

            Font textFont = Primary ? Theme.Font(9f, true) : Font;
            TextRenderer.DrawText(g, Text, textFont, rect, textColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }

    /// <summary>白卡片容器，灰色细边框。</summary>
    public class CardPanel : Panel
    {
        public string Title = "";
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
            titleHeight = string.IsNullOrEmpty(title) ? 0 : 24;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Theme.EnableSmoothing(g);
            Rectangle bounds = new Rectangle(0, 0, Width - 1, Height - 1);
            using (SolidBrush brush = new SolidBrush(Theme.Card))
                g.FillRectangle(brush, bounds);
            using (Pen pen = new Pen(Theme.Border, 1f))
                g.DrawRectangle(pen, bounds.X, bounds.Y, bounds.Width, bounds.Height);

            if (!string.IsNullOrEmpty(Title))
            {
                using (Font font = Theme.H2Font)
                    TextRenderer.DrawText(g, Title, font, new Rectangle(12, 0, Width - 24, titleHeight),
                        Theme.Text, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
            }
        }

        public int TitleHeight { get { return titleHeight; } }
    }

    /// <summary>左侧导航项：白底，选中蓝底白字。</summary>
    public class NavButton : Control
    {
        private bool hovered;
        public bool Active = false;

        public NavButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint
                   | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Cursor = Cursors.Hand;
            Font = Theme.Font(9f, false);
            Height = 40;
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

            Color fill = Theme.Background;
            Color color = Theme.Text;
            if (Active) { fill = Theme.Highlight; color = Color.White; }
            else if (hovered) { fill = Theme.CardHover; }

            using (SolidBrush brush = new SolidBrush(fill))
                g.FillRectangle(brush, rect);

            Font font = Active ? Theme.Font(9f, true) : Font;
            TextRenderer.DrawText(g, Text, font,
                new Rectangle(10, 0, Width - 14, Height), color,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
        }
    }

    /// <summary>白底输入框，灰色边框。</summary>
    public class TextField : Control
    {
        private readonly TextBox box;
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
            box.TextChanged += delegate { if (TextValueChanged != null) TextValueChanged(this, EventArgs.Empty); };
            Controls.Add(box);
            Height = 22;
        }

        public override string Text { get { return box.Text; } set { box.Text = value; } }

        public bool ReadOnly
        {
            get { return box.ReadOnly; }
            set { box.ReadOnly = value; box.ForeColor = value ? Theme.TextDim : Theme.Text; }
        }

        public char PasswordChar { get { return box.PasswordChar; } set { box.PasswordChar = value; } }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (box == null) return;
            int top = (Height - box.PreferredHeight) / 2;
            box.SetBounds(4, Math.Max(1, top), Math.Max(10, Width - 8), box.PreferredHeight);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Theme.EnableSmoothing(g);
            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (SolidBrush brush = new SolidBrush(Theme.Field))
                g.FillRectangle(brush, rect);
            using (Pen pen = new Pen(Theme.Border, 1f))
                g.DrawRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height);
            base.OnPaint(e);
        }
    }

    /// <summary>白底下拉框。</summary>
    public class UiSelect : Control
    {
        private readonly List<string> items = new List<string>();
        private int index = -1;

        public event EventHandler SelectedIndexChanged;

        public UiSelect()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint
                   | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Height = 22;
            Cursor = Cursors.Hand;
            Font = Theme.BodyFont;
            BackColor = Theme.Background;
            ForeColor = Theme.Text;
        }

        public int Count { get { return items.Count; } }
        public string SelectedText { get { return (index >= 0 && index < items.Count) ? items[index] : ""; } }

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

        public void Clear() { items.Clear(); index = -1; Invalidate(); }
        public void AddItem(string text) { items.Add(text); }
        public void SetItems(IEnumerable<string> values)
        {
            items.Clear();
            foreach (string value in values) items.Add(value);
            index = items.Count > 0 ? 0 : -1;
            Invalidate();
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            if (items.Count == 0) return;
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.BackColor = Color.White;
            menu.ForeColor = Theme.Text;
            menu.Font = Theme.BodyFont;
            menu.ShowImageMargin = false;
            for (int i = 0; i < items.Count; i++)
            {
                ToolStripMenuItem item = new ToolStripMenuItem(items[i]);
                item.BackColor = Color.White;
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
            using (SolidBrush brush = new SolidBrush(Theme.Field))
                g.FillRectangle(brush, rect);
            using (Pen pen = new Pen(Theme.Border, 1f))
                g.DrawRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height);

            TextRenderer.DrawText(g, SelectedText, Font, new Rectangle(4, 0, Width - 20, Height),
                Theme.Text, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);

            using (Pen pen = new Pen(Theme.Border, 1f))
                g.DrawLine(pen, Width - 17, 2, Width - 17, Height - 2);
            int cx = Width - 9;
            int cy = Height / 2;
            using (Pen apen = new Pen(Theme.Text, 1.2f))
            {
                g.DrawLine(apen, cx - 3, cy - 1, cx, cy + 2);
                g.DrawLine(apen, cx, cy + 2, cx + 3, cy - 1);
            }
        }
    }

    /// <summary>滑块。</summary>
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
            Height = 20;
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

        private void Clamp() { if (value < minimum) value = minimum; if (value > maximum) value = maximum; }

        protected override void OnMouseDown(MouseEventArgs e) { dragging = true; SetFromMouse(e.X); base.OnMouseDown(e); }
        protected override void OnMouseMove(MouseEventArgs e) { if (dragging) SetFromMouse(e.X); base.OnMouseMove(e); }
        protected override void OnMouseUp(MouseEventArgs e) { dragging = false; base.OnMouseUp(e); }

        private void SetFromMouse(int x)
        {
            int pad = 4;
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
            int pad = 4;
            int width = Width - pad * 2;
            int centerY = Height / 2;

            Rectangle track = new Rectangle(pad, centerY - 2, width, 4);
            using (SolidBrush brush = new SolidBrush(Color.White))
                g.FillRectangle(brush, track);
            using (Pen pen = new Pen(Theme.Border, 1f))
                g.DrawRectangle(pen, track.X, track.Y, track.Width, track.Height);

            double ratio = maximum == minimum ? 0 : (double)(value - minimum) / (maximum - minimum);
            int filled = (int)(width * ratio);
            if (filled > 4)
            {
                using (SolidBrush brush = new SolidBrush(Theme.Highlight))
                    g.FillRectangle(brush, new Rectangle(pad, centerY - 2, filled, 4));
            }

            int thumbX = pad + filled - 4;
            thumbX = Math.Max(pad - 4, Math.Min(Width - pad - 4, thumbX));
            Rectangle thumb = new Rectangle(thumbX, centerY - 6, 8, 12);
            using (SolidBrush brush = new SolidBrush(Color.White))
                g.FillRectangle(brush, thumb);
            using (Pen pen = new Pen(Theme.Border, 1f))
                g.DrawRectangle(pen, thumb.X, thumb.Y, thumb.Width, thumb.Height);
        }
    }

    /// <summary>复选框。</summary>
    public class UiCheck : Control
    {
        private bool isChecked;
        public event EventHandler CheckedChanged;

        public UiCheck()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint
                   | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw
                   | ControlStyles.SupportsTransparentBackColor, true);
            Height = 20;
            Cursor = Cursors.Hand;
            BackColor = Color.Transparent;
            Font = Theme.BodyFont;
        }

        public bool Checked { get { return isChecked; } set { isChecked = value; Invalidate(); } }

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
            Rectangle boxRect = new Rectangle(0, (Height - 13) / 2, 13, 13);
            using (SolidBrush brush = new SolidBrush(Color.White))
                g.FillRectangle(brush, boxRect);
            using (Pen pen = new Pen(Theme.Border, 1f))
                g.DrawRectangle(pen, boxRect.X, boxRect.Y, boxRect.Width, boxRect.Height);
            if (isChecked)
            {
                using (Pen pen = new Pen(Color.Black, 1.6f))
                {
                    Point[] points = new Point[]
                    {
                        new Point(boxRect.X + 2, boxRect.Y + 7),
                        new Point(boxRect.X + 5, boxRect.Y + 10),
                        new Point(boxRect.X + 10, boxRect.Y + 3)
                    };
                    g.DrawLines(pen, points);
                }
            }
            TextRenderer.DrawText(g, Text, Font, new Rectangle(18, 0, Width - 18, Height),
                Theme.Text, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
        }
    }

    /// <summary>进度条。</summary>
    public class UiProgress : Control
    {
        private int percent;
        private string caption = "";

        public UiProgress()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint
                   | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw
                   | ControlStyles.SupportsTransparentBackColor, true);
            Height = 32;
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
            Rectangle bar = new Rectangle(0, Height - 14, Width, 10);
            using (SolidBrush brush = new SolidBrush(Color.White))
                g.FillRectangle(brush, bar);
            using (Pen pen = new Pen(Theme.Border, 1f))
                g.DrawRectangle(pen, bar.X, bar.Y, bar.Width, bar.Height);

            int filled = (int)(Width * (double)percent / 100.0);
            if (filled > 4)
            {
                using (SolidBrush brush = new SolidBrush(Theme.Highlight))
                    g.FillRectangle(brush, new Rectangle(bar.Left + 1, bar.Top + 1, filled - 2, bar.Height - 2));
            }

            if (caption.Length > 0)
            {
                using (Font font = Theme.SmallFont)
                    TextRenderer.DrawText(g, caption + "  " + percent + "%", font,
                        new Rectangle(0, 0, Width, Height - 16), Theme.Text,
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }
        }
    }
}
