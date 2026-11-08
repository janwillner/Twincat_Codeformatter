using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using TcFormatter.Lexing;

namespace TcFormatter.Formatting
{
    /// <summary>
    /// Black-style safety net: the formatted text must consist of the same significant tokens as the input.
    /// Only the differences the formatter is allowed to make are tolerated:
    /// whitespace between tokens, casing of keywords / types / literals, trailing whitespace and the space after
    /// "//" in line comments, line endings inside block comments, and the semicolon after END_xx when
    /// <see cref="FormatterOptions.EndKeywordSemicolon"/> asks to add or remove it.
    /// Identifiers, strings, pragmas and comment contents must stay byte-identical.
    /// </summary>
    internal static class Equivalence
    {
        public static string FindDifference(string original, string formatted, FormatterOptions options)
        {
            bool semicolonMayChange = options != null && options.EndKeywordSemicolon != SemicolonStyle.Preserve;
            var a = Normalize(original, semicolonMayChange);
            var b = Normalize(formatted, semicolonMayChange);
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

        private static List<string> Normalize(string text, bool ignoreEndSemicolons)
        {
            var result = new List<string>();
            string previousKeyword = null;
            foreach (var token in Lexer.Tokenize(text).Where(t => !t.IsTrivia))
            {
                if (ignoreEndSemicolons && token.Text == ";" && previousKeyword != null &&
                    StLanguage.EndKeywordsWithOptionalSemicolon.Contains(previousKeyword))
                {
                    previousKeyword = null;
                    continue;
                }

                result.Add(token.Kind + ":" + Key(token));
                previousKeyword = token.Kind == TokenKind.Keyword ? token.Text : null;
            }
            return result;
        }

        private static string Key(Token token)
        {
            switch (token.Kind)
            {
                case TokenKind.Keyword:
                case TokenKind.TypeName:
                case TokenKind.Number:
                case TokenKind.TimeLiteral:
                case TokenKind.DirectAddress:
                case TokenKind.Operator:
                    return token.Text.ToUpperInvariant();
                case TokenKind.LineComment:
                    string comment = token.Text.TrimEnd();
                    if (comment.Length > 3 && comment[2] == ' ' && char.IsLetterOrDigit(comment[3]))
                        comment = "//" + comment.Substring(3);
                    return comment;
                case TokenKind.BlockComment:
                    return Regex.Replace(token.Text, "\r\n|\r", "\n");
                default:
                    // identifiers, strings, pragmas, unknown characters: exact
                    return token.Text;
            }
        }
    }
}
