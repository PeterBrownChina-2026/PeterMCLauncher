using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace PeterLauncher.Core
{
    /// <summary>某个游戏版本对 Java 的要求。</summary>
    public class JavaRequirement
    {
        public int Major = 8;
        /// <summary>这个要求是怎么得出的（写进 UI 和日志，方便排查）。</summary>
        public string Basis = "";
        public bool Known = true;

        public string Text { get { return "Java " + Major; } }
    }

    /// <summary>
    /// 按游戏版本判断需要哪个 Java，并且负责从镜像站下载对应的 JRE。
    /// 只下载 Java 运行时，不下载任何游戏文件，也不联机。
    /// </summary>
    public static class JavaProvisioner
    {
        /// <summary>能自动下载到的主版本（Eclipse Temurin 的 JRE，x64 Windows）。</summary>
        private static readonly int[] Downloadable = new int[] { 8, 11, 17, 21, 25 };

        private const string MirrorRoot = "https://mirrors.tuna.tsinghua.edu.cn/Adoptium/";
        private const string AdoptiumApi = "https://api.adoptium.net/v3/binary/latest/{0}/ga/windows/x64/jre/hotspot/normal/eclipse";

        // ==================================================================
        // 需求判断
        // ==================================================================

        /// <summary>从 java -version 的输出文本里取主版本号（1.8.0_512 → 8，17.0.21 → 17）。</summary>
        public static int FeatureOf(string versionText)
        {
            if (versionText == null) return 0;
            Match match = Regex.Match(versionText, "([0-9]+)(?:\\.([0-9]+))?");
            if (!match.Success) return 0;
            int first = int.Parse(match.Groups[1].Value);
            if (first == 1 && match.Groups[2].Success)
                return int.Parse(match.Groups[2].Value);
            return first;
        }

        public static int FeatureOf(JavaInfo info)
        {
            return info == null ? 0 : FeatureOf(info.Version);
        }

        /// <summary>按 .minecraft 目录和版本号算出需要的 Java；优先读版本清单里的 javaVersion。</summary>
        public static JavaRequirement RequirementFor(string gameDir, string versionId)
        {
            JavaRequirement requirement = new JavaRequirement();
            if (gameDir != null && gameDir.Length > 0 && versionId != null && versionId.Length > 0)
            {
                try
                {
                    VersionMeta meta = VersionMeta.Load(gameDir, versionId);
                    if (meta.JavaMajor > 0)
                    {
                        requirement.Major = meta.JavaMajor;
                        requirement.Basis = "版本清单写明需要 Java " + meta.JavaMajor;
                        return requirement;
                    }
                }
                catch
                {
                    // 版本清单不存在 / 读不懂都很正常（没装这个版本），静默退回按版本号推断
                }
            }
            requirement.Major = GuessByVersionId(versionId);
            requirement.Basis = "按版本号推断";
            return requirement;
        }

        /// <summary>版本号 → Java 主版本的兜底映射（版本清单里没写 javaVersion 时才会走到）。</summary>
        public static int GuessByVersionId(string versionId)
        {
            if (versionId == null || versionId.Length == 0) return 8;
            Match mc = Regex.Match(versionId, "^1\\.([0-9]+)(?:\\.([0-9]+))?");
            if (!mc.Success)
            {
                // 快照（21w37a、24w14a 之类）以及 Forge 版本号：一律按新版处理
                return versionId.StartsWith("1.") ? 8 : 21;
            }
            int minor = int.Parse(mc.Groups[1].Value);
            int patch = mc.Groups[2].Success ? int.Parse(mc.Groups[2].Value) : 0;

            if (minor <= 16) return 8;                 // 1.16.5 及更早：Java 8
            if (minor == 17) return 17;                // 官方要 16，但 16 已经下架，17 实测能跑
            if (minor <= 20 && patch < 5) return 17;    // 1.18 ~ 1.20.4：Java 17
            return 21;                                 // 1.20.5 及以后：Java 21
        }

        /// <summary>这个 Java 能不能跑这个版本。</summary>
        public static bool Satisfies(JavaInfo info, JavaRequirement requirement)
        {
            if (info == null || info.Broken) return false;
            int feature = FeatureOf(info);
            if (feature <= 0) return false;
            if (requirement.Major <= 8) return feature == 8;   // 1.16.5 只能吃 Java 8，更高的反而崩
            return feature >= requirement.Major;                // 新版向上兼容
        }

        /// <summary>在已扫描到的 Java 里挑一个最合适的（优先正好等于要求的主版本）。</summary>
        public static JavaInfo Match(List<JavaInfo> list, JavaRequirement requirement)
        {
            if (list == null || list.Count == 0) return null;
            JavaInfo exact = null;
            JavaInfo higher = null;
            foreach (JavaInfo info in list)
            {
                if (!Satisfies(info, requirement)) continue;
                int feature = FeatureOf(info);
                if (feature == requirement.Major)
                {
                    if (exact == null || (!exact.Is64Bit && info.Is64Bit)) exact = info;
                }
                else if (higher == null || feature < FeatureOf(higher)) higher = info;
            }
            if (exact != null) return exact;
            return higher;
        }

        // ==================================================================
        // 下载
        // ==================================================================

        /// <summary>把想要的主版本换成镜像站里真实存在的版本（16 已下架 → 17）。</summary>
        public static int DownloadableFeature(int wanted)
        {
            if (wanted <= 8) return 8;
            foreach (int feature in Downloadable)
                if (feature >= wanted) return feature;
            return Downloadable[Downloadable.Length - 1];
        }

        /// <summary>去镜像站目录页找最新的 zip 文件名。</summary>
        public static bool ResolveDownloadUrl(int feature, out string url, out string zipName, out string error)
        {
            url = "";
            zipName = "";
            error = "";

            string listing = MirrorRoot + feature + "/jre/x64/windows/";
            try
            {
                string html = Http.GetJson(listing, null);
                MatchCollection matches = Regex.Matches(html, "href=\"([^\"]+\\.zip)\"");
                List<string> names = new List<string>();
                foreach (Match match in matches)
                {
                    string name = match.Groups[1].Value;
                    if (name.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                    {
                        int slash = name.LastIndexOf('/');
                        if (slash > 0) name = name.Substring(slash + 1);
                    }
                    if (name.ToLowerInvariant().Contains("jre")) names.Add(name);
                }
                if (names.Count > 0)
                {
                    zipName = Newest(names);
                    url = listing + zipName;
                    Log.Info("镜像站选的 Java " + feature + " 包：" + zipName);
                    return true;
                }
                Log.Warn("镜像站目录里没有 Java " + feature + " 的 JRE zip：" + listing);
            }
            catch (Exception ex)
            {
                Log.Warn("读取镜像站目录失败：" + ex.Message);
            }

            // 兜底：直接走 Adoptium 官方 API（会 302 到 GitHub，被墙时这里也会失败）
            string api = string.Format(AdoptiumApi, feature);
            try
            {
                Http.GetJson(api, null);
                url = api;
                zipName = "Temurin-jre" + feature + ".zip";
                Log.Info("镜像站不可用，改用 Adoptium 官方 API：" + api);
                return true;
            }
            catch (Exception ex)
            {
                error = "镜像站和官方 API 都拿不到 Java " + feature + " 的下载地址：" + ex.Message;
                Log.Warn(error);
                return false;
            }
        }

        private static string Newest(List<string> names)
        {
            string best = names[0];
            foreach (string candidate in names)
                if (CompareVersion(candidate, best) > 0) best = candidate;
            return best;
        }

        /// <summary>比较文件名里的版本号（17.0.9 与 17.0.21 要按数字比，不能按字符串比）。</summary>
        private static int CompareVersion(string left, string right)
        {
            List<int> a = Numbers(left);
            List<int> b = Numbers(right);
            int count = Math.Min(a.Count, b.Count);
            for (int i = 0; i < count; i++)
                if (a[i] != b[i]) return a[i] < b[i] ? -1 : 1;
            if (a.Count == b.Count) return 0;
            return a.Count < b.Count ? -1 : 1;
        }

        private static List<int> Numbers(string text)
        {
            List<int> list = new List<int>();
            foreach (Match match in Regex.Matches(text, "[0-9]+"))
            {
                try { list.Add(int.Parse(match.Value)); } catch { list.Add(0); }
            }
            return list;
        }

        /// <summary>
        /// 下载并安装指定主版本的 JRE 到 Java\jre{主版本}。progress(百分比, 说明)。
        /// </summary>
        public static bool Install(int wanted, Action<int, string> progress, out string error, out string message)
        {
            error = "";
            message = "";
            int feature = DownloadableFeature(wanted);
            string target = Path.Combine(AppInfo.JavaRoot, "jre" + feature);

            JavaInfo existing = JavaManager.Probe(Path.Combine(target, "bin", "java.exe"));
            if (existing != null)
            {
                message = "Java " + feature + " 已经就绪（" + existing.Version + "），不用重复下载";
                Report(progress, 100, message);
                return true;
            }

            string url, zipName;
            if (!ResolveDownloadUrl(feature, out url, out zipName, out error)) return false;
            Report(progress, 2, "连接下载地址 …");

            string cacheDir = Path.Combine(AppInfo.JavaRoot, "cache");
            string zipPath = Path.Combine(cacheDir, zipName);
            long lastReport = 0;
            bool ok = Http.DownloadFile(url, zipPath, delegate (long total, long done)
            {
                // 下载占 5% ~ 65%
                int percent = total > 0 ? 5 + (int)(done * 60 / total) : 30;
                long now = Environment.TickCount;
                if (now - lastReport < 200 && percent < 65) return;
                lastReport = now;
                string size = total > 0
                    ? Mb(done) + " / " + Mb(total)
                    : Mb(done);
                Report(progress, percent, "下载 " + zipName + "（" + size + "）");
            }, out error);

            if (!ok)
            {
                error = "下载 Java " + feature + " 失败：" + error;
                return false;
            }

            Report(progress, 68, "解压 " + zipName + " …");
            string temp = Path.Combine(AppInfo.JavaRoot, "tmp" + feature);
            try
            {
                if (Directory.Exists(temp)) Directory.Delete(temp, true);
                Directory.CreateDirectory(temp);
                Zip.Extract(zipPath, temp, delegate (int percent, string name)
                {
                    Report(progress, 68 + percent * 27 / 100, "解压 " + name);
                });

                string javaExe = FindJavaExe(temp);
                if (javaExe == null)
                {
                    error = "解压出来的包里没有找到 bin\\java.exe，压缩包可能不完整";
                    return false;
                }
                string root = Path.GetDirectoryName(Path.GetDirectoryName(javaExe));

                if (Directory.Exists(target)) Directory.Delete(target, true);
                if (string.Equals(root, temp, StringComparison.OrdinalIgnoreCase))
                {
                    Directory.Move(temp, target);
                    temp = "";
                }
                else
                {
                    Directory.Move(root, target);
                }

                JavaInfo info = JavaManager.Probe(Path.Combine(target, "bin", "java.exe"));
                if (info == null || info.Broken)
                {
                    error = "Java " + feature + " 解压完成但跑不起来（可能是系统缺少运行库，或者杀毒软件拦了）";
                    return false;
                }

                if (!info.Is64Bit)
                    Log.Warn("下载到的 Java " + feature + " 不是 64 位，可能启动时分配不到大内存");

                message = "已安装 Java " + feature + "：" + info.Version + "（" + info.BitText + "）";
                Log.Info(message);
                Report(progress, 100, message);
                return true;
            }
            catch (Exception ex)
            {
                error = "安装 Java " + feature + " 失败：" + ex.Message;
                Log.Error(error);
                return false;
            }
            finally
            {
                try { if (temp.Length > 0 && Directory.Exists(temp)) Directory.Delete(temp, true); } catch { }
                try { if (File.Exists(zipPath)) File.Delete(zipPath); } catch { }
            }
        }

        private static string FindJavaExe(string root)
        {
            try
            {
                foreach (string file in Directory.GetFiles(root, "java.exe", SearchOption.AllDirectories))
                {
                    string dir = Path.GetDirectoryName(file);
                    if (dir != null && Path.GetFileName(dir).Equals("bin", StringComparison.OrdinalIgnoreCase))
                        return file;
                }
            }
            catch { }
            return null;
        }

        private static void Report(Action<int, string> progress, int percent, string text)
        {
            if (progress != null) progress(percent, text);
        }

        private static string Mb(long bytes)
        {
            if (bytes <= 0) return "0 MB";
            return (bytes / 1048576.0).ToString("0.0") + " MB";
        }
    }
}
