using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace PeterLauncher.Core
{
    public static class Json
    {
        public static object Parse(string text)
        {
            int pos = 0;
            object value = ParseValue(text, ref pos);
            return value;
        }

        public static object Get(object obj, string key)
        {
            if (!(obj is Dictionary<string, object>)) return null;
            Dictionary<string, object> map = (Dictionary<string, object>)obj;
            object value;
            if (map.TryGetValue(key, out value)) return value;
            return null;
        }

        public static string Str(object obj, string key)
        {
            object v = Get(obj, key);
            if (v is string) return (string)v;
            return null;
        }

        public static string Str(object obj, string key, string fallback)
        {
            string v = Str(obj, key);
            return v == null ? fallback : v;
        }

        private static object ParseValue(string text, ref int pos)
        {
            SkipWhitespace(text, ref pos);
            if (pos >= text.Length) return null;

            char c = text[pos];
            if (c == '{') return ParseObject(text, ref pos);
            if (c == '[') return ParseArray(text, ref pos);
            if (c == '"') return ParseString(text, ref pos);

            if (c == 't') { pos += 4; return true; }
            if (c == 'f') { pos += 5; return false; }
            if (c == 'n') { pos += 4; return null; }

            int start = pos;
            while (pos < text.Length && ",}]\n\r\t ".IndexOf(text[pos]) < 0) pos++;
            string num = text.Substring(start, pos - start);
            double d;
            if (double.TryParse(num, NumberStyles.Float, CultureInfo.InvariantCulture, out d))
                return d;
            return num;
        }

        private static Dictionary<string, object> ParseObject(string text, ref int pos)
        {
            Dictionary<string, object> map = new Dictionary<string, object>();
            pos++; // { 
            SkipWhitespace(text, ref pos);
            if (pos < text.Length && text[pos] == '}') { pos++; return map; }

            while (pos < text.Length)
            {
                SkipWhitespace(text, ref pos);
                string key = ParseString(text, ref pos);
                SkipWhitespace(text, ref pos);
                if (pos < text.Length && text[pos] == ':') pos++;
                object value = ParseValue(text, ref pos);
                map[key] = value;

                SkipWhitespace(text, ref pos);
                if (pos < text.Length && text[pos] == ',') { pos++; continue; }
                if (pos < text.Length && text[pos] == '}') { pos++; break; }
                break;
            }
            return map;
        }

        private static List<object> ParseArray(string text, ref int pos)
        {
            List<object> list = new List<object>();
            pos++; // [
            SkipWhitespace(text, ref pos);
            if (pos < text.Length && text[pos] == ']') { pos++; return list; }

            while (pos < text.Length)
            {
                object value = ParseValue(text, ref pos);
                list.Add(value);

                SkipWhitespace(text, ref pos);
                if (pos < text.Length && text[pos] == ',') { pos++; continue; }
                if (pos < text.Length && text[pos] == ']') { pos++; break; }
                break;
            }
            return list;
        }

        private static string ParseString(string text, ref int pos)
        {
            StringBuilder sb = new StringBuilder();
            pos++; // "
            while (pos < text.Length)
            {
                char c = text[pos];
                if (c == '\\')
                {
                    pos++;
                    if (pos >= text.Length) break;
                    char e = text[pos];
                    switch (e)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'u':
                            if (pos + 4 < text.Length)
                            {
                                string hex = text.Substring(pos + 1, 4);
                                sb.Append((char)int.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                                pos += 4;
                            }
                            break;
                    }
                    pos++;
                }
                else if (c == '"') { pos++; break; }
                else { sb.Append(c); pos++; }
            }
            return sb.ToString();
        }

        private static void SkipWhitespace(string text, ref int pos)
        {
            while (pos < text.Length && char.IsWhiteSpace(text[pos])) pos++;
        }
    }

    public static class TextFile
    {
        public static string ReadAllText(string path)
        {
            using (StreamReader reader = new StreamReader(path, Encoding.UTF8))
                return reader.ReadToEnd();
        }

        public static void WriteAllText(string path, string content, bool utf8Bom)
        {
            Encoding enc = utf8Bom ? new UTF8Encoding(true) : new UTF8Encoding(false);
            File.WriteAllText(path, content, enc);
        }
    }
}
