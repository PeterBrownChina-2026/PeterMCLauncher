using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;

namespace PeterLauncher.Core
{
    /// <summary>HTTP 请求失败（带状态码与响应体，方便解析 XSTS 的 XErr）。</summary>
    public class HttpError : Exception
    {
        public int StatusCode;
        public string Body;

        public HttpError(int statusCode, string body, string message) : base(message)
        {
            StatusCode = statusCode;
            Body = body;
        }
    }

    /// <summary>极简 HTTP 客户端：只做本启动器需要的 POST/GET，不引入第三方库。</summary>
    public static class Http
    {
        private static bool tlsReady = false;

        public static void EnsureTls()
        {
            if (tlsReady) return;
            tlsReady = true;
            try
            {
                int protocols = 768 | 3072; // TLS1.0 | TLS1.2
                try { protocols |= 12288; } catch { } // TLS1.3（老系统上可能不存在）
                ServicePointManager.SecurityProtocol = (SecurityProtocolType)protocols;
            }
            catch (Exception ex) { Log.Warn("设置 TLS 协议失败：" + ex.Message); }
        }

        public static string PostJson(string url, string json, Dictionary<string, string> headers)
        {
            return Send("POST", url, json, "application/json", headers);
        }

        public static string PostForm(string url, string form, Dictionary<string, string> headers)
        {
            return Send("POST", url, form, "application/x-www-form-urlencoded", headers);
        }

        public static string GetJson(string url, Dictionary<string, string> headers)
        {
            return Send("GET", url, null, null, headers);
        }

        private static string Send(string method, string url, string body, string contentType, Dictionary<string, string> headers)
        {
            EnsureTls();
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = method;
            request.Timeout = 30000;
            request.ReadWriteTimeout = 40000;
            request.KeepAlive = false;
            request.UserAgent = "PeterMinecraftLauncher/" + AppInfo.Version;
            request.Accept = "application/json";
            if (contentType != null) request.ContentType = contentType;

            if (headers != null)
            {
                foreach (KeyValuePair<string, string> kv in headers)
                {
                    if (string.Equals(kv.Key, "Authorization", StringComparison.OrdinalIgnoreCase))
                        request.Headers["Authorization"] = kv.Value;
                    else
                        request.Headers[kv.Key] = kv.Value;
                }
            }

            if (body != null)
            {
                byte[] data = Encoding.UTF8.GetBytes(body);
                request.ContentLength = data.Length;
                using (Stream stream = request.GetRequestStream())
                    stream.Write(data, 0, data.Length);
            }

            try
            {
                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                    return ReadBody(response);
            }
            catch (WebException ex)
            {
                int status = 0;
                string text = "";
                if (ex.Response is HttpWebResponse)
                {
                    HttpWebResponse resp = (HttpWebResponse)ex.Response;
                    status = (int)resp.StatusCode;
                    try { text = ReadBody(resp); } catch { }
                    resp.Close();
                }
                if (status == 0) throw new Exception("网络请求失败（" + ex.Message + "）：" + url);
                throw new HttpError(status, text, "HTTP " + status + "：" + Compact(text));
            }
        }

        private static string ReadBody(WebResponse response)
        {
            using (Stream stream = response.GetResponseStream())
            using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
                return reader.ReadToEnd();
        }

        private static string Compact(string text)
        {
            if (string.IsNullOrEmpty(text)) return "(空响应)";
            string flat = text.Replace("\r", " ").Replace("\n", " ").Replace("  ", " ");
            if (flat.Length > 300) flat = flat.Substring(0, 300) + "…";
            return flat;
        }
    }
}
