namespace TcFormatter.Lexing
{
    public enum TokenKind
    {
        Whitespace,
        Newline,
        LineComment,
        BlockComment,
        Pragma,
        Keyword,
        TypeName,
        Identifier,
        Number,
        TimeLiteral,
        String,
        DirectAddress,
        Operator,
        Unknown,
    }

    /// <summary>A lexical token. Concatenating the <see cref="Text"/> of all tokens reproduces the source exactly.</summary>
    public sealed class Token
    {
        public Token(TokenKind kind, string text, int position)
        {
            Kind = kind;
            Text = text;
            Position = position;
        }

        public TokenKind Kind { get; }
        public string Text { get; }

        /// <summary>Character offset in the source text.</summary>
        public int Position { get; }

        public bool IsTrivia => Kind == TokenKind.Whitespace || Kind == TokenKind.Newline;

        public bool IsComment => Kind == TokenKind.LineComment || Kind == TokenKind.BlockComment;

        public string Upper => Text.ToUpperInvariant();

        public bool Is(string text) => string.Equals(Text, text, System.StringComparison.OrdinalIgnoreCase);

        public bool IsKeyword(string keyword) => Kind == TokenKind.Keyword && Is(keyword);

        public bool IsOperator(string op) => Kind == TokenKind.Operator && Text == op;

        public override string ToString() => Kind + ":" + Text;
    }
}
