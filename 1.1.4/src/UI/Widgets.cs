using System;
using System.Drawing;
using System.Windows.Forms;
using PeterLauncher.Core;

namespace PeterLauncher.UI
{
    /// <summary>公共小控件：标题条、分组卡片、进度条等。</summary>
    public static class Widgets
    {
        public static GroupBox Card(string text)
        {
            GroupBox box = new GroupBox
            {
                Text = text,
                ForeColor = Theme.Text,
                BackColor = Theme.Card,
                Padding = new Padding(12),
                FlatStyle = FlatStyle.Flat
            };
            return box;
        }

        public static Button AccentButton(string text)
        {
            Button button = new Button
            {
                Text = text,
                FlatStyle = FlatStyle.Flat,
                BackColor = Theme.Accent,
                ForeColor = Color.White,
                Height = 34,
                Cursor = Cursors.Hand
            };
            button.FlatAppearance.BorderSize = 0;
            button.Font = new Font("Microsoft YaHei UI", 10F);
            return button;
        }

        public static Button GhostButton(string text)
        {
            Button button = new Button
            {
                Text = text,
                FlatStyle = FlatStyle.Flat,
                BackColor = Theme.Card,
                ForeColor = Theme.Text,
                Height = 34,
                Cursor = Cursors.Hand
            };
            button.FlatAppearance.BorderColor = Theme.Border;
            button.Font = new Font("Microsoft YaHei UI", 10F);
            return button;
        }

        public static Label Caption(string text)
        {
            return new Label
            {
                Text = text,
                ForeColor = Theme.Muted,
                AutoSize = true
            };
        }

        public static void StatusStrip(Control parent, string text)
        {
            StatusStrip strip = new StatusStrip { BackColor = Theme.Card, ForeColor = Theme.Muted };
            strip.Items.Add(text);
            parent.Controls.Add(strip);
        }
    }

    /// <summary>一次启动需要的全部参数。</summary>
    public class LaunchOptions
    {
        public Config Config;
        public string VersionId;
        public string JavaPath;
        public int MemoryGb;
    }
}
