using System.Collections.Generic;
using System.Linq;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using TcFormatter.Lexing;
using TcFormatter.Syntax.Generated;

namespace TcFormatter.Syntax
{
    /// <summary>
    /// The tokens of a parsed code part and the roles the parse tree gives them. The formatter reads these
    /// roles instead of guessing them from neighbouring tokens or from how a word is spelled.
    /// </summary>
    internal sealed class TokenRoles
    {
        private TokenRoles(List<Token> tokens)
        {
            Tokens = tokens;
        }

        /// <summary>
        /// All tokens of the source in order (including whitespace, comments and pragmas). Keywords and type names
        /// that the parse tree uses as names ("E_Cmd.Repeat", "stLog.Time") are <see cref="TokenKind.Identifier"/>.
        /// </summary>
        public List<Token> Tokens { get; }

        /// <summary>Prefix '+' and '-' (sign of an operand), not the binary operators.</summary>
        public HashSet<Token> UnaryOperators { get; } = new HashSet<Token>();

        /// <summary>First token of the labels of a CASE branch ("1, 2:", "E_State.Idle:") → the ':' that ends them.</summary>
        public Dictionary<Token, Token> CaseLabelColonByFirstToken { get; } = new Dictionary<Token, Token>();

        /// <summary>Name of a jump label ("lbl_End:") → its ':'.</summary>
        public Dictionary<Token, Token> JumpLabelColonByName { get; } = new Dictionary<Token, Token>();

        /// <summary>':=' and '=>' of named call arguments, FB initialisation, struct initialisation and enum values.</summary>
        public HashSet<Token> ArgumentAssignments { get; } = new HashSet<Token>();

        /// <summary>
        /// Keywords that open, continue or close a block: IF THEN ELSIF ELSE END_IF, CASE OF ELSE END_CASE, FOR DO END_FOR,
        /// WHILE DO END_WHILE, REPEAT UNTIL END_REPEAT, __TRY __CATCH __FINALLY __ENDTRY, VAR... END_VAR, TYPE END_TYPE,
        /// STRUCT END_STRUCT, UNION END_UNION. The keyword itself names the kind of block.
        /// </summary>
        public HashSet<Token> BlockKeywords { get; } = new HashSet<Token>();

        /// <summary>The ';' that end a statement, a declaration or a block (END_IF;).</summary>
        public HashSet<Token> StatementEnds { get; } = new HashSet<Token>();

        public static TokenRoles From(ParseResult parse)
        {
            var collector = new Collector();
            ParseTreeWalker.Default.Walk(collector, parse.Tree);

            var tokens = new List<Token>(parse.Tokens.Count);
            var byIndex = new Dictionary<int, Token>(parse.Tokens.Count);
            foreach (var t in parse.Tokens)
            {
                if (t.Type == TokenConstants.EOF)
                    continue;
                var token = Lexing.Lexer.Convert(t);
                if (collector.Names.Contains(t.TokenIndex) && (token.Kind == TokenKind.Keyword || token.Kind == TokenKind.TypeName))
                    token = new Token(TokenKind.Identifier, token.Text, token.Position);
                tokens.Add(token);
                byIndex[t.TokenIndex] = token;
            }

            var roles = new TokenRoles(tokens);
            Fill(roles.UnaryOperators, collector.UnaryOperators, byIndex);
            Fill(roles.ArgumentAssignments, collector.ArgumentAssignments, byIndex);
            Fill(roles.BlockKeywords, collector.BlockKeywords, byIndex);
            Fill(roles.StatementEnds, collector.StatementEnds, byIndex);
            Fill(roles.CaseLabelColonByFirstToken, collector.CaseLabelColons, byIndex);
            Fill(roles.JumpLabelColonByName, collector.JumpLabelColons, byIndex);
            return roles;
        }

        private static void Fill(HashSet<Token> target, HashSet<int> indexes, Dictionary<int, Token> byIndex)
        {
            foreach (int i in indexes)
                target.Add(byIndex[i]);
        }

        private static void Fill(Dictionary<Token, Token> target, Dictionary<int, int> indexes, Dictionary<int, Token> byIndex)
        {
            foreach (var pair in indexes)
                target[byIndex[pair.Key]] = byIndex[pair.Value];
        }

        /// <summary>Collects token indexes per role while walking the parse tree.</summary>
        private sealed class Collector : StParserBaseListener
        {
            public HashSet<int> UnaryOperators { get; } = new HashSet<int>();
            public HashSet<int> ArgumentAssignments { get; } = new HashSet<int>();
            public HashSet<int> BlockKeywords { get; } = new HashSet<int>();
            public HashSet<int> StatementEnds { get; } = new HashSet<int>();
            public HashSet<int> Names { get; } = new HashSet<int>();

            /// <summary>Index of the first label token → index of the ':'.</summary>
            public Dictionary<int, int> CaseLabelColons { get; } = new Dictionary<int, int>();

            /// <summary>Index of the label name → index of the ':'.</summary>
            public Dictionary<int, int> JumpLabelColons { get; } = new Dictionary<int, int>();

            private static void Add(HashSet<int> set, ITerminalNode node)
            {
                if (node != null)
                    set.Add(node.Symbol.TokenIndex);
            }

            /// <summary>Adds the direct child tokens of <paramref name="context"/> that have one of <paramref name="types"/>.</summary>
            private static void AddChildren(HashSet<int> set, ParserRuleContext context, params int[] types)
            {
                foreach (var node in context.children?.OfType<ITerminalNode>() ?? Enumerable.Empty<ITerminalNode>())
                {
                    if (types.Contains(node.Symbol.Type))
                        set.Add(node.Symbol.TokenIndex);
                }
            }

            public override void VisitTerminal(ITerminalNode node)
            {
                if (node.Symbol.Type == StLexer.SEMI)
                    StatementEnds.Add(node.Symbol.TokenIndex);
            }

            public override void EnterUnaryExpr(StParser.UnaryExprContext context)
            {
                if (context.op.Type == StLexer.MINUS || context.op.Type == StLexer.PLUS)
                    UnaryOperators.Add(context.op.TokenIndex);
            }

            public override void EnterCaseBranch(StParser.CaseBranchContext context) =>
                CaseLabelColons[context.Start.TokenIndex] = context.COLON().Symbol.TokenIndex;

            public override void EnterLabelStatement(StParser.LabelStatementContext context) =>
                JumpLabelColons[context.Start.TokenIndex] = context.COLON().Symbol.TokenIndex;

            public override void EnterArgument(StParser.ArgumentContext context)
            {
                Add(ArgumentAssignments, context.ASSIGN());
                Add(ArgumentAssignments, context.OUTPUT_ASSIGN());
            }

            public override void EnterStructInitializerField(StParser.StructInitializerFieldContext context) =>
                Add(ArgumentAssignments, context.ASSIGN());

            public override void EnterEnumMember(StParser.EnumMemberContext context) =>
                Add(ArgumentAssignments, context.ASSIGN());

            // ---------------------------------------------------------------- block structure

            public override void EnterIfStatement(StParser.IfStatementContext context) =>
                AddChildren(BlockKeywords, context, StLexer.IF, StLexer.THEN, StLexer.ELSIF, StLexer.ELSE, StLexer.END_IF);

            public override void EnterCaseStatement(StParser.CaseStatementContext context) =>
                AddChildren(BlockKeywords, context, StLexer.CASE, StLexer.OF, StLexer.ELSE, StLexer.END_CASE);

            public override void EnterForStatement(StParser.ForStatementContext context) =>
                AddChildren(BlockKeywords, context, StLexer.FOR, StLexer.DO, StLexer.END_FOR);

            public override void EnterWhileStatement(StParser.WhileStatementContext context) =>
                AddChildren(BlockKeywords, context, StLexer.WHILE, StLexer.DO, StLexer.END_WHILE);

            public override void EnterRepeatStatement(StParser.RepeatStatementContext context) =>
                AddChildren(BlockKeywords, context, StLexer.REPEAT, StLexer.UNTIL, StLexer.END_REPEAT);

            public override void EnterTryStatement(StParser.TryStatementContext context) =>
                AddChildren(BlockKeywords, context, StLexer.TRY, StLexer.CATCH, StLexer.FINALLY, StLexer.ENDTRY);

            public override void EnterVarSection(StParser.VarSectionContext context)
            {
                BlockKeywords.Add(context.varSectionKeyword().Start.TokenIndex);
                AddChildren(BlockKeywords, context, StLexer.END_VAR);
            }

            public override void EnterTypeDeclaration(StParser.TypeDeclarationContext context) =>
                AddChildren(BlockKeywords, context, StLexer.TYPE, StLexer.END_TYPE);

            public override void EnterTypeBody(StParser.TypeBodyContext context) =>
                AddChildren(BlockKeywords, context, StLexer.STRUCT, StLexer.END_STRUCT, StLexer.UNION, StLexer.END_UNION);

            // ---------------------------------------------------------------- names

            /// <summary>A keyword or type name after '.' is a member name ("E_Cmd.Repeat", "stLog.Time").</summary>
            public override void EnterMemberName(StParser.MemberNameContext context)
            {
                if (context.name() == null && context.NUMBER() == null)
                    Names.Add(context.Start.TokenIndex);
            }
        }
    }
}
