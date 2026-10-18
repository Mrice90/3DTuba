using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace IC.Net
{
    /// <summary>
    /// Minimal JSON reader/writer for the relay-1 protocol. The library is
    /// dependency-free by design (Unity drop-in: no NuGet restore inside the
    /// Unity project), so this covers exactly what the protocol needs:
    /// objects, arrays, strings (with full escape support), numbers,
    /// booleans and null. Not a general-purpose parser.
    /// </summary>
    internal static class JsonLite
    {
        public sealed class JsonException : Exception
        {
            public JsonException(string message) : base(message) { }
        }

        public static Dictionary<string, object?> ParseObject(string text)
        {
            var p = new Parser(text);
            var v = p.ParseValue();
            p.SkipWs();
            if (!p.Eof) throw new JsonException("Trailing characters after JSON value");
            if (v is Dictionary<string, object?> o) return o;
            throw new JsonException("Expected a JSON object");
        }

        public static List<object?> ParseArray(string text)
        {
            var p = new Parser(text);
            var v = p.ParseValue();
            p.SkipWs();
            if (!p.Eof) throw new JsonException("Trailing characters after JSON value");
            if (v is List<object?> a) return a;
            throw new JsonException("Expected a JSON array");
        }

        /// <summary>
        /// Serializes a value: Dictionary&lt;string, object?&gt;, List&lt;object?&gt;,
        /// string, long/int, double, bool, or null.
        /// </summary>
        public static string Stringify(object? value)
        {
            var sb = new StringBuilder();
            Write(sb, value);
            return sb.ToString();
        }

        public static Dictionary<string, object?> Obj(params (string Key, object? Value)[] props)
        {
            var d = new Dictionary<string, object?>(props.Length);
            foreach (var (k, v) in props) d[k] = v;
            return d;
        }

        private static void Write(StringBuilder sb, object? value)
        {
            switch (value)
            {
                case null: sb.Append("null"); break;
                case string s: WriteString(sb, s); break;
                case bool b: sb.Append(b ? "true" : "false"); break;
                case long l: sb.Append(l.ToString(CultureInfo.InvariantCulture)); break;
                case int i: sb.Append(i.ToString(CultureInfo.InvariantCulture)); break;
                case double d:
                    if (double.IsNaN(d) || double.IsInfinity(d))
                        throw new JsonException("Cannot serialize NaN/Infinity");
                    sb.Append(d.ToString("R", CultureInfo.InvariantCulture));
                    break;
                case Dictionary<string, object?> o:
                    sb.Append('{');
                    var first = true;
                    foreach (var kv in o)
                    {
                        if (!first) sb.Append(',');
                        first = false;
                        WriteString(sb, kv.Key);
                        sb.Append(':');
                        Write(sb, kv.Value);
                    }
                    sb.Append('}');
                    break;
                case List<object?> a:
                    sb.Append('[');
                    for (var i = 0; i < a.Count; i++)
                    {
                        if (i > 0) sb.Append(',');
                        Write(sb, a[i]);
                    }
                    sb.Append(']');
                    break;
                default:
                    throw new JsonException("Cannot serialize " + value.GetType().Name);
            }
        }

        private static void WriteString(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (var c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    default:
                        if (c < 0x20)
                        {
                            sb.Append("\\u");
                            sb.Append(((int)c).ToString("x4"));
                        }
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }

        private sealed class Parser
        {
            private readonly string _s;
            private int _i;
            public Parser(string s) { _s = s; }
            public bool Eof => _i >= _s.Length;

            public void SkipWs()
            {
                while (_i < _s.Length && char.IsWhiteSpace(_s[_i])) _i++;
            }

            private char Peek()
            {
                if (_i >= _s.Length) throw new JsonException("Unexpected end of JSON");
                return _s[_i];
            }

            private void Expect(char c)
            {
                if (Peek() != c) throw new JsonException($"Expected '{c}' at offset {_i}");
                _i++;
            }

            public object? ParseValue()
            {
                SkipWs();
                var c = Peek();
                switch (c)
                {
                    case '{': return ParseObjectInner();
                    case '[': return ParseArrayInner();
                    case '"': return ParseString();
                    case 't': ExpectLiteral("true"); return true;
                    case 'f': ExpectLiteral("false"); return false;
                    case 'n': ExpectLiteral("null"); return null;
                    default:
                        if (c == '-' || (c >= '0' && c <= '9')) return ParseNumber();
                        throw new JsonException($"Unexpected character '{c}' at offset {_i}");
                }
            }

            private void ExpectLiteral(string lit)
            {
                if (_s.Length - _i < lit.Length || _s.Substring(_i, lit.Length) != lit)
                    throw new JsonException($"Expected '{lit}' at offset {_i}");
                _i += lit.Length;
            }

            private Dictionary<string, object?> ParseObjectInner()
            {
                var o = new Dictionary<string, object?>();
                Expect('{');
                SkipWs();
                if (Peek() == '}') { _i++; return o; }
                while (true)
                {
                    SkipWs();
                    if (Peek() != '"') throw new JsonException($"Expected string key at offset {_i}");
                    var key = ParseString();
                    SkipWs();
                    Expect(':');
                    o[key] = ParseValue();
                    SkipWs();
                    var c = Peek();
                    if (c == ',') { _i++; continue; }
                    if (c == '}') { _i++; return o; }
                    throw new JsonException($"Expected ',' or '}}' at offset {_i}");
                }
            }

            private List<object?> ParseArrayInner()
            {
                var a = new List<object?>();
                Expect('[');
                SkipWs();
                if (Peek() == ']') { _i++; return a; }
                while (true)
                {
                    a.Add(ParseValue());
                    SkipWs();
                    var c = Peek();
                    if (c == ',') { _i++; continue; }
                    if (c == ']') { _i++; return a; }
                    throw new JsonException($"Expected ',' or ']' at offset {_i}");
                }
            }

            private string ParseString()
            {
                Expect('"');
                var sb = new StringBuilder();
                while (true)
                {
                    if (_i >= _s.Length) throw new JsonException("Unterminated string");
                    var c = _s[_i++];
                    if (c == '"') return sb.ToString();
                    if (c == '\\')
                    {
                        if (_i >= _s.Length) throw new JsonException("Unterminated escape");
                        var e = _s[_i++];
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
                                if (_i + 4 > _s.Length) throw new JsonException("Bad \\u escape");
                                var hex = _s.Substring(_i, 4);
                                _i += 4;
                                if (!int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var cp))
                                    throw new JsonException("Bad \\u escape");
                                // Surrogate pair.
                                if (cp >= 0xD800 && cp <= 0xDBFF && _i + 6 <= _s.Length
                                    && _s[_i] == '\\' && _s[_i + 1] == 'u'
                                    && int.TryParse(_s.Substring(_i + 2, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var lo)
                                    && lo >= 0xDC00 && lo <= 0xDFFF)
                                {
                                    _i += 6;
                                    sb.Append(char.ConvertFromUtf32(0x10000 + ((cp - 0xD800) << 10) + (lo - 0xDC00)));
                                }
                                else sb.Append(char.ConvertFromUtf32(cp));
                                break;
                            default: throw new JsonException($"Bad escape '\\{e}'");
                        }
                    }
                    else sb.Append(c);
                }
            }

            private object ParseNumber()
            {
                var start = _i;
                if (Peek() == '-') _i++;
                while (_i < _s.Length && _s[_i] >= '0' && _s[_i] <= '9') _i++;
                var isFloat = false;
                if (_i < _s.Length && _s[_i] == '.') { isFloat = true; _i++; while (_i < _s.Length && _s[_i] >= '0' && _s[_i] <= '9') _i++; }
                if (_i < _s.Length && (_s[_i] == 'e' || _s[_i] == 'E'))
                {
                    isFloat = true; _i++;
                    if (_i < _s.Length && (_s[_i] == '+' || _s[_i] == '-')) _i++;
                    while (_i < _s.Length && _s[_i] >= '0' && _s[_i] <= '9') _i++;
                }
                var raw = _s.Substring(start, _i - start);
                if (!isFloat && long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l))
                    return l;
                if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
                    return d;
                throw new JsonException($"Bad number '{raw}'");
            }
        }
    }

    /// <summary>Typed accessors over a parsed JSON object.</summary>
    internal static class JsonReq
    {
        public static string Str(Dictionary<string, object?> o, string key)
        {
            if (o.TryGetValue(key, out var v) && v is string s) return s;
            throw new JsonLite.JsonException($"Missing string '{key}'");
        }

        public static string? OptStr(Dictionary<string, object?> o, string key)
        {
            if (o.TryGetValue(key, out var v) && v is string s) return s;
            return null;
        }

        public static long Long(Dictionary<string, object?> o, string key)
        {
            if (o.TryGetValue(key, out var v))
            {
                if (v is long l) return l;
                if (v is double d) return (long)d;
            }
            throw new JsonLite.JsonException($"Missing number '{key}'");
        }

        public static long OptLong(Dictionary<string, object?> o, string key, long def = 0)
        {
            if (o.TryGetValue(key, out var v))
            {
                if (v is long l) return l;
                if (v is double d) return (long)d;
            }
            return def;
        }

        public static Dictionary<string, object?> Obj(Dictionary<string, object?> o, string key)
        {
            if (o.TryGetValue(key, out var v) && v is Dictionary<string, object?> d) return d;
            throw new JsonLite.JsonException($"Missing object '{key}'");
        }

        public static List<object?> Arr(Dictionary<string, object?> o, string key)
        {
            if (o.TryGetValue(key, out var v) && v is List<object?> a) return a;
            throw new JsonLite.JsonException($"Missing array '{key}'");
        }
    }
}
