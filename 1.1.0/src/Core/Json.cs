using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace PeterLauncher.Core
{
    /// <summary>
    /// 轻量 JSON 解析器。
    /// 不依赖 System.Web.Extensions，也不要求 .NET 4.5+，方便在 Windows 7 + .NET 4.0 上运行。
    /// 解析结果：Dictionary&lt;string,object&gt; / List&lt;object&gt; / string / double / bool / null。
    /// </summary>
    public static class Json
    {
        public static object Parse(string text)
        {
            JsonReader r = new JsonReader(text ?? string.Empty);
            return r.ReadValue();
        }

        public static object ParseFile(string path)
        {
            return Parse(TextFile.ReadAllText(path));
        }

        public static Dictionary<string, object> Map(object node)
        {
            return node is Dictionary<string, object> ? (Dictionary<string, object>)node : new Dictionary<string, object>();
        }

        public static List<object> Array(object node)
        {
            return node is List<object> ? (List<object>)node : new List<object>();
        }

        public static bool Has(object node, string key)
        {
            return Map(node).ContainsKey(key);
        }

        public static object Get(object node, string key)
        {
            Dictionary<string, object> m = Map(node);
            return m.ContainsKey(key) ? m[key] : null;
        }

        public static string Str(object node, string key, string fallback)
        {
            object v = Get(node, key);
            if (v == null) return fallback;
            return v is string ? (string)v : Convert.ToString(v, CultureInfo.InvariantCulture);
        }

        public static string Str(object node, string key)
        {
            return Str(node, key, string.Empty);
        }

        public static int Int(object node, string key, int fallback)
        {
            object v = Get(node, key);
            if (v is double) return (int)(double)v;
            if (v is string)
            {
                int n;
                if (int.TryParse((string)v, out n)) return n;
            }
            return fallback;
        }

        public static bool Bool(object node, string key, bool fallback)
        {
            object v = Get(node, key);
            if (v is bool) return (bool)v;
            if (v is string) return ((string)v).ToLowerInvariant() == "true";
            return fallback;
        }

        /// <summary>把任意节点重新序列化成一行文本（调试输出用）。</summary>
        public static string Describe(object node)
        {
            if (node == null) return "null";
            if (node is Dictionary<string, object>)
            {
                StringBuilder sb = new StringBuilder("{");
                Dictionary<string, object> m = (Dictionary<string, object>)node;
                bool first = true;
                foreach (KeyValuePair<string, object> kv in m)
                {
                    if (!first) sb.Append(", ");
                    first = false;
                    sb.Append(kv.Key).Append(": ").Append(Describe(kv.Value));
                }
                return sb.Append("}").ToString();
            }
            if (node is List<object>)
            {
                StringBuilder sb = new StringBuilder("[");
                List<object> list = (List<object>)node;
                for (int i = 0; i < list.Count; i++)
                {
                    if (i > 0) sb.Append(", ");
                    sb.Append(Describe(list[i]));
                }
                return sb.Append("]").ToString();
            }
            return node.ToString();
        }

        private sealed class JsonReader
        {
            private readonly string text;
            private int pos;

            public JsonReader(string s)
            {
                text = s;
                pos = 0;
            }

            public object ReadValue()
            {
                SkipWhite();
                if (pos >= text.Length) return null;
                char c = text[pos];
                if (c == '{') return ReadObject();
                if (c == '[') return ReadArray();
                if (c == '"') return ReadString();
                if (c == 't') { SkipWord("true"); return true; }
                if (c == 'f') { SkipWord("false"); return false; }
                if (c == 'n') { SkipWord("null"); return null; }
                return ReadNumber();
            }

            private void SkipWhite()
            {
                while (pos < text.Length)
                {
                    char c = text[pos];
                    if (c == ' ' || c == '\t' || c == '\r' || c == '\n') pos++;
                    else break;
                }
            }

            private void SkipWord(string word)
            {
                if (pos + word.Length <= text.Length && text.Substring(pos, word.Length) == word)
                    pos += word.Length;
                else
                    pos++;
            }

            private Dictionary<string, object> ReadObject()
            {
                Dictionary<string, object> map = new Dictionary<string, object>();
                pos++; // {
                while (true)
                {
                    SkipWhite();
                    if (pos >= text.Length) break;
                    if (text[pos] == '}') { pos++; break; }
                    if (text[pos] == ',') { pos++; continue; }
                    if (text[pos] != '"') { pos++; continue; }
                    string key = ReadString();
                    SkipWhite();
                    if (pos < text.Length && text[pos] == ':') pos++;
                    map[key] = ReadValue();
                }
                return map;
            }

            private List<object> ReadArray()
            {
                List<object> list = new List<object>();
                pos++; // [
                while (true)
                {
                    SkipWhite();
                    if (pos >= text.Length) break;
                    if (text[pos] == ']') { pos++; break; }
                    if (text[pos] == ',') { pos++; continue; }
                    list.Add(ReadValue());
                }
                return list;
            }

            private string ReadString()
            {
                StringBuilder sb = new StringBuilder();
                pos++; // 开引号
                while (pos < text.Length)
                {
                    char c = text[pos];
                    if (c == '"') { pos++; break; }
                    if (c == '\\')
                    {
                        pos++;
                        if (pos >= text.Length) break;
                        char esc = text[pos];
                        switch (esc)
                        {
                            case 'n': sb.Append('\n'); break;
                            case 'r': sb.Append('\r'); break;
                            case 't': sb.Append('\t'); break;
                            case 'b': sb.Append('\b'); break;
                            case 'f': sb.Append('\f'); break;
                            case 'u':
                                if (pos + 4 < text.Length)
                                {
                                    string hex = text.Substring(pos + 1, 4);
                                    try { sb.Append((char)Convert.ToInt32(hex, 16)); }
                                    catch { }
                                    pos += 4;
                                }
                                break;
                            default: sb.Append(esc); break;
                        }
                        pos++;
                        continue;
                    }
                    sb.Append(c);
                    pos++;
                }
                return sb.ToString();
            }

            private object ReadNumber()
            {
                int start = pos;
                while (pos < text.Length)
                {
                    char c = text[pos];
                    if ((c >= '0' && c <= '9') || c == '-' || c == '+' || c == '.' || c == 'e' || c == 'E') pos++;
                    else break;
                }
                string raw = text.Substring(start, pos - start);
                double value = 0;
                double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
                return value;
            }
        }
    }

    /// <summary>UTF-8 文本读写（自动识别 BOM / UTF-16，写盘默认带 BOM 方便记事本显示中文）。</summary>
    public static class TextFile
    {
        public static string ReadAllText(string path)
        {
            // 用共享读打开：游戏运行时也能在启动器里看日志
            byte[] bytes;
            using (System.IO.FileStream stream = new System.IO.FileStream(path, System.IO.FileMode.Open,
                System.IO.FileAccess.Read, System.IO.FileShare.ReadWrite))
            {
                bytes = new byte[stream.Length];
                int offset = 0;
                int read;
                while (offset < bytes.Length && (read = stream.Read(bytes, offset, bytes.Length - offset)) > 0)
                    offset += read;
                if (offset < bytes.Length) Array.Resize(ref bytes, offset);
            }
            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            {
                byte[] body = new byte[bytes.Length - 3];
                Array.Copy(bytes, 3, body, 0, body.Length);
                return Encoding.UTF8.GetString(body);
            }
            if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
            {
                byte[] body = new byte[bytes.Length - 2];
                Array.Copy(bytes, 2, body, 0, body.Length);
                return Encoding.Unicode.GetString(body);
            }
            return Encoding.UTF8.GetString(bytes);
        }

        public static void WriteAllText(string path, string content, bool withBom)
        {
            string dir = System.IO.Path.GetDirectoryName(path);
            if (dir != null && dir.Length > 0 && !System.IO.Directory.Exists(dir))
                System.IO.Directory.CreateDirectory(dir);
            System.IO.File.WriteAllText(path, content, new UTF8Encoding(withBom));
        }
    }
}
