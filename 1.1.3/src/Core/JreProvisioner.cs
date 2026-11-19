using System;
using System.Collections.Generic;
using System.IO;

namespace PeterLauncher.Core
{
    /// <summary>
    /// 启动时检查便携 JRE 是否完整，缺失则从 data\jreX.zip 自动释放。
    /// 已存在的 JRE 直接跳过，不覆盖。
    /// </summary>
    public static class JreProvisioner
    {
        private static readonly int[] BundledVersions = new int[] { 8, 17, 21, 25 };

        /// <summary>检查所有内置 JRE，缺失的从 zip 释放。</summary>
        public static void EnsureBundled()
        {
            foreach (int v in BundledVersions)
            {
                string javaExe = Path.Combine(AppInfo.JreRoot, "jre" + v, "bin", "java.exe");
                if (File.Exists(javaExe)) continue;

                string zip = Path.Combine(AppInfo.DataRoot, "jre" + v + ".zip");
                if (!File.Exists(zip)) continue;

                string dest = Path.Combine(AppInfo.JreRoot, "jre" + v);
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
            }
        }
    }
}
