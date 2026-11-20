using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace PeterLauncher.Core
{
    public static class MicrosoftAuth
    {
        public const string ClientIdDefault = "00000000402b5328";
        private const string AuthorizationUrl = "https://login.microsoftonline.com/consumers/oauth2/v2.0/authorize";
        private const string TokenUrl = "https://login.microsoftonline.com/consumers/oauth2.0/token";
        private const string XstsUrl = "https://api.minecraftservices.com/authentication/login_with_xbox";
        private const string MinecraftProfileUrl = "https://api.minecraftservices.com/minecraft/profile";

        public static string BuildAuthorizeUrl(string clientId, string redirectUri, string challenge)n        {
            return AuthorizationUrl
                + "?client_id=" + Uri.EscapeDataString(clientId)
                + "&response_type=code"
                + "&scope=XboxLive.signin+offline_access"
                + "&redirect_uri=" + Uri.EscapeDataString(redirectUri)
                + "&code_challenge=" + challenge
                + "&code_challenge_method=S256";
        }

        public static string BuildCodeChallenge(string verifier)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(verifier));
                return Base64UrlNoPad(hash);
            }
        }

        public static string NewVerifier()
        {
            byte[] buffer = new byte[32];
            using (RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider())
                rng.GetBytes(buffer);
            return Base64UrlNoPad(buffer);
        }

        public static void ExchangeCode(string clientId, string redirectUri, string code, string verifier, out string accessToken, out string refreshToken, out string error)
        {
            accessToken = "";
            refreshToken = "";
            error = "";

            string body = "client_id=" + Uri.EscapeDataString(clientId)
                + "&grant_type=authorization_code"
                + "&code=" + Uri.EscapeDataString(code)
                + "&redirect_uri=" + Uri.EscapeDataString(redirectUri)
                + "&code_verifier=" + Uri.EscapeDataString(verifier);

            try
            {
                string resp = Http.PostForm(TokenUrl, body, null);
                Dictionary<string, string> headers = new Dictionary<string, string>();
                headers["Content-Type"] = "application/json";
                object root = Json.Parse(resp);
                accessToken = Json.Str(root, "access_token", "");
                refreshToken = Json.Str(root, "refresh_token", "");
                if (accessToken.Length == 0)
                    error = Json.Str(root, "error_description", "没有 access_token");
            }
            catch (HttpError ex)
            {
                error = ex.Message;
            }
            catch (Exception ex)
            {
                error = ex.Message;
            }
        }

        public static void Refresh(string clientId, string refreshToken, out string accessToken, out string newRefresh, out string error)
        {
            accessToken = "";
            newRefresh = "";
            error = "";

            string body = "client_id=" + Uri.EscapeDataString(clientId)
                + "&grant_type=refresh_token"
                + "&refresh_token=" + Uri.EscapeDataString(refreshToken);

            try
            {
                string resp = Http.PostForm(TokenUrl, body, null);
                object root = Json.Parse(resp);
                accessToken = Json.Str(root, "access_token", "");
                newRefresh = Json.Str(root, "refresh_token", "");
                if (accessToken.Length == 0)
                    error = Json.Str(root, "error_description", "没有 access_token");
            }
            catch (HttpError ex)
            {
                error = ex.Message;
            }
            catch (Exception ex)
            {
                error = ex.Message;
            }
        }

        public static void ToMinecraft(string microsoftAccessToken, out string name, out string uuid, out string minecraftToken, out string error)
        {
            name = "";
            uuid = "";
            minecraftToken = "";
            error = "";

            try
            {
                string xboxBody = JsonSerialize(new Dictionary<string, string>()
                {
                    { "Properties", "{\"AuthMethod\":\"RPS\",\"SiteName\":\"user.auth.xboxlive.com\",\"RpsTicket\":\"d=" + microsoftAccessToken + "\"}" },
                    { "RelyingParty", "http://auth.xboxlive.com" },
                    { "TokenType", "JWT" }
                });

                Dictionary<string, string> xboxHeaders = new Dictionary<string, string>();
                xboxHeaders["Content-Type"] = "application/json";
                xboxHeaders["Accept"] = "application/json";

                string xboxResp = Http.PostJson("https://user.auth.xboxlive.com/user/authenticate", xboxBody, xboxHeaders);
                object xboxRoot = Json.Parse(xboxResp);
                string token = Json.Str(xboxRoot, "Token", "");
                object displayClaims = Json.Get(xboxRoot, "DisplayClaims");
                string uhs = Json.Str(displayClaims, "xui", "");
                if (token.Length == 0 || uhs.Length == 0)
                {
                    error = "XBox Live 登录失败：未取得 Token/uhs";
                    return;
                }

                string xstsBody = JsonSerialize(new Dictionary<string, string>()
                {
                    { "Properties", "{\"SandboxId\":\"RETAIL\",\"UserTokens\":[\"" + token + \"]}" },
                    { "RelyingParty", "rp://api.minecraftservices.com/" },
                    { "TokenType", "JWT" }
                });

                string xstsResp = Http.PostJson("https://xsts.auth.xboxlive.com/xsts/authorize", xstsBody, xboxHeaders);
                object xstsRoot = Json.Parse(xstsResp);
                string xstsToken = Json.Str(xstsRoot, "Token", "");
                object xstsClaims = Json.Get(xstsRoot, "DisplayClaims");
                string xUhs = Json.Str(xstsClaims, "xui", "");
                if (xstsToken.Length == 0 || xUhs.Length == 0)
                {
                    error = "XSTS 授权失败：账号可能没有 Xbox 账号";
                    return;
                }

                string mcBody = JsonSerialize(new Dictionary<string, string>()
                {
                    { "identityToken", "XBL3.0 x=" + xUhs + ";" + xstsToken }
                });

                string mcResp = Http.PostJson(XstsUrl, mcBody, xboxHeaders);
                object mcRoot = Json.Parse(mcResp);
                minecraftToken = Json.Str(mcRoot, "access_token", "");
                if (minecraftToken.Length == 0)
                {
                    error = "Minecraft 服务认证失败：未取得 access_token";
                    return;
                }

                Dictionary<string, string> profileHeaders = new Dictionary<string, string>();
                profileHeaders["Authorization"] = "Bearer " + minecraftToken;
                string profile = Http.GetJson(MinecraftProfileUrl, profileHeaders);
                object profileRoot = Json.Parse(profile);
                name = Json.Str(profileRoot, "name", "");
                uuid = Json.Str(profileRoot, "id", "");
                if (name.Length == 0 || uuid.Length == 0)
                    error = "无法获取 Minecraft 档案（可能未购买游戏）";
            }
            catch (HttpError ex)
            {
                error = ex.Message;
            }
            catch (Exception ex)
            {
                error = ex.Message;
            }
        }

        private static string JsonSerialize(Dictionary<string, string> map)
        {
            StringBuilder sb = new StringBuilder("{");
            bool first = true;
            foreach (KeyValuePair<string, string> kv in map)
            {
                if (!first) sb.Append(",");
                sb.Append('"').Append(kv.Key).Append("":");
                if (kv.Value.StartsWith("{") || kv.Value.StartsWith("["))
                    sb.Append(kv.Value);
                else
                    sb.Append('"').Append(kv.Value).Append('"');
                first = false;
            }
            sb.Append('}');
            return sb.ToString();
        }

        private static string Base64UrlNoPad(byte[] data)
        {
            return Convert.ToBase64String(data)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }
    }
}
