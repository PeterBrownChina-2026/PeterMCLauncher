using System;
using System.IO;
using System.Text;

namespace PeterLauncher.Core
{
    public static class Log
    {
        private static readonly object Gate = new object();

        public static string Path_
        {
            get { return System.IO.Path.Combine(AppInfo.Directory, "launcher.log"); }
        }

        public static void Info(string message) { Write("INFO", message); }
        public static void Warn(string message) { Write("WARN", message); }
        public static void Error(string message) { Write("ERR ", message); }
        public static void Exception(string where, Exception ex)
        {
            Write("ERR ", where + " 异常：" + ex.GetType().Name + " " + ex.Message
                + Environment.NewLine + ex.StackTrace);
        }

        public static void Message(string level, string text) { Write(level, text); }

        private static void Write(string level, string message)
        {
            string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " [" + level + "] " + message;
            lock (Gate)
            {
                try
                {
                    using (StreamWriter sw = new StreamWriter(Path_, true, new UTF8Encoding(true)))
                        sw.WriteLine(line);
                }
                catch { }
            }
        }

        public static string Tail(int maxLines)
        {
            try
            {
                if (!File.Exists(Path_)) return "（暂无启动器日志）";
                string[] lines = TextFile.ReadAllText(Path_).Split('\n');
                int start = Math.Max(0, lines.Length - maxLines);
                StringBuilder sb = new StringBuilder();
                for (int i = start; i < lines.Length; i++)
                    sb.Append(lines[i].TrimEnd('\r')).Append(Environment.NewLine);
                return sb.ToString();
            }
            catch (Exception ex)
            {
                return "（读取日志失败：" + ex.Message + "）";
            }
        }

        public static void Clear()
        {
            lock (Gate)
            {
                try { if (File.Exists(Path_)) File.Delete(Path_); }
                catch { }
            }
        }
    }
}
