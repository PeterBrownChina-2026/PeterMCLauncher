using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace PeterLauncher.Core
{
    /// <summary>一个可用的 Java 运行时。</summary>
    public class JavaInfo
    {
        public string Path = "";
        public string Version = "";
        public bool IsJava8 = false;
        public bool Is64Bit = true;
        /// <summary>能探测到文件但跑 -version 没输出（多半是坏的转发壳子）。</summary>
        public bool Broken = false;
        /// <summary>位数是否真的探测到了（没输出时不瞎猜）。</summary>
        public bool BitKnown = true;

        public string BitText { get { return Is64Bit ? "64 位" : "32 位"; } }

        public override string ToString()
        {
            return Version + "  " + BitText + "  ·  " + Path;
        }
    }

    /// <summary>Java 探测、便携 JRE 部署、安装包调起。</summary>
    public static class JavaManager
    {
        /// <summary>跑一次 java -version 拿真实版本，失败返回 null。</summary>
        public static JavaInfo Probe(string javaExe)
        {
            if (javaExe == null || javaExe.Trim().Length == 0) return null;
            if (!File.Exists(javaExe)) return null;
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(javaExe, "-version");
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardError = true;
                psi.RedirectStandardOutput = true;
                Process process = Process.Start(psi);
                string output = "";
                using (StreamReader err = process.StandardError) output += err.ReadToEnd();
                using (StreamReader std = process.StandardOutput) output += std.ReadToEnd();
                if (!process.WaitForExit(6000))
                {
                    try { process.Kill(); } catch { }
                    return null;
                }

                JavaInfo info = new JavaInfo();
                info.Path = javaExe;
                string version = "";
                System.Text.RegularExpressions.Match quoted = System.Text.RegularExpressions.Regex.Match(output, "\"([0-9][^\"]*)\"");
                if (quoted.Success) version = quoted.Groups[1].Value;
                if (version.Length == 0)
                {
                    System.Text.RegularExpressions.Match loose =
                        System.Text.RegularExpressions.Regex.Match(output, "([0-9]+\\.[0-9]+(\\.[0-9]+)?(_[0-9]+)?)");
                    if (loose.Success) version = loose.Groups[1].Value;
                }
                if (version.Length == 0)
                {
                    // 跑不出版本时退回读 exe 的文件版本信息（javapath 这类转发壳子靠这个也能认出来）
                    try
                    {
                        FileVersionInfo fi = FileVersionInfo.GetVersionInfo(javaExe);
                        string product = fi.ProductVersion == null ? "" : fi.ProductVersion.Trim();
                        if (product.Length == 0 && fi.FileVersion != null) product = fi.FileVersion.Trim();
                        if (product.Length > 0) version = product;
                    }
                    catch { }
                }
                info.Version = version.Length > 0 ? version : "未知版本";
                info.IsJava8 = version.StartsWith("1.8") || version.StartsWith("8.");
                string lower = output.ToLowerInvariant();
                info.Is64Bit = lower.Contains("64-bit") || lower.Contains("amd64") || lower.Contains("x86_64");
                info.Broken = output.Trim().Length == 0;
                info.BitKnown = !info.Broken;
                return info;
            }
            catch (Exception ex)
            {
                Log.Warn("探测 Java 失败（" + javaExe + "）：" + ex.Message);
                return null;
            }
        }

        /// <summary>扫描本机所有能找到的 Java，按「Java 8 优先、64 位优先」排序。</summary>
        public static List<JavaInfo> Scan(string preferredPath)
        {
            List<string> candidates = new List<string>();
            if (preferredPath != null && preferredPath.Length > 0) candidates.Add(preferredPath);

            candidates.Add(Path.Combine(AppInfo.PortableTarget, "bin", "java.exe"));
            candidates.Add(Path.Combine(AppInfo.PortableSource, "bin", "java.exe"));
            // 启动器自己下载的 JRE 都放在 Java\\jre{主版本} 下
            AddFolders(candidates, AppInfo.JavaRoot, "jre*");
            AddFolders(candidates, AppInfo.JavaRoot, "jdk*");
            // 1.1.2 起兼容 JRE\\ 目录名（用户可能把便携 JRE 放在 JRE 而非 Java）
            AddFolders(candidates, AppInfo.JreRoot, "jre*");
            AddFolders(candidates, AppInfo.JreRoot, "jdk*");

            CollectFromRegistry(candidates);

            foreach (string drive in DriveLetters())
            {
                string programFiles = drive + ":\\Program Files";
                string programFilesX86 = drive + ":\\Program Files (x86)";
                foreach (string dir in new string[] { programFiles, programFilesX86 })
                {
                    AddFolders(candidates, Path.Combine(dir, "Java"), "jre1.8*");
                    AddFolders(candidates, Path.Combine(dir, "Java"), "jdk1.8*");
                    AddFolders(candidates, Path.Combine(dir, "Java"), "jre-1*");
                    AddFolders(candidates, Path.Combine(dir, "Java"), "jdk-1*");
                    AddFolders(candidates, Path.Combine(dir, "Eclipse Adoptium"), "jre-*");
                    AddFolders(candidates, Path.Combine(dir, "Eclipse Adoptium"), "jdk-*");
                    AddFolders(candidates, Path.Combine(dir, "Eclipse Temurin"), "jre-*");
                    AddFolders(candidates, Path.Combine(dir, "Eclipse Temurin"), "jdk-*");
                    AddFolders(candidates, Path.Combine(dir, "Microsoft"), "jdk-*");
                    AddFolders(candidates, Path.Combine(dir, "Zulu"), "zulu-*");
                    AddFolders(candidates, Path.Combine(dir, "Amazon Corretto"), "jre*");
                    AddFolders(candidates, Path.Combine(dir, "Amazon Corretto"), "jdk*");
                    AddFolders(candidates, Path.Combine(dir, "BellSoft"), "LibericaJDK-*");
                    AddMinecraftRuntime(candidates, Path.Combine(dir, "Minecraft Launcher", "runtime"));
                    AddOracleJavapathTarget(candidates, Path.Combine(dir, "Common Files", "Oracle", "Java"));
                }
            }

            string pathJava = FindInPath("java.exe");
            if (pathJava != null) candidates.Add(pathJava);

            string javaHome = Environment.GetEnvironmentVariable("JAVA_HOME");
            if (javaHome != null && javaHome.Length > 0)
                candidates.Add(Path.Combine(javaHome, "bin", "java.exe"));

            List<JavaInfo> result = new List<JavaInfo>();
            foreach (string candidate in candidates)
            {
                try
                {
                    string full = Path.GetFullPath(candidate);
                    // Common Files\\Oracle\\Java\\javapath\\java.exe 是个转发壳子，跑 -version 不出东西，跳过
                    if (full.ToLowerInvariant().Contains("\\javapath\\java.exe")) continue;
                    bool duplicated = false;
                    foreach (JavaInfo existing in result)
                        if (string.Equals(existing.Path, full, StringComparison.OrdinalIgnoreCase)) { duplicated = true; break; }
                    if (duplicated) continue;
                    JavaInfo info = Probe(full);
                    if (info != null) result.Add(info);
                }
                catch { }
            }

            result.Sort(delegate (JavaInfo a, JavaInfo b)
            {
                if (a.Broken != b.Broken) return a.Broken ? 1 : -1;
                if (a.Is64Bit != b.Is64Bit) return a.Is64Bit ? -1 : 1;
                int fa = JavaProvisioner.FeatureOf(a);
                int fb = JavaProvisioner.FeatureOf(b);
                if (fa != fb) return fa > fb ? -1 : 1;
                return string.Compare(a.Path, b.Path, StringComparison.OrdinalIgnoreCase);
            });
            return result;
        }

        private static string[] DriveLetters()
        {
            List<string> list = new List<string>();
            try
            {
                foreach (DriveInfo drive in DriveInfo.GetDrives())
                {
                    if (drive.DriveType == DriveType.Fixed || drive.DriveType == DriveType.Removable)
                        list.Add(drive.Name.Substring(0, 1).ToUpperInvariant());
                }
            }
            catch { }
            if (list.Count == 0) { list.Add("C"); list.Add("D"); }
            return list.ToArray();
        }

        private static void AddFolders(List<string> list, string baseDir, string pattern)
        {
            try
            {
                if (!Directory.Exists(baseDir)) return;
                foreach (string sub in Directory.GetDirectories(baseDir, pattern))
                    list.Add(Path.Combine(sub, "bin", "java.exe"));
            }
            catch { }
        }

        private static void AddOracleJavapathTarget(List<string> list, string commonFiles)
        {
            try
            {
                if (!Directory.Exists(commonFiles)) return;
                foreach (string sub in Directory.GetDirectories(commonFiles, "javapath_target*"))
                    list.Add(Path.Combine(sub, "java.exe"));
            }
            catch { }
        }

        private static void AddMinecraftRuntime(List<string> list, string runtimeDir)
        {
            try
            {
                if (!Directory.Exists(runtimeDir)) return;
                foreach (string component in Directory.GetDirectories(runtimeDir))
                    foreach (string platform in Directory.GetDirectories(component))
                        list.Add(Path.Combine(platform, "bin", "java.exe"));
            }
            catch { }
        }

        private static string FindInPath(string exeName)
        {
            try
            {
                string path = Environment.GetEnvironmentVariable("PATH");
                if (path == null) return null;
                foreach (string part in path.Split(';'))
                {
                    if (part.Trim().Length == 0) continue;
                    string full = Path.Combine(part.Trim(), exeName);
                    if (File.Exists(full)) return full;
                }
            }
            catch { }
            return null;
        }

        private static void CollectFromRegistry(List<string> list)
        {
            string[] keys = new string[]
            {
                "SOFTWARE\\JavaSoft\\Java Runtime Environment",
                "SOFTWARE\\JavaSoft\\Java Development Kit",
                "SOFTWARE\\Wow6432Node\\JavaSoft\\Java Runtime Environment",
                "SOFTWARE\\Wow6432Node\\JavaSoft\\Java Development Kit"
            };
            foreach (string key in keys)
            {
                try
                {
                    using (RegistryKey root = Registry.LocalMachine.OpenSubKey(key))
                    {
                        if (root == null) continue;
                        foreach (string sub in root.GetSubKeyNames())
                        {
                            using (RegistryKey item = root.OpenSubKey(sub))
                            {
                                if (item == null) continue;
                                object javaHome = item.GetValue("JavaHome");
                                if (javaHome != null)
                                    list.Add(Path.Combine(javaHome.ToString(), "bin", "java.exe"));
                            }
                        }
                    }
                }
                catch { }
            }
        }

        // ------------------------------------------------------------------
        // 说明：1.1.1 起不再调起 Oracle 安装包 / 复制便携 JRE，
        // 改由 JavaProvisioner 按游戏版本从镜像站下载 Temurin JRE 到 Java\\jre{主版本}。
        // ------------------------------------------------------------------

        /// <summary>把系统上已有的 Java 注册进 config（供未来扩展使用）。</summary>
        public static string VersionLevel(string version)
        {
            if (version == null) return "unknown";
            if (version.StartsWith("1.8") || version.StartsWith("8.")) return "8";
            if (version.StartsWith("1.7") || version.StartsWith("7.")) return "7";
            int dot = version.IndexOf('.');
            return dot > 0 ? version.Substring(0, dot) : version;
        }
    }
}
