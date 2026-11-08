using System.Collections.Generic;
using System.Linq;
using System.Text;
using TcFormatter.Lexing;

namespace TcFormatter.Formatting
{
    /// <summary>
    /// Black-style safety net: the formatted text must consist of the same significant tokens as the input.
    /// Allowed differences: whitespace, casing of everything except strings, whitespace inside comments and
    /// the optional semicolon after END_IF / END_CASE / END_FOR / END_WHILE / END_REPEAT.
    /// </summary>
    internal static class Equivalence
    {
        private static readonly HashSet<string> EndKeywords = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
        {
            "END_IF", "END_CASE", "END_FOR", "END_WHILE", "END_REPEAT",
        };

        public static string FindDifference(string original, string formatted)
        {
            var a = Normalize(original);
            var b = Normalize(formatted);
            int n = System.Math.Min(a.Count, b.Count);
            for (int i = 0; i < n; i++)
            {
                if (a[i] != b[i])
                    return $"token #{i}: '{a[i]}' became '{b[i]}'";
            }
            if (a.Count != b.Count)
                return $"token count changed from {a.Count} to {b.Count}";
            return null;
        }

        private static List<string> Normalize(string text)
        {
            var result = new List<string>();
            string previous = null;
            foreach (var token in Lexer.Tokenize(text).Where(t => !t.IsTrivia))
            {
                string key;
                switch (token.Kind)
                {
                    case TokenKind.String:
                        key = "S:" + token.Text;
                        break;
                    case TokenKind.LineComment:
                    case TokenKind.BlockComment:
                        key = "C:" + RemoveWhitespace(token.Text);
                        break;
                    default:
                        key = token.Kind + ":" + token.Text.ToUpperInvariant();
                        break;
                }

                // optional semicolon after END_xx
                if (token.Text == ";" && previous != null && EndKeywords.Contains(previous))
                {
                    previous = null;
                    continue;
                }

                result.Add(key);
                previous = token.Kind == TokenKind.Keyword ? token.Text : null;
            }
            return result;
        }

        private static string RemoveWhitespace(string s)
        {
            var sb = new StringBuilder(s.Length);
            foreach (char c in s)
            {
                if (!char.IsWhiteSpace(c))
                    sb.Append(c);
            }
            return sb.ToString();
        }
    }
}
