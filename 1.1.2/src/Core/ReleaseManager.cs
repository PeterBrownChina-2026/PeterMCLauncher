using System;
using System.Collections.Generic;
using System.IO;

namespace PeterLauncher.Core
{
    /// <summary>
    /// 首次启动时自动"释放" .minecraft 目录。
    /// 释放来源按优先级：data\minecraft.zip → data\.minecraft → 启动器目录里的 Games\.minecraft；
    /// 一个都没有时只建空目录骨架（保证启动器不会因为目录缺失而报错）。
    /// 全部为本地文件操作，不联网下载。
    /// </summary>
    public static class ReleaseManager
    {
        private const string MarkerFile = ".pmcl_release";

        public static bool IsReleased(string gameDir)
        {
            return File.Exists(Path.Combine(gameDir, MarkerFile));
        }

        /// <summary>首次释放；已经释放过则直接返回 false（message 里说明）。</summary>
        public static bool EnsureMinecraft(string gameDir, Action<int, string> progress, out string message)
        {
            message = "";
            string marker = Path.Combine(gameDir, MarkerFile);
            if (File.Exists(marker) && Directory.Exists(Path.Combine(gameDir, "versions")))
            {
                message = ".minecraft 已经释放过了，跳过";
                return false;
            }
            return Release(gameDir, false, progress, out message);
        }

        /// <summary>强制重新释放/修复（覆盖缺失文件，不动已有存档）。</summary>
        public static bool Release(string gameDir, bool force, Action<int, string> progress, out string message)
        {
            message = "";
            try
            {
                Directory.CreateDirectory(gameDir);

                string zipSource = Path.Combine(AppInfo.DataRoot, "minecraft.zip");
                string folderSource = Path.Combine(AppInfo.DataRoot, ".minecraft");
                string kitSource = Path.Combine(AppInfo.GamesRoot, ".minecraft");

                if (File.Exists(zipSource))
                {
                    Log.Info("从压缩包释放 .minecraft：" + zipSource);
                    Zip.Extract(zipSource, gameDir, progress);
                    message = "已从 data\\minecraft.zip 释放 .minecraft";
                }
                else if (Directory.Exists(folderSource) && !SamePath(folderSource, gameDir))
                {
                    Log.Info("从 data\\.minecraft 复制：" + folderSource);
                    CopyFolder(folderSource, gameDir, progress);
                    message = "已从 data\\.minecraft 复制游戏目录";
                }
                else if (Directory.Exists(kitSource) && !SamePath(kitSource, gameDir))
                {
                    Log.Info("从 Games\\.minecraft 复制：" + kitSource);
                    CopyFolder(kitSource, gameDir, progress);
                    message = "已从 Games\\.minecraft 复制游戏目录";
                }
                else if (HasGameFiles(gameDir))
                {
                    message = "游戏目录已经存在，无需释放（只补写了标记文件）";
                }
                else
                {
                    CreateSkeleton(gameDir);
                    message = "没有找到可释放的游戏包，已创建空目录骨架";
                }

                TextFile.WriteAllText(Path.Combine(gameDir, MarkerFile),
                    "released=" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + Environment.NewLine
                    + "launcher=" + AppInfo.Version + Environment.NewLine, false);
                return true;
            }
            catch (Exception ex)
            {
                message = "释放 .minecraft 失败：" + ex.Message;
                Log.Error(message);
                return false;
            }
        }

        private static bool HasGameFiles(string gameDir)
        {
            string versions = Path.Combine(gameDir, "versions");
            if (!Directory.Exists(versions)) return false;
            foreach (string dir in Directory.GetDirectories(versions)) return true;
            return false;
        }

        private static bool SamePath(string a, string b)
        {
            try
            {
                return string.Equals(Path.GetFullPath(a).TrimEnd('\\'),
                                     Path.GetFullPath(b).TrimEnd('\\'),
                                     StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        private static void CopyFolder(string source, string target, Action<int, string> progress)
        {
            List<string> files = new List<string>();
            foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            {
                string relative = file.Substring(source.Length).TrimStart('\\');
                // 跳过启动器自己的标记文件
                if (relative.Equals(MarkerFile, StringComparison.OrdinalIgnoreCase)) continue;
                // 跳过官方启动器留下的浏览器缓存（几十 MB 垃圾，路径还特别长）
                if (relative.StartsWith("webcache", StringComparison.OrdinalIgnoreCase)) continue;
                files.Add(file);
            }

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
                    // 单个坏文件不拖垮整次释放：跳过并记账（U 盘上确实见过文件名损坏的条目）
                    skipped++;
                    Log.Warn("跳过无法复制的文件 " + relative + "：" + ex.Message);
                }
                if (progress != null && ((i + 1) % 20 == 0 || i + 1 == total))
                    progress((i + 1) * 100 / total, relative);
            }
            if (skipped > 0) Log.Warn("本次复制共跳过 " + skipped + " 个文件");
        }

        private static void CreateSkeleton(string gameDir)
        {
            string[] folders = new string[]
            {
                "saves", "resourcepacks", "screenshots", "logs",
                "versions", "libraries", "assets", "mods"
            };
            foreach (string folder in folders)
                Directory.CreateDirectory(Path.Combine(gameDir, folder));

            string options = Path.Combine(gameDir, "options.txt");
            if (!File.Exists(options))
            {
                TextFile.WriteAllText(options,
                    "version:1165" + Environment.NewLine
                    + "fullscreen:false" + Environment.NewLine
                    + "renderDistance:8" + Environment.NewLine
                    + "guiScale:2" + Environment.NewLine
                    + "lang:zh_cn" + Environment.NewLine, false);
            }

            string profiles = Path.Combine(gameDir, "launcher_profiles.json");
            if (!File.Exists(profiles))
            {
                TextFile.WriteAllText(profiles,
                    "{" + Environment.NewLine
                    + "  \"profiles\": {}," + Environment.NewLine
                    + "  \"settings\": { \"keepLauncherOpen\": true }," + Environment.NewLine
                    + "  \"version\": 3" + Environment.NewLine
                    + "}" + Environment.NewLine, false);
            }
        }
    }
}
