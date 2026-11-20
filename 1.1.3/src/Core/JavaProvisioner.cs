using System;
using System.Collections.Generic;
using System.IO;

namespace PeterLauncher.Core
{
    public class JavaRequirement
    {
        public int Major = 8;
        public string Text = "Java 8";
        public string Basis = "";
    }

    public static class JreProvisioner
    {
        public static void EnsureBundled()
        {
            try
            {
                string src = AppInfo.PortableSource;
                string dst = AppInfo.PortableTarget;
                if (!Directory.Exists(src)) return;
                if (Directory.Exists(dst))
                {
                    string marker = Path.Combine(dst, "bin", "java.exe");
                    if (File.Exists(marker)) return;
                }
                CopyDirectory(src, dst, null);
                Log.Info("便携 JRE 已复制到：" + dst);
            }
            catch (Exception ex)
            {
                Log.Warn("复制便携 JRE 失败：" + ex.Message);
            }
        }

        private static void CopyDirectory(string source, string target, Action<int, string> progress)
        {
            Directory.CreateDirectory(target);
            foreach (string dir in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
                Directory.CreateDirectory(dir.Replace(source, target));

            int total = 0, done = 0;
            string[] files = Directory.GetFiles(source, "*", SearchOption.AllDirectories);
            total = files.Length;
            foreach (string file in files)
            {
                string rel = file.Substring(source.Length).TrimStart('\\');
                File.Copy(file, Path.Combine(target, rel), true);
                done++;
                if (progress != null && (done % 20 == 0 || done == total)) progress(done * 100 / total, rel);
            }
        }
    }

    public static class JavaProvisioner
    {
        public static JavaRequirement RequirementFor(string gameDir, string versionId)
        {
            JavaRequirement req = new JavaRequirement();
            req.Basis = "默认";

            if (string.IsNullOrEmpty(versionId) || versionId.StartsWith("（"))
            {
                req.Major = 8;
                req.Text = "Java 8";
                return req;
            }

            int major = ParseMajor(versionId);

            if (major >= 21)
            {
                req.Major = 21;
                req.Text = "Java 21";
                req.Basis = versionId + " 需要 Java 21";
            }
            else if (major >= 18)
            {
                req.Major = 17;
                req.Text = "Java 17";
                req.Basis = versionId + " 需要 Java 17";
            }
            else if (major >= 17)
            {
                req.Major = 17;
                req.Text = "Java 17";
                req.Basis = versionId + " 需要 Java 17";
            }
            else
            {
                req.Major = 8;
                req.Text = "Java 8";
                req.Basis = versionId + " 使用 Java 8";
            }

            string jsonPath = Path.Combine(gameDir, "versions", versionId, versionId + ".json");
            if (File.Exists(jsonPath))
            {
                try
                {
                    string text = TextFile.ReadAllText(jsonPath);
                    object root = Json.Parse(text);
                    object javaVersion = Json.Get(root, "javaVersion");
                    if (javaVersion != null)
                    {
                        string component = Json.Str(javaVersion, "component");
                        int parsed = ParseMajorFromComponent(component);
                        if (parsed > 0)
                        {
                            req.Major = parsed;
                            req.Text = "Java " + parsed;
                            req.Basis = versionId + " 的 json 指定 Java " + parsed;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Warn("读取版本 javaVersion 失败：" + ex.Message);
                }
            }

            return req;
        }

        public static bool Satisfies(JavaInfo info, JavaRequirement requirement)
        {
            if (info == null || info.Broken) return false;
            int feature = FeatureOf(info);
            return feature >= requirement.Major;
        }

        public static JavaInfo Match(List<JavaInfo> list, JavaRequirement requirement)
        {
            JavaInfo best = null;
            foreach (JavaInfo info in list)
            {
                if (info.Broken) continue;
                int feature = FeatureOf(info);
                if (feature >= requirement.Major)
                {
                    if (best == null || feature < FeatureOf(best))
                        best = info;
                }
            }
            return best;
        }

        public static int FeatureOf(JavaInfo info)
        {
            if (info == null || info.Version.Length == 0) return 0;
            try
            {
                string v = info.Version;
                if (v.StartsWith("Java 1.")) return int.Parse(v.Substring(7));
                if (v.StartsWith("Java ")) return int.Parse(v.Substring(5));
            }
            catch { }
            return 0;
        }

        public static int DownloadableFeature(int major)
        {
            if (major >= 21) return 21;
            if (major >= 17) return 17;
            return 8;
        }

        public static bool Install(int major, Action<int, string> progress, out string error, out string message)
        {
            error = "";
            message = "";
            string feature = DownloadableFeature(major).ToString();

            Dictionary<string, string> mirrors = new Dictionary<string, string>();
            if (feature == "8")
            {
                mirrors["清华"] = "https://mirrors.tuna.tsinghua.edu.cn/Adoptium/8/jre/x64/windows/OpenJDK8U-jre_x64_windows_hotspot_8u412b08.zip";
                mirrors["华为"] = "https://repo.huaweicloud.com/java/jdk/8u202-b08/jre-8u202-windows-x64.zip";
            }
            else if (feature == "17")
            {
                mirrors["清华"] = "https://mirrors.tuna.tsinghua.edu.cn/Adoptium/17/jre/x64/windows/OpenJDK17U-jre_x64_windows_hotspot_17.0.13_11.zip";
                mirrors["华为"] = "https://repo.huaweicloud.com/openjdk/17.0.2/openjdk-17.0.2_windows-x64_bin.zip";
            }
            else
            {
                mirrors["清华"] = "https://mirrors.tuna.tsinghua.edu.cn/Adoptium/21/jre/x64/windows/OpenJDK21U-jre_x64_windows_hotspot_21.0.5_11.zip";
            }

            string targetDir = Path.Combine(AppInfo.JavaRoot, "jre" + feature);
            string marker = Path.Combine(targetDir, "bin", "java.exe");
            if (File.Exists(marker))
            {
                message = "本机已经有 Java " + feature + "：" + marker;
                return true;
            }

            string zipPath = Path.Combine(AppInfo.JavaRoot, "download_jre" + feature + ".zip");

            foreach (KeyValuePair<string, string> kv in mirrors)
            {
                if (progress != null) progress(1, "从" + kv.Key + "下载 Java " + feature + " …");
                string downloadError;
                bool ok = Http.DownloadFile(kv.Value, zipPath, delegate (long total, long done)
                {
                    if (progress != null && total > 0)
                        progress(5 + (int)(done * 85 / total), kv.Key + "下载中… " + done / 1024 / 1024 + "/" + total / 1024 / 1024 + " MB");
                }, out downloadError);

                if (ok)
                {
                    if (progress != null) progress(92, "解压中…");
                    try
                    {
                        Zip.Extract(zipPath, AppInfo.JavaRoot, delegate (int p, string name)
                        {
                            if (progress != null) progress(92 + p / 10, name);
                        });

                        string topDir = FindTopDir(AppInfo.JavaRoot, feature);
                        if (topDir != null && !string.Equals(Path.GetFullPath(topDir), Path.GetFullPath(targetDir), StringComparison.OrdinalIgnoreCase))
                        {
                            try { if (Directory.Exists(targetDir)) Directory.Delete(targetDir, true); } catch { }
                            Directory.Move(topDir, targetDir);
                        }

                        if (File.Exists(zipPath)) { try { File.Delete(zipPath); } catch { } }

                        message = "Java " + feature + " 已下载并解压到：" + targetDir;
                        return true;
                    }
                    catch (Exception ex)
                    {
                        error = "解压失败：" + ex.Message;
                        Log.Warn(error);
                    }
                }
                else
                {
                    error = kv.Key + "镜像下载失败：" + downloadError;
                    Log.Warn(error);
                }
            }

            error = "所有镜像都下载失败：" + error;
            return false;
        }

        private static string FindTopDir(string root, string feature)
        {
            foreach (string dir in Directory.GetDirectories(root))
            {
                string name = Path.GetFileName(dir).ToLowerInvariant();
                if (name.Contains("jre") && name.Contains("jdk")) return dir;
                if (name.Contains("jre")) return dir;
                if (name.StartsWith("openjdk" + feature) || name.StartsWith("jdk" + feature)) return dir;
            }
            return null;
        }

        private static int ParseMajor(string versionId)
        {
            try
            {
                string trimmed = versionId.Trim();
                int dash = trimmed.IndexOf('-');
                if (dash > 0) trimmed = trimmed.Substring(0, dash);

                if (trimmed.StartsWith("1."))
                {
                    string[] parts = trimmed.Split('.');
                    if (parts.Length >= 2)
                    {
                        int n;
                        if (int.TryParse(parts[1], out n)) return n;
                    }
                }
                else
                {
                    int dot = trimmed.IndexOf('.');
                    if (dot > 0) trimmed = trimmed.Substring(0, dot);
                    int n;
                    if (int.TryParse(trimmed, out n) && n >= 2 && n <= 100) return n;
                }
            }
            catch { }
            return 0;
        }

        private static int ParseMajorFromComponent(string component)
        {
            try
            {
                if (string.IsNullOrEmpty(component)) return 0;
                string s = component.ToLowerInvariant().Replace("jre", "").Replace("jdk", "").Replace("-", "");
                int n;
                if (int.TryParse(s, out n)) return n;
            }
            catch { }
            return 0;
        }
    }
}
