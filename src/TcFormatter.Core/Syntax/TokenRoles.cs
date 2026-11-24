using System.Collections.Generic;
using System.Linq;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using TcFormatter.Lexing;
using TcFormatter.Syntax.Generated;

namespace TcFormatter.Syntax
{
    /// <summary>A list in parentheses or brackets: its brackets, the ';' that ends its line and the first and last token of each element.</summary>
    internal sealed class ListSpan
    {
        public ListSpan(Token open, Token close, Token end, List<(Token First, Token Last)> items)
        {
            Open = open;
            Close = close;
            End = end;
            Items = items;
        }

        public Token Open { get; }
        public Token Close { get; }
        public Token End { get; }
        public List<(Token First, Token Last)> Items { get; }
    }

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

        /// <summary>The ':' of CASE labels (values of <see cref="CaseLabelColonByFirstToken"/>).</summary>
        public HashSet<Token> CaseLabelColons { get; } = new HashSet<Token>();

        /// <summary>The ':' of jump labels (values of <see cref="JumpLabelColonByName"/>).</summary>
        public HashSet<Token> JumpLabelColons { get; } = new HashSet<Token>();

        /// <summary>
        /// Variable declarations "names : type := init;" by their first token: the ':', the ':=' of the initial value
        /// (null without one, or for REF=) and the ';'.
        /// </summary>
        public Dictionary<Token, (Token Colon, Token Assign, Token End)> Declarations { get; } =
            new Dictionary<Token, (Token, Token, Token)>();

        /// <summary>Assignment statements "target := value;" by their first token: the ':=' and the ';'.</summary>
        public Dictionary<Token, (Token Operator, Token End)> Assignments { get; } = new Dictionary<Token, (Token, Token)>();

        /// <summary>Keywords that are part of an expression (AND, NOT, MOD, TRUE, THIS, __NEW, __DELETE, ...).</summary>
        public HashSet<Token> ExpressionKeywords { get; } = new HashSet<Token>();

        /// <summary>The first keyword of a POU header (FUNCTION_BLOCK, METHOD, PROPERTY, ...).</summary>
        public HashSet<Token> PouHeaderKeywords { get; } = new HashSet<Token>();

        /// <summary>The keyword that opens a VAR section (VAR, VAR_INPUT, VAR_GLOBAL, ...).</summary>
        public HashSet<Token> VarSectionKeywords { get; } = new HashSet<Token>();

        /// <summary>CONSTANT, RETAIN and PERSISTENT after a VAR section keyword.</summary>
        public HashSet<Token> VarModifiers { get; } = new HashSet<Token>();

        /// <summary>
        /// Lists that the formatter may wrap one element per line, by the first token of their statement, declaration or
        /// type definition: the arguments of a call statement, the struct / array / FB initialisation of a declaration
        /// and the values of an enum.
        /// </summary>
        public Dictionary<Token, ListSpan> Calls { get; } = new Dictionary<Token, ListSpan>();

        /// <inheritdoc cref="Calls"/>
        public Dictionary<Token, ListSpan> Initializers { get; } = new Dictionary<Token, ListSpan>();

        /// <inheritdoc cref="Calls"/>
        public Dictionary<Token, ListSpan> Enums { get; } = new Dictionary<Token, ListSpan>();

        /// <summary>Values of enum types by their name: the ':=' (null without a value) and the last token of the value.</summary>
        public Dictionary<Token, (Token Assign, Token Last)> EnumMembers { get; } = new Dictionary<Token, (Token, Token)>();

        /// <summary>TYPE keywords and the names of type definitions (where a type definition line starts).</summary>
        public HashSet<Token> TypeDefinitionStarts { get; } = new HashSet<Token>();

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
            Fill(roles.ExpressionKeywords, collector.ExpressionKeywords, byIndex);
            Fill(roles.PouHeaderKeywords, collector.PouHeaderKeywords, byIndex);
            Fill(roles.VarSectionKeywords, collector.VarSectionKeywords, byIndex);
            Fill(roles.VarModifiers, collector.VarModifiers, byIndex);
            Fill(roles.TypeDefinitionStarts, collector.TypeDefinitionStarts, byIndex);
            FillLists(roles.Calls, collector.Calls, byIndex);
            FillLists(roles.Initializers, collector.Initializers, byIndex);
            FillLists(roles.Enums, collector.Enums, byIndex);
            foreach (var pair in collector.EnumMembers)
                roles.EnumMembers[byIndex[pair.Key]] = (pair.Value.Assign < 0 ? null : byIndex[pair.Value.Assign], byIndex[pair.Value.Last]);
            roles.CaseLabelColons.UnionWith(roles.CaseLabelColonByFirstToken.Values);
            roles.JumpLabelColons.UnionWith(roles.JumpLabelColonByName.Values);
            foreach (var pair in collector.Declarations)
            {
                var (colon, assign, end) = pair.Value;
                roles.Declarations[byIndex[pair.Key]] = (byIndex[colon], assign < 0 ? null : byIndex[assign], byIndex[end]);
            }
            foreach (var pair in collector.Assignments)
                roles.Assignments[byIndex[pair.Key]] = (byIndex[pair.Value.Operator], byIndex[pair.Value.End]);
            return roles;
        }

        private static void FillLists(Dictionary<Token, ListSpan> target, Dictionary<int, IndexSpan> spans, Dictionary<int, Token> byIndex)
        {
            foreach (var pair in spans)
            {
                var s = pair.Value;
                target[byIndex[pair.Key]] = new ListSpan(byIndex[s.Open], byIndex[s.Close], byIndex[s.End],
                    s.Items.Select(i => (byIndex[i.First], byIndex[i.Last])).ToList());
            }
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

        /// <summary>A <see cref="ListSpan"/> as token indexes, while the tree is walked.</summary>
        private sealed class IndexSpan
        {
            public IndexSpan(int open, int close, int end, List<(int First, int Last)> items)
            {
                Open = open;
                Close = close;
                End = end;
                Items = items;
            }

            public int Open { get; }
            public int Close { get; }
            public int End { get; }
            public List<(int First, int Last)> Items { get; }
        }

        /// <summary>Collects token indexes per role while walking the parse tree.</summary>
        private sealed class Collector : StParserBaseListener
        {
            public HashSet<int> UnaryOperators { get; } = new HashSet<int>();
            public HashSet<int> ArgumentAssignments { get; } = new HashSet<int>();
            public HashSet<int> BlockKeywords { get; } = new HashSet<int>();
            public HashSet<int> StatementEnds { get; } = new HashSet<int>();
            public HashSet<int> Names { get; } = new HashSet<int>();
            public HashSet<int> ExpressionKeywords { get; } = new HashSet<int>();
            public HashSet<int> PouHeaderKeywords { get; } = new HashSet<int>();
            public HashSet<int> VarSectionKeywords { get; } = new HashSet<int>();
            public HashSet<int> VarModifiers { get; } = new HashSet<int>();
            public HashSet<int> TypeDefinitionStarts { get; } = new HashSet<int>();
            public Dictionary<int, IndexSpan> Calls { get; } = new Dictionary<int, IndexSpan>();
            public Dictionary<int, IndexSpan> Initializers { get; } = new Dictionary<int, IndexSpan>();
            public Dictionary<int, IndexSpan> Enums { get; } = new Dictionary<int, IndexSpan>();
            public Dictionary<int, (int Assign, int Last)> EnumMembers { get; } = new Dictionary<int, (int, int)>();

            private static IndexSpan Span(IToken open, IToken close, IToken end, IEnumerable<ParserRuleContext> items) =>
                new IndexSpan(open.TokenIndex, close.TokenIndex, end.TokenIndex,
                    items.Select(i => (i.Start.TokenIndex, i.Stop.TokenIndex)).ToList());

            /// <summary>A list with a comma before its closing bracket stays as written (the layout would drop the comma).</summary>
            private static bool TrailingComma(ITerminalNode[] commas, int items) => commas.Length >= items;

            /// <summary>The call that a statement consists of: "callee(args);" or "target := callee(args);".</summary>
            private static IndexSpan CallSpan(StParser.ExpressionContext expression, IToken end)
            {
                if (!(expression is StParser.PrimaryExprContext primary))
                    return null;
                if (!(primary.postfixExpression().postfix().LastOrDefault() is StParser.CallContext call))
                    return null;
                var args = call.argumentList();
                if (args != null && TrailingComma(args.COMMA(), args.argument().Length))
                    return null;
                return Span(call.LPAREN().Symbol, call.RPAREN().Symbol, end,
                    (IEnumerable<ParserRuleContext>)args?.argument() ?? new ParserRuleContext[0]);
            }

            public override void EnterExpressionStatement(StParser.ExpressionStatementContext context)
            {
                var span = CallSpan(context.expression(), context.SEMI().Symbol);
                if (span != null)
                    Calls[context.Start.TokenIndex] = span;
            }

            /// <summary>"name : TYPE := (...);", "name : TYPE := [...];", "fb : FB_X(...);" or "e : (A, B);".</summary>
            private static IndexSpan InitializerSpan(StParser.VarDeclarationContext context)
            {
                var end = context.SEMI().Symbol;
                var init = context.initialization();
                if (init != null)
                {
                    if (init.ASSIGN() == null || !(init.expression() is StParser.PrimaryExprContext p) ||
                        p.postfixExpression().postfix().Length > 0)
                        return null;
                    var primary = p.postfixExpression().primary();
                    if (primary.structInitializer() != null)
                    {
                        var s = primary.structInitializer();
                        return Span(s.LPAREN().Symbol, s.RPAREN().Symbol, end, s.structInitializerField());
                    }
                    if (primary.arrayInitializer() != null)
                    {
                        var a = primary.arrayInitializer();
                        return Span(a.LBRACKET().Symbol, a.RBRACKET().Symbol, end, a.expression());
                    }
                    return null;
                }
                var type = context.dataType();
                if (type?.qualifiedName() != null && type.argumentList() != null)
                {
                    var args = type.argumentList();
                    return TrailingComma(args.COMMA(), args.argument().Length)
                        ? null
                        : Span(type.LPAREN().Symbol, type.RPAREN().Symbol, end, args.argument());
                }
                var e = context.enumBody();
                if (e != null && !TrailingComma(e.COMMA(), e.enumMember().Length))
                    return Span(e.LPAREN().Symbol, e.RPAREN().Symbol, end, e.enumMember());
                return null;
            }

            public override void EnterTypeDefinition(StParser.TypeDefinitionContext context)
            {
                TypeDefinitionStarts.Add(context.name().Start.TokenIndex);
                var e = context.typeBody().enumBody();
                if (e == null)
                    return;
                foreach (var member in e.enumMember())
                    EnumMembers[member.name().Start.TokenIndex] = (member.ASSIGN()?.Symbol.TokenIndex ?? -1, member.Stop.TokenIndex);

                // "TYPE name : (A, B := 2) [BASETYPE];": an initial value or a longer base type keep the layout as written
                bool simpleBase = e.dataType() == null || e.dataType().Start.TokenIndex == e.dataType().Stop.TokenIndex;
                if (e.initialization() == null && simpleBase && context.SEMI() != null && !TrailingComma(e.COMMA(), e.enumMember().Length))
                    Enums[context.name().Start.TokenIndex] = Span(e.LPAREN().Symbol, e.RPAREN().Symbol, context.SEMI().Symbol, e.enumMember());
            }

            public override void EnterPouHeader(StParser.PouHeaderContext context) =>
                PouHeaderKeywords.Add(context.Start.TokenIndex);

            public override void EnterVarSectionKeyword(StParser.VarSectionKeywordContext context) =>
                VarSectionKeywords.Add(context.Start.TokenIndex);

            public override void EnterVarModifier(StParser.VarModifierContext context) =>
                VarModifiers.Add(context.Start.TokenIndex);

            private static bool InExpression(IParseTree node)
            {
                for (var parent = node.Parent; parent != null; parent = parent.Parent)
                {
                    if (parent is StParser.ExpressionContext)
                        return true;
                }
                return false;
            }

            /// <summary>Index of the first label token → index of the ':'.</summary>
            public Dictionary<int, int> CaseLabelColons { get; } = new Dictionary<int, int>();

            /// <summary>Index of the label name → index of the ':'.</summary>
            public Dictionary<int, int> JumpLabelColons { get; } = new Dictionary<int, int>();

            /// <summary>Index of the first token of a declaration → indexes of ':', ':=' (-1) and ';'.</summary>
            public Dictionary<int, (int Colon, int Assign, int End)> Declarations { get; } = new Dictionary<int, (int, int, int)>();

            /// <summary>Index of the first token of an assignment → indexes of ':=' and ';'.</summary>
            public Dictionary<int, (int Operator, int End)> Assignments { get; } = new Dictionary<int, (int, int)>();

            public override void EnterVarDeclaration(StParser.VarDeclarationContext context)
            {
                if (context.COLON() == null)
                    return; // empty declaration ";"
                var assign = context.initialization()?.ASSIGN();
                Declarations[context.Start.TokenIndex] = (context.COLON().Symbol.TokenIndex,
                    assign?.Symbol.TokenIndex ?? -1, context.SEMI().Symbol.TokenIndex);
                var list = InitializerSpan(context);
                if (list != null && list.Items.Count > 0)
                    Initializers[context.Start.TokenIndex] = list;
            }

            public override void EnterAssignmentStatement(StParser.AssignmentStatementContext context)
            {
                var op = context.assignmentOperator().ASSIGN();
                if (op != null)
                    Assignments[context.Start.TokenIndex] = (op.Symbol.TokenIndex, context.SEMI().Symbol.TokenIndex);
                var call = op == null ? null : CallSpan(context.expression(1), context.SEMI().Symbol);
                if (call != null)
                    Calls[context.Start.TokenIndex] = call;
            }

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
                else if (Lexing.Lexer.KindOf(node.Symbol.Type) == TokenKind.Keyword && InExpression(node))
                    ExpressionKeywords.Add(node.Symbol.TokenIndex);
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

            public override void EnterTypeDeclaration(StParser.TypeDeclarationContext context)
            {
                AddChildren(BlockKeywords, context, StLexer.TYPE, StLexer.END_TYPE);
                TypeDefinitionStarts.Add(context.TYPE().Symbol.TokenIndex);
            }

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
