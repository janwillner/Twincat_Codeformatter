using System.Collections.Generic;
using Antlr4.Runtime;
using TcFormatter.Syntax;
using TcFormatter.Syntax.Generated;

namespace TcFormatter.Lexing
{
    /// <summary>
    /// Lossless lexer for TwinCAT Structured Text: an adapter from the ANTLR lexer (Grammar/StLexer.g4) to
    /// <see cref="Token"/>. Never throws: unknown characters become <see cref="TokenKind.Unknown"/> tokens and
    /// unterminated comments run to the end of input.
    /// </summary>
    public static class Lexer
    {
        public static IReadOnlyList<Token> Tokenize(string source)
        {
            var tokens = new List<Token>();
            if (string.IsNullOrEmpty(source))
                return tokens;

            foreach (var token in StSyntax.CreateLexer(source).GetAllTokens())
                tokens.Add(Convert(token));
            return tokens;
        }

        internal static Token Convert(IToken token) => new Token(KindOf(token.Type), token.Text, token.StartIndex);

        /// <summary>
        /// Maps an ANTLR token type to its kind. The ranges follow the order of the rules in StLexer.g4:
        /// keywords from IF to ENDTRY, elementary type names from BOOL to SYS_SYSTEM, operators from SET_ASSIGN to HASH.
        /// </summary>
        internal static TokenKind KindOf(int type)
        {
            switch (type)
            {
                case StLexer.NEWLINE: return TokenKind.Newline;
                case StLexer.WS: return TokenKind.Whitespace;
                case StLexer.LINE_COMMENT: return TokenKind.LineComment;
                case StLexer.BLOCK_COMMENT:
                case StLexer.C_COMMENT: return TokenKind.BlockComment;
                case StLexer.PRAGMA: return TokenKind.Pragma;
                case StLexer.DURATION_LITERAL:
                case StLexer.DATE_LITERAL: return TokenKind.TimeLiteral;
                case StLexer.TYPED_LITERAL:
                case StLexer.NUMBER: return TokenKind.Number;
                case StLexer.STRING_LITERAL:
                case StLexer.WSTRING_LITERAL: return TokenKind.String;
                case StLexer.DIRECT_ADDRESS: return TokenKind.DirectAddress;
                case StLexer.IDENTIFIER: return TokenKind.Identifier;
            }
            if (type >= StLexer.IF && type <= StLexer.ENDTRY)
                return TokenKind.Keyword;
            if (type >= StLexer.BOOL && type <= StLexer.SYS_SYSTEM)
                return TokenKind.TypeName;
            if (type >= StLexer.SET_ASSIGN && type <= StLexer.HASH)
                return TokenKind.Operator;
            return TokenKind.Unknown;
        }
    }
}
