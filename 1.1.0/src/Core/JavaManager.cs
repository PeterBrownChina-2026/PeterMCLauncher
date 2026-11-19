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

            CollectFromRegistry(candidates);

            foreach (string drive in DriveLetters())
            {
                string programFiles = drive + ":\\Program Files";
                string programFilesX86 = drive + ":\\Program Files (x86)";
                AddFolders(candidates, Path.Combine(programFiles, "Java"), "jre1.8*");
                AddFolders(candidates, Path.Combine(programFiles, "Java"), "jdk1.8*");
                AddFolders(candidates, Path.Combine(programFilesX86, "Java"), "jre1.8*");
                AddFolders(candidates, Path.Combine(programFiles, "Eclipse Adoptium"), "jre-8*");
                AddFolders(candidates, Path.Combine(programFiles, "Eclipse Adoptium"), "jdk-8*");
                AddFolders(candidates, Path.Combine(programFiles, "Eclipse Temurin"), "jre-8*");
                AddFolders(candidates, Path.Combine(programFiles, "Zulu"), "zulu-8*");
                AddFolders(candidates, Path.Combine(programFiles, "Amazon Corretto"), "jre8*");
                AddMinecraftRuntime(candidates, Path.Combine(programFilesX86, "Minecraft Launcher", "runtime"));
                AddMinecraftRuntime(candidates, Path.Combine(programFiles, "Minecraft Launcher", "runtime"));
                AddOracleJavapathTarget(candidates, Path.Combine(programFiles, "Common Files", "Oracle", "Java"));
                AddOracleJavapathTarget(candidates, Path.Combine(programFilesX86, "Common Files", "Oracle", "Java"));
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
                    // Common Files\Oracle\Java\javapath\java.exe 是个转发壳子，跑 -version 不出东西，跳过
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
                if (a.IsJava8 != b.IsJava8) return a.IsJava8 ? -1 : 1;
                if (a.Broken != b.Broken) return a.Broken ? 1 : -1;
                if (a.Is64Bit != b.Is64Bit) return a.Is64Bit ? -1 : 1;
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
        // 一键安装
        // ------------------------------------------------------------------

        /// <summary>在 Java 目录里找安装包；x64 找 Setup.exe，x86 找 Setup-x86.exe。</summary>
        public static string FindInstaller(bool x64)
        {
            string[] exact = x64
                ? new string[] { "Setup.exe", "jre-8u*-windows-x64.exe", "jdk-8u*-windows-x64.exe" }
                : new string[] { "Setup-x86.exe", "jre-8u*-windows-i586.exe", "jdk-8u*-windows-i586.exe" };

            foreach (string dir in new string[] { AppInfo.JavaRoot, AppInfo.Directory, AppInfo.DataRoot })
            {
                foreach (string pattern in exact)
                {
                    try
                    {
                        if (pattern.Contains("*"))
                        {
                            string[] hits = Directory.GetFiles(dir, pattern);
                            if (hits.Length > 0) return hits[0];
                        }
                        else
                        {
                            string full = Path.Combine(dir, pattern);
                            if (File.Exists(full)) return full;
                        }
                    }
                    catch { }
                }
            }
            return null;
        }

        /// <summary>以管理员身份调起安装包（会弹 UAC）。返回 false 时 error 里是原因。</summary>
        public static bool RunInstaller(string installerPath, out string error)
        {
            error = "";
            if (installerPath == null || !File.Exists(installerPath))
            {
                error = "没有找到安装包，请把安装包放进启动器目录下的 Java 文件夹";
                return false;
            }
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(installerPath);
                psi.UseShellExecute = true;
                psi.Verb = "runas";
                psi.WorkingDirectory = Path.GetDirectoryName(installerPath);
                Log.Info("开始安装 Java：" + installerPath);
                Process process = Process.Start(psi);
                process.WaitForExit();
                Log.Info("安装包进程结束，退出码 " + process.ExitCode);
                return true;
            }
            catch (Win32Exception ex)
            {
                if (ex.NativeErrorCode == 1223) error = "你取消了管理员授权（UAC），安装没有执行";
                else error = "调起安装包失败：" + ex.Message;
                Log.Warn(error);
                return false;
            }
            catch (Exception ex)
            {
                error = "调起安装包失败：" + ex.Message;
                Log.Error(error);
                return false;
            }
        }

        /// <summary>部署便携 JRE：把 Java\jre8 复制为 Java\portable_jre8。已可用时直接返回 true。</summary>
        public static bool InstallPortable(Action<int, string> progress, out string error, out string message)
        {
            error = "";
            message = "";
            string source = AppInfo.PortableSource;
            string target = AppInfo.PortableTarget;

            JavaInfo existing = Probe(Path.Combine(target, "bin", "java.exe"));
            if (existing != null)
            {
                message = "便携 Java 已就绪：" + existing.Version + "，无需重复部署";
                return true;
            }
            if (!Directory.Exists(source))
            {
                error = "找不到便携 JRE 源目录：" + source;
                return false;
            }

            try
            {
                Log.Info("开始部署便携 JRE：" + source + " -> " + target);
                List<string> files = new List<string>();
                foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories)) files.Add(file);
                int total = files.Count;
                int skipped = 0;
                for (int i = 0; i < total; i++)
                {
                    string relative = files[i].Substring(source.Length).TrimStart('\\');
                    string destination = Path.Combine(target, relative);
                    try
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(destination));
                        File.Copy(files[i], destination, true);
                    }
                    catch (Exception ex)
                    {
                        skipped++;
                        Log.Warn("跳过无法复制的 JRE 文件 " + relative + "：" + ex.Message);
                    }
                    if (progress != null && ((i + 1) % 10 == 0 || i + 1 == total))
                        progress((i + 1) * 100 / total, "复制 " + relative);
                }
                Log.Info("便携 JRE 部署完成，共 " + total + " 个文件，跳过 " + skipped + " 个");
                return true;
            }
            catch (Exception ex)
            {
                error = "部署便携 JRE 失败：" + ex.Message + "（目标目录不可写时可换到可写磁盘）";
                Log.Error(error);
                return false;
            }
        }

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
