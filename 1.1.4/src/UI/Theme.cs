using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace PeterLauncher.UI
{
    /// <summary>配色、字体与圆角绘制工具。整体走深色 + 草绿点缀，站着也有 Minecraft 味。</summary>
    public static class Theme
    {
        public static readonly Color Background = Color.FromArgb(14, 17, 22);
        public static readonly Color Sidebar = Color.FromArgb(18, 22, 28);
        public static readonly Color TitleBar = Color.FromArgb(18, 22, 28);
        public static readonly Color Card = Color.FromArgb(23, 28, 36);
        public static readonly Color CardHover = Color.FromArgb(29, 35, 44);
        public static readonly Color Field = Color.FromArgb(30, 36, 46);
        public static readonly Color Border = Color.FromArgb(38, 45, 56);
        public static readonly Color Text = Color.FromArgb(230, 234, 240);
        public static readonly Color TextDim = Color.FromArgb(138, 148, 166);
        public static readonly Color Accent = Color.FromArgb(86, 196, 106);
        public static readonly Color AccentHover = Color.FromArgb(104, 214, 124);
        public static readonly Color AccentDown = Color.FromArgb(62, 159, 81);
        public static readonly Color Danger = Color.FromArgb(224, 93, 93);
        public static readonly Color Warning = Color.FromArgb(230, 176, 80);

        public static readonly string FontFamily = PickFontFamily();

        public static Font Font(float size, bool bold)
        {
            return new Font(FontFamily, size, bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Point);
        }

        public static Font TitleFont { get { return Font(19f, true); } }
        public static Font H2Font { get { return Font(13.5f, true); } }
        public static Font BodyFont { get { return Font(9.5f, false); } }
        public static Font SmallFont { get { return Font(8.5f, false); } }

        private static string PickFontFamily()
        {
            string[] candidates = new string[] { "Microsoft YaHei UI", "微软雅黑", "Microsoft YaHei", "Segoe UI", "Tahoma" };
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
            if (radius <= 0)
            {
                path.AddRectangle(bounds);
                return path;
            }
            int d = radius * 2;
            d = Math.Min(d, Math.Min(bounds.Width, bounds.Height));
            radius = d / 2;
            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        public static void FillRound(Graphics g, Rectangle bounds, int radius, Color color)
        {
            using (GraphicsPath path = RoundRect(bounds, radius))
            using (SolidBrush brush = new SolidBrush(color))
                g.FillPath(brush, path);
        }

        public static void StrokeRound(Graphics g, Rectangle bounds, int radius, Color color, float width)
        {
            using (GraphicsPath path = RoundRect(bounds, radius))
            using (Pen pen = new Pen(color, width))
                g.DrawPath(pen, path);
        }

        /// <summary>垂直渐变圆角块，用在大按钮和标题条上。</summary>
        public static void FillRoundGradient(Graphics g, Rectangle bounds, int radius, Color top, Color bottom)
        {
            using (GraphicsPath path = RoundRect(bounds, radius))
            {
                using (LinearGradientBrush brush = new LinearGradientBrush(bounds, top, bottom, LinearGradientMode.Vertical))
                    g.FillPath(brush, path);
            }
        }

        public static void EnableSmoothing(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        }
    }
}
