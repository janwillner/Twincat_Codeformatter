using System.IO;
using Antlr4.Runtime;

namespace TcFormatter.Syntax.Generated
{
    /// <summary>
    /// Base class of the generated <see cref="StLexer"/>: remembers the last token on the default channel so that
    /// "S=", "R=" and "REF=" are only assignment operators directly after an operand.
    /// </summary>
    public abstract class StLexerBase : Lexer
    {
        private int _lastSignificantType = TokenConstants.InvalidType;

        protected StLexerBase(ICharStream input, TextWriter output, TextWriter errorOutput)
            : base(input, output, errorOutput)
        {
        }

        public override IToken NextToken()
        {
            var token = base.NextToken();
            if (token.Channel == TokenConstants.DefaultChannel)
                _lastSignificantType = token.Type;
            return token;
        }

        public override void Reset()
        {
            base.Reset();
            _lastSignificantType = TokenConstants.InvalidType;
        }

        /// <summary>
        /// Predicate of SET_ASSIGN, RESET_ASSIGN and REF_ASSIGN; LA(1) is the character after the '='. An operand ends
        /// with a name, ')', ']' or '^'; in a declaration "r : REFERENCE TO INT REF= n;" also with a type name.
        /// </summary>
        protected bool IsSetResetAssignment() =>
            InputStream.LA(1) != '>' &&
            (_lastSignificantType == StLexer.IDENTIFIER || _lastSignificantType == StLexer.RPAREN ||
             _lastSignificantType == StLexer.RBRACKET || _lastSignificantType == StLexer.CARET ||
             (_lastSignificantType >= StLexer.BOOL && _lastSignificantType <= StLexer.SYS_SYSTEM));
    }
}
