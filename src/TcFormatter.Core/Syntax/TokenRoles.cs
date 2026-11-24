using System.Collections.Generic;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using TcFormatter.Lexing;
using TcFormatter.Syntax.Generated;

namespace TcFormatter.Syntax
{
    /// <summary>
    /// The tokens of a parsed code part and the roles the parse tree gives them. The formatter reads these
    /// roles instead of guessing them from neighbouring tokens.
    /// </summary>
    internal sealed class TokenRoles
    {
        private TokenRoles(List<Token> tokens)
        {
            Tokens = tokens;
        }

        /// <summary>All tokens of the source in order (including whitespace, comments and pragmas).</summary>
        public List<Token> Tokens { get; }

        /// <summary>Prefix '+' and '-' (sign of an operand), not the binary operators.</summary>
        public HashSet<Token> UnaryOperators { get; } = new HashSet<Token>();

        /// <summary>First token of the labels of a CASE branch ("1, 2:", "E_State.Idle:") → the ':' that ends them.</summary>
        public Dictionary<Token, Token> CaseLabelColons { get; } = new Dictionary<Token, Token>();

        /// <summary>Name of a jump label ("lbl_End:") → its ':'.</summary>
        public Dictionary<Token, Token> JumpLabelColons { get; } = new Dictionary<Token, Token>();

        /// <summary>':=' and '=>' of named call arguments, FB initialisation, struct initialisation and enum values.</summary>
        public HashSet<Token> ArgumentAssignments { get; } = new HashSet<Token>();

        public static TokenRoles From(ParseResult parse)
        {
            var tokens = new List<Token>(parse.Tokens.Count);
            var byIndex = new Dictionary<int, Token>(parse.Tokens.Count);
            foreach (var t in parse.Tokens)
            {
                if (t.Type == TokenConstants.EOF)
                    continue;
                var token = Lexing.Lexer.Convert(t);
                tokens.Add(token);
                byIndex[t.TokenIndex] = token;
            }

            var roles = new TokenRoles(tokens);
            ParseTreeWalker.Default.Walk(new Collector(roles, byIndex), parse.Tree);
            return roles;
        }

        private sealed class Collector : StParserBaseListener
        {
            private readonly TokenRoles _roles;
            private readonly Dictionary<int, Token> _byIndex;

            public Collector(TokenRoles roles, Dictionary<int, Token> byIndex)
            {
                _roles = roles;
                _byIndex = byIndex;
            }

            private void Add(HashSet<Token> set, IToken token)
            {
                if (token != null && _byIndex.TryGetValue(token.TokenIndex, out var t))
                    set.Add(t);
            }

            private void Add(HashSet<Token> set, ITerminalNode node) => Add(set, node?.Symbol);

            public override void EnterUnaryExpr(StParser.UnaryExprContext context)
            {
                if (context.op.Type == StLexer.MINUS || context.op.Type == StLexer.PLUS)
                    Add(_roles.UnaryOperators, context.op);
            }

            private void Add(Dictionary<Token, Token> map, IToken first, ITerminalNode colon)
            {
                if (colon != null && _byIndex.TryGetValue(first.TokenIndex, out var f) && _byIndex.TryGetValue(colon.Symbol.TokenIndex, out var c))
                    map[f] = c;
            }

            public override void EnterCaseBranch(StParser.CaseBranchContext context) =>
                Add(_roles.CaseLabelColons, context.Start, context.COLON());

            public override void EnterLabelStatement(StParser.LabelStatementContext context) =>
                Add(_roles.JumpLabelColons, context.Start, context.COLON());

            public override void EnterArgument(StParser.ArgumentContext context)
            {
                Add(_roles.ArgumentAssignments, context.ASSIGN());
                Add(_roles.ArgumentAssignments, context.OUTPUT_ASSIGN());
            }

            public override void EnterStructInitializerField(StParser.StructInitializerFieldContext context) =>
                Add(_roles.ArgumentAssignments, context.ASSIGN());

            public override void EnterEnumMember(StParser.EnumMemberContext context) =>
                Add(_roles.ArgumentAssignments, context.ASSIGN());
        }
    }
}
