using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace PeterLauncher.Core
{
    /// <summary>登录后的微软 / Minecraft 账户信息。</summary>
    public class MsAccount
    {
        public string Name = "";
        public string Uuid = "";
        public string AccessToken = "";
        public string RefreshToken = "";
        public long ExpireAt = 0;
        public bool OwnsJavaEdition = true;
    }

    /// <summary>设备码登录需要展示给用户的信息。</summary>
    public class DeviceCodeInfo
    {
        public string UserCode = "";
        public string VerificationUri = "";
        public string DeviceCode = "";
        public int Interval = 5;
        public int ExpiresIn = 900;
    }

    /// <summary>
    /// 微软正版登录（OAuth 2.0 设备码流程 + 旧版弹窗流程）。
    /// 只有这里会访问网络：登录微软 → Xbox Live → Minecraft 服务，其余所有功能都是纯本地。
    /// </summary>
    public static class MicrosoftAuth
    {
        private const string DeviceCodeUrl = "https://login.microsoftonline.com/consumers/oauth2/v2.0/devicecode";
        private const string TokenUrl = "https://login.microsoftonline.com/consumers/oauth2/v2.0/token";
        private const string Scope = "XboxLive.signin offline_access";
        private const string XblUrl = "https://user.auth.xboxlive.com/user/authenticate";
        private const string XstsUrl = "https://xsts.auth.xboxlive.com/xsts/authorize";
        private const string McLoginUrl = "https://api.minecraftservices.com/authentication/login_with_xbox";
        private const string McProfileUrl = "https://api.minecraftservices.com/minecraft/profile";

        /// <summary>旧版弹窗流程使用的公共客户端 ID（微软自家的 “Xbox Live” 应用）。</summary>
        public const string LegacyClientId = "00000000402b5328";
        public const string LegacyAuthorizeUrl = "https://login.live.com/oauth20_authorize.srf";
        public const string LegacyRedirectUrl = "https://login.live.com/oauth20_desktop.srf";
        public const string LegacyScope = "service::user.auth.xboxlive.com::MBI_SSL";

        // ------------------------------------------------------------------
        // 第一步：申请设备码
        // ------------------------------------------------------------------
        public static DeviceCodeInfo RequestDeviceCode(string clientId, string callerName)
        {
            if (clientId == null || clientId.Trim().Length == 0)
                throw new Exception("还没有填写 Azure 应用（客户端）ID，无法使用设备码登录。\n可以在「账户」页填写自己的客户端 ID，或改用「旧版弹窗登录」。");

            string body = "client_id=" + Uri.EscapeDataString(clientId.Trim())
                        + "&scope=" + Uri.EscapeDataString(Scope);
            string json = Http.PostForm(DeviceCodeUrl, body, null);
            object root = Json.Parse(json);

            DeviceCodeInfo info = new DeviceCodeInfo();
            info.UserCode = Json.Str(root, "user_code");
            info.VerificationUri = Json.Str(root, "verification_uri");
            info.DeviceCode = Json.Str(root, "device_code");
            info.Interval = Json.Int(root, "interval", 5);
            info.ExpiresIn = Json.Int(root, "expires_in", 900);
            if (info.DeviceCode.Length == 0)
                throw new Exception("微软没有返回设备码：" + Json.Str(root, "error_description", json));
            return info;
        }

        /// <summary>打开浏览器让用户输入设备码。</summary>
        public static void OpenBrowser(string url)
        {
            try { Process.Start(url); }
            catch (Exception ex) { Log.Warn("打开浏览器失败：" + ex.Message); }
        }

        // ------------------------------------------------------------------
        // 第二步：轮询换取微软令牌
        // ------------------------------------------------------------------
        public static string PollAccessToken(string clientId, DeviceCodeInfo info, Action<string> status, Func<bool> cancelled)
        {
            int interval = Math.Max(2, info.Interval);
            long deadline = DateTime.Now.AddSeconds(Math.Max(60, info.ExpiresIn)).Ticks;
            while (DateTime.Now.Ticks < deadline)
            {
                if (cancelled != null && cancelled()) throw new Exception("登录已取消");
                Thread.Sleep(interval * 1000);

                string body = "grant_type=urn:ietf:params:oauth:grant-type:device_code"
                            + "&client_id=" + Uri.EscapeDataString(clientId.Trim())
                            + "&device_code=" + Uri.EscapeDataString(info.DeviceCode);
                try
                {
                    string json = Http.PostForm(TokenUrl, body, null);
                    object root = Json.Parse(json);
                    string token = Json.Str(root, "access_token");
                    if (token.Length > 0) return json;
                }
                catch (HttpError ex)
                {
                    object root = null;
                    try { root = Json.Parse(ex.Body); } catch { }
                    string error = Json.Str(root, "error");
                    if (error == "authorization_pending") { if (status != null) status("等待你在浏览器里完成授权…"); continue; }
                    if (error == "slow_down") { interval += 5; if (status != null) status("请求太快，放慢轮询…"); continue; }
                    if (error == "authorization_declined") throw new Exception("你拒绝了这个登录请求");
                    if (error == "expired_token") throw new Exception("设备码已过期，请重新发起登录");
                    throw new Exception("微软登录失败：" + Json.Str(root, "error_description", ex.Message));
                }
            }
            throw new Exception("设备码等待超时，请重新发起登录");
        }

        // ------------------------------------------------------------------
        // 第三步：微软令牌 → Xbox Live → Minecraft 令牌 → 档案
        // ------------------------------------------------------------------
        public static MsAccount ChainToMinecraft(string msAccessToken, Action<string> status)
        {
            // 1) Xbox Live 用户认证
            if (status != null) status("正在通过 Xbox Live 验证…");
            Dictionary<string, string> headers = new Dictionary<string, string>();
            headers["x-xbl-contract-version"] = "1";

            string xblBody = "{\"Properties\":{\"AuthMethod\":\"RPS\",\"SiteName\":\"user.auth.xboxlive.com\","
                           + "\"RpsTicket\":\"d=" + msAccessToken + "\"},\"RelyingParty\":\"http://auth.xboxlive.com\",\"TokenType\":\"JWT\"}";
            object xbl = Json.Parse(Http.PostJson(XblUrl, xblBody, headers));
            string xblToken = Json.Str(xbl, "Token");
            if (xblToken.Length == 0) throw new Exception("Xbox Live 认证没有返回令牌");

            string uhs = "";
            object claims = Json.Get(xbl, "DisplayClaims");
            List<object> xui = Json.Array(Json.Get(claims, "xui"));
            if (xui.Count > 0) uhs = Json.Str(xui[0], "uhs");
            if (uhs.Length == 0) throw new Exception("Xbox Live 认证没有返回用户标识（uhs）");

            // 2) XSTS 授权
            if (status != null) status("正在获取 XSTS 授权…");
            string xstsBody = "{\"Properties\":{\"SandboxId\":\"RETAIL\",\"UserTokens\":[\"" + xblToken + "\"]},"
                            + "\"RelyingParty\":\"rp://api.minecraftservices.com/\",\"TokenType\":\"JWT\"}";
            string xstsToken = "";
            try
            {
                object xsts = Json.Parse(Http.PostJson(XstsUrl, xstsBody, headers));
                xstsToken = Json.Str(xsts, "Token");
            }
            catch (HttpError ex)
            {
                throw new Exception(ExplainXstsError(ex));
            }
            if (xstsToken.Length == 0) throw new Exception("XSTS 授权没有返回令牌");

            // 3) 用 Xbox 身份登录 Minecraft 服务
            if (status != null) status("正在登录 Minecraft 服务…");
            string identityToken = "XBL3.0 x=" + uhs + ";" + xstsToken;
            string mcJson = Http.PostJson(McLoginUrl, "{\"identityToken\":\"" + identityToken + "\"}", null);
            object mc = Json.Parse(mcJson);
            string mcToken = Json.Str(mc, "access_token");
            if (mcToken.Length == 0) throw new Exception("Minecraft 登录失败：没有返回访问令牌");

            MsAccount account = new MsAccount();
            account.AccessToken = mcToken;
            account.ExpireAt = DateTime.Now.AddSeconds(Json.Int(mc, "expires_in", 86400) - 300).Ticks;

            // 4) 拉取游戏档案（名字 / UUID / 是否拥有游戏）
            if (status != null) status("正在读取游戏档案…");
            Dictionary<string, string> authHeader = new Dictionary<string, string>();
            authHeader["Authorization"] = "Bearer " + mcToken;
            try
            {
                object profile = Json.Parse(Http.GetJson(McProfileUrl, authHeader));
                account.Name = Json.Str(profile, "name");
                account.Uuid = Json.Str(profile, "id");
                if (account.Name.Length == 0)
                    throw new Exception("这个微软账户还没有购买 Minecraft Java 版（找不到游戏档案）");
            }
            catch (HttpError ex)
            {
                if (ex.StatusCode == 404)
                    throw new Exception("这个微软账户还没有购买 Minecraft Java 版，无法以正版身份进入游戏");
                throw;
            }
            return account;
        }

        /// <summary>XSTS 常见错误翻译成人话。</summary>
        private static string ExplainXstsError(HttpError ex)
        {
            object root = null;
            try { root = Json.Parse(ex.Body); } catch { }
            long xerr = 0;
            object raw = Json.Get(root, "XErr");
            if (raw is double) xerr = (long)(double)raw;

            switch (xerr)
            {
                case 2148916233L:
                    return "这个微软账户还没有 Xbox 账户。请先用它登录一次 Xbox 官网（xbox.com）创建档案，再回来登录。";
                case 2148916235L:
                    return "这个账户所在的地区被 Xbox Live 限制，无法登录。";
                case 2148916236L:
                case 2148916237L:
                    return "这个账户需要完成成人验证（地区限制），请在浏览器里登录 xbox.com 处理。";
                case 2148916238L:
                    return "这是一个未成年人账户，需要先加入微软家庭组（由家长批准）才能登录。";
                default:
                    return "XSTS 授权失败（XErr=" + xerr + "）：" + ex.Message;
            }
        }

        /// <summary>用 refresh_token 换新的微软令牌（设备码流程才会拿到 refresh_token）。</summary>
        public static string RefreshMsToken(string clientId, string refreshToken)
        {
            if (clientId == null || clientId.Length == 0 || refreshToken == null || refreshToken.Length == 0) return "";
            string body = "grant_type=refresh_token&client_id=" + Uri.EscapeDataString(clientId)
                        + "&refresh_token=" + Uri.EscapeDataString(refreshToken)
                        + "&scope=" + Uri.EscapeDataString(Scope);
            try
            {
                string json = Http.PostForm(TokenUrl, body, null);
                return json;
            }
            catch (Exception ex)
            {
                Log.Warn("刷新微软令牌失败：" + ex.Message);
                return "";
            }
        }

        /// <summary>从 device code 流程返回的 JSON 里取出 access / refresh token。</summary>
        public static void ReadTokenResponse(string json, out string accessToken, out string refreshToken, out int expiresIn)
        {
            object root = Json.Parse(json);
            accessToken = Json.Str(root, "access_token");
            refreshToken = Json.Str(root, "refresh_token");
            expiresIn = Json.Int(root, "expires_in", 3600);
        }

        /// <summary>从旧版弹窗跳转 URL 的 #片段里取出 access_token。</summary>
        public static string ParseLegacyToken(string url)
        {
            if (url == null) return "";
            int hash = url.IndexOf('#');
            if (hash < 0) return "";
            string fragment = url.Substring(hash + 1);
            foreach (string pair in fragment.Split('&'))
            {
                int eq = pair.IndexOf('=');
                if (eq <= 0) continue;
                string key = pair.Substring(0, eq);
                string value = pair.Substring(eq + 1);
                if (key == "access_token") return Uri.UnescapeDataString(value);
            }
            return "";
        }

        public static string LegacyLoginUrl()
        {
            return LegacyAuthorizeUrl
                 + "?client_id=" + LegacyClientId
                 + "&response_type=token"
                 + "&scope=" + Uri.EscapeDataString(LegacyScope)
                 + "&redirect_uri=" + Uri.EscapeDataString(LegacyRedirectUrl)
                 + "&display=touch&locale=zh_CN";
        }
    }
}
