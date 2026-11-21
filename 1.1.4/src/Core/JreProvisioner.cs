using System;
using System.IO;
using System.Reflection;

namespace PeterLauncher.Core
{
    /// <summary>
    /// 启动时检查便携 JRE 是否完整，缺失则自动释放。
    /// 释放源优先级：data\jreX.zip（外部分发改法）→ 编译进 exe 的内置资源
    /// （PeterLauncher.Resources.jreX.zip，单文件分发）。
    /// 已存在的 JRE 直接跳过，不覆盖用户文件。
    /// </summary>
    public static class JreProvisioner
    {
        private static readonly int[] BundledVersions = new int[] { 8, 17, 21, 25 };

        /// <summary>检查所有内置 JRE，缺失的自动释放。</summary>
        public static void EnsureBundled()
        {
            foreach (int v in BundledVersions)
            {
                string javaExe = Path.Combine(AppInfo.JreRoot, "jre" + v, "bin", "java.exe");
                if (File.Exists(javaExe)) continue;

                string dest = Path.Combine(AppInfo.JreRoot, "jre" + v);

                // 优先：外部 data\jreX.zip（兼容 1.1.3 的分发方式）
                string zip = Path.Combine(AppInfo.DataRoot, "jre" + v + ".zip");
                if (File.Exists(zip))
                {
                    Log.Info("释放便携 JRE " + v + "：" + zip);
                    try
                    {
                        Zip.Extract(zip, dest, null);
                        Log.Info("JRE " + v + " 释放完成");
                    }
                    catch (Exception ex)
                    {
                        Log.Warn("JRE " + v + " 释放失败：" + ex.Message);
                    }
                    continue;
                }

                // 兜底：从 exe 内置资源释放（单文件分发，运行时自动解压）
                ExtractFromEmbedded(v, dest);
            }
        }

        /// <summary>把编译进 exe 的 jreX.zip 资源落到临时文件，再解压到 JRE\jreX。</summary>
        private static void ExtractFromEmbedded(int v, string dest)
        {
            string resName = "PeterLauncher.Resources.jre" + v + ".zip";
            Stream stream;
            try
            {
                stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resName);
            }
            catch (Exception ex)
            {
                Log.Warn("读取内置 JRE " + v + " 资源失败：" + ex.Message);
                return;
            }
            if (stream == null)
            {
                // 源码仓库里直接编译、没内置资源时走这里，静默跳过
                return;
            }

            string tmp = null;
            try
            {
                tmp = Path.Combine(Path.GetTempPath(), "pmcl_jre" + v + "_" + Guid.NewGuid().ToString("N") + ".zip");
                using (stream)
                using (FileStream fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    stream.CopyTo(fs);
                }
                Log.Info("从内置资源释放便携 JRE " + v);
                Zip.Extract(tmp, dest, null);
                Log.Info("JRE " + v + " 释放完成");
            }
            catch (Exception ex)
            {
                Log.Warn("JRE " + v + " 内置释放失败：" + ex.Message);
            }
            finally
            {
                if (tmp != null)
                {
                    try { File.Delete(tmp); } catch { }
                }
            }
        }
    }
}
