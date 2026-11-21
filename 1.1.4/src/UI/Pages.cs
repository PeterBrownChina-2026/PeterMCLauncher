using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using PeterLauncher.Core;

namespace PeterLauncher.UI
{
    /// <summary>启动页：大按钮 + 版本选择 + 内存滑块 + 状态条。</summary>
    public class LaunchPage : UserControl, IPage
    {
        private readonly Config config;
        private readonly ReleaseManager release;
        private ComboBox versionBox;
        private Label title;
        private Button playButton;
        private TrackBar memorySlider;
        private Label memoryLabel;
        private ProgressBar progress;
        private Label progressLabel;
        private ComboBox javaBox;
        private Button openGameDir;

        public Action<string> OnStatus; set; private get;
        public Action<LaunchOptions> OnLaunch; set; private get;
        public Action<JavaRequirement> OnLaunchBlocked; set; private get;
        public Action OnSettingsChange; set; private get;

        public LaunchPage(Config config, ReleaseManager release)
        {
            this.config = config;
            this.release = release;
            Build();
        }

        private void Build()
        {
            Dock = DockStyle.Fill;
            BackColor = Theme.Background;
            Padding = new Padding(28);

            title = new Label
            {
                Text = AppInfo.Name,
                Font = new Font("Microsoft YaHei UI", 22F, FontStyle.Bold),
                ForeColor = Theme.Text,
                AutoSize = true,
                Location = new Point(0, 8)
            };
            Controls.Add(title);

            Label versionCaption = new Label { Text = "游戏版本", ForeColor = Theme.Muted, AutoSize = true, Location = new Point(4, 80) };
            versionBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(4, 100), Width = 260 };
            versionBox.SelectedIndexChanged += delegate
            {
                if (versionBox.SelectedItem == null) return;
                config.Version = versionBox.SelectedItem.ToString();
                RefreshJavaOptions();
                if (OnSettingsChange != null) OnSettingsChange();
            };

            Label memoryCaption = new Label { Text = "内存分配", ForeColor = Theme.Muted, AutoSize = true, Location = new Point(4, 148) };
            memorySlider = new TrackBar { Minimum = 1, Maximum = 16, Value = 4, Location = new Point(4, 168), Width = 260, TickFrequency = 1 };
            memoryLabel = new Label { Text = "4 GB", ForeColor = Theme.Text, AutoSize = true, Location = new Point(272, 172) };
            memorySlider.ValueChanged += delegate
            {
                memoryLabel.Text = memorySlider.Value + " GB";
                if (config != null) config.MemoryGb = memorySlider.Value;
            };

            Label javaCaption = new Label { Text = "Java 运行时", ForeColor = Theme.Muted, AutoSize = true, Location = new Point(4, 216) };
            javaBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(4, 236), Width = 320 };
            javaBox.SelectedIndexChanged += delegate
            {
                if (javaBox.SelectedItem is JavaItem item) config.JavaPath = item.Path;
            };

            openGameDir = new Button { Text = "打开游戏目录", Location = new Point(4, 280), Width = 130, Height = 30, FlatStyle = FlatStyle.Flat };
            openGameDir.FlatAppearance.BorderColor = Theme.Border;
            openGameDir.Click += delegate
            {
                try { Process.Start("explorer.exe", config.GameDir); }
                catch (Exception ex) { ReportStatus("打不开游戏目录：" + ex.Message); }
            };

            playButton = new Button
            {
                Text = "启动游戏",
                Size = new Size(220, 64),
                Location = new Point(4, 330),
                FlatStyle = FlatStyle.Flat,
                BackColor = Theme.Accent,
                ForeColor = Color.White,
                Font = new Font("Microsoft YaHei UI", 13F, FontStyle.Bold)
            };
            playButton.FlatAppearance.BorderSize = 0;
            playButton.Click += OnPlay;

            progress = new ProgressBar { Location = new Point(4, 410), Width = 460, Height = 8 };
            progressLabel = new Label { Text = "", ForeColor = Theme.Muted, AutoSize = true, Location = new Point(4, 424) };

            Controls.AddRange(new Control[] {
                title, versionCaption, versionBox, memoryCaption, memorySlider, memoryLabel,
                javaCaption, javaBox, openGameDir, playButton, progress, progressLabel
            });
        }

        public void OnShown()
        {
            RefreshVersions();
            RefreshJavaOptions();
            memorySlider.Value = Math.Max(1, Math.Min(16, config.MemoryGb));
        }

        private void RefreshVersions()
        {
            versionBox.Items.Clear();
            List<string> versions = new List<string>();
            try
            {
                if (Directory.Exists(config.VersionsDir))
                {
                    foreach (string dir in Directory.GetDirectories(config.VersionsDir))
                    {
                        string name = Path.GetFileName(dir);
                        if (File.Exists(Path.Combine(dir, name + ".json"))) versions.Add(name);
                    }
                }
            }
            catch (Exception ex) { ReportStatus("读不到版本目录：" + ex.Message); }

            versions.Sort((a, b) => string.CompareOrdinal(b, a));
            foreach (string v in versions) versionBox.Items.Add(v);

            if (versionBox.Items.Count == 0)
            {
                versionBox.Items.Add("（还没有任何版本）");
                versionBox.SelectedIndex = 0;
                playButton.Enabled = false;
                return;
            }

            playButton.Enabled = true;
            int index = versionBox.FindStringExact(config.Version);
            if (index < 0) index = 0;
            versionBox.SelectedIndex = index;
        }

        private void RefreshJavaOptions()
        {
            javaBox.Items.Clear();
            List<JavaInfo> list = JavaManager.Scan(config);
            foreach (JavaInfo info in list)
            {
                javaBox.Items.Add(new JavaItem(info));
                if (string.Equals(info.Path, config.JavaPath, StringComparison.OrdinalIgnoreCase))
                    javaBox.SelectedIndex = javaBox.Items.Count - 1;
            }

            JavaRequirement requirement = JavaProvisioner.RequirementFor(config.GameDir, config.Version);
            config.JavaMajor = requirement.Major;
            title.Text = AppInfo.Name + "  ·  需要 Java " + requirement.Major;

            if (javaBox.SelectedIndex < 0)
            {
                JavaInfo match = JavaProvisioner.Match(list, requirement);
                if (match != null)
                {
                    config.JavaPath = match.Path;
                    RefreshJavaOptions();
                }
            }
        }

        private void OnPlay(object sender, EventArgs e)
        {
            string version = versionBox.SelectedItem == null ? "" : versionBox.SelectedItem.ToString();
            if (version.Length == 0 || version.StartsWith("（"))
            {
                MessageBox.Show(this, "先选一个已经装好的版本。", "还没选版本", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            JavaRequirement requirement = JavaProvisioner.RequirementFor(config.GameDir, version);
            List<JavaInfo> list = JavaManager.Scan(config);
            JavaInfo match = JavaProvisioner.Match(list, requirement);
            if (match == null)
            {
                if (OnLaunchBlocked != null) OnLaunchBlocked(requirement);
                return;
            }

            config.JavaPath = match.Path;
            LaunchOptions options = new LaunchOptions();
            options.Config = config;
            options.VersionId = version;
            options.JavaPath = match.Path;
            options.MemoryGb = config.MemoryGb;
            if (OnLaunch != null) OnLaunch(options);
        }

        private void ReportStatus(string text)
        {
            if (OnStatus != null) OnStatus(text);
            progressLabel.Text = text;
        }

        private class JavaItem
        {
            public JavaInfo Info;
            public string Path { get { return Info.Path; } }
            public JavaItem(JavaInfo info) { Info = info; }
            public override string ToString() { return Info.ShortDescription; }
        }
    }

    /// <summary>版本管理页：列出本地版本，可删。</summary>
    public class VersionsPage : UserControl, IPage
    {
        private readonly Config config;
        private ListBox list;

        public VersionsPage(Config config)
        {
            this.config = config;
            Dock = DockStyle.Fill;
            BackColor = Theme.Background;
            Padding = new Padding(24);

            Label title = new Label { Text = "已安装版本", Font = new Font("Microsoft YaHei UI", 14F, FontStyle.Bold), ForeColor = Theme.Text, AutoSize = true, Location = new Point(4, 4) };
            list = new ListBox { Location = new Point(4, 44), Width = 420, Height = 360, IntegralHeight = false };
            Button delete = new Button { Text = "删除选中版本", Location = new Point(4, 414), Width = 140, Height = 30, FlatStyle = FlatStyle.Flat };
            delete.FlatAppearance.BorderColor = Theme.Border;
            delete.Click += delegate
            {
                if (list.SelectedItem == null) return;
                string name = list.SelectedItem.ToString();
                if (MessageBox.Show(this, "确定删除版本 \"" + name + "\"？游戏目录里这个版本的文件会一起没掉。", "删除版本", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) != DialogResult.OK) return;
                try
                {
                    Directory.Delete(Path.Combine(config.VersionsDir, name), true);
                    OnShown();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "删不掉：" + ex.Message, "失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };
            Controls.AddRange(new Control[] { title, list, delete });
        }

        public void OnShown()
        {
            list.Items.Clear();
            try
            {
                if (Directory.Exists(config.VersionsDir))
                {
                    foreach (string dir in Directory.GetDirectories(config.VersionsDir))
                    {
                        string name = Path.GetFileName(dir);
                        if (File.Exists(Path.Combine(dir, name + ".json"))) list.Items.Add(name);
                    }
                }
            }
            catch { }
        }
    }

    /// <summary>整合包页：列出仓库里已登记的整合包，一键解压到游戏目录。</summary>
    public class ReleasesPage : UserControl, IPage
    {
        private readonly ReleaseManager release;
        private readonly Config config;
        private ListBox list;
        private TextBox log;

        public ReleasesPage(ReleaseManager release, Config config)
        {
            this.release = release;
            this.config = config;
            Dock = DockStyle.Fill;
            BackColor = Theme.Background;
            Padding = new Padding(24);

            Label title = new Label { Text = "整合包", Font = new Font("Microsoft YaHei UI", 14F, FontStyle.Bold), ForeColor = Theme.Text, AutoSize = true, Location = new Point(4, 4) };
            list = new ListBox { Location = new Point(4, 44), Width = 300, Height = 280, IntegralHeight = false };
            Button apply = new Button { Text = "解压选中整合包", Location = new Point(4, 332), Width = 160, Height = 30, FlatStyle = FlatStyle.Flat };
            apply.FlatAppearance.BorderColor = Theme.Border;
            apply.Click += OnApply;
            log = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = Vertical, Location = new Point(320, 44), Width = 460, Height = 320, BackColor = Color.White };

            Controls.AddRange(new Control[] { title, list, apply, log });
        }

        public void OnShown()
        {
            list.Items.Clear();
            foreach (ReleaseInfo info in release.List()) list.Items.Add(info);
        }

        private void OnApply(object sender, EventArgs e)
        {
            if (list.SelectedItem is not ReleaseInfo info) return;
            log.Text = "正在解压 " + info.FileName + " …\r\n";
            Thread thread = new Thread(delegate ()
            {
                try
                {
                    release.Apply(info, config.GameDir, delegate (int percent, string text)
                    {
                        AppendLog(percent + "%  " + text);
                    });
                    AppendLog("完成。请到启动页选版本。");
                }
                catch (Exception ex)
                {
                    AppendLog("失败：" + ex.Message);
                }
            });
            thread.IsBackground = true;
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
        }

        private void AppendLog(string text)
        {
            if (InvokeRequired) { Invoke(new Action<string>(AppendLog), text); return; }
            log.AppendText(text + "\r\n");
        }
    }

    /// <summary>Java 设置页：列出已识别 Java，可下载缺的版本。</summary>
    public class JavaPage : UserControl, IPage
    {
        private readonly Config config;
        private readonly Action save;
        private ListBox list;
        private ComboBox needBox;

        public JavaPage(Config config, Action save)
        {
            this.config = config;
            this.save = save;
            Dock = DockStyle.Fill;
            BackColor = Theme.Background;
            Padding = new Padding(24);

            Label title = new Label { Text = "Java 运行时", Font = new Font("Microsoft YaHei UI", 14F, FontStyle.Bold), ForeColor = Theme.Text, AutoSize = true, Location = new Point(4, 4) };
            list = new ListBox { Location = new Point(4, 44), Width = 560, Height = 280, IntegralHeight = false };
            Label needCaption = new Label { Text = "要补的主版本：", ForeColor = Theme.Muted, AutoSize = true, Location = new Point(4, 336) };
            needBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(110, 332), Width = 80 };
            needBox.Items.AddRange(new object[] { 8, 17, 21, 25 });
            needBox.SelectedIndex = 0;
            Button download = new Button { Text = "下载并解压", Location = new Point(200, 330), Width = 120, Height = 30, FlatStyle = FlatStyle.Flat };
            download.FlatAppearance.BorderColor = Theme.Border;
            download.Click += OnDownload;

            Controls.AddRange(new Control[] { title, list, needCaption, needBox, download });
        }

        public void OnShown() { Refresh(); }

        private void Refresh()
        {
            list.Items.Clear();
            foreach (JavaInfo info in JavaManager.Scan(config)) list.Items.Add(info);
        }

        private void OnDownload(object sender, EventArgs e)
        {
            int wanted = (int)needBox.SelectedItem;
            DialogResult ask = MessageBox.Show(this, "要从镜像站下载 Java " + wanted + "（约 50~60 MB）并解压到启动器目录，继续？", "下载 Java", MessageBoxButtons.OKCancel, MessageBoxIcon.Question);
            if (ask != DialogResult.OK) return;

            Button button = (Button)sender;
            button.Enabled = false;
            Thread thread = new Thread(delegate ()
            {
                try
                {
                    string error, message;
                    bool ok = JavaProvisioner.Install(wanted, delegate (int percent, string text)
                    {
                        BeginInvoke(new Action(delegate
                        {
                            button.Text = percent + "%  " + text;
                        }));
                    }, out error, out message);

                    BeginInvoke(new Action(delegate
                    {
                        button.Enabled = true;
                        button.Text = "下载并解压";
                        Refresh();
                        if (ok) MessageBox.Show(this, message, "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        else MessageBox.Show(this, error, "失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        if (save != null) save();
                    }));
                }
                catch (Exception ex)
                {
                    BeginInvoke(new Action(delegate
                    {
                        button.Enabled = true;
                        button.Text = "下载并解压";
                        MessageBox.Show(this, ex.Message, "失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }));
                }
            });
            thread.IsBackground = true;
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
        }
    }

    /// <summary>启动器设置页：游戏目录、窗口大小。</summary>
    public class SettingsPage : UserControl, IPage
    {
        private readonly Config config;
        private TextBox gameDir;
        private TextBox width;
        private TextBox height;

        public SettingsPage(Config config, Action save)
        {
            this.config = config;
            Dock = DockStyle.Fill;
            BackColor = Theme.Background;
            Padding = new Padding(24);

            Label title = new Label { Text = "启动器设置", Font = new Font("Microsoft YaHei UI", 14F, FontStyle.Bold), ForeColor = Theme.Text, AutoSize = true, Location = new Point(4, 4) };
            Label gameCaption = new Label { Text = "游戏目录（.minecraft）", ForeColor = Theme.Muted, AutoSize = true, Location = new Point(4, 50) };
            gameDir = new TextBox { Text = config.GameDir, Location = new Point(4, 70), Width = 420 };
            Label winCaption = new Label { Text = "游戏窗口（宽 × 高）", ForeColor = Theme.Muted, AutoSize = true, Location = new Point(4, 110) };
            width = new TextBox { Text = config.WindowWidth.ToString(), Location = new Point(4, 130), Width = 80 };
            Label x = new Label { Text = "×", AutoSize = true, Location = new Point(92, 134) };
            height = new TextBox { Text = config.WindowHeight.ToString(), Location = new Point(112, 130), Width = 80 };
            Button saveBtn = new Button { Text = "保存", Location = new Point(4, 180), Width = 100, Height = 32, FlatStyle = FlatStyle.Flat };
            saveBtn.FlatAppearance.BorderColor = Theme.Border;
            saveBtn.Click += delegate
            {
                try
                {
                    config.GameDir = gameDir.Text.Trim();
                    config.WindowWidth = int.Parse(width.Text.Trim());
                    config.WindowHeight = int.Parse(height.Text.Trim());
                    save();
                    MessageBox.Show(this, "已保存", "设置", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "保存失败：" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };

            Controls.AddRange(new Control[] { title, gameCaption, gameDir, winCaption, width, x, height, saveBtn });
        }

        public void OnShown() { }
    }

    /// <summary>关于页。</summary>
    public class AboutPage : UserControl, IPage
    {
        public AboutPage()
        {
            Dock = DockStyle.Fill;
            BackColor = Theme.Background;
            Padding = new Padding(24);

            Label title = new Label { Text = AppInfo.Name, Font = new Font("Microsoft YaHei UI", 18F, FontStyle.Bold), ForeColor = Theme.Text, AutoSize = true, Location = new Point(4, 4) };
            Label body = new Label
            {
                Text = "版本 " + AppInfo.Version + "\r\n离线优先的 Minecraft 启动器，纯 C# / .NET Framework 4.0 WinForms。\r\n\r\n内置便携 JRE 8 / 17 / 21 / 25，运行时自动解压，单 exe 即可启动。\r\n\r\n仓库：github.com/PeterBrownChina-2026/PeterMCLauncher",
                ForeColor = Theme.Text,
                AutoSize = true,
                Location = new Point(4, 56)
            };
            Controls.Add(title);
            Controls.Add(body);
        }

        public void OnShown() { }
    }
}
