using System;
using System.Collections.Generic;
using System.IO;

namespace PeterLauncher.Core
{
    public static class ReleaseManager
    {
        public class ReleaseInfo
        {
            public string Version = "";
            public string Channel = "";
            public string FileName = "";
            public string Url = "";
            public DateTime Date = DateTime.MinValue;
            public string Notes = "";
        }

        public static List<ReleaseInfo> ListReleases(out string error)
        {
            error = "";
            List<ReleaseInfo> list = new List<ReleaseInfo>();
            try
            {
                string url = "https://api.github.com/repos/PeterBrownChina-2026/PeterMCLauncher/releases";
                Dictionary<string, string> headers = new Dictionary<string, string>();
                headers["User-Agent"] = "PeterMinecraftLauncher/" + AppInfo.Version;

                string text = Http.GetJson(url, headers);
                object root = Json.Parse(text);

                if (root is List<object>)
                {
                    foreach (object relObj in (List<object>)root)
                    {
                        ReleaseInfo info = new ReleaseInfo();
                        info.Version = Json.Str(relObj, "tag_name", "");
                        info.Channel = Json.Str(relObj, "prerelease", false) ? "beta" : "stable";
                        info.Notes = Json.Str(relObj, "body", "");

                        string published = Json.Str(relObj, "published_at", "");
                        DateTime.TryParse(published, out info.Date);

                        object assets = Json.Get(relObj, "assets");
                        if (assets is List<object>)
                        {
                            foreach (object asset in (List<object>)assets)
                            {
                                string name = Json.Str(asset, "name", "");
                                string browserUrl = Json.Str(asset, "browser_download_url", "");
                                if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                                {
                                    info.FileName = name;
                                    info.Url = browserUrl;
                                    break;
                                }
                            }
                        }

                        if (info.Version.Length > 0) list.Add(info);
                    }
                }
            }
            catch (HttpError ex)
            {
                error = ex.Message;
            }
            catch (Exception ex)
            {
                error = ex.Message;
            }
            return list;
        }

        public static bool IsNewer(string current, string remote)
        {
            try
            {
                string a = current.Trim().TrimStart('v');
                string b = remote.Trim().TrimStart('v');

                if (a == b) return false;

                int dashA = a.IndexOf('-');
                int dashB = b.IndexOf('-');
                if (dashA >= 0) a = a.Substring(0, dashA);
                if (dashB >= 0) b = b.Substring(0, dashB);

                string[] partsA = a.Split('.');
                string[] partsB = b.Split('.');
                int len = Math.Max(partsA.Length, partsB.Length);

                for (int i = 0; i < len; i++)
                {
                    int na = i < partsA.Length ? SafeInt(partsA[i]) : 0;
                    int nb = i < partsB.Length ? SafeInt(partsB[i]) : 0;
                    if (nb > na) return true;
                    if (nb < na) return false;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        private static int SafeInt(string s)
        {
            int n;
            int.TryParse(s, out n);
            return n;
        }
    }
}
