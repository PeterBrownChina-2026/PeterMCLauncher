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
    /// <summary>主窗口。所有页面都在这里切换，不拆 Form。</summary>
    public class MainForm : Form
    {
        private readonly Dictionary<string, Control> pages = new Dictionary<string, Control>();
        private readonly Dictionary<string, Button> navButtons = new Dictionary<string, Button>();
        private Panel sidebar;
        private Panel content;
        private Panel navWrap;
        private Label versionLabel;
        private Label statusLabel;

        private Config config;
        private ReleaseManager release;
        private JavaRequirement pendingRequirement;

        public MainForm()
        {
            Text = AppInfo.Name + " " + AppInfo.Version;
            MinimumSize = new Size(900, 600);
            Size = new Size(1040, 660);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Microsoft YaHei UI", 9F);

            config = Config.Load();
            release = new ReleaseManager();

            BuildLayout();

            Load += delegate { SelectPage("launch"); ShowStatus("就绪，Java " + config.JavaMajor + " 要求已按上一次配置记住"); };
        }

        private void BuildLayout()
        {
            sidebar = new Panel { Dock = DockStyle.Left, Width = 200, BackColor = Theme.Sidebar };
            content = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Background };

            Label title = new Label
            {
                Text = AppInfo.Name,
                Font = new Font("Microsoft YaHei UI", 16F, FontStyle.Bold),
                ForeColor = Theme.Text,
                Dock = DockStyle.Top,
                Height = 64,
                TextAlign = ContentAlignment.MiddleCenter
            };
            sidebar.Controls.Add(title);

            navWrap = new Panel { Dock = DockStyle.Top, Padding = new Padding(10, 8, 10, 16) };
            sidebar.Controls.Add(navWrap);

            versionLabel = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 28,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Theme.Muted,
                Text = "v" + AppInfo.Version
            };
            sidebar.Controls.Add(versionLabel);

            statusLabel = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 28,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(12, 0, 0, 0),
                ForeColor = Theme.Muted,
                BackColor = Theme.Card,
                Text = ""
            };
            content.Controls.Add(statusLabel);

            Controls.Add(content);
            Controls.Add(sidebar);

            AddNav("launch", "启动", BuildLaunchPage);
            AddNav("versions", "版本管理", BuildVersionsPage);
            AddNav("releases", "整合包", BuildReleasesPage);
            AddNav("java", "Java 设置", BuildJavaPage);
            AddNav("settings", "启动器设置", BuildSettingsPage);
            AddNav("about", "关于", BuildAboutPage);

            sidebar.Controls.SetChildIndex(navWrap, 0);
            sidebar.Controls.SetChildIndex(title, 0);
        }

        private void AddNav(string key, string text, Func<Control> factory)
        {
            Button button = new Button
            {
                Text = text,
                Dock = DockStyle.Top,
                Height = 42,
                FlatStyle = FlatStyle.Flat,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(14, 0, 0, 0),
                Font = new Font("Microsoft YaHei UI", 10F),
                BackColor = Theme.Sidebar,
                ForeColor = Theme.Text
            };
            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseOverBackColor = Theme.SidebarHover;
            button.Click += delegate { SelectPage(key); };
            navWrap.Controls.Add(button);
            navButtons[key] = button;

            pages[key] = factory();
            pages[key].Dock = DockStyle.Fill;
            pages[key].Visible = false;
            content.Controls.Add(pages[key]);
            content.Controls.SetChildIndex(pages[key], 0);
        }

        private void SelectPage(string key)
        {
            foreach (KeyValuePair<string, Control> kv in pages)
                kv.Value.Visible = kv.Key == key;
            foreach (KeyValuePair<string, Button> kv in navButtons)
                kv.Value.BackColor = kv.Key == key ? Theme.SidebarActive : Theme.Sidebar;
            if (pages[key] is IPage page) page.OnShown();
        }

        private void ShowStatus(string text)
        {
            if (InvokeRequired) { Invoke(new Action<string>(ShowStatus), text); return; }
            statusLabel.Text = text;
        }

        private Control BuildLaunchPage()
        {
            LaunchPage page = new LaunchPage(config, release);
            page.OnStatus = ShowStatus;
            page.OnLaunch += HandleLaunch;
            page.OnLaunchBlocked += HandleLaunchBlocked;
            page.OnSettingsChange += delegate { Save(); };
            return page;
        }

        private Control BuildVersionsPage()
        {
            return new VersionsPage(config);
        }

        private Control BuildReleasesPage()
        {
            return new ReleasesPage(release, config);
        }

        private Control BuildJavaPage()
        {
            return new JavaPage(config, Save);
        }

        private Control BuildSettingsPage()
        {
            return new SettingsPage(config, Save);
        }

        private Control BuildAboutPage()
        {
            return new AboutPage();
        }

        private void Save()
        {
            try { config.Save(); ShowStatus("设置已保存"); }
            catch (Exception ex) { ShowStatus("保存失败：" + ex.Message); }
        }

        // ==================================================================
        // 启动流程
        // ==================================================================

        private void HandleLaunchBlocked(JavaRequirement requirement)
        {
            pendingRequirement = requirement;
            ShowStatus("当前缺少 Java " + requirement.Major + "，正在打开 Java 设置页");
            SelectPage("java");
        }

        private void HandleLaunch(LaunchOptions options)
        {
            ShowStatus("正在启动 Minecraft …");
            Thread thread = new Thread(delegate ()
            {
                try
                {
                    string logPath;
                    Process process = GameLauncher.Launch(options, out logPath);
                    ShowStatus("游戏进程已启动（PID " + process.Id + "），日志：" + logPath);
                    Process monitor = new Process();
                    monitor.StartInfo.FileName = process.Id.ToString();
                    monitor.EnableRaisingEvents = true;
                    monitor.Exited += delegate
                    {
                        ShowStatus("游戏进程退出了，可以回来启动器继续");
                    };
                }
                catch (Exception ex)
                {
                    ShowStatus("启动失败：" + ex.Message);
                    MessageBox.Show(this, ex.Message, "启动失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            });
            thread.IsBackground = true;
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
        }
    }

    internal interface IPage
    {
        void OnShown();
    }
}
