using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;

namespace PeterLauncher.Core
{
    public class JavaInfo
    {
        public string Path = "";
        public string Version = "";
        public bool BitKnown = false;
        public string BitText = "";
        public bool Broken = false;
    }

    public static class JavaManager
    {
        public static List<JavaInfo> Scan(string preferredPath)
        {
            List<JavaInfo> found = new List<JavaInfo>();

            if (!string.IsNullOrEmpty(preferredPath) && File.Exists(preferredPath))
            {
                JavaInfo probe = Probe(preferredPath);
                if (probe != null) found.Add(probe);
            }

            string[] roots = new string[]
            {
                AppInfo.JavaRoot,
                AppInfo.JreRoot,
                AppInfo.PortableSource,
                AppInfo.PortableTarget,
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Java"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Eclipse Adoptium"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Microsoft"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Java")
            };

            foreach (string root in roots)
            {
                if (string.IsNullOrEmpty(root) || !Directory.Exists(root)) continue;
                try
                {
                    foreach (string java in Directory.GetFiles(root, "java.exe", SearchOption.AllDirectories))
                    {
                        bool dup = false;
                        foreach (JavaInfo old in found)
                            if (string.Equals(old.Path, java, StringComparison.OrdinalIgnoreCase)) { dup = true; break; }
                        if (dup) continue;

                        JavaInfo info = Probe(java);
                        if (info != null) found.Add(info);
                    }
                }
                catch (Exception ex) { Log.Warn("扫描 Java 目录失败：" + root + "  " + ex.Message); }
            }

            return found;
        }

        public static JavaInfo Probe(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;
                if (!path.EndsWith("java.exe", StringComparison.OrdinalIgnoreCase)) return null;

                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = path;
                psi.Arguments = "-version";
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardError = true;
                psi.RedirectStandardOutput = true;

                Process p = Process.Start(psi);
                string stderr = p.StandardError.ReadToEnd();
                string stdout = p.StandardOutput.ReadToEnd();
                p.WaitForExit(5000);
                if (!p.HasExited) { try { p.Kill(); } catch { } }

                string all = stderr + "\n" + stdout;
                JavaInfo info = new JavaInfo();
                info.Path = path;

                string ver = ExtractVersion(all);
                if (ver.Length == 0) { info.Broken = true; return info; }
                info.Version = ver;

                if (all.IndexOf("64-Bit", StringComparison.OrdinalIgnoreCase) >= 0)
                { info.BitKnown = true; info.BitText = "64位"; }
                else if (all.IndexOf("32-Bit", StringComparison.OrdinalIgnoreCase) >= 0)
                { info.BitKnown = true; info.BitText = "32位"; }

                return info;
            }
            catch (Exception ex)
            {
                Log.Warn("探测 java 失败：" + path + "  " + ex.Message);
                return null;
            }
        }

        private static string ExtractVersion(string text)
        {
            try
            {
                System.Text.RegularExpressions.Regex r = new System.Text.RegularExpressions.Regex(
                    @"version \"(?:1\.)?([0-9]+)(\.[0-9.]+)?\"",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                System.Text.RegularExpressions.Match m = r.Match(text);
                if (m.Success)
                {
                    int major = int.Parse(m.Groups[1].Value);
                    if (major >= 9) return "Java " + major;
                    return "Java 1." + major;
                }
            }
            catch { }
            return "";
        }
    }
}
