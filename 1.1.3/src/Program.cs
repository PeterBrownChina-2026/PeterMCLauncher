using System;
using System.Windows.Forms;
using PeterLauncher.Core;
using PeterLauncher.UI;

namespace PeterLauncher
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            Log.Info("启动 " + AppInfo.Name + " v" + AppInfo.Version);
            JreProvisioner.EnsureBundled();

            try
            {
                using (MainForm form = new MainForm())
                {
                    Application.Run(form);
                }
            }
            catch (Exception ex)
            {
                Log.Exception("主窗口崩溃", ex);
                MessageBox.Show("启动器遇到错误：" + ex.Message + "\n\n日志：" + Log.Path_, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
