using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using PeterLauncher.Core;

namespace PeterLauncher.UI
{
    public static class Theme
    {
        // 纯 Win32 控制面板风格
        public static readonly Color WindowBg = Color.White;
        public static readonly Color ControlBg = Color.White;
        public static readonly Color BorderColor = Color.FromArgb(171, 171, 171);
        public static readonly Color TextColor = Color.Black;
        public static readonly Color SubTextColor = Color.FromArgb(96, 96, 96);
        public static readonly Color AccentColor = Color.FromArgb(0, 120, 215);
        public static readonly Color DisabledTextColor = Color.FromArgb(160, 160, 160);

        public static readonly Font DefaultFont = new Font("Microsoft YaHei UI", 9F);
        public static readonly Font TitleFont = new Font("Microsoft YaHei UI", 11F, FontStyle.Bold);
        public static readonly Font SmallFont = new Font("Microsoft YaHei UI", 8F);

        public static void ApplyForm(Form form)
        {
            form.BackColor = WindowBg;
            form.Font = DefaultFont;
            form.ForeColor = TextColor;
        }

        public static void ApplyButton(Button btn)
        {
            btn.FlatStyle = FlatStyle.System;
            btn.UseVisualStyleBackColor = true;
        }

        public static void ApplyTextBox(TextBox tb)
        {
            tb.BorderStyle = BorderStyle.FixedSingle;
            tb.BackColor = ControlBg;
        }

        public static void ApplyGroupBox(GroupBox gb)
        {
            gb.BackColor = WindowBg;
            gb.ForeColor = TextColor;
        }

        public static void ApplyLabel(Label lbl)
        {
            lbl.BackColor = WindowBg;
            lbl.ForeColor = TextColor;
        }
    }
}
