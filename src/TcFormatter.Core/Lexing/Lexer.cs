using System.Collections.Generic;

namespace TcFormatter.Lexing
{
    /// <summary>
    /// Lossless lexer for TwinCAT Structured Text. Never throws: unknown characters become
    /// <see cref="TokenKind.Unknown"/> tokens and unterminated constructs run to the end of input.
    /// </summary>
    public static class Lexer
    {
        public static IReadOnlyList<Token> Tokenize(string source)
        {
            var tokens = new List<Token>();
            if (string.IsNullOrEmpty(source))
                return tokens;

            int i = 0;
            int n = source.Length;
            Token lastSignificant = null;

            while (i < n)
            {
                int start = i;
                char c = source[i];
                TokenKind kind;

                if (c == '\r' || c == '\n')
                {
                    i += (c == '\r' && i + 1 < n && source[i + 1] == '\n') ? 2 : 1;
                    kind = TokenKind.Newline;
                }
                else if (c == ' ' || c == '\t' || c == '\f' || c == '\v' || c == ' ')
                {
                    while (i < n && (source[i] == ' ' || source[i] == '\t' || source[i] == '\f' || source[i] == '\v' || source[i] == ' '))
                        i++;
                    kind = TokenKind.Whitespace;
                }
                else if (c == '/' && Peek(source, i + 1) == '/')
                {
                    while (i < n && source[i] != '\r' && source[i] != '\n')
                        i++;
                    kind = TokenKind.LineComment;
                }
                else if (c == '(' && Peek(source, i + 1) == '*')
                {
                    i = SkipNestedComment(source, i);
                    kind = TokenKind.BlockComment;
                }
                else if (c == '/' && Peek(source, i + 1) == '*')
                {
                    int end = source.IndexOf("*/", i + 2, System.StringComparison.Ordinal);
                    i = end < 0 ? n : end + 2;
                    kind = TokenKind.BlockComment;
                }
                else if (c == '{')
                {
                    i = SkipPragma(source, i);
                    kind = TokenKind.Pragma;
                }
                else if (c == '\'' || c == '"')
                {
                    i = SkipString(source, i, c);
                    kind = TokenKind.String;
                }
                else if (c == '%' && i + 1 < n && "IQMiqm".IndexOf(source[i + 1]) >= 0)
                {
                    // direct address: %I*, %QX0.1, %MW10, %IB*
                    i += 2;
                    if (i < n && "XBWDLxbwdl".IndexOf(source[i]) >= 0)
                        i++;
                    if (i < n && source[i] == '*')
                        i++;
                    else
                        while (i < n && (char.IsDigit(source[i]) || (source[i] == '.' && i + 1 < n && char.IsDigit(source[i + 1]))))
                            i++;
                    kind = TokenKind.DirectAddress;
                }
                else if (char.IsDigit(c))
                {
                    i = SkipNumber(source, i);
                    kind = TokenKind.Number;
                }
                else if (IsIdentStart(c))
                {
                    while (i < n && IsIdentPart(source[i]))
                        i++;
                    string word = source.Substring(start, i - start);
                    kind = ClassifyWord(source, word, ref i, lastSignificant);
                }
                else
                {
                    i = SkipOperator(source, i);
                    kind = i > start ? TokenKind.Operator : TokenKind.Unknown;
                    if (kind == TokenKind.Unknown)
                        i = start + 1;
                }

                var token = new Token(kind, source.Substring(start, i - start), start);
                tokens.Add(token);
                if (!token.IsTrivia && !token.IsComment && kind != TokenKind.Pragma)
                    lastSignificant = token;
            }

            return tokens;
        }

        private static TokenKind ClassifyWord(string source, string word, ref int i, Token lastSignificant)
        {
            int n = source.Length;

            if (Peek(source, i) == '#')
            {
                // Time / date literals: T#1s, DT#2024-01-01-12:00:00, ...
                if (StLanguage.DurationPrefixes.Contains(word) || StLanguage.DatePrefixes.Contains(word))
                {
                    bool isDate = StLanguage.DatePrefixes.Contains(word);
                    int j = i + 1;
                    if (j < n && (source[j] == '-' || source[j] == '+'))
                        j++;
                    while (j < n && (char.IsLetterOrDigit(source[j]) || source[j] == '_' || source[j] == '.' || source[j] == ':' || (isDate && source[j] == '-')))
                        j++;
                    if (j > i + 1)
                    {
                        i = j;
                        return TokenKind.TimeLiteral;
                    }
                }

                // Typed literals: INT#5, INT#-5, INT#16#7F, BOOL#TRUE, REAL#1.5
                if (StLanguage.IsTypeName(word))
                {
                    int j = i + 1;
                    if (j < n && (source[j] == '-' || source[j] == '+'))
                        j++;
                    if (j < n && char.IsDigit(source[j]))
                    {
                        i = SkipNumber(source, j);
                        return TokenKind.Number;
                    }
                    if (j < n && IsIdentStart(source[j]))
                    {
                        int k = j;
                        while (k < n && IsIdentPart(source[k]))
                            k++;
                        string value = source.Substring(j, k - j);
                        if (value.Equals("TRUE", System.StringComparison.OrdinalIgnoreCase) || value.Equals("FALSE", System.StringComparison.OrdinalIgnoreCase))
                        {
                            i = k;
                            return TokenKind.Number;
                        }
                    }
                }
            }

            // Set / reset / reference assignment operators: x S= y; x R= y; ref REF= x;
            if (Peek(source, i) == '=' && Peek(source, i + 1) != '>' &&
                (word.Equals("S", System.StringComparison.OrdinalIgnoreCase) ||
                 word.Equals("R", System.StringComparison.OrdinalIgnoreCase) ||
                 word.Equals("REF", System.StringComparison.OrdinalIgnoreCase)) &&
                lastSignificant != null && EndsOperand(lastSignificant))
            {
                i++;
                return TokenKind.Operator;
            }

            if (StLanguage.IsKeyword(word))
                return TokenKind.Keyword;
            if (StLanguage.IsTypeName(word))
                return TokenKind.TypeName;
            return TokenKind.Identifier;
        }

        private static bool EndsOperand(Token token) =>
            token.Kind == TokenKind.Identifier ||
            (token.Kind == TokenKind.Operator && (token.Text == ")" || token.Text == "]" || token.Text == "^"));

        private static int SkipNestedComment(string s, int i)
        {
            int depth = 0;
            int n = s.Length;
            while (i < n)
            {
                if (s[i] == '(' && Peek(s, i + 1) == '*')
                {
                    depth++;
                    i += 2;
                }
                else if (s[i] == '*' && Peek(s, i + 1) == ')')
                {
                    depth--;
                    i += 2;
                    if (depth == 0)
                        return i;
                }
                else
                {
                    i++;
                }
            }
            return n;
        }

        private static int SkipPragma(string s, int i)
        {
            int n = s.Length;
            i++;
            while (i < n)
            {
                char c = s[i];
                if (c == '}')
                    return i + 1;
                if (c == '\'' || c == '"')
                {
                    i = SkipString(s, i, c);
                    continue;
                }
                i++;
            }
            return n;
        }

        private static int SkipString(string s, int i, char quote)
        {
            int n = s.Length;
            i++;
            while (i < n)
            {
                char c = s[i];
                if (c == '$')
                {
                    i += 2;
                    continue;
                }
                if (c == quote)
                    return i + 1;
                if (c == '\r' || c == '\n')
                    return i; // unterminated: stop at end of line
                i++;
            }
            return n;
        }

        private static int SkipNumber(string s, int i)
        {
            int n = s.Length;
            while (i < n && (char.IsDigit(s[i]) || s[i] == '_'))
                i++;

            // based literal 16#FF, 2#1010, 8#17
            if (i < n && s[i] == '#' && i + 1 < n && IsHexDigit(s[i + 1]))
            {
                i++;
                while (i < n && (IsHexDigit(s[i]) || s[i] == '_'))
                    i++;
                return i;
            }

            // fraction (but not range operator "..")
            if (i + 1 < n && s[i] == '.' && char.IsDigit(s[i + 1]))
            {
                i++;
                while (i < n && (char.IsDigit(s[i]) || s[i] == '_'))
                    i++;
            }

            // exponent
            if (i < n && (s[i] == 'e' || s[i] == 'E'))
            {
                int j = i + 1;
                if (j < n && (s[j] == '+' || s[j] == '-'))
                    j++;
                if (j < n && char.IsDigit(s[j]))
                {
                    i = j;
                    while (i < n && char.IsDigit(s[i]))
                        i++;
                }
            }
            return i;
        }

        private static readonly string[] MultiCharOperators = { ":=", "=>", "<=", ">=", "<>", "**", ".." };

        private static int SkipOperator(string s, int i)
        {
            foreach (var op in MultiCharOperators)
            {
                if (string.CompareOrdinal(s, i, op, 0, op.Length) == 0)
                    return i + op.Length;
            }
            switch (s[i])
            {
                case '+': case '-': case '*': case '/': case '<': case '>': case '=': case '&':
                case '(': case ')': case '[': case ']': case ',': case ';': case ':': case '.':
                case '^': case '#':
                    return i + 1;
                default:
                    return i;
            }
        }

        private static char Peek(string s, int i) => i < s.Length ? s[i] : '\0';

        private static bool IsIdentStart(char c) => char.IsLetter(c) || c == '_';

        private static bool IsIdentPart(char c) => char.IsLetterOrDigit(c) || c == '_';

        private static bool IsHexDigit(char c) =>
            (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
    }
}
