using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace PeterLauncher.Core
{
    public class LaunchOptions
    {
        public string JavaPath = "";
        public string GameDir = "";
        public string VersionId = "";
        public string PlayerName = "Player";
        public string Uuid = "";
        public string AccessToken = "0";
        public int MinMemoryMb = 1024;
        public int MaxMemoryMb = 2048;
        public bool MicrosoftAccount = false;
        public Action<string> Status = delegate { };
    }

    public static class GameLauncher
    {
        private static readonly string[] ModulePatterns = new string[] { "client", "server" };

        public static string[] ListVersions(string gameDir)
        {
            try
            {
                string versionsDir = Path.Combine(gameDir, "versions");
                if (!Directory.Exists(versionsDir)) return new string[0];
                List<string> list = new List<string>();
                foreach (string dir in Directory.GetDirectories(versionsDir))
                {
                    string name = Path.GetFileName(dir);
                    string json = Path.Combine(dir, name + ".json");
                    if (File.Exists(json) || File.Exists(Path.Combine(dir, name + ".jar")))
                        list.Add(name);
                }
                list.Sort();
                return list.ToArray();
            }
            catch (Exception ex)
            {
                Log.Warn("列出版本失败：" + ex.Message);
                return new string[0];
            }
        }

        public static List<string> CheckFiles(string gameDir, string versionId)
        {
            List<string> problems = new List<string>();
            if (string.IsNullOrEmpty(gameDir)) { problems.Add("游戏目录为空"); return problems; }
            if (string.IsNullOrEmpty(versionId)) { problems.Add("没有选择版本"); return problems; }

            string versionDir = Path.Combine(gameDir, "versions", versionId);
            if (!Directory.Exists(versionDir)) { problems.Add("版本目录不存在：" + versionDir); return problems; }

            string jar = Path.Combine(versionDir, versionId + ".jar");
            if (!File.Exists(jar)) problems.Add("缺少客户端 jar：" + jar);

            string json = Path.Combine(versionDir, versionId + ".json");
            if (!File.Exists(json)) problems.Add("缺少版本 json：" + json);

            if (!Directory.Exists(Path.Combine(gameDir, "libraries")))
                problems.Add("缺少 libraries 目录（依赖库未下载）");
            return problems;
        }

        public static string OfflineUuid(string playerName)
        {
            try
            {
                string data = "OfflinePlayer:" + playerName;
                using (System.Security.Cryptography.MD5 md5 = System.Security.Cryptography.MD5.Create())
                {
                    byte[] hash = md5.ComputeHash(Encoding.UTF8.GetBytes(data));
                    hash[6] = (byte)((hash[6] & 0x0f) | 0x30);
                    hash[8] = (byte)((hash[8] & 0x3f) | 0x80);
                    return UuidFromBytes(hash);
                }
            }
            catch (Exception ex)
            {
                Log.Warn("生成离线 UUID 失败：" + ex.Message);
                return "00000000-0000-0000-0000-000000000000";
            }
        }

        public static System.Diagnostics.Process Launch(LaunchOptions options, out string logPath)
        {
            logPath = Path.Combine(options.GameDir, "mc_log.txt");

            string versionDir = Path.Combine(options.GameDir, "versions", options.VersionId);
            string versionJson = Path.Combine(versionDir, options.VersionId + ".json");
            string mainClass = "net.minecraft.client.main.Main";
            string assetIndex = "";
            List<string> gameArgs = new List<string>();

            if (File.Exists(versionJson))
            {
                try
                {
                    string jsonText = TextFile.ReadAllText(versionJson);
                    object root = Json.Parse(jsonText);
                    mainClass = Json.Str(root, "mainClass", mainClass);

                    object assets = Json.Get(root, "assets");
                    if (assets is string) assetIndex = (string)assets;

                    object args = Json.Get(root, "arguments");
                    object gameBlock = Json.Get(args, "game");
                    if (gameBlock is List<object>)
                    {
                        foreach (object entry in (List<object>)gameBlock)
                        {
                            if (entry is string) gameArgs.Add((string)entry);
                        }
                    }
                    else
                    {
                        string legacy = Json.Str(root, "minecraftArguments", "");
                        if (legacy.Length > 0) gameArgs.AddRange(legacy.Split(' '));
                    }
                }
                catch (Exception ex)
                {
                    Log.Warn("解析版本 json 失败，使用默认参数：" + ex.Message);
                }
            }

            StringBuilder cp = new StringBuilder();
            AppendClassPath(cp, options.GameDir, versionDir, options.VersionId);

            List<string> arguments = new List<string>();
            arguments.Add("-Xms" + options.MinMemoryMb + "M");
            arguments.Add("-Xmx" + options.MaxMemoryMb + "M");
            arguments.Add("-Dminecraft.launcher.version=" + options.VersionId);
            arguments.Add("-Djava.library.path=" + Quote(Path.Combine(options.GameDir, "libraries")));
            arguments.Add("-cp");
            arguments.Add(Quote(cp.ToString()));
            arguments.Add(mainClass);

            ReplaceArgs(gameArgs, options, assetIndex);
            arguments.AddRange(gameArgs);

            string command = string.Join(" ", arguments.ToArray());
            Log.Info("启动命令：" + options.JavaPath + " " + command);

            System.Diagnostics.ProcessStartInfo psi = new System.Diagnostics.ProcessStartInfo();
            psi.FileName = options.JavaPath;
            psi.Arguments = command;
            psi.WorkingDirectory = options.GameDir;
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;

            System.Diagnostics.Process process = new System.Diagnostics.Process();
            process.StartInfo = psi;

            System.IO.StreamWriter logWriter = null;
            try
            {
                logWriter = new System.IO.StreamWriter(logPath, false, Encoding.UTF8);
            }
            catch { }

            process.OutputDataReceived += delegate (object sender, System.Diagnostics.DataReceivedEventArgs e)
            {
                if (e.Data == null) return;
                try { if (logWriter != null) logWriter.WriteLine(e.Data); } catch { }
            };
            process.ErrorDataReceived += delegate (object sender, System.Diagnostics.DataReceivedEventArgs e)
            {
                if (e.Data == null) return;
                try { if (logWriter != null) logWriter.WriteLine("[ERR] " + e.Data); } catch { }
            };
            process.EnableRaisingEvents = true;

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            process.Exited += delegate
            {
                try
                {
                    if (logWriter != null) { logWriter.Flush(); logWriter.Dispose(); }
                }
                catch { }
            };

            return process;
        }

        private static void AppendClassPath(StringBuilder cp, string gameDir, string versionDir, string versionId)
        {
            string libRoot = Path.Combine(gameDir, "libraries");
            if (Directory.Exists(libRoot))
            {
                foreach (string jar in Directory.GetFiles(libRoot, "*.jar", SearchOption.AllDirectories))
                {
                    if (jar.IndexOf("\\natives-", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                    AppendPath(cp, jar);
                }
            }
            AppendPath(cp, Path.Combine(versionDir, versionId + ".jar"));
        }

        private static void AppendPath(StringBuilder cp, string path)
        {
            if (cp.Length > 0) cp.Append(Path.PathSeparator);
            cp.Append(path);
        }

        private static string Quote(string text)
        {
            return "\"" + text + "\"";
        }

        private static void ReplaceArgs(List<string> args, LaunchOptions o, string assetIndex)
        {
            string assetsDir = Path.Combine(o.GameDir, "assets");
            for (int i = 0; i < args.Count; i++)
            {
                args[i] = args[i]
                    .Replace("${auth_player_name}", o.PlayerName)
                    .Replace("${auth_uuid}", o.Uuid)
                    .Replace("${auth_access_token}", o.AccessToken)
                    .Replace("${auth_session}", o.AccessToken)
                    .Replace("${user_type}", o.MicrosoftAccount ? "msa" : "legacy")
                    .Replace("${version_name}", o.VersionId)
                    .Replace("${game_directory}", o.GameDir)
                    .Replace("${assets_root}", assetsDir)
                    .Replace("${game_assets}", assetsDir)
                    .Replace("${user_properties}", "{}")
                    .Replace("${clientid}", "PeterLauncher")
                    .Replace("${version_type}", o.VersionId);

                if (assetIndex.Length > 0 && args[i] == "--assetsDir")
                {
                    if (i + 1 < args.Count) args[i + 1] = assetsDir;
                }
            }
        }
    }
}
