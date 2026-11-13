using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace LunaQuest.Core.Content
{
    /// <summary>
    /// Minimal dependency-free JSON reader/writer.
    /// Both engines and the .NET tests use the same implementation, so content and saves behave
    /// identically in Unity, Godot and a plain test runner without any third-party package.
    /// </summary>
    public static class Json
    {
        public static object? Parse(string text)
        {
            int i = 0;
            object? value = ParseValue(text, ref i);
            SkipWs(text, ref i);
            if (i < text.Length) throw new FormatException($"Unexpected trailing JSON at {i}");
            return value;
        }

        private static object? ParseValue(string s, ref int i)
        {
            SkipWs(s, ref i);
            if (i >= s.Length) throw new FormatException("Unexpected end of JSON");
            char c = s[i];
            switch (c)
            {
                case '{': return ParseObject(s, ref i);
                case '[': return ParseArray(s, ref i);
                case '"': return ParseString(s, ref i);
                case 't': Expect(s, ref i, "true"); return true;
                case 'f': Expect(s, ref i, "false"); return false;
                case 'n': Expect(s, ref i, "null"); return null;
                default: return ParseNumber(s, ref i);
            }
        }

        private static Dictionary<string, object?> ParseObject(string s, ref int i)
        {
            var o = new Dictionary<string, object?>(StringComparer.Ordinal);
            i++; // {
            SkipWs(s, ref i);
            if (i < s.Length && s[i] == '}') { i++; return o; }
            while (true)
            {
                SkipWs(s, ref i);
                string key = ParseString(s, ref i);
                SkipWs(s, ref i);
                if (s[i] != ':') throw new FormatException($"Expected ':' at {i}");
                i++;
                o[key] = ParseValue(s, ref i);
                SkipWs(s, ref i);
                if (s[i] == ',') { i++; continue; }
                if (s[i] == '}') { i++; return o; }
                throw new FormatException($"Expected ',' or '}}' at {i}");
            }
        }

        private static List<object?> ParseArray(string s, ref int i)
        {
            var a = new List<object?>();
            i++; // [
            SkipWs(s, ref i);
            if (i < s.Length && s[i] == ']') { i++; return a; }
            while (true)
            {
                a.Add(ParseValue(s, ref i));
                SkipWs(s, ref i);
                if (s[i] == ',') { i++; continue; }
                if (s[i] == ']') { i++; return a; }
                throw new FormatException($"Expected ',' or ']' at {i}");
            }
        }

        private static string ParseString(string s, ref int i)
        {
            if (s[i] != '"') throw new FormatException($"Expected string at {i}");
            i++;
            var sb = new StringBuilder();
            while (i < s.Length)
            {
                char c = s[i++];
                if (c == '"') return sb.ToString();
                if (c != '\\') { sb.Append(c); continue; }
                char e = s[i++];
                switch (e)
                {
                    case '"': sb.Append('"'); break;
                    case '\\': sb.Append('\\'); break;
                    case '/': sb.Append('/'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'u':
                        sb.Append((char)int.Parse(s.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                        i += 4;
                        break;
                    default: throw new FormatException($"Bad escape \\{e}");
                }
            }
            throw new FormatException("Unterminated string");
        }

        private static double ParseNumber(string s, ref int i)
        {
            int start = i;
            if (s[i] == '-') i++;
            while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '.' || s[i] == 'e' || s[i] == 'E' || s[i] == '+' || s[i] == '-')) i++;
            return double.Parse(s.Substring(start, i - start), CultureInfo.InvariantCulture);
        }

        private static void Expect(string s, ref int i, string token)
        {
            if (string.CompareOrdinal(s, i, token, 0, token.Length) != 0)
                throw new FormatException($"Expected {token} at {i}");
            i += token.Length;
        }

        private static void SkipWs(string s, ref int i)
        {
            while (i < s.Length && (s[i] == ' ' || s[i] == '\n' || s[i] == '\r' || s[i] == '\t')) i++;
        }

        public static string Serialize(object? value, bool pretty = true)
        {
            var sb = new StringBuilder();
            Write(sb, value, pretty, 0);
            return sb.ToString();
        }

        private static void Write(StringBuilder sb, object? v, bool pretty, int depth)
        {
            switch (v)
            {
                case null: sb.Append("null"); return;
                case string str: WriteString(sb, str); return;
                case bool b: sb.Append(b ? "true" : "false"); return;
                case int n: sb.Append(n.ToString(CultureInfo.InvariantCulture)); return;
                case long n: sb.Append(n.ToString(CultureInfo.InvariantCulture)); return;
                case float n: sb.Append(n.ToString("R", CultureInfo.InvariantCulture)); return;
                case double n: sb.Append(n.ToString("R", CultureInfo.InvariantCulture)); return;
                case Dictionary<string, object?> o: WriteObject(sb, o, pretty, depth); return;
                case IEnumerable<KeyValuePair<string, object?>> pairs: WriteObject(sb, pairs, pretty, depth); return;
                case System.Collections.IEnumerable list: WriteArray(sb, list, pretty, depth); return;
                default: WriteString(sb, v.ToString() ?? string.Empty); return;
            }
        }

        private static void WriteObject(StringBuilder sb, IEnumerable<KeyValuePair<string, object?>> o, bool pretty, int depth)
        {
            sb.Append('{');
            bool first = true;
            foreach (var kv in o)
            {
                if (!first) sb.Append(',');
                first = false;
                if (pretty) NewLine(sb, depth + 1);
                WriteString(sb, kv.Key);
                sb.Append(pretty ? ": " : ":");
                Write(sb, kv.Value, pretty, depth + 1);
            }
            if (pretty && !first) NewLine(sb, depth);
            sb.Append('}');
        }

        private static void WriteArray(StringBuilder sb, System.Collections.IEnumerable list, bool pretty, int depth)
        {
            sb.Append('[');
            bool first = true;
            foreach (var item in list)
            {
                if (!first) sb.Append(',');
                first = false;
                if (pretty) NewLine(sb, depth + 1);
                Write(sb, item, pretty, depth + 1);
            }
            if (pretty && !first) NewLine(sb, depth);
            sb.Append(']');
        }

        private static void NewLine(StringBuilder sb, int depth)
        {
            sb.Append('\n');
            sb.Append(' ', depth * 2);
        }

        private static void WriteString(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }

        // ---- typed helpers -------------------------------------------------

        public static Dictionary<string, object?> Obj(object? v)
            => v as Dictionary<string, object?> ?? new Dictionary<string, object?>();

        public static List<object?> Arr(object? v)
            => v as List<object?> ?? new List<object?>();

        public static string Str(object? o, string fallback = "")
            => o is string s ? s : fallback;

        public static double Num(object? o, double fallback = 0.0)
            => o is double d ? d : o is int i ? i : fallback;

        public static bool Bool(object? o, bool fallback = false)
            => o is bool b ? b : fallback;

        public static int Int(object? o, int fallback = 0)
            => o is double d ? (int)d : o is int i ? i : fallback;
    }
}
