using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace PeterLauncher.Core
{
    /// <summary>启动参数集合。</summary>
    public class LaunchOptions
    {
        public string JavaPath = "";
        public int MinMemoryMb = 1024;
        public int MaxMemoryMb = 2048;
        public string GameDir = "";
        public string VersionId = AppInfo.GameVersionDefault;
        public string PlayerName = "Player";
        public string Uuid = "";
        public string AccessToken = "0";
        public bool MicrosoftAccount = false;
        public Action<string> Status;
    }

    /// <summary>
    /// 版本清单，已经处理过 inheritsFrom 继承（Forge / Fabric / OptiFine 都用这个字段指向原版）。
    /// </summary>
    public class VersionMeta
    {
        public string VersionId = "";
        public string ParentId = "";
        public string MainClass = "";
        public string AssetIndex = "legacy";
        public string JarPath = "";
        public string MinecraftArguments = "";
        public bool HasArguments = false;
        /// <summary>版本清单里自己声明要求的 Java 主版本，0 = 没声明。</summary>
        public int JavaMajor = 0;
        public List<object> Libraries = new List<object>();
        public List<object> JvmArguments = new List<object>();
        public List<object> GameArguments = new List<object>();

        public static VersionMeta Load(string gameDir, string versionId)
        {
            return LoadInternal(gameDir, versionId, 0);
        }

        private static VersionMeta LoadInternal(string gameDir, string versionId, int depth)
        {
            if (depth > 6) throw new Exception("inheritsFrom 继承层数过深：" + versionId);

            string dir = Path.Combine(gameDir, "versions", versionId);
            string jsonPath = Path.Combine(dir, versionId + ".json");
            if (!File.Exists(jsonPath)) throw new Exception("找不到版本清单：" + jsonPath);

            object root = Json.ParseFile(jsonPath);
            string parentId = Json.Str(root, "inheritsFrom");

            VersionMeta meta;
            if (parentId.Length > 0)
            {
                meta = LoadInternal(gameDir, parentId, depth + 1);
                meta.ParentId = parentId;
            }
            else
            {
                meta = new VersionMeta();
            }
            meta.VersionId = versionId;

            string mainClass = Json.Str(root, "mainClass");
            if (mainClass.Length > 0) meta.MainClass = mainClass;

            object assetIndexNode = Json.Get(root, "assetIndex");
            if (assetIndexNode != null)
            {
                string indexId = Json.Str(assetIndexNode, "id");
                if (indexId.Length > 0) meta.AssetIndex = indexId;
            }
            else
            {
                string legacyIndex = Json.Str(root, "assets");
                if (legacyIndex.Length > 0) meta.AssetIndex = legacyIndex;
            }

            int javaMajor = Json.Int(Json.Get(root, "javaVersion"), "majorVersion", 0);
            if (javaMajor > 0) meta.JavaMajor = javaMajor;

            object argumentsNode = Json.Get(root, "arguments");
            if (argumentsNode != null)
            {
                meta.HasArguments = true;
                foreach (object entry in Json.Array(Json.Get(argumentsNode, "jvm"))) meta.JvmArguments.Add(entry);
                foreach (object entry in Json.Array(Json.Get(argumentsNode, "game"))) meta.GameArguments.Add(entry);
            }

            string legacyArguments = Json.Str(root, "minecraftArguments");
            if (legacyArguments.Length > 0 && argumentsNode == null)
            {
                // 老格式（1.12 及更早、部分 Forge）用一整串参数，覆盖父版本的 arguments
                meta.MinecraftArguments = legacyArguments;
                meta.HasArguments = false;
                meta.JvmArguments.Clear();
                meta.GameArguments.Clear();
            }

            List<object> ownLibraries = Json.Array(Json.Get(root, "libraries"));
            if (ownLibraries.Count > 0)
            {
                List<object> merged = new List<object>(meta.Libraries);
                merged.AddRange(ownLibraries);
                meta.Libraries = merged;
            }

            // 主程序 jar：优先自己的，其次 "jar" 字段指定的，最后用父版本继承来的
            string jarField = Json.Str(root, "jar");
            string ownJar = Path.Combine(dir, versionId + ".jar");
            if (File.Exists(ownJar)) meta.JarPath = ownJar;
            else if (jarField.Length > 0)
            {
                string fromField = Path.Combine(gameDir, "versions", jarField, jarField + ".jar");
                if (File.Exists(fromField)) meta.JarPath = fromField;
            }
            return meta;
        }
    }

    /// <summary>
    /// 用版本 JSON 拼出完整启动命令。除本地文件外不访问网络。
    /// </summary>
    public static class GameLauncher
    {
        /// <summary>列出 .minecraft\versions 下所有带版本清单的目录（jar 可以由父版本继承，所以不强制要求）。</summary>
        public static string[] ListVersions(string gameDir)
        {
            List<string> list = new List<string>();
            string versionsDir = Path.Combine(gameDir, "versions");
            if (!Directory.Exists(versionsDir)) return list.ToArray();
            foreach (string dir in Directory.GetDirectories(versionsDir))
            {
                string id = Path.GetFileName(dir);
                string json = Path.Combine(dir, id + ".json");
                if (File.Exists(json)) list.Add(id);
            }
            list.Sort(StringComparer.OrdinalIgnoreCase);
            return list.ToArray();
        }

        /// <summary>启动前的本地文件自检，返回问题列表（空数组表示没问题）。</summary>
        public static List<string> CheckFiles(string gameDir, string versionId)
        {
            List<string> problems = new List<string>();
            string versionDir = Path.Combine(gameDir, "versions", versionId);
            if (!Directory.Exists(versionDir))
            {
                problems.Add("找不到版本目录：" + versionDir);
            }
            else
            {
                if (!File.Exists(Path.Combine(versionDir, versionId + ".json")))
                    problems.Add("缺少版本清单：" + versionId + ".json");
                else
                {
                    try
                    {
                        VersionMeta meta = VersionMeta.Load(gameDir, versionId);
                        if (meta.JarPath.Length == 0)
                            problems.Add("缺少主程序：" + versionId + ".jar（父版本里也没有）");
                    }
                    catch (Exception ex)
                    {
                        problems.Add("版本清单读不懂：" + ex.Message);
                    }
                }
            }
            if (!Directory.Exists(Path.Combine(gameDir, "libraries")))
                problems.Add("缺少 libraries 依赖库目录");
            if (!Directory.Exists(Path.Combine(gameDir, "assets")))
                problems.Add("缺少 assets 资源目录");
            return problems;
        }

        /// <summary>在线账户用随机 UUID，离线账户用固定的 MD5 派生 UUID（与 Java 的 UUID.nameUUIDFromBytes 一致）。</summary>
        public static string OfflineUuid(string playerName)
        {
            byte[] hash;
            using (MD5 md5 = MD5.Create())
                hash = md5.ComputeHash(Encoding.UTF8.GetBytes("OfflinePlayer:" + playerName));
            hash[6] = (byte)((hash[6] & 0x0F) | 0x30);
            hash[8] = (byte)((hash[8] & 0x3F) | 0x80);
            StringBuilder sb = new StringBuilder(32);
            for (int i = 0; i < 16; i++) sb.Append(hash[i].ToString("x2"));
            return sb.ToString();
        }

        /// <summary>真正启动游戏，返回进程对象；logPath 是重定向的游戏日志。</summary>
        public static Process Launch(LaunchOptions options, out string logPath)
        {
            string gameDir = Path.GetFullPath(options.GameDir);

            if (!File.Exists(options.JavaPath))
                throw new Exception("找不到 Java：" + options.JavaPath);

            Report(options.Status, "读取版本清单 " + options.VersionId + " …");
            VersionMeta meta = VersionMeta.Load(gameDir, options.VersionId);
            if (meta.JarPath.Length == 0)
                throw new Exception("找不到版本主程序：" + options.VersionId + ".jar，也没有找到可继承的父版本");
            string mainClass = meta.MainClass;
            string assetIndex = meta.AssetIndex;
            string versionJar = meta.JarPath;
            if (meta.ParentId.Length > 0)
                Report(options.Status, "这个版本继承自 " + meta.ParentId + "，已合并两份清单");

            bool is64 = Environment.Is64BitOperatingSystem;
            Dictionary<string, bool> features = new Dictionary<string, bool>();
            features["is_demo_user"] = false;
            features["has_custom_resolution"] = false;

            // ---- 1. 依赖库与 classpath ----
            Report(options.Status, "解析依赖库 …");
            List<string> classpath = new List<string>();
            List<string> nativeJars = new List<string>();
            List<object> libraries = meta.Libraries;
            foreach (object library in libraries)
            {
                if (!RulesAllow(Json.Array(Json.Get(library, "rules")), features, is64)) continue;

                object downloads = Json.Get(library, "downloads");
                object artifact = Json.Get(downloads, "artifact");
                if (artifact != null)
                {
                    string relative = Json.Str(artifact, "path");
                    if (relative.Length > 0)
                    {
                        string jar = Path.Combine(gameDir, "libraries", relative.Replace('/', Path.DirectorySeparatorChar));
                        if (File.Exists(jar)) classpath.Add(jar);
                        else Log.Warn("缺少依赖库：" + jar);
                    }
                }

                object natives = Json.Get(library, "natives");
                if (natives != null)
                {
                    string classifier = Json.Str(natives, "windows");
                    if (classifier.Length > 0)
                    {
                        object classifiers = Json.Get(downloads, "classifiers");
                        object classifierNode = Json.Get(classifiers, classifier);
                        if (classifierNode != null)
                        {
                            string relative = Json.Str(classifierNode, "path");
                            if (relative.Length > 0)
                            {
                                string jar = Path.Combine(gameDir, "libraries", relative.Replace('/', Path.DirectorySeparatorChar));
                                if (File.Exists(jar)) nativeJars.Add(jar);
                                else Log.Warn("缺少本地库：" + jar);
                            }
                        }
                    }
                }
            }
            classpath.Add(versionJar);

            // ---- 2. 解压 natives ----
            string nativesDir = Path.Combine(gameDir, "natives");
            Report(options.Status, "准备本地库（natives）…");
            ExtractNatives(nativeJars, nativesDir);

            // ---- 3. 变量表 ----
            string assetsDir = Path.Combine(gameDir, "assets");
            if (!File.Exists(Path.Combine(assetsDir, "indexes", assetIndex + ".json")))
                Log.Warn("缺少资源索引：" + Path.Combine(assetsDir, "indexes", assetIndex + ".json"));

            Dictionary<string, string> variables = new Dictionary<string, string>();
            variables["auth_player_name"] = options.MicrosoftAccount || options.PlayerName.Length > 0 ? options.PlayerName : "Player";
            variables["version_name"] = options.VersionId;
            variables["game_directory"] = gameDir;
            variables["assets_root"] = assetsDir;
            variables["assets_index_name"] = assetIndex;
            variables["auth_uuid"] = options.MicrosoftAccount
                ? options.Uuid.Replace("-", "")
                : OfflineUuid(options.PlayerName);
            variables["auth_access_token"] = options.AccessToken.Length > 0 ? options.AccessToken : "0";
            variables["auth_session"] = variables["auth_access_token"];
            variables["user_type"] = options.MicrosoftAccount ? "msa" : "legacy";
            variables["version_type"] = "release";
            variables["natives_directory"] = nativesDir;
            variables["classpath"] = string.Join(";", classpath.ToArray());
            variables["classpath_separator"] = ";";
            variables["launcher_name"] = "PeterMinecraftLauncher";
            variables["launcher_version"] = AppInfo.Version;
            variables["resolution_width"] = "854";
            variables["resolution_height"] = "480";
            variables["user_properties"] = "{}";

            // ---- 4. JVM 参数 ----
            List<string> arguments = new List<string>();
            arguments.Add("-Xms" + options.MinMemoryMb + "M");
            arguments.Add("-Xmx" + options.MaxMemoryMb + "M");
            arguments.Add("-Dfile.encoding=UTF-8");

            if (meta.HasArguments)
            {
                foreach (object entry in meta.JvmArguments)
                    AppendArgument(arguments, entry, variables, features, is64);
                if (!ContainsClasspathOption(arguments))
                {
                    arguments.Add("-cp");
                    arguments.Add(variables["classpath"]);
                }
            }
            else
            {
                arguments.Add("-Djava.library.path=" + nativesDir);
                arguments.Add("-cp");
                arguments.Add(variables["classpath"]);
            }

            arguments.Add(mainClass);

            // ---- 5. 游戏参数 ----
            if (meta.HasArguments)
            {
                foreach (object entry in meta.GameArguments)
                    AppendArgument(arguments, entry, variables, features, is64);
            }
            else
            {
                foreach (string part in meta.MinecraftArguments.Split(' '))
                {
                    if (part.Length == 0) continue;
                    arguments.Add(ReplaceVariables(part, variables));
                }
            }

            // ---- 6. 启动 ----
            string logDir = IsDotMinecraft(gameDir) ? Path.GetDirectoryName(gameDir) : gameDir;
            if (logDir == null || !Directory.Exists(logDir)) logDir = gameDir;
            logPath = Path.Combine(logDir, "mc_log.txt");

            Report(options.Status, "启动游戏进程 …");
            Log.Info("启动 Java：" + options.JavaPath);
            Log.Info("启动参数：" + string.Join(" ", arguments.ToArray()));

            ProcessStartInfo psi = new ProcessStartInfo(options.JavaPath);
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.WorkingDirectory = gameDir;
            psi.RedirectStandardError = true;
            psi.RedirectStandardOutput = true;
            foreach (string arg in arguments)
            {
                if (arg == null || arg.Length == 0) continue;
                // ProcessStartInfo 在 UseShellExecute=false 时按 MSDN 规则自行加引号，这里直接拼字符串更可控
                if (psi.Arguments.Length == 0) psi.Arguments = Quote(arg);
                else psi.Arguments += " " + Quote(arg);
            }

            Process process = Process.Start(psi);
            // 输出异步落到 mc_log.txt，避免缓冲区塞满导致游戏卡住
            StreamWriter writer = new StreamWriter(new FileStream(logPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite), new UTF8Encoding(true));
            StartPump(process.StandardOutput, writer);
            StartPump(process.StandardError, writer);
            return process;
        }

        private static void StartPump(StreamReader reader, StreamWriter writer)
        {
            System.Threading.Thread thread = new System.Threading.Thread(delegate ()
            {
                try
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        lock (writer) { writer.WriteLine(line); writer.Flush(); }
                    }
                }
                catch { }
            });
            thread.IsBackground = true;
            thread.Start();
        }

        private static string Quote(string value)
        {
            if (value.Length == 0) return "\"\"";
            if (value.IndexOf(' ') >= 0 || value.IndexOf('\t') >= 0)
                return "\"" + value.Replace("\"", "\\\"") + "\"";
            return value;
        }

        private static bool IsDotMinecraft(string gameDir)
        {
            string name = Path.GetFileName(gameDir.TrimEnd('\\'));
            return name != null && name.Equals(".minecraft", StringComparison.OrdinalIgnoreCase);
        }

        private static void Report(Action<string> status, string message)
        {
            if (status != null) status(message);
        }

        private static bool ContainsClasspathOption(List<string> args)
        {
            foreach (string arg in args)
                if (arg == "-cp" || arg == "-classpath") return true;
            return false;
        }

        /// <summary>处理 arguments 数组里「字符串 或 {rules,value} 对象」两种形态。</summary>
        private static void AppendArgument(List<string> output, object entry,
                                           Dictionary<string, string> variables,
                                           Dictionary<string, bool> features, bool is64)
        {
            if (entry is string)
            {
                string text = ReplaceVariables((string)entry, variables);
                if (text.Length > 0) output.Add(text);
                return;
            }
            if (!(entry is Dictionary<string, object>)) return;
            if (!RulesAllow(Json.Array(Json.Get(entry, "rules")), features, is64)) return;
            object value = Json.Get(entry, "value");
            if (value is string)
            {
                string text = ReplaceVariables((string)value, variables);
                if (text.Length > 0) output.Add(text);
                return;
            }
            foreach (object item in Json.Array(value))
            {
                string text = ReplaceVariables(item is string ? (string)item : string.Empty, variables);
                if (text.Length > 0) output.Add(text);
            }
        }

        private static string ReplaceVariables(string text, Dictionary<string, string> variables)
        {
            if (text == null) return "";
            foreach (KeyValuePair<string, string> kv in variables)
                text = text.Replace("${" + kv.Key + "}", kv.Value);
            return text;
        }

        private static bool RulesAllow(List<object> rules, Dictionary<string, bool> features, bool is64)
        {
            if (rules.Count == 0) return true;
            bool allowed = false;
            foreach (object rule in rules)
            {
                if (!RuleMatches(rule, features, is64)) continue;
                string action = Json.Str(rule, "action");
                allowed = action == "allow";
            }
            return allowed;
        }

        private static bool RuleMatches(object rule, Dictionary<string, bool> features, bool is64)
        {
            object os = Json.Get(rule, "os");
            if (os != null)
            {
                string name = Json.Str(os, "name");
                if (name.Length > 0 && name != "windows") return false;
                string arch = Json.Str(os, "arch");
                if (arch.Length > 0)
                {
                    if (arch == "x86" && is64) return false;
                    if ((arch == "x86_64" || arch == "amd64") && !is64) return false;
                }
            }
            object featureNode = Json.Get(rule, "features");
            if (featureNode != null)
            {
                foreach (KeyValuePair<string, object> kv in Json.Map(featureNode))
                {
                    bool required = kv.Value is bool ? (bool)kv.Value : false;
                    bool actual = features.ContainsKey(kv.Key) ? features[kv.Key] : false;
                    if (required != actual) return false;
                }
            }
            return true;
        }

        /// <summary>把 natives 压缩包里的 dll 解到 .minecraft\\natives（先清空旧文件）。</summary>
        private static void ExtractNatives(List<string> jars, string nativesDir)
        {
            if (!Directory.Exists(nativesDir)) Directory.CreateDirectory(nativesDir);
            foreach (string file in Directory.GetFiles(nativesDir)) File.Delete(file);

            foreach (string jar in jars)
            {
                try
                {
                    Zip.Extract(jar, nativesDir, delegate (string name)
                    {
                        return !name.ToUpperInvariant().StartsWith("META-INF/");
                    }, null);
                }
                catch (Exception ex)
                {
                    Log.Warn("解压本地库失败（" + jar + "）：" + ex.Message);
                }
            }
        }
    }
}
