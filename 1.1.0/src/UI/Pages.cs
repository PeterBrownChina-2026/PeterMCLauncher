using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using PeterLauncher.Core;

namespace PeterLauncher.UI
{
    /// <summary>页面公用的小工具。</summary>
    public static class PageKit
    {
        public static Label Text(string text, int x, int y, int width, int height, Font font, Color color)
        {
            Label label = new Label();
            label.Text = text;
            label.AutoSize = false;
            label.SetBounds(x, y, width, height);
            label.Font = font;
            label.ForeColor = color;
            label.BackColor = Color.Transparent;
            label.TextAlign = ContentAlignment.MiddleLeft;
            return label;
        }

        public static CardPanel Card(string title, int x, int y, int width, int height)
        {
            CardPanel card = new CardPanel();
            card.SetBounds(x, y, width, height);
            card.SetTitle(title);
            return card;
        }

        public static UiButton Button(string text, int x, int y, int width, int height, bool primary)
        {
            UiButton button = new UiButton();
            button.Text = text;
            button.Primary = primary;
            button.SetBounds(x, y, width, height);
            return button;
        }
    }

    /// <summary>页面基类：统一内边距与尺寸。</summary>
    public class PageBase : Panel
    {
        protected MainForm Host;
        public const int Pad = 24;
        public const int Inner = 742;

        public PageBase(MainForm host)
        {
            Host = host;
            Dock = DockStyle.Fill;
            BackColor = Theme.Background;
            AutoScroll = false;
        }

        public virtual void RefreshState() { }

        protected Label Title(string text)
        {
            Label label = PageKit.Text(text, Pad, 12, Inner, 34, Theme.TitleFont, Theme.Text);
            Controls.Add(label);
            return label;
        }

        protected Label Hint(string text, int x, int y, int width)
        {
            Label label = PageKit.Text(text, x, y, width, 20, Theme.SmallFont, Theme.TextDim);
            Controls.Add(label);
            return label;
        }
    }

    // ==================================================================
    // 启动页
    // ==================================================================
    public class LaunchPage : PageBase
    {
        private Label accountName;
        private Label accountType;
        private UiSelect versionBox;
        private Label javaLabel;
        private Label memoryLabel;
        private UiProgress progress;
        private Label status;

        public LaunchPage(MainForm host) : base(host)
        {
            Title("启动游戏");

            // ---- 当前账户卡片 ----
            CardPanel accountCard = PageKit.Card("当前账户", Pad, 52, Inner, 104);
            Controls.Add(accountCard);

            accountName = PageKit.Text("Player", 18, 34, 420, 34, Theme.Font(17f, true), Theme.Text);
            accountCard.Controls.Add(accountName);

            accountType = PageKit.Text("离线账户", 18, 66, 420, 20, Theme.SmallFont, Theme.TextDim);
            accountCard.Controls.Add(accountType);

            UiButton switchButton = PageKit.Button("切换账户", 560, 34, 150, 34, false);
            switchButton.Compact = true;
            switchButton.Click += delegate { Host.Navigate(1); };
            accountCard.Controls.Add(switchButton);

            // ---- 版本 / Java / 内存 ----
            CardPanel infoCard = PageKit.Card("本次启动", Pad, 172, Inner, 146);
            Controls.Add(infoCard);

            infoCard.Controls.Add(PageKit.Text("游戏版本", 18, 34, 120, 20, Theme.SmallFont, Theme.TextDim));
            versionBox = new UiSelect();
            versionBox.SetBounds(18, 56, 210, 32);
            versionBox.SelectedIndexChanged += delegate
            {
                string text = versionBox.SelectedText;
                if (text.Length > 0 && !text.StartsWith("（") && Host.Cfg.LastVersion != text)
                {
                    Host.Cfg.LastVersion = text;
                    Host.Cfg.Save();
                }
            };
            infoCard.Controls.Add(versionBox);

            infoCard.Controls.Add(PageKit.Text("Java", 248, 34, 120, 20, Theme.SmallFont, Theme.TextDim));
            javaLabel = PageKit.Text("未检测到", 248, 58, 240, 28, Theme.BodyFont, Theme.Text);
            infoCard.Controls.Add(javaLabel);

            infoCard.Controls.Add(PageKit.Text("内存", 508, 34, 120, 20, Theme.SmallFont, Theme.TextDim));
            memoryLabel = PageKit.Text("", 508, 58, 210, 28, Theme.BodyFont, Theme.Text);
            infoCard.Controls.Add(memoryLabel);

            // ---- 启动按钮 ----
            UiButton launchButton = PageKit.Button("启 动 游 戏", Pad, 336, Inner, 58, true);
            launchButton.Font = Theme.Font(14f, true);
            launchButton.Click += delegate { Host.LaunchGame(); };
            Controls.Add(launchButton);

            // ---- 快捷操作 ----
            UiButton releaseButton = PageKit.Button("释放 / 修复 .minecraft", Pad, 410, 238, 34, false);
            releaseButton.Compact = true;
            releaseButton.Click += delegate { Host.ReleaseMinecraft(true); };
            Controls.Add(releaseButton);

            UiButton openDirButton = PageKit.Button("打开游戏目录", Pad + 250, 410, 238, 34, false);
            openDirButton.Compact = true;
            openDirButton.Click += delegate { Host.OpenFolder(Host.Cfg.EffectiveGameDir); };
            Controls.Add(openDirButton);

            UiButton shellButton = PageKit.Button("系统命令窗口", Pad + 500, 410, 242, 34, false);
            shellButton.Compact = true;
            shellButton.Click += delegate { Host.OpenAdminShell(); };
            Controls.Add(shellButton);

            // ---- 进度与状态 ----
            progress = new UiProgress();
            progress.SetBounds(Pad, 464, Inner, 46);
            Controls.Add(progress);

            status = PageKit.Text("准备就绪", Pad, 518, Inner, 22, Theme.SmallFont, Theme.TextDim);
            Controls.Add(status);
        }

        public void SetStatus(string text)
        {
            status.Text = text;
        }

        public void SetProgress(int percent, string text)
        {
            progress.SetState(percent, text);
        }

        public void ClearProgress()
        {
            progress.SetState(0, "");
        }

        public override void RefreshState()
        {
            if (Host.Account != null)
            {
                accountName.Text = Host.Account.Name;
                accountType.Text = "微软正版账户 · 已通过 Xbox Live 验证";
                accountType.ForeColor = Theme.Accent;
            }
            else
            {
                accountName.Text = Host.Cfg.UserName;
                accountType.Text = "离线账户 · 只影响单机与局域网显示名";
                accountType.ForeColor = Theme.TextDim;
            }

            versionBox.Clear();
            string[] versions = GameLauncher.ListVersions(Host.Cfg.EffectiveGameDir);
            if (versions.Length == 0)
            {
                versionBox.AddItem("（没有找到版本）");
                versionBox.SelectedIndex = 0;
            }
            else
            {
                foreach (string version in versions) versionBox.AddItem(version);
                int index = Array.IndexOf(versions, Host.Cfg.LastVersion);
                versionBox.SelectedIndex = index >= 0 ? index : 0;
            }

            JavaInfo java = Host.CurrentJava;
            if (java == null)
            {
                javaLabel.Text = "未检测到，请去 Java 页";
                javaLabel.ForeColor = Theme.Warning;
            }
            else
            {
                string note = java.Broken ? "（无法运行）" : (java.IsJava8 ? "" : "（版本不符）");
                javaLabel.Text = java.Version + (java.BitKnown ? "  " + java.BitText : "") + note;
                javaLabel.ForeColor = (java.Broken || !java.IsJava8) ? Theme.Warning : Theme.Text;
            }

            memoryLabel.Text = Host.Cfg.MinMemoryMb + " MB  ~  " + Host.Cfg.MaxMemoryMb + " MB";
            ClearProgress();
        }
    }

    // ==================================================================
    // 账户页
    // ==================================================================
    public class AccountPage : PageBase
    {
        private TextField offlineField;
        private TextField clientIdField;
        private Label loginStatus;
        private Label currentIdentity;
        private UiButton signOutButton;

        public AccountPage(MainForm host) : base(host)
        {
            Title("账户");

            // ---- 离线账户 ----
            CardPanel offlineCard = PageKit.Card("离线账户", Pad, 52, Inner, 118);
            Controls.Add(offlineCard);

            offlineCard.Controls.Add(PageKit.Text("游戏内显示的昵称", 18, 32, 300, 20, Theme.SmallFont, Theme.TextDim));
            offlineField = new TextField();
            offlineField.SetBounds(18, 54, 420, 38);
            offlineField.Text = host.Cfg.UserName;
            offlineCard.Controls.Add(offlineField);

            UiButton applyButton = PageKit.Button("保存昵称", 456, 54, 140, 38, true);
            applyButton.Compact = false;
            applyButton.Click += delegate { ApplyOfflineName(); };
            offlineCard.Controls.Add(applyButton);

            offlineCard.Controls.Add(PageKit.Text("昵称不能为空、不能超过 16 个字符；只用于单机与局域网。", 18, 96, 560, 20, Theme.SmallFont, Theme.TextDim));

            // ---- 微软正版 ----
            CardPanel msCard = PageKit.Card("微软正版登录", Pad, 180, Inner, 246);
            Controls.Add(msCard);

            msCard.Controls.Add(PageKit.Text("客户端 ID（Azure 应用的应用程序 ID）", 18, 30, 420, 20, Theme.SmallFont, Theme.TextDim));
            clientIdField = new TextField();
            clientIdField.SetBounds(18, 54, 440, 38);
            clientIdField.Text = host.Cfg.ClientId;
            msCard.Controls.Add(clientIdField);

            UiButton saveClientButton = PageKit.Button("保存", 470, 54, 100, 38, false);
            saveClientButton.Compact = false;
            saveClientButton.Click += delegate
            {
                Host.Cfg.ClientId = clientIdField.Text.Trim();
                Host.Cfg.Save();
                loginStatus.Text = "客户端 ID 已保存";
            };
            msCard.Controls.Add(saveClientButton);

            UiButton deviceButton = PageKit.Button("设备码登录（推荐）", 18, 104, 276, 40, true);
            deviceButton.Click += delegate
            {
                Host.Cfg.ClientId = clientIdField.Text.Trim();
                Host.Cfg.Save();
                Host.LoginDeviceCode(loginStatus);
            };
            msCard.Controls.Add(deviceButton);

            UiButton legacyButton = PageKit.Button("旧版弹窗登录", 306, 104, 276, 40, false);
            legacyButton.Click += delegate { Host.LoginLegacy(loginStatus); };
            msCard.Controls.Add(legacyButton);

            signOutButton = PageKit.Button("退出微软账户", 594, 104, 130, 40, false);
            signOutButton.Click += delegate { Host.SignOutMicrosoft(); };
            msCard.Controls.Add(signOutButton);

            loginStatus = PageKit.Text("未登录。设备码登录需要一个 Azure 应用客户端 ID。", 18, 156, 706, 20, Theme.SmallFont, Theme.TextDim);
            msCard.Controls.Add(loginStatus);

            msCard.Controls.Add(PageKit.Text(
                "登录链路：微软账户 → Xbox Live → XSTS → Minecraft 服务，成功后账号状态会显示在启动页。\n"
                + "启动器除登录外不做任何联网操作，不下载游戏文件、不联机。",
                18, 180, 706, 44, Theme.SmallFont, Theme.TextDim));

            // ---- 当前身份 ----
            CardPanel identityCard = PageKit.Card("当前身份", Pad, 436, Inner, 68);
            Controls.Add(identityCard);
            currentIdentity = PageKit.Text("", 18, 24, 706, 40, Theme.BodyFont, Theme.Text);
            identityCard.Controls.Add(currentIdentity);
        }

        private void ApplyOfflineName()
        {
            string name = offlineField.Text.Trim();
            if (name.Length == 0)
            {
                MessageBox.Show("昵称不能为空", AppInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (name.Length > 16) name = name.Substring(0, 16);
            Host.Cfg.UserName = name;
            Host.Cfg.UseMicrosoftAccount = false;
            Host.Cfg.MicrosoftToken = "";
            Host.Account = null;
            Host.Cfg.Save();
            offlineField.Text = name;
            Host.NotifyAccountChanged();
            loginStatus.Text = "已切换到离线账户：" + name;
            loginStatus.ForeColor = Theme.TextDim;
        }

        public override void RefreshState()
        {
            offlineField.Text = Host.Cfg.UserName;
            clientIdField.Text = Host.Cfg.ClientId;
            signOutButton.Enabled = Host.Account != null;

            if (Host.Account != null)
            {
                currentIdentity.Text = Host.Account.Name + "（微软正版）\nUUID：" + Host.Account.Uuid;
                currentIdentity.ForeColor = Theme.Accent;
            }
            else
            {
                currentIdentity.Text = Host.Cfg.UserName + "（离线账户）\n离线 UUID：" + GameLauncher.OfflineUuid(Host.Cfg.UserName);
                currentIdentity.ForeColor = Theme.TextDim;
            }
        }

        public void SetLoginStatus(string text, bool isError)
        {
            loginStatus.Text = text;
            loginStatus.ForeColor = isError ? Theme.Danger : Theme.TextDim;
            loginStatus.Refresh();
        }
    }

    // ==================================================================
    // Java 页
    // ==================================================================
    public class JavaPage : PageBase
    {
        private ListBox list;
        private UiProgress progress;
        private Label hint;

        public JavaPage(MainForm host) : base(host)
        {
            Title("Java 环境");

            CardPanel listCard = PageKit.Card("已检测到的 Java", Pad, 52, Inner, 236);
            Controls.Add(listCard);

            list = new ListBox();
            list.BorderStyle = BorderStyle.None;
            list.BackColor = Theme.Field;
            list.ForeColor = Theme.Text;
            list.Font = Theme.BodyFont;
            list.DrawMode = DrawMode.OwnerDrawFixed;
            list.ItemHeight = 30;
            list.IntegralHeight = false;
            list.SetBounds(18, 34, 706, 128);
            list.DrawItem += DrawJavaItem;
            list.SelectedIndexChanged += delegate
            {
                if (list.SelectedIndex >= 0) Host.SelectJava(list.SelectedIndex);
            };
            listCard.Controls.Add(list);

            UiButton rescanButton = PageKit.Button("重新检测", 18, 172, 150, 34, false);
            rescanButton.Compact = true;
            rescanButton.Click += delegate { Host.RescanJava(); };
            listCard.Controls.Add(rescanButton);

            UiButton browseButton = PageKit.Button("手动指定 java.exe", 182, 172, 190, 34, false);
            browseButton.Compact = true;
            browseButton.Click += delegate { Host.PickJavaFile(); };
            listCard.Controls.Add(browseButton);

            hint = PageKit.Text("", 386, 172, 338, 34, Theme.SmallFont, Theme.TextDim);
            listCard.Controls.Add(hint);

            CardPanel installCard = PageKit.Card("一键安装 Java", Pad, 304, Inner, 226);
            Controls.Add(installCard);

            UiButton x64Button = PageKit.Button("安装 64 位 Java 8", 18, 38, 224, 46, true);
            x64Button.Click += delegate { Host.InstallJava(true); };
            installCard.Controls.Add(x64Button);

            UiButton x86Button = PageKit.Button("安装 32 位 Java 8", 258, 38, 224, 46, true);
            x86Button.Click += delegate { Host.InstallJava(false); };
            installCard.Controls.Add(x86Button);

            UiButton portableButton = PageKit.Button("部署便携 Java", 498, 38, 226, 46, false);
            portableButton.Click += delegate { Host.DeployPortableJava(); };
            installCard.Controls.Add(portableButton);

            installCard.Controls.Add(PageKit.Text(
                "安装包位置：启动器目录\\Java\\Setup.exe（64 位）、Setup-x86.exe（32 位）；\n"
                + "便携 Java：把解压好的 JRE 8 放到 启动器目录\\Java\\jre8，点按钮即部署到 Java\\portable_jre8。\n"
                + "1.16.5 只认 Java 8；Java 11/17/21 启动会直接崩。",
                18, 96, 706, 62, Theme.SmallFont, Theme.TextDim));

            progress = new UiProgress();
            progress.SetBounds(18, 168, 706, 46);
            installCard.Controls.Add(progress);
        }

        private void DrawJavaItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= list.Items.Count) return;
            JavaInfo info = list.Items[e.Index] as JavaInfo;
            Rectangle bounds = new Rectangle(e.Bounds.X, e.Bounds.Y, list.Width, e.Bounds.Height);
            Color background = (e.State & DrawItemState.Selected) == DrawItemState.Selected
                ? Color.FromArgb(38, 52, 42) : Theme.Field;
            using (SolidBrush brush = new SolidBrush(background))
                e.Graphics.FillRectangle(brush, bounds);

            if (info == null) return;
            Color color = info.IsJava8 ? Theme.Text : Theme.TextDim;
            string text = info.Version + (info.BitKnown ? "   " + info.BitText : "")
                + (info.Broken ? "   （无法运行）" : (info.IsJava8 ? "" : "   （非 Java 8）"));
            TextRenderer.DrawText(e.Graphics, text, Theme.BodyFont,
                new Rectangle(bounds.X + 10, bounds.Y, 260, bounds.Height), color,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
            TextRenderer.DrawText(e.Graphics, info.Path, Theme.SmallFont,
                new Rectangle(bounds.X + 280, bounds.Y, bounds.Width - 290, bounds.Height), Theme.TextDim,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.PathEllipsis);
        }

        public void SetProgress(int percent, string text)
        {
            progress.SetState(percent, text);
        }

        public override void RefreshState()
        {
            list.Items.Clear();
            foreach (JavaInfo info in Host.JavaList) list.Items.Add(info);

            if (Host.JavaList.Count > 0)
            {
                int index = Host.JavaList.IndexOf(Host.CurrentJava);
                if (index >= 0) list.SelectedIndex = index;
            }
            hint.Text = Host.JavaList.Count == 0 ? "没有找到 Java，请安装或指定路径" : "共 " + Host.JavaList.Count + " 个运行时";
            progress.SetState(0, "");
        }
    }

    // ==================================================================
    // 设置页
    // ==================================================================
    public class SettingsPage : PageBase
    {
        private UiSlider maxSlider;
        private UiSlider minSlider;
        private Label maxLabel;
        private Label minLabel;
        private TextField dirField;
        private UiCheck keepOpen;

        public SettingsPage(MainForm host) : base(host)
        {
            Title("设置");

            CardPanel memoryCard = PageKit.Card("内存分配", Pad, 52, Inner, 176);
            Controls.Add(memoryCard);

            maxLabel = PageKit.Text("最大内存 2048 MB", 18, 34, 400, 24, Theme.BodyFont, Theme.Text);
            memoryCard.Controls.Add(maxLabel);
            maxSlider = new UiSlider();
            maxSlider.SetBounds(18, 56, 480, 28);
            maxSlider.Minimum = 512;
            maxSlider.Maximum = 8192;
            maxSlider.Value = host.Cfg.MaxMemoryMb;
            maxSlider.ValueChanged += delegate
            {
                maxLabel.Text = "最大内存 " + maxSlider.Value + " MB";
                if (minSlider.Value > maxSlider.Value) minSlider.Value = maxSlider.Value;
            };
            memoryCard.Controls.Add(maxSlider);

            minLabel = PageKit.Text("最小内存 1024 MB", 18, 88, 400, 24, Theme.BodyFont, Theme.Text);
            memoryCard.Controls.Add(minLabel);
            minSlider = new UiSlider();
            minSlider.SetBounds(18, 110, 480, 28);
            minSlider.Minimum = 256;
            minSlider.Maximum = 4096;
            minSlider.Value = host.Cfg.MinMemoryMb;
            minSlider.ValueChanged += delegate
            {
                minLabel.Text = "最小内存 " + minSlider.Value + " MB";
                if (minSlider.Value > maxSlider.Value) maxSlider.Value = minSlider.Value;
            };
            memoryCard.Controls.Add(minSlider);

            UiButton saveMemory = PageKit.Button("保存内存设置", 520, 46, 204, 40, true);
            saveMemory.Click += delegate
            {
                Host.Cfg.MaxMemoryMb = maxSlider.Value;
                Host.Cfg.MinMemoryMb = minSlider.Value;
                Host.Cfg.Save();
                Host.NotifyAccountChanged();
                MessageBox.Show("已保存：" + Host.Cfg.MinMemoryMb + " MB ~ " + Host.Cfg.MaxMemoryMb + " MB", AppInfo.Name);
            };
            memoryCard.Controls.Add(saveMemory);

            memoryCard.Controls.Add(PageKit.Text(
                "建议不超过物理内存的一半；X99 平台 12 核处理器跑 1.16.5，2~4 GB 足够。",
                18, 142, 480, 22, Theme.SmallFont, Theme.TextDim));

            CardPanel dirCard = PageKit.Card("游戏目录", Pad, 244, Inner, 132);
            Controls.Add(dirCard);

            dirCard.Controls.Add(PageKit.Text("游戏目录（.minecraft 所在路径）", 18, 34, 400, 20, Theme.SmallFont, Theme.TextDim));
            dirField = new TextField();
            dirField.SetBounds(18, 58, 520, 38);
            dirField.Text = host.Cfg.GameDir;
            dirCard.Controls.Add(dirField);

            UiButton pickDir = PageKit.Button("浏览", 550, 58, 80, 38, false);
            pickDir.Compact = false;
            pickDir.Click += delegate
            {
                FolderBrowserDialog dialog = new FolderBrowserDialog();
                dialog.Description = "选择 .minecraft 目录";
                if (dialog.ShowDialog() == DialogResult.OK) dirField.Text = dialog.SelectedPath;
            };
            dirCard.Controls.Add(pickDir);

            UiButton saveDir = PageKit.Button("保存目录", 640, 58, 84, 38, true);
            saveDir.Compact = false;
            saveDir.Click += delegate
            {
                Host.Cfg.GameDir = dirField.Text.Trim();
                Host.Cfg.Save();
                Host.NotifyAccountChanged();
            };
            dirCard.Controls.Add(saveDir);

            dirCard.Controls.Add(PageKit.Text("留空则使用 启动器目录\\Games\\.minecraft", 18, 102, 400, 20, Theme.SmallFont, Theme.TextDim));

            CardPanel otherCard = PageKit.Card("其它", Pad, 392, Inner, 110);
            Controls.Add(otherCard);

            keepOpen = new UiCheck();
            keepOpen.Text = "游戏启动后保持启动器窗口（方便看日志）";
            keepOpen.SetBounds(18, 36, 480, 24);
            keepOpen.Checked = host.Cfg.KeepLauncherOpen;
            keepOpen.CheckedChanged += delegate
            {
                Host.Cfg.KeepLauncherOpen = keepOpen.Checked;
                Host.Cfg.Save();
            };
            otherCard.Controls.Add(keepOpen);

            UiButton openConfig = PageKit.Button("打开 config.ini", 18, 66, 180, 32, false);
            openConfig.Compact = true;
            openConfig.Click += delegate { Host.OpenFile(AppInfo.ConfigFile); };
            otherCard.Controls.Add(openConfig);
        }

        public override void RefreshState()
        {
            maxSlider.Value = Host.Cfg.MaxMemoryMb;
            minSlider.Value = Host.Cfg.MinMemoryMb;
            maxLabel.Text = "最大内存 " + maxSlider.Value + " MB";
            minLabel.Text = "最小内存 " + minSlider.Value + " MB";
            dirField.Text = Host.Cfg.GameDir;
            keepOpen.Checked = Host.Cfg.KeepLauncherOpen;
        }
    }

    // ==================================================================
    // 日志页
    // ==================================================================
    public class LogPage : PageBase
    {
        private RichTextBox viewer;
        private UiSelect source;
        private Label pathLabel;

        public LogPage(MainForm host) : base(host)
        {
            Title("日志");

            CardPanel card = PageKit.Card("", Pad, 52, Inner, 478);
            Controls.Add(card);

            source = new UiSelect();
            source.SetBounds(18, 12, 240, 32);
            source.AddItem("游戏输出 (mc_log.txt)");
            source.AddItem("游戏 latest.log");
            source.AddItem("启动器 launcher.log");
            source.SelectedIndex = 0;
            source.SelectedIndexChanged += delegate { LoadLog(); };
            card.Controls.Add(source);

            UiButton refresh = PageKit.Button("刷新", 268, 14, 90, 28, false);
            refresh.Compact = true;
            refresh.Click += delegate { LoadLog(); };
            card.Controls.Add(refresh);

            UiButton openFolder = PageKit.Button("打开日志目录", 368, 14, 130, 28, false);
            openFolder.Compact = true;
            openFolder.Click += delegate { Host.OpenFolder(Path.GetDirectoryName(CurrentLogPath())); };
            card.Controls.Add(openFolder);

            UiButton clear = PageKit.Button("清空启动器日志", 508, 14, 140, 28, false);
            clear.Compact = true;
            clear.Click += delegate
            {
                Log.Clear();
                LoadLog();
            };
            card.Controls.Add(clear);

            viewer = new RichTextBox();
            viewer.BorderStyle = BorderStyle.None;
            viewer.BackColor = Color.FromArgb(16, 20, 26);
            viewer.ForeColor = Color.FromArgb(196, 205, 218);
            viewer.Font = new Font("Consolas", 9f, FontStyle.Regular);
            viewer.ReadOnly = true;
            viewer.WordWrap = true;
            viewer.ScrollBars = RichTextBoxScrollBars.Vertical;
            viewer.DetectUrls = false;
            viewer.SetBounds(18, 52, 706, 386);
            card.Controls.Add(viewer);

            pathLabel = PageKit.Text("", 18, 444, 706, 22, Theme.SmallFont, Theme.TextDim);
            pathLabel.AutoEllipsis = true;
            card.Controls.Add(pathLabel);
        }

        private string CurrentLogPath()
        {
            string gameDir = Host.Cfg.EffectiveGameDir;
            string gamesRoot = GameLauncher.ListVersions(gameDir).Length > 0 ? Path.GetDirectoryName(gameDir) : AppInfo.GamesRoot;
            switch (source.SelectedIndex)
            {
                case 1: return Path.Combine(gameDir, "logs", "latest.log");
                case 2: return Log.Path_;
                default: return Path.Combine(gamesRoot ?? AppInfo.Directory, "mc_log.txt");
            }
        }

        private void LoadLog()
        {
            string path = CurrentLogPath();
            string text;
            try
            {
                if (File.Exists(path))
                {
                    text = TextFile.ReadAllText(path);
                    string[] lines = text.Split('\n');
                    if (lines.Length > 3000)
                    {
                        text = string.Join("\n", lines, lines.Length - 3000, 3000);
                        text = "（只显示最后 3000 行）\n" + text;
                    }
                }
                else
                {
                    text = "（还没有这个日志文件）" + Environment.NewLine + path;
                }
            }
            catch (Exception ex)
            {
                text = "（读取失败：" + ex.Message + "）";
            }
            viewer.Text = text;
            viewer.SelectionStart = viewer.TextLength;
            viewer.ScrollToCaret();
            pathLabel.Text = path;
        }

        public override void RefreshState()
        {
            LoadLog();
        }
    }

    // ==================================================================
    // 关于页
    // ==================================================================
    public class AboutPage : PageBase
    {
        private RichTextBox changeLog;

        public AboutPage(MainForm host) : base(host)
        {
            Title("关于");

            CardPanel card = PageKit.Card("", Pad, 52, Inner, 182);
            Controls.Add(card);

            Label name = PageKit.Text(AppInfo.Name, 18, 20, 700, 38, Theme.Font(18f, true), Theme.Text);
            card.Controls.Add(name);

            card.Controls.Add(PageKit.Text("版本 " + AppInfo.Version, 18, 60, 200, 24, Theme.BodyFont, Theme.Accent));
            card.Controls.Add(PageKit.Text("作者 " + AppInfo.Author, 220, 60, 200, 24, Theme.BodyFont, Theme.TextDim));

            card.Controls.Add(PageKit.Text(
                "一个离线优先的 Minecraft 启动器：内置便携 Java 管理、微软正版登录、内存分配、日志查看。\n"
                + "除微软正版登录必须联网外，启动器不做任何联网操作：不下载文件、不联机、不读服务器。\n"
                + "构建方式：build.bat 调用系统自带 csc 编译，无需 Visual Studio。",
                18, 96, 706, 76, Theme.SmallFont, Theme.TextDim));

            CardPanel logCard = PageKit.Card("更新日志", Pad, 248, Inner, 192);
            Controls.Add(logCard);

            changeLog = new RichTextBox();
            changeLog.BorderStyle = BorderStyle.None;
            changeLog.BackColor = Color.FromArgb(16, 20, 26);
            changeLog.ForeColor = Color.FromArgb(196, 205, 218);
            changeLog.Font = Theme.BodyFont;
            changeLog.ReadOnly = true;
            changeLog.DetectUrls = false;
            changeLog.SetBounds(18, 40, 706, 142);
            logCard.Controls.Add(changeLog);

            UiButton openChangeLog = PageKit.Button("打开更新日志文件", 18, 456, 200, 32, false);
            openChangeLog.Compact = true;
            openChangeLog.Click += delegate { Host.OpenFile(AppInfo.ChangeLogFile); };
            Controls.Add(openChangeLog);

            UiButton openLog = PageKit.Button("查看运行日志", 228, 456, 160, 32, false);
            openLog.Compact = true;
            openLog.Click += delegate { Host.Navigate(4); };
            Controls.Add(openLog);

            Controls.Add(PageKit.Text("MIT License © 2026 Peter Brown  ·  源码随发布包一起提供，可自行改编译",
                18, 500, 700, 24, Theme.SmallFont, Theme.TextDim));
        }

        public override void RefreshState()
        {
            try
            {
                changeLog.Text = File.Exists(AppInfo.ChangeLogFile)
                    ? TextFile.ReadAllText(AppInfo.ChangeLogFile)
                    : "（没有找到更新日志文件）";
            }
            catch (Exception ex) { changeLog.Text = "（读取失败：" + ex.Message + "）"; }
        }
    }
}
