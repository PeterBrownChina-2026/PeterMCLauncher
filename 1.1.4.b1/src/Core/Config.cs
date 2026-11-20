using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace PeterLauncher.Core
{
    /// <summary>产品常量与便携目录约定。启动器所有文件都在 exe 所在目录下，方便塞在 U 盘里带走。</summary>
    public static class AppInfo
    {
        public const string Name = "Peter Minecraft launcher";
        public const string Version = "1.1.4.b1";
        public const string Author = "Peter Brown";
        public const string GameVersionDefault = "1.16.5";

        /// <summary>exe 所在目录（不带结尾反斜杠）。</summary>
        public static string Directory
        {
            get
            {
                string d = AppDomain.CurrentDomain.BaseDirectory;
                if (d.Length > 0 && (d.EndsWith("\\") || d.EndsWith("/")))
                    d = d.Substring(0, d.Length - 1);
                return d;
            }
        }

        public static string ConfigFile { get { return Path.Combine(Directory, "config.ini"); } }
        public static string GamesRoot { get { return Path.Combine(Directory, "Games"); } }
        public static string DefaultGameDir { get { return Path.Combine(GamesRoot, ".minecraft"); } }
        public static string JavaRoot { get { return Path.Combine(Directory, "Java"); } }
        public static string JreRoot { get { return Path.Combine(Directory, "JRE"); } }
        public static string PortableSource { get { return Path.Combine(JavaRoot, "jre8"); } }
        public static string PortableTarget { get { return Path.Combine(JavaRoot, "portable_jre8"); } }
        public static string DataRoot { get { return Path.Combine(Directory, "data"); } }
        public static string ChangeLogFile { get { return Path.Combine(Directory, "更新日志.txt"); } }
    }

    /// <summary>
    /// config.ini 读写。键名向后兼容旧版批处理启动器（username / maxmem 直接沿用）。
    /// </summary>
    public class Config
    {
        public string UserName = "Player";
        public int MinMemoryMb = 1024;
        public int MaxMemoryMb = 2048;
        public string JavaPath = "";
        public string GameDir = "";
        public string ClientId = "";
        public bool UseMicrosoftAccount = false;
        public string MicrosoftName = "";
        public string MicrosoftUuid = "";
        public string MicrosoftToken = "";
        public long MicrosoftExpire = 0;
        public string MicrosoftRefresh = "";
        public string LastVersion = AppInfo.GameVersionDefault;
        public bool KeepLauncherOpen = false;

        private string filePath = AppInfo.ConfigFile;

        public static Config Load()
        {
            Config c = new Config();
            string path = AppInfo.ConfigFile;
            if (!File.Exists(path)) return c;
            string[] lines;
            try { lines = File.ReadAllLines(path, Encoding.UTF8); }
            catch { return c; }

            foreach (string raw in lines)
            {
                string line = raw.Trim();
                if (line.Length == 0) continue;
                if (line.StartsWith("#") || line.StartsWith(";") || line.StartsWith("//")) continue;
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                string key = line.Substring(0, eq).Trim().ToLowerInvariant();
                string value = line.Substring(eq + 1).Trim();
                if (value.Length >= 2 && value.StartsWith("\"") && value.EndsWith("\""))
                    value = value.Substring(1, value.Length - 2);
                switch (key)
                {
                    case "username": c.UserName = value; break;
                    case "minmem": c.MinMemoryMb = ParseMemory(value, c.MinMemoryMb); break;
                    case "maxmem": c.MaxMemoryMb = ParseMemory(value, c.MaxMemoryMb); break;
                    case "java": c.JavaPath = value; break;
                    case "gamedir": c.GameDir = value; break;
                    case "clientid": c.ClientId = value; break;
                    case "account": c.UseMicrosoftAccount = (value.ToLowerInvariant() == "ms"); break;
                    case "msname": c.MicrosoftName = value; break;
                    case "msuuid": c.MicrosoftUuid = value; break;
                    case "mstoken": c.MicrosoftToken = value; break;
                    case "msrefresh": c.MicrosoftRefresh = value; break;
                    case "msexpire": long.TryParse(value, out c.MicrosoftExpire); break;
                    case "lastversion": c.LastVersion = value; break;
                    case "keepopen": c.KeepLauncherOpen = (value == "1" || value.ToLowerInvariant() == "true"); break;
                    default: break;
                }
            }
            if (c.UserName.Trim().Length == 0) c.UserName = "Player";
            return c;
        }

        public void Save()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("# " + AppInfo.Name + " 配置文件（版本 " + AppInfo.Version + "）");
            sb.AppendLine("# 兼容旧版 config.ini：username = 离线昵称，minmem / maxmem = 内存");
            sb.AppendLine();
            sb.AppendLine("username=" + UserName);
            sb.AppendLine("minmem=" + MinMemoryMb + "M");
            sb.AppendLine("maxmem=" + MaxMemoryMb + "M");
            sb.AppendLine("java=" + JavaPath);
            sb.AppendLine("gamedir=" + GameDir);
            sb.AppendLine("clientid=" + ClientId);
            sb.AppendLine("account=" + (UseMicrosoftAccount ? "ms" : "offline"));
            sb.AppendLine("msname=" + MicrosoftName);
            sb.AppendLine("msuuid=" + MicrosoftUuid);
            sb.AppendLine("mstoken=" + MicrosoftToken);
            sb.AppendLine("msrefresh=" + MicrosoftRefresh);
            sb.AppendLine("msexpire=" + MicrosoftExpire);
            sb.AppendLine("lastversion=" + LastVersion);
            sb.AppendLine("keepopen=" + (KeepLauncherOpen ? "1" : "0"));
            try
            {
                TextFile.WriteAllText(filePath, sb.ToString(), true);
            }
            catch (Exception ex)
            {
                Log.Error("保存 config.ini 失败：" + ex.Message);
            }
        }

        public string EffectiveGameDir
        {
            get
            {
                if (GameDir != null && GameDir.Trim().Length > 0) return GameDir.Trim();
                return AppInfo.DefaultGameDir;
            }
        }

        /// <summary>把 "2G" / "2048M" / "2048" 解析成 MB。</summary>
        public static int ParseMemory(string text, int fallbackMb)
        {
            if (text == null) return fallbackMb;
            string s = text.Trim().ToUpperInvariant();
            if (s.Length == 0) return fallbackMb;
            int digits = 0;
            foreach (char ch in s)
            {
                if (char.IsDigit(ch)) digits = digits * 10 + (ch - '0');
                else break;
            }
            if (digits <= 0) return fallbackMb;
            if (s.EndsWith("G")) return digits * 1024;
            if (s.EndsWith("T")) return digits * 1024 * 1024;
            return digits;
        }

        public static string MemoryText(int mb)
        {
            if (mb % 1024 == 0) return (mb / 1024) + "G";
            return mb + "M";
        }
    }
}
