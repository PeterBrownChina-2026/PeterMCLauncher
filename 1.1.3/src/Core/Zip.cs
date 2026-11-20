using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace PeterLauncher.Core
{
    public static class Zip
    {
        public static void Extract(string zipPath, string targetDir, Action<int, string> progress)
        {
            Directory.CreateDirectory(targetDir);
            using (ZipArchive archive = ZipFile.OpenRead(zipPath))
            {
                int total = archive.Entries.Count;
                int done = 0;
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    done++;
                    string target = Path.Combine(targetDir, entry.FullName);
                    string dir = Path.GetDirectoryName(target);
                    if (dir != null && dir.Length > 0) Directory.CreateDirectory(dir);

                    if (!entry.FullName.EndsWith("/"))
                        entry.ExtractToFile(target, true);

                    if (progress != null && total > 0)
                        progress(done * 100 / total, entry.FullName);
                }
            }
        }
    }
}
