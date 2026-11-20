using System;
using System.Drawing;
using System.Windows.Forms;

namespace PeterLauncher.UI
{
    // 通用控件：简单的卡片面板，Win32 风格单线边框
    public class CardPanel : Panel
    {
        public CardPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            BackColor = Color.White;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (Pen pen = new Pen(Theme.BorderColor))
            {
                e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
            }
        }
    }

    // 导航按钮：左侧竖向，选中时高亮
    public class NavButton : Button
    {
        public bool Selected = false;

        public NavButton()
        {
            FlatStyle = FlatStyle.System;
            TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            Padding = new Padding(12, 0, 0, 0);
            Height = 36;
            Dock = DockStyle.Top;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (Selected)
            {
                using (Pen pen = new Pen(Theme.AccentColor, 3))
                {
                    e.Graphics.DrawLine(pen, 0, 0, 0, Height);
                }
            }
        }
    }

    // 状态标签
    public class StatusLabel : Label
    {
        public StatusLabel()
        {
            ForeColor = Theme.SubTextColor;
            BackColor = Color.White;
            Font = Theme.SmallFont;
        }
    }

    // 主操作按钮（启动游戏）
    public class LaunchButton : Button
    {
        public LaunchButton()
        {
            FlatStyle = FlatStyle.System;
            Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold);
            Height = 36;
        }
    }

    // 提示框
    public class HintBox : Label
    {
        public HintBox()
        {
            ForeColor = Theme.SubTextColor;
            BackColor = Color.White;
            Font = Theme.SmallFont;
            AutoSize = false;
        }
    }
}
