using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace LToLocal.Sim
{
    /// <summary>
    /// Small, dependency-free JSON reader for content files (road packs, strings). Objects become
    /// <c>Dictionary&lt;string, object?&gt;</c>, arrays <c>List&lt;object?&gt;</c>, numbers <c>double</c>.
    /// </summary>
    public static class MiniJson
    {
        public static object? Parse(string text)
        {
            int i = 0;
            var v = Value(text, ref i);
            Skip(text, ref i);
            if (i != text.Length) throw new FormatException($"Unexpected text at {i}.");
            return v;
        }

        public static Dictionary<string, object?> Obj(object? o) => o as Dictionary<string, object?> ?? throw new FormatException("Expected an object.");
        public static List<object?> Arr(object? o) => o as List<object?> ?? throw new FormatException("Expected an array.");
        public static string Str(object? o) => o as string ?? throw new FormatException("Expected a string.");
        public static double Num(object? o) => o is double d ? d : throw new FormatException("Expected a number.");

        public static object? Get(this Dictionary<string, object?> o, string key) => o.TryGetValue(key, out var v) ? v : null;

        private static object? Value(string s, ref int i)
        {
            Skip(s, ref i);
            if (i >= s.Length) throw new FormatException("Unexpected end of JSON.");
            char c = s[i];
            if (c == '{') return Object(s, ref i);
            if (c == '[') return Array(s, ref i);
            if (c == '"') return String(s, ref i);
            if (c == 't' && Match(s, ref i, "true")) return true;
            if (c == 'f' && Match(s, ref i, "false")) return false;
            if (c == 'n' && Match(s, ref i, "null")) return null;
            return Number(s, ref i);
        }

        private static Dictionary<string, object?> Object(string s, ref int i)
        {
            var o = new Dictionary<string, object?>();
            i++;
            Skip(s, ref i);
            if (s[i] == '}') { i++; return o; }
            while (true)
            {
                Skip(s, ref i);
                string key = String(s, ref i);
                Skip(s, ref i);
                if (s[i++] != ':') throw new FormatException($"Expected ':' at {i - 1}.");
                o[key] = Value(s, ref i);
                Skip(s, ref i);
                char c = s[i++];
                if (c == '}') return o;
                if (c != ',') throw new FormatException($"Expected ',' or '}}' at {i - 1}.");
            }
        }

        private static List<object?> Array(string s, ref int i)
        {
            var a = new List<object?>();
            i++;
            Skip(s, ref i);
            if (s[i] == ']') { i++; return a; }
            while (true)
            {
                a.Add(Value(s, ref i));
                Skip(s, ref i);
                char c = s[i++];
                if (c == ']') return a;
                if (c != ',') throw new FormatException($"Expected ',' or ']' at {i - 1}.");
            }
        }

        private static string String(string s, ref int i)
        {
            if (s[i] != '"') throw new FormatException($"Expected a string at {i}.");
            i++;
            var sb = new StringBuilder();
            while (true)
            {
                char c = s[i++];
                if (c == '"') return sb.ToString();
                if (c != '\\') { sb.Append(c); continue; }
                char e = s[i++];
                switch (e)
                {
                    case 'n': sb.Append('\n'); break;
                    case 't': sb.Append('\t'); break;
                    case 'r': sb.Append('\r'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'u': sb.Append((char)int.Parse(s.Substring(i, 4), NumberStyles.HexNumber)); i += 4; break;
                    default: sb.Append(e); break;
                }
            }
        }

        private static double Number(string s, ref int i)
        {
            int start = i;
            while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
            if (i == start) throw new FormatException($"Unexpected character '{s[i]}' at {i}.");
            return double.Parse(s.Substring(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        private static bool Match(string s, ref int i, string word)
        {
            if (string.CompareOrdinal(s, i, word, 0, word.Length) != 0) throw new FormatException($"Unexpected text at {i}.");
            i += word.Length;
            return true;
        }

        private static void Skip(string s, ref int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
        }
    }
}
