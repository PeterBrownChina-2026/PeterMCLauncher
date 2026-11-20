using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using PeterLauncher.Core;
using PeterLauncher.UI;

[assembly: AssemblyTitle("Peter Minecraft launcher")]
[assembly: AssemblyDescription("离线优先的 Minecraft 启动器，按游戏版本自动匹配并下载 Java，带微软正版登录")]
[assembly: AssemblyProduct("Peter Minecraft launcher")]
[assembly: AssemblyCompany("Peter Brown")]
[assembly: AssemblyCopyright("Copyright (c) 2026 Peter Brown")]
[assembly: AssemblyVersion("1.1.4.0")]
[assembly: AssemblyFileVersion("1.1.4.0")]

namespace PeterLauncher
{
    internal static class Program
    {
        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        [STAThread]
        private static void Main()
        {
            try { SetProcessDPIAware(); } catch { }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            Application.ThreadException += delegate (object sender, ThreadExceptionEventArgs e)
            {
                Log.Exception("界面线程", e.Exception);
                MessageBox.Show("启动器遇到一个未处理的错误：\n\n" + e.Exception.Message
                    + "\n\n详细信息已写入 launcher.log", AppInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Error);
            };
            AppDomain.CurrentDomain.UnhandledException += delegate (object sender, UnhandledExceptionEventArgs e)
            {
                Exception ex = e.ExceptionObject as Exception;
                if (ex != null)
                    Log.Message("ERR ", "后台线程未捕获异常：" + Environment.NewLine + ex.ToString());
            };

            Log.Info("================================");
            Log.Info(AppInfo.Name + " 启动，版本 " + AppInfo.Version);

            Application.Run(new MainForm());
        }
    }
}
