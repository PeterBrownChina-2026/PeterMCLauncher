using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace PeterLauncher.UI
{
    /// <summary>
    /// 配色、字体与绘制工具。1.1.4.b1：纯白背景 + 方正直角边框风格。
    /// </summary>
    public static class Theme
    {
        // ---- 纯白 + 方正边框风格 ----
        public static readonly Color Background = Color.FromArgb(255, 255, 255);
        public static readonly Color Sidebar = Color.FromArgb(255, 255, 255);
        public static readonly Color TitleBar = Color.FromArgb(255, 255, 255);
        public static readonly Color Card = Color.FromArgb(255, 255, 255);
        public static readonly Color CardHover = Color.FromArgb(235, 244, 252);
        public static readonly Color Field = Color.FromArgb(255, 255, 255);
        public static readonly Color Border = Color.FromArgb(128, 128, 128);
        public static readonly Color Text = Color.FromArgb(0, 0, 0);
        public static readonly Color TextDim = Color.FromArgb(96, 96, 96);
        public static readonly Color Accent = Color.FromArgb(10, 36, 106);
        public static readonly Color AccentHover = Color.FromArgb(16, 66, 165);
        public static readonly Color AccentDown = Color.FromArgb(8, 28, 80);
        public static readonly Color Highlight = Color.FromArgb(49, 106, 197);
        public static readonly Color Danger = Color.FromArgb(192, 0, 0);
        public static readonly Color Warning = Color.FromArgb(180, 95, 0);

        public static readonly string FontFamily = PickFontFamily();

        public static Font Font(float size, bool bold)
        {
            return new Font(FontFamily, size, bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Point);
        }

        public static Font TitleFont { get { return Font(14f, true); } }
        public static Font H2Font { get { return Font(11.5f, true); } }
        public static Font BodyFont { get { return Font(9f, false); } }
        public static Font SmallFont { get { return Font(8f, false); } }

        private static string PickFontFamily()
        {
            string[] candidates = new string[] { "Tahoma", "MS Sans Serif", "Microsoft Sans Serif", "Segoe UI", "Microsoft YaHei UI" };
            try
            {
                InstalledFontCollection fonts = new InstalledFontCollection();
                foreach (FontFamily family in fonts.Families)
                {
                    foreach (string candidate in candidates)
                        if (family.Name == candidate) return candidate;
                }
            }
            catch { }
            return SystemFonts.MessageBoxFont.FontFamily.Name;
        }

        public static GraphicsPath RoundRect(Rectangle bounds, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            path.AddRectangle(bounds);
            return path;
        }

        public static void FillRound(Graphics g, Rectangle bounds, int radius, Color color)
        {
            using (SolidBrush brush = new SolidBrush(color))
                g.FillRectangle(brush, bounds);
        }

        public static void StrokeRound(Graphics g, Rectangle bounds, int radius, Color color, float width)
        {
            using (Pen pen = new Pen(color, width))
                g.DrawRectangle(pen, bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1);
        }

        public static void DrawRaisedBorder(Graphics g, Rectangle rect)
        {
            Color light = Color.FromArgb(255, 255, 255);
            Color shadow = Color.FromArgb(128, 128, 128);
            using (Pen p1 = new Pen(light))
            using (Pen p2 = new Pen(shadow))
            {
                g.DrawLine(p1, rect.Left, rect.Top, rect.Right - 1, rect.Top);
                g.DrawLine(p1, rect.Left, rect.Top, rect.Left, rect.Bottom - 1);
                g.DrawLine(p2, rect.Left, rect.Bottom - 1, rect.Right - 1, rect.Bottom - 1);
                g.DrawLine(p2, rect.Right - 1, rect.Top, rect.Right - 1, rect.Bottom - 1);
            }
        }

        public static void DrawSunkenBorder(Graphics g, Rectangle rect)
        {
            Color light = Color.FromArgb(255, 255, 255);
            Color shadow = Color.FromArgb(128, 128, 128);
            Color dark = Color.FromArgb(96, 96, 96);
            using (Pen p1 = new Pen(dark))
            using (Pen p2 = new Pen(shadow))
            using (Pen p3 = new Pen(light))
            {
                g.DrawLine(p1, rect.Left, rect.Top, rect.Right - 1, rect.Top);
                g.DrawLine(p1, rect.Left, rect.Top, rect.Left, rect.Bottom - 1);
                g.DrawLine(p2, rect.Left + 1, rect.Top + 1, rect.Right - 2, rect.Top + 1);
                g.DrawLine(p2, rect.Left + 1, rect.Top + 1, rect.Left + 1, rect.Bottom - 2);
                g.DrawLine(p3, rect.Left, rect.Bottom - 1, rect.Right - 1, rect.Bottom - 1);
                g.DrawLine(p3, rect.Right - 1, rect.Top, rect.Right - 1, rect.Bottom - 1);
            }
        }

        public static void FillRoundGradient(Graphics g, Rectangle bounds, int radius, Color top, Color bottom)
        {
            using (SolidBrush brush = new SolidBrush(top))
                g.FillRectangle(brush, bounds);
        }

        public static void EnableSmoothing(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.None;
            g.TextRenderingHint = TextRenderingHint.SystemDefault;
            g.InterpolationMode = InterpolationMode.Low;
        }
    }
}
