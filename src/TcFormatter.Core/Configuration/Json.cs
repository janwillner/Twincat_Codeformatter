using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace TcFormatter.Configuration
{
    public sealed class ConfigException : Exception
    {
        public ConfigException(string message) : base(message) { }
    }

    /// <summary>
    /// Minimal JSON (with // and /* */ comments) reader. Objects become ordered lists of key/value pairs,
    /// arrays become <see cref="List{Object}"/>, numbers become double. Kept dependency free on purpose so the
    /// extension does not ship assemblies that might clash with the ones loaded by Visual Studio.
    /// </summary>
    internal sealed class Json
    {
        private readonly string _s;
        private int _i;

        private Json(string s) => _s = s;

        public static object Parse(string text)
        {
            var parser = new Json(text ?? string.Empty);
            parser.SkipWhitespace();
            var value = parser.ReadValue();
            parser.SkipWhitespace();
            if (parser._i < parser._s.Length)
                parser.Fail("unexpected content after the end of the document");
            return value;
        }

        private object ReadValue()
        {
            SkipWhitespace();
            if (_i >= _s.Length)
                Fail("unexpected end of document");
            char c = _s[_i];
            switch (c)
            {
                case '{': return ReadObject();
                case '[': return ReadArray();
                case '"': return ReadString();
                case 't': Expect("true"); return true;
                case 'f': Expect("false"); return false;
                case 'n': Expect("null"); return null;
            }
            if (c == '-' || char.IsDigit(c))
                return ReadNumber();
            Fail($"unexpected character '{c}'");
            return null;
        }

        private List<KeyValuePair<string, object>> ReadObject()
        {
            var result = new List<KeyValuePair<string, object>>();
            _i++;
            SkipWhitespace();
            if (Peek() == '}')
            {
                _i++;
                return result;
            }
            while (true)
            {
                SkipWhitespace();
                if (Peek() != '"')
                    Fail("expected a property name in double quotes");
                string key = ReadString();
                SkipWhitespace();
                if (Peek() != ':')
                    Fail("expected ':'");
                _i++;
                var value = ReadValue();
                result.Add(new KeyValuePair<string, object>(key, value));
                SkipWhitespace();
                char c = Peek();
                _i++;
                if (c == '}')
                    return result;
                if (c != ',')
                    Fail("expected ',' or '}'");
                SkipWhitespace();
                if (Peek() == '}')
                {
                    _i++; // trailing comma
                    return result;
                }
            }
        }

        private List<object> ReadArray()
        {
            var result = new List<object>();
            _i++;
            SkipWhitespace();
            if (Peek() == ']')
            {
                _i++;
                return result;
            }
            while (true)
            {
                result.Add(ReadValue());
                SkipWhitespace();
                char c = Peek();
                _i++;
                if (c == ']')
                    return result;
                if (c != ',')
                    Fail("expected ',' or ']'");
                SkipWhitespace();
                if (Peek() == ']')
                {
                    _i++;
                    return result;
                }
            }
        }

        private string ReadString()
        {
            var sb = new StringBuilder();
            _i++;
            while (_i < _s.Length)
            {
                char c = _s[_i++];
                if (c == '"')
                    return sb.ToString();
                if (c != '\\')
                {
                    sb.Append(c);
                    continue;
                }
                if (_i >= _s.Length)
                    break;
                char e = _s[_i++];
                switch (e)
                {
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'u':
                        if (_i + 4 > _s.Length)
                            Fail("invalid unicode escape");
                        sb.Append((char)int.Parse(_s.Substring(_i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                        _i += 4;
                        break;
                    default: sb.Append(e); break;
                }
            }
            Fail("unterminated string");
            return null;
        }

        private double ReadNumber()
        {
            int start = _i;
            while (_i < _s.Length && "+-0123456789.eE".IndexOf(_s[_i]) >= 0)
                _i++;
            if (!double.TryParse(_s.Substring(start, _i - start), NumberStyles.Float, CultureInfo.InvariantCulture, out double d))
            {
                _i = start;
                Fail("invalid number");
            }
            return d;
        }

        private void Expect(string word)
        {
            if (string.CompareOrdinal(_s, _i, word, 0, word.Length) != 0)
                Fail($"expected '{word}'");
            _i += word.Length;
        }

        private char Peek() => _i < _s.Length ? _s[_i] : '\0';

        private void SkipWhitespace()
        {
            while (_i < _s.Length)
            {
                char c = _s[_i];
                if (char.IsWhiteSpace(c) || c == '﻿')
                {
                    _i++;
                }
                else if (c == '/' && _i + 1 < _s.Length && _s[_i + 1] == '/')
                {
                    while (_i < _s.Length && _s[_i] != '\n')
                        _i++;
                }
                else if (c == '/' && _i + 1 < _s.Length && _s[_i + 1] == '*')
                {
                    int end = _s.IndexOf("*/", _i + 2, StringComparison.Ordinal);
                    _i = end < 0 ? _s.Length : end + 2;
                }
                else
                {
                    return;
                }
            }
        }

        private void Fail(string message)
        {
            int line = 1, column = 1;
            for (int i = 0; i < _i && i < _s.Length; i++)
            {
                if (_s[i] == '\n')
                {
                    line++;
                    column = 1;
                }
                else
                {
                    column++;
                }
            }
            throw new ConfigException($"Invalid .tcformat.json at line {line}, column {column}: {message}.");
        }

        public static string Quote(string s)
        {
            var sb = new StringBuilder("\"");
            foreach (char c in s ?? string.Empty)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20)
                            sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else
                            sb.Append(c);
                        break;
                }
            }
            return sb.Append('"').ToString();
        }
    }
}
