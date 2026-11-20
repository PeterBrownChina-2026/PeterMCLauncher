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
    /// <summary>窗口右上角的最小化 / 关闭按钮。</summary>
    public class WindowButton : Control
    {
        public bool IsCloseButton = false;
        private bool hovered;

        public WindowButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint
                   | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Size = new Size(38, 32);
            Cursor = Cursors.Hand;
        }

        protected override void OnMouseEnter(EventArgs e) { hovered = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hovered = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Theme.EnableSmoothing(g);
            if (hovered)
            {
                Color color = IsCloseButton ? Color.FromArgb(196, 62, 62) : Color.FromArgb(40, 47, 58);
                Theme.FillRound(g, new Rectangle(0, 0, Width - 1, Height - 1), 8, color);
            }
            if (IsCloseButton)
            {
                using (Pen pen = new Pen(Color.FromArgb(226, 232, 240), 1.4f))
                {
                    g.DrawLine(pen, Width / 2f - 5, Height / 2f - 5, Width / 2f + 5, Height / 2f + 5);
                    g.DrawLine(pen, Width / 2f + 5, Height / 2f - 5, Width / 2f - 5, Height / 2f + 5);
                }
            }
            else
            {
                using (Pen pen = new Pen(Color.FromArgb(170, 180, 196), 1.4f))
                    g.DrawLine(pen, Width / 2f - 6, Height / 2f, Width / 2f + 6, Height / 2f);
            }
        }
    }

    /// <summary>主窗口：自定义标题栏 + 左侧导航 + 右侧页面。</summary>
    public class MainForm : Form
    {
        // ---- 运行状态 ----
        public Config Cfg;
        public List<JavaInfo> JavaList = new List<JavaInfo>();
        public JavaInfo CurrentJava;
        public MsAccount Account;

        private bool busy = false;
        private Process runningGame;

        // ---- 界面 ----
        private Panel topBar;
        private Panel sidebar;
        private Panel content;
        private Label statusLabel;
        private List<NavButton> navButtons = new List<NavButton>();
        private List<PageBase> pages = new List<PageBase>();

        private LaunchPage launchPage;
        private AccountPage accountPage;
        private JavaPage javaPage;
        private SettingsPage settingsPage;
        private LogPage logPage;
        private AboutPage aboutPage;

        private bool dragging;
        private Point dragCursor;
        private Point dragWindow;

        public MainForm()
        {
            Cfg = Config.Load();

            Text = AppInfo.Name + " " + AppInfo.Version;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(1000, 660);
            MinimumSize = new Size(1000, 660);
            BackColor = Theme.Background;
            DoubleBuffered = true;
            Font = Theme.BodyFont;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            BuildTopBar();
            BuildSidebar();
            BuildContent();
            BuildStatusBar();

            Log.Info("启动器启动，版本 " + AppInfo.Version);

            // 从 config.ini 恢复上次的微软账户，否则启动时就退化成离线账户（1.1.1 修复）
            RestoreMicrosoftAccount();
            launchPage.RefreshState();
            accountPage.RefreshState();

            // 首次启动：在后台把 .minecraft 释放出来，同时开始扫描 Java
            RunAsync(delegate
            {
                string message;
                ReleaseManager.EnsureMinecraft(Cfg.EffectiveGameDir, null, out message);
                Log.Info("首次释放检查：" + message);
            }, delegate
            {
                launchPage.RefreshState();
                RescanJava();
            });
        }

        // ==================================================================
        // 界面搭建
        // ==================================================================
        private void BuildTopBar()
        {
            topBar = new Panel();
            topBar.SetBounds(0, 0, ClientSize.Width, 54);
            topBar.BackColor = Theme.TitleBar;
            topBar.MouseDown += delegate (object s, MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Left)
                {
                    dragging = true;
                    dragCursor = Cursor.Position;
                    dragWindow = Location;
                }
            };
            topBar.MouseMove += delegate (object s, MouseEventArgs e)
            {
                if (dragging)
                {
                    Point now = Cursor.Position;
                    Location = new Point(dragWindow.X + (now.X - dragCursor.X), dragWindow.Y + (now.Y - dragCursor.Y));
                }
            };
            topBar.MouseUp += delegate { dragging = false; };
            topBar.Paint += PaintLogo;
            Controls.Add(topBar);

            Label title = new Label();
            title.Text = AppInfo.Name;
            title.Font = Theme.Font(13f, true);
            title.ForeColor = Theme.Text;
            title.BackColor = Color.Transparent;
            title.AutoSize = false;
            title.SetBounds(76, 12, 320, 30);
            title.TextAlign = ContentAlignment.MiddleLeft;
            title.MouseDown += delegate (object s, MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Left) { dragging = true; dragCursor = Cursor.Position; dragWindow = Location; }
            };
            topBar.Controls.Add(title);

            Label versionChip = new Label();
            versionChip.Text = "v" + AppInfo.Version;
            versionChip.Font = Theme.Font(9f, true);
            versionChip.ForeColor = Theme.Accent;
            versionChip.BackColor = Color.FromArgb(30, 44, 34);
            versionChip.AutoSize = false;
            versionChip.SetBounds(76 + 232, 16, 62, 22);
            versionChip.TextAlign = ContentAlignment.MiddleCenter;
            topBar.Controls.Add(versionChip);
            versionChip.BringToFront();

            WindowButton close = new WindowButton();
            close.IsCloseButton = true;
            close.SetBounds(ClientSize.Width - 48, 11, 38, 32);
            close.Click += delegate { Close(); };
            topBar.Controls.Add(close);

            WindowButton minimize = new WindowButton();
            minimize.SetBounds(ClientSize.Width - 90, 11, 38, 32);
            minimize.Click += delegate { WindowState = FormWindowState.Minimized; };
            topBar.Controls.Add(minimize);
        }

        private void PaintLogo(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Theme.EnableSmoothing(g);
            Rectangle canvas = new Rectangle(24, 10, 36, 36);
            Theme.FillRoundGradient(g, canvas, 10, Theme.AccentHover, Theme.AccentDown);
            using (Font font = Theme.Font(12f, true))
                TextRenderer.DrawText(g, "P", font, canvas, Color.FromArgb(14, 24, 16),
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            using (Pen pen = new Pen(Theme.Border, 1f))
                g.DrawLine(pen, 0, 53, Width, 53);
        }

        private void BuildSidebar()
        {
            sidebar = new Panel();
            sidebar.SetBounds(0, 54, 210, ClientSize.Height - 54);
            sidebar.BackColor = Theme.Sidebar;
            sidebar.Paint += delegate (object s, PaintEventArgs e)
            {
                using (Pen pen = new Pen(Theme.Border, 1f))
                    e.Graphics.DrawLine(pen, 209, 0, 209, sidebar.Height);
            };
            Controls.Add(sidebar);

            string[] titles = new string[] { "启动游戏", "账户", "Java 环境", "设置", "日志", "关于" };
            for (int i = 0; i < titles.Length; i++)
            {
                NavButton button = new NavButton();
                button.Text = titles[i];
                button.SetBounds(14, 14 + i * 50, 182, 44);
                int index = i;
                button.Click += delegate { Navigate(index); };
                sidebar.Controls.Add(button);
                navButtons.Add(button);
            }

            Label hint = new Label();
            hint.Text = "离线优先 · 只做正版登录";
            hint.Font = Theme.SmallFont;
            hint.ForeColor = Color.FromArgb(96, 106, 122);
            hint.AutoSize = false;
            hint.SetBounds(16, sidebar.Height - 34, 180, 20);
            sidebar.Controls.Add(hint);
        }

        private void BuildContent()
        {
            content = new Panel();
            content.SetBounds(210, 54, ClientSize.Width - 210, ClientSize.Height - 92);
            content.BackColor = Theme.Background;
            Controls.Add(content);

            launchPage = new LaunchPage(this);
            accountPage = new AccountPage(this);
            javaPage = new JavaPage(this);
            settingsPage = new SettingsPage(this);
            logPage = new LogPage(this);
            aboutPage = new AboutPage(this);

            pages.Add(launchPage);
            pages.Add(accountPage);
            pages.Add(javaPage);
            pages.Add(settingsPage);
            pages.Add(logPage);
            pages.Add(aboutPage);

            foreach (PageBase page in pages)
            {
                page.Visible = false;
                content.Controls.Add(page);
            }
            Navigate(0);
        }

        private void BuildStatusBar()
        {
            Panel bar = new Panel();
            bar.SetBounds(210, ClientSize.Height - 38, ClientSize.Width - 210, 38);
            bar.BackColor = Color.FromArgb(16, 20, 26);
            bar.Paint += delegate (object s, PaintEventArgs e)
            {
                using (Pen pen = new Pen(Theme.Border, 1f))
                    e.Graphics.DrawLine(pen, 0, 0, bar.Width, 0);
            };
            Controls.Add(bar);

            statusLabel = new Label();
            statusLabel.Text = "准备就绪";
            statusLabel.Font = Theme.SmallFont;
            statusLabel.ForeColor = Theme.TextDim;
            statusLabel.AutoSize = false;
            statusLabel.SetBounds(24, 0, bar.Width - 260, 38);
            statusLabel.TextAlign = ContentAlignment.MiddleLeft;
            bar.Controls.Add(statusLabel);

            Label sign = new Label();
            sign.Text = "Peter Brown  ·  " + AppInfo.Version;
            sign.Font = Theme.SmallFont;
            sign.ForeColor = Color.FromArgb(96, 106, 122);
            sign.AutoSize = false;
            sign.TextAlign = ContentAlignment.MiddleRight;
            sign.SetBounds(bar.Width - 232, 0, 208, 38);
            bar.Controls.Add(sign);
        }

        public void Navigate(int index)
        {
            if (index < 0 || index >= pages.Count) return;
            for (int i = 0; i < pages.Count; i++) pages[i].Visible = (i == index);
            for (int i = 0; i < navButtons.Count; i++) navButtons[i].SetActive(i == index);
            try { pages[index].RefreshState(); } catch (Exception ex) { Log.Exception("刷新页面", ex); }
        }

        public void SetStatus(string text)
        {
            statusLabel.Text = text;
            statusLabel.ForeColor = Theme.TextDim;
        }

        // ==================================================================
        // 线程与提示
        // ==================================================================
        private void Ui(Action action)
        {
            if (IsDisposed) return;
            try
            {
                if (InvokeRequired) BeginInvoke(new Action(action));
                else action();
            }
            catch { }
        }

        public void RunAsync(Action work, Action done)
        {
            Thread thread = new Thread(delegate ()
            {
                try
                {
                    work();
                }
                catch (Exception ex)
                {
                    Log.Exception("后台任务", ex);
                    Ui(delegate { ShowError(ex.Message); });
                }
                finally
                {
                    if (done != null) Ui(done);
                }
            });
            thread.IsBackground = true;
            thread.Start();
        }

        private void ShowError(string message)
        {
            SetStatus(message);
            MessageBox.Show(message, AppInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        public void NotifyAccountChanged()
        {
            launchPage.RefreshState();
            if (accountPage.Visible) accountPage.RefreshState();
        }

        // ==================================================================
        // 功能：启动游戏
        // ==================================================================
        public void LaunchGame()
        {
            if (busy) return;

            string gameDir = Cfg.EffectiveGameDir;
            string version = Cfg.LastVersion;

            if (!string.IsNullOrEmpty(version) && version.StartsWith("（"))
            {
                ShowError("游戏目录里没有可用版本，请检查 .minecraft\\versions");
                return;
            }

            JavaRequirement requirement = JavaProvisioner.RequirementFor(gameDir, version);
            JavaInfo java = CurrentJava;
            if (java == null)
            {
                DialogResult answer = MessageBox.Show(
                    "还没有可用的 Java。\n" + version + " 需要 " + requirement.Text + "，要现在按版本自动下载吗？",
                    AppInfo.Name, MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (answer == DialogResult.Yes) AutoSelectJava();
                return;
            }
            if (!JavaProvisioner.Satisfies(java, requirement))
            {
                DialogResult answer = MessageBox.Show(
                    "当前 Java 是 " + java.Version + "，但 " + version + " 需要 " + requirement.Text + "。\n\n"
                    + "「是」= 现在自动换（本机没有就下载）；「否」= 硬着头皮用当前 Java 启动；「取消」= 什么都不做。",
                    AppInfo.Name, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning);
                if (answer == DialogResult.Cancel) return;
                if (answer == DialogResult.Yes)
                {
                    AutoSelectJava();
                    return;
                }
            }

            List<string> problems = GameLauncher.CheckFiles(gameDir, version);
            if (problems.Count > 0)
            {
                string text = "游戏文件不完整：\n\n" + string.Join("\n", problems.ToArray());
                ShowError(text);
                return;
            }

            if (Account != null && Account.ExpireAt > 0 && Account.ExpireAt < DateTime.Now.Ticks)
            {
                ShowError("微软登录令牌已过期，请重新登录一次");
                Account = null;
                Navigate(1);
                return;
            }

            LaunchOptions options = new LaunchOptions();
            options.JavaPath = java.Path;
            options.GameDir = gameDir;
            options.VersionId = version;
            options.MinMemoryMb = Cfg.MinMemoryMb;
            options.MaxMemoryMb = Cfg.MaxMemoryMb;
            options.Status = delegate (string message) { Ui(delegate { launchPage.SetStatus(message); }); };

            if (Account != null)
            {
                options.PlayerName = Account.Name;
                options.Uuid = Account.Uuid;
                options.AccessToken = Account.AccessToken;
                options.MicrosoftAccount = true;
            }
            else
            {
                options.PlayerName = Cfg.UserName;
                options.Uuid = GameLauncher.OfflineUuid(Cfg.UserName);
                options.AccessToken = "0";
                options.MicrosoftAccount = false;
            }

            busy = true;
            launchPage.SetStatus("正在拼装启动参数…");
            Navigate(0);

            RunAsync(delegate
            {
                string logPath;
                Process process = GameLauncher.Launch(options, out logPath);
                runningGame = process;
                Ui(delegate
                {
                    launchPage.SetProgress(100, "游戏已启动");
                    SetStatus("游戏已启动，输出写入 " + logPath);
                    Log.Info("游戏进程已启动，PID=" + process.Id);
                });
                process.WaitForExit();
                int code = process.ExitCode;
                Log.Info("游戏进程结束，退出码 " + code);
                Ui(delegate
                {
                    launchPage.SetProgress(0, "");
                    launchPage.SetStatus("游戏已退出（退出码 " + code + "）");
                    SetStatus("游戏已退出，退出码 " + code);
                });
            }, delegate
            {
                busy = false;
                if (logPage.Visible) logPage.RefreshState();
            });
        }

        // ==================================================================
        // 功能：释放 .minecraft
        // ==================================================================
        public void ReleaseMinecraft(bool userInitiated)
        {
            string gameDir = Cfg.EffectiveGameDir;
            RunAsync(delegate
            {
                string message;
                DateTime start = DateTime.Now;
                bool changed = ReleaseManager.Release(gameDir, true, delegate (int percent, string name)
                {
                    Ui(delegate { launchPage.SetProgress(percent, name); });
                }, out message);
                double seconds = (DateTime.Now - start).TotalSeconds;
                Log.Info("释放 .minecraft：" + message + "，用时 " + seconds.ToString("0.0") + " 秒");
                Ui(delegate
                {
                    launchPage.SetProgress(0, "");
                    launchPage.SetStatus(message);
                    SetStatus(message);
                    launchPage.RefreshState();
                    if (userInitiated)
                        MessageBox.Show(message, AppInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Information);
                });
            }, null);
        }

        // ==================================================================
        // 功能：Java
        // ==================================================================
        public void RescanJava()
        {
            SetStatus("正在扫描本机 Java…");
            RunAsync(delegate
            {
                List<JavaInfo> found = JavaManager.Scan(Cfg.JavaPath);
                Ui(delegate
                {
                    JavaList = found;
                    object selected = null;
                    if (CurrentJava != null)
                    {
                        foreach (JavaInfo info in found)
                            if (string.Equals(info.Path, CurrentJava.Path, StringComparison.OrdinalIgnoreCase)) selected = info;
                    }
                    CurrentJava = (JavaInfo)selected;
                    JavaRequirement requirement = JavaProvisioner.RequirementFor(Cfg.EffectiveGameDir, Cfg.LastVersion);
                    if (CurrentJava == null || !JavaProvisioner.Satisfies(CurrentJava, requirement))
                    {
                        JavaInfo matched = JavaProvisioner.Match(found, requirement);
                        if (matched != null) CurrentJava = matched;
                    }
                    if (CurrentJava == null && found.Count > 0) CurrentJava = found[0];
                    if (CurrentJava != null && !string.Equals(Cfg.JavaPath, CurrentJava.Path, StringComparison.OrdinalIgnoreCase))
                    {
                        Cfg.JavaPath = CurrentJava.Path;
                        Cfg.Save();
                    }
                    if (javaPage.Visible) javaPage.RefreshState();
                    launchPage.RefreshState();
                    SetStatus(JavaList.Count == 0
                        ? "没有找到 Java，请到「Java 环境」页安装"
                        : "已找到 " + JavaList.Count + " 个 Java 运行时");
                });
            }, null);
        }

        public void SelectJava(int index)
        {
            if (index < 0 || index >= JavaList.Count) return;
            CurrentJava = JavaList[index];
            Cfg.JavaPath = CurrentJava.Path;
            Cfg.Save();
            launchPage.RefreshState();
            SetStatus("已选用 Java：" + CurrentJava.Version + "  " + CurrentJava.Path);
        }

        public void PickJavaFile()
        {
            OpenFileDialog dialog = new OpenFileDialog();
            dialog.Title = "选择 java.exe";
            dialog.Filter = "java.exe|java.exe|所有文件|*.*";
            if (dialog.ShowDialog() != DialogResult.OK) return;

            SetStatus("正在验证 " + dialog.FileName + " …");
            string path = dialog.FileName;
            RunAsync(delegate
            {
                JavaInfo info = JavaManager.Probe(path);
                Ui(delegate
                {
                    if (info == null)
                    {
                        ShowError("这个文件不是可用的 java.exe");
                        return;
                    }
                    JavaList.Insert(0, info);
                    CurrentJava = info;
                    Cfg.JavaPath = info.Path;
                    Cfg.Save();
                    javaPage.RefreshState();
                    launchPage.RefreshState();
                    SetStatus("已指定 Java：" + info.Version);
                });
            }, null);
        }

        /// <summary>切换游戏版本后重新匹配一次 Java（不会下载，只要能匹配上就立刻切过去）。</summary>
        public void OnVersionChanged()
        {
            JavaRequirement requirement = JavaProvisioner.RequirementFor(Cfg.EffectiveGameDir, Cfg.LastVersion);
            if (CurrentJava == null || !JavaProvisioner.Satisfies(CurrentJava, requirement))
            {
                JavaInfo matched = JavaProvisioner.Match(JavaList, requirement);
                if (matched != null)
                {
                    CurrentJava = matched;
                    Cfg.JavaPath = matched.Path;
                    Cfg.Save();
                    SetStatus("已按 " + Cfg.LastVersion + " 自动选用 Java " + JavaProvisioner.FeatureOf(matched));
                }
            }
            launchPage.RefreshState();
            if (javaPage.Visible) javaPage.RefreshState();
        }

        /// <summary>
        /// 「根据我的游戏版本自动选择 Java」：先按版本清单 / 版本号算出要哪个 Java，
        /// 本机有就直接切过去，没有就从官方镜像下载 JRE 装到 Java\\jre{主版本}。
        /// </summary>
        public void AutoSelectJava()
        {
            if (busy) return;

            string gameDir = Cfg.EffectiveGameDir;
            string version = Cfg.LastVersion;
            if (string.IsNullOrEmpty(version) || version.StartsWith("（"))
            {
                ShowError("还没有选游戏版本。\n先把游戏目录指好、在「启动游戏」页选一个版本，启动器才知道要下哪个 Java。");
                Navigate(0);
                return;
            }

            JavaRequirement requirement = JavaProvisioner.RequirementFor(gameDir, version);
            Log.Info("自动选择 Java：" + version + " → " + requirement.Text + "（" + requirement.Basis + "）");

            JavaInfo matched = JavaProvisioner.Match(JavaList, requirement);
            if (matched != null)
            {
                CurrentJava = matched;
                Cfg.JavaPath = matched.Path;
                Cfg.Save();
                javaPage.RefreshState();
                launchPage.RefreshState();
                SetStatus(version + " 用 " + matched.Version + "（" + requirement.Text + "），已切换");
                return;
            }

            int feature = JavaProvisioner.DownloadableFeature(requirement.Major);
            int major = requirement.Major;

            SetStatus("正在为 " + version + " 准备 Java " + feature + " …");
            javaPage.SetProgress(2, "准备下载 Java " + feature);
            Navigate(2);

            RunAsync(delegate
            {
                string error, message;
                bool ok = JavaProvisioner.Install(major, delegate (int percent, string text)
                {
                    Ui(delegate { javaPage.SetProgress(percent, text); });
                }, out error, out message);

                List<JavaInfo> found = JavaManager.Scan(Cfg.JavaPath);
                Ui(delegate
                {
                    javaPage.SetProgress(0, "");
                    JavaList = found;
                    if (!ok)
                    {
                        javaPage.RefreshState();
                        ShowError(error
                            + "\n\n也可以自己下载 JRE，解压到 " + Path.Combine(AppInfo.JavaRoot, "jre" + feature)
                            + "，再点「重新检测」。");
                        SetStatus("Java 下载失败");
                        return;
                    }

                    JavaInfo best = JavaProvisioner.Match(found, requirement);
                    if (best == null && found.Count > 0) best = found[0];
                    if (best != null)
                    {
                        CurrentJava = best;
                        Cfg.JavaPath = best.Path;
                        Cfg.Save();
                    }
                    javaPage.RefreshState();
                    launchPage.RefreshState();
                    SetStatus(message);
                    MessageBox.Show(message + "\n\n启动器已经自动切换到这个 Java。", AppInfo.Name,
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                });
            }, null);
        }

        // ==================================================================
        // 功能：微软登录
        // ==================================================================
        public void LoginDeviceCode(Label statusLabel)
        {
            string clientId = Cfg.ClientId.Trim();
            if (clientId.Length == 0)
            {
                statusLabel.Text = "先填写 Azure 应用的客户端 ID";
                statusLabel.ForeColor = Theme.Warning;
                return;
            }

            RunAsync(delegate
            {
                try
                {
                    DeviceCodeInfo info = MicrosoftAuth.RequestDeviceCode(clientId, AppInfo.Name);
                    Ui(delegate
                    {
                        try { Clipboard.SetText(info.UserCode); } catch { }
                        statusLabel.Text = "请在 " + info.VerificationUri + " 输入代码 " + info.UserCode + "（已复制到剪贴板）";
                        statusLabel.ForeColor = Theme.Accent;
                    });
                    MicrosoftAuth.OpenBrowser(info.VerificationUri);

                    string tokenJson = MicrosoftAuth.PollAccessToken(clientId, info, delegate (string message)
                    {
                        Ui(delegate { statusLabel.Text = message; statusLabel.ForeColor = Theme.TextDim; });
                    }, delegate { return false; });

                    string accessToken, refreshToken;
                    int expiresIn;
                    MicrosoftAuth.ReadTokenResponse(tokenJson, out accessToken, out refreshToken, out expiresIn);

                    MsAccount account = MicrosoftAuth.ChainToMinecraft(accessToken, delegate (string message)
                    {
                        Ui(delegate { statusLabel.Text = message; statusLabel.ForeColor = Theme.TextDim; });
                    });
                    account.RefreshToken = refreshToken;
                    ApplyMicrosoftAccount(account);

                    Ui(delegate
                    {
                        statusLabel.Text = "登录成功：" + account.Name;
                        statusLabel.ForeColor = Theme.Accent;
                    });
                }
                catch (Exception ex)
                {
                    Log.Exception("微软登录", ex);
                    Ui(delegate
                    {
                        statusLabel.Text = ex.Message;
                        statusLabel.ForeColor = Theme.Danger;
                    });
                }
            }, null);
        }

        public void LoginLegacy(Label statusLabel)
        {
            LegacyLoginForm form = new LegacyLoginForm();
            if (form.ShowDialog(this) != DialogResult.OK)
            {
                statusLabel.Text = "已取消登录";
                statusLabel.ForeColor = Theme.TextDim;
                return;
            }

            ChainAndApply(form.AccessToken, statusLabel, "微软登录（旧版）");
        }

        /// <summary>用浏览器登录后粘贴回来的回调链接登录（内嵌浏览器被微软拦截时的兜底方案）。</summary>
        public void LoginFromCallback(string rawUrl, Label statusLabel)
        {
            string token = MicrosoftAuth.ParseLegacyToken(rawUrl);
            if (token.Length == 0)
            {
                statusLabel.Text = "这段链接里没有 access_token。请复制浏览器地址栏里 oauth20_desktop.srf#access_token= 那一整条。";
                statusLabel.ForeColor = Theme.Danger;
                return;
            }
            statusLabel.Text = "已从链接里读到令牌，正在验证…";
            statusLabel.ForeColor = Theme.TextDim;
            ChainAndApply(token, statusLabel, "微软登录（浏览器链接）");
        }

        /// <summary>微软令牌 → Xbox Live → XSTS → Minecraft 档案 → 落盘。</summary>
        private void ChainAndApply(string msToken, Label statusLabel, string tag)
        {
            statusLabel.Text = "正在通过 Xbox Live 验证…";
            statusLabel.ForeColor = Theme.TextDim;

            RunAsync(delegate
            {
                try
                {
                    MsAccount account = MicrosoftAuth.ChainToMinecraft(msToken, delegate (string message)
                    {
                        Ui(delegate { statusLabel.Text = message; });
                    });
                    ApplyMicrosoftAccount(account);
                    Ui(delegate
                    {
                        statusLabel.Text = "登录成功：" + account.Name;
                        statusLabel.ForeColor = Theme.Accent;
                    });
                }
                catch (Exception ex)
                {
                    Log.Exception(tag, ex);
                    Ui(delegate
                    {
                        statusLabel.Text = ex.Message;
                        statusLabel.ForeColor = Theme.Danger;
                    });
                }
            }, null);
        }

        /// <summary>启动时把 config.ini 里保存的微软账户恢复到内存对象，否则启动游戏会当成离线账户。</summary>
        private void RestoreMicrosoftAccount()
        {
            if (!Cfg.UseMicrosoftAccount) return;
            if (Cfg.MicrosoftToken == null || Cfg.MicrosoftToken.Trim().Length == 0) return;
            if (Cfg.MicrosoftName == null || Cfg.MicrosoftName.Trim().Length == 0) return;

            if (Cfg.MicrosoftExpire > 0 && Cfg.MicrosoftExpire < DateTime.Now.Ticks)
            {
                Log.Warn("config.ini 里的微软令牌已过期，本次按离线账户启动");
                Cfg.UseMicrosoftAccount = false;
                return;
            }

            MsAccount account = new MsAccount();
            account.Name = Cfg.MicrosoftName;
            account.Uuid = Cfg.MicrosoftUuid;
            account.AccessToken = Cfg.MicrosoftToken;
            account.RefreshToken = Cfg.MicrosoftRefresh;
            account.ExpireAt = Cfg.MicrosoftExpire;
            Account = account;
            Log.Info("已从 config.ini 恢复微软账户：" + account.Name);
        }

        private void ApplyMicrosoftAccount(MsAccount account)
        {
            Account = account;
            Cfg.UseMicrosoftAccount = true;
            Cfg.MicrosoftName = account.Name;
            Cfg.MicrosoftUuid = account.Uuid;
            Cfg.MicrosoftToken = account.AccessToken;
            Cfg.MicrosoftExpire = account.ExpireAt;
            Cfg.MicrosoftRefresh = account.RefreshToken;
            Cfg.Save();
            NotifyAccountChanged();
            SetStatus("已登录微软账户：" + account.Name);
        }

        public void SignOutMicrosoft()
        {
            Account = null;
            Cfg.UseMicrosoftAccount = false;
            Cfg.MicrosoftName = "";
            Cfg.MicrosoftUuid = "";
            Cfg.MicrosoftToken = "";
            Cfg.MicrosoftRefresh = "";
            Cfg.MicrosoftExpire = 0;
            Cfg.Save();
            NotifyAccountChanged();
            Navigate(1);
            SetStatus("已退出微软账户，当前使用离线账户 " + Cfg.UserName);
        }

        public void OpenMicrosoftLoginPage()
        {
            try { Process.Start(new ProcessStartInfo(MicrosoftAuth.LegacyLoginUrl()) { UseShellExecute = true }); }
            catch (Exception ex) { ShowError("打开浏览器失败：" + ex.Message); }
        }

        // ==================================================================
        // 常见操作
        // ==================================================================
        public void OpenFolder(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path)) return;
                if (!Directory.Exists(path)) Directory.CreateDirectory(path);
                Process.Start("explorer.exe", "\"" + path + "\"");
            }
            catch (Exception ex) { ShowError("打开目录失败：" + ex.Message); }
        }

        public void OpenFile(string path)
        {
            try
            {
                if (!File.Exists(path))
                {
                    ShowError("文件不存在：" + path);
                    return;
                }
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch (Exception ex) { ShowError("打开文件失败：" + ex.Message); }
        }

        /// <summary>相当于旧版 bat 的「进入系统级命令窗口」。</summary>
        public void OpenAdminShell()
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo("cmd.exe", "/k cd /d \"" + AppInfo.Directory + "\"");
                psi.UseShellExecute = true;
                psi.Verb = "runas";
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                ShowError("打开管理员命令行失败：" + ex.Message);
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (busy && runningGame == null)
            {
                DialogResult answer = MessageBox.Show("还有任务在跑，确定要退出吗？", AppInfo.Name,
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (answer != DialogResult.Yes) e.Cancel = true;
            }
            base.OnFormClosing(e);
        }
    }

    /// <summary>旧版弹窗登录：内嵌浏览器走 login.live.com，从跳转地址里取 access_token。</summary>
    public class LegacyLoginForm : Form
    {
        private WebBrowser browser;
        public string AccessToken = "";

        public LegacyLoginForm()
        {
            Text = "微软账户登录";
            ClientSize = new Size(520, 680);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = Color.White;

            browser = new WebBrowser();
            browser.Dock = DockStyle.Fill;
            browser.ScriptErrorsSuppressed = true;
            browser.Navigating += delegate (object sender, WebBrowserNavigatingEventArgs e)
            {
                Check(e.Url == null ? "" : e.Url.ToString());
            };
            browser.Navigated += delegate (object sender, WebBrowserNavigatedEventArgs e)
            {
                Check(e.Url == null ? "" : e.Url.ToString());
            };
            Controls.Add(browser);
            browser.Navigate(MicrosoftAuth.LegacyLoginUrl());
        }

        private void Check(string url)
        {
            if (url == null || !url.Contains("oauth20_desktop.srf")) return;
            string token = MicrosoftAuth.ParseLegacyToken(url);
            if (token.Length == 0) return;
            AccessToken = token;
            try { browser.Stop(); } catch { }
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
