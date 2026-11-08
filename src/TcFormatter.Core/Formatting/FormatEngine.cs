using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using TcFormatter.Lexing;

namespace TcFormatter.Formatting
{
    /// <summary>
    /// Token based formatter. Keeps the user's line structure (except for statement splitting, joining of
    /// wrapped lists and the line break options) and recomputes indentation, spacing, casing, alignment and
    /// blank lines.
    /// </summary>
    internal sealed class FormatEngine
    {
        // "tcfmt: off" / "tcfmt: on" and STweep's conditional formatting directives {STweep.Disable} / {STweep.Enable}
        private static readonly Regex FormatOff = new Regex(@"tcfmt\s*:\s*off\b|STweep\.Disable\b", RegexOptions.IgnoreCase);
        private static readonly Regex FormatOn = new Regex(@"tcfmt\s*:\s*on\b|STweep\.Enable\b", RegexOptions.IgnoreCase);

        /// <summary>Keywords allowed inside statements that may be joined onto one line.</summary>
        private static readonly HashSet<string> ExpressionKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "AND", "OR", "XOR", "NOT", "MOD", "AND_THEN", "OR_ELSE", "TRUE", "FALSE", "THIS", "SUPER",
            "__NEW", "__QUERYINTERFACE", "__QUERYPOINTER", "__ISVALIDREF",
        };

        private readonly string _source;
        private readonly CodePart _part;
        private readonly FormatterOptions _o;

        private readonly List<Frame> _stack = new List<Frame>();

        /// <summary>Open parentheses and brackets.</summary>
        private int _paren;

        /// <summary>The current statement is not terminated yet (following lines are continuations).</summary>
        private bool _open;

        /// <summary>The open statement is a condition (IF / ELSIF / WHILE / UNTIL) that has not reached THEN / DO yet.</summary>
        private bool _conditionOpen;

        /// <summary>Indentation level and first line of the statement that is currently open.</summary>
        private int _statementLevel;
        private Line _statementHead;

        private Token _prevSignificant;
        private int _frameIds;

        public FormatEngine(string source, CodePart part, FormatterOptions options)
        {
            _source = source;
            _part = part;
            _o = options;
        }

        internal HashSet<Token> UnaryOperators { get; } = new HashSet<Token>();

        internal HashSet<Token> CaseLabelColons { get; } = new HashSet<Token>();

        /// <summary>':=' and '=>' inside parentheses (named call arguments, FB initialisation).</summary>
        internal HashSet<Token> ArgumentAssignments { get; } = new HashSet<Token>();

        /// <summary>Tokens that were preceded by whitespace in the source (used for unknown characters).</summary>
        internal HashSet<Token> SpaceBefore { get; } = new HashSet<Token>();

        public string Run()
        {
            var tokens = Lexer.Tokenize(_source);
            if (tokens.All(t => t.IsTrivia))
                return _source.Trim().Length == 0 ? string.Empty : _source;

            string newline = DetectNewline(tokens);
            bool endsWithNewline = EndsWithNewline(tokens);

            var lines = BuildLines(tokens);
            MarkVerbatim(lines);
            if (_part == CodePart.Declaration && !_o.StructOnNewLine)
                JoinStructWithTypeHeader(lines);

            for (int i = 0; i < lines.Count; i++)
                ProcessLine(lines, i);

            var renderer = new Renderer(_o, UnaryOperators, CaseLabelColons, ArgumentAssignments, SpaceBefore);
            var physical = renderer.Render(lines);

            var sb = new StringBuilder();
            for (int i = 0; i < physical.Count; i++)
            {
                if (i > 0)
                    sb.Append(newline);
                sb.Append(NormalizeNewlines(physical[i], newline));
            }

            bool finalNewline = _o.FinalNewline == FinalNewlineStyle.Add ||
                                (_o.FinalNewline == FinalNewlineStyle.Preserve && endsWithNewline);
            if (finalNewline && physical.Count > 0)
                sb.Append(newline);
            return sb.ToString();
        }

        // ------------------------------------------------------------------ line building

        private string DetectNewline(IReadOnlyList<Token> tokens)
        {
            switch (_o.LineEnding)
            {
                case LineEndingStyle.CrLf: return "\r\n";
                case LineEndingStyle.Lf: return "\n";
            }
            int crlf = 0, lf = 0;
            foreach (var t in tokens.Where(t => t.Kind == TokenKind.Newline))
            {
                if (t.Text == "\r\n")
                    crlf++;
                else
                    lf++;
            }
            return lf > crlf ? "\n" : "\r\n";
        }

        private static bool EndsWithNewline(IReadOnlyList<Token> tokens)
        {
            for (int i = tokens.Count - 1; i >= 0; i--)
            {
                if (tokens[i].Kind == TokenKind.Newline)
                    return true;
                if (tokens[i].Kind != TokenKind.Whitespace)
                    return false;
            }
            return false;
        }

        private static string NormalizeNewlines(string text, string newline) =>
            text.IndexOf('\n') < 0 && text.IndexOf('\r') < 0
                ? text
                : Regex.Replace(text, "\r\n|\r|\n", newline);

        private List<Line> BuildLines(IReadOnlyList<Token> tokens)
        {
            var lines = new List<Line>();
            var current = new Line();
            var original = new StringBuilder();
            int blanks = 0;

            void Finish()
            {
                if (current.Tokens.Count == 0)
                {
                    blanks++;
                }
                else
                {
                    current.BlankBefore = blanks;
                    current.OriginalText = original.ToString();
                    lines.Add(current);
                    blanks = 0;
                }
                current = new Line();
                original.Clear();
            }

            bool afterSpace = false;
            foreach (var token in tokens)
            {
                if (token.Kind == TokenKind.Newline)
                {
                    Finish();
                    afterSpace = false;
                    continue;
                }
                original.Append(token.Text);
                if (token.Kind == TokenKind.Whitespace)
                {
                    afterSpace = true;
                    continue;
                }
                if (afterSpace)
                    SpaceBefore.Add(token);
                afterSpace = false;
                current.Tokens.Add(token);
            }
            if (current.Tokens.Count > 0)
                Finish();
            return lines;
        }

        private static bool IsDirective(Token t, Regex directive) =>
            (t.IsComment || t.Kind == TokenKind.Pragma) && directive.IsMatch(t.Text);

        private static void MarkVerbatim(List<Line> lines)
        {
            bool off = false;
            foreach (var line in lines)
            {
                bool hasOff = line.Tokens.Any(t => IsDirective(t, FormatOff));
                bool hasOn = line.Tokens.Any(t => IsDirective(t, FormatOn));
                if (off && !hasOn)
                    line.Verbatim = true;
                if (hasOn)
                    off = false;
                if (hasOff)
                    off = true;
            }
        }

        /// <summary>StructOnNewLine = false: "TYPE ST_X :" + "STRUCT" -> "TYPE ST_X : STRUCT".</summary>
        private static void JoinStructWithTypeHeader(List<Line> lines)
        {
            for (int i = 1; i < lines.Count; i++)
            {
                var line = lines[i];
                var previous = lines[i - 1];
                if (line.Verbatim || previous.Verbatim || line.BlankBefore > 0)
                    continue;
                bool structOnly = line.Tokens.Count >= 1 &&
                                  (line.Tokens[0].IsKeyword("STRUCT") || line.Tokens[0].IsKeyword("UNION")) &&
                                  line.Tokens.Skip(1).All(t => t.IsComment);
                if (!structOnly || previous.Tokens.Count < 3 || !previous.Tokens[0].IsKeyword("TYPE") ||
                    !previous.Tokens[previous.Tokens.Count - 1].IsOperator(":"))
                    continue;
                previous.Tokens.AddRange(line.Tokens);
                lines.RemoveAt(i);
                i--;
            }
        }

        // ------------------------------------------------------------------ main pass

        private Frame Top => _stack.Count == 0 ? null : _stack[_stack.Count - 1];

        private bool InDeclarationStatementContext =>
            _part == CodePart.Declaration && Top != null &&
            (Top.Kind == FrameKind.Var || Top.Kind == FrameKind.Struct || Top.Kind == FrameKind.Union ||
             (Top.Kind == FrameKind.Region && _stack.Any(f => f.Kind == FrameKind.Var || f.Kind == FrameKind.Struct || f.Kind == FrameKind.Union)));

        private bool StatementContext => _part == CodePart.Implementation || InDeclarationStatementContext;

        private int BodyLevel()
        {
            var top = Top;
            if (top == null)
                return 0;
            switch (top.Kind)
            {
                case FrameKind.Var: return top.Level + (_o.IndentVarBlocks ? 1 : 0);
                case FrameKind.Struct:
                case FrameKind.Union: return top.Level + (_o.IndentStructMembers ? 1 : 0);
                case FrameKind.Region: return top.Level + (_o.IndentRegions ? 1 : 0);
                case FrameKind.Type: return top.Level + (_o.IndentTypeDeclaration ? 1 : 0);
                case FrameKind.Case:
                    if (top.InElse && !_o.IndentCaseElse)
                        return top.Level + 1;
                    return top.CaseInBody ? CaseBodyLevel(top) : CaseLabelLevel(top);
                default: return top.Level + 1;
            }
        }

        private int CaseLabelLevel(Frame f) => f.Level + (_o.IndentCaseLabels ? 1 : 0);

        private int CaseBodyLevel(Frame f) => CaseLabelLevel(f) + (_o.IndentCaseBody ? 1 : 0);

        private int CaseElseLevel(Frame f) => _o.IndentCaseElse ? CaseLabelLevel(f) : f.Level;

        private Frame Find(params FrameKind[] kinds)
        {
            for (int i = _stack.Count - 1; i >= 0; i--)
            {
                if (Array.IndexOf(kinds, _stack[i].Kind) >= 0)
                    return _stack[i];
            }
            return null;
        }

        private void Push(FrameKind kind, int level) => _stack.Add(new Frame(kind, level, ++_frameIds));

        private void Pop(params FrameKind[] kinds)
        {
            var frame = Find(kinds);
            if (frame == null)
                return;
            int index = _stack.LastIndexOf(frame);
            _stack.RemoveRange(index, _stack.Count - index);
        }

        private void ProcessLine(List<Line> lines, int index)
        {
            var line = lines[index];

            if (!line.Verbatim && !_open && _paren == 0)
            {
                TryJoinStatement(lines, index);
                if (_o.SimpleIfOnSingleLine && _part == CodePart.Implementation)
                    TryKeepSimpleIf(lines, index);
            }

            var first = line.Tokens[0];
            line.StartsWithCloser = IsCloser(first);
            bool thenOrDo = first.IsKeyword("THEN") || first.IsKeyword("DO");
            if ((line.StartsWithCloser || thenOrDo) && _paren == 0)
                _open = false;

            bool continuation = _open || _paren > 0;
            int caseLabelColon = -1;
            int level;
            if (continuation)
            {
                // continuation lines are indented relative to the line that started the statement
                int cont = Math.Max(0, _o.ContinuationIndent);
                if (_paren == 0 && _conditionOpen && !_o.IndentConditionContinuation)
                    cont = 0;
                level = _statementLevel + (_paren > 0 ? _paren * cont : cont);
                if (_paren > 0 && first.Kind == TokenKind.Operator && (first.Text == ")" || first.Text == "]"))
                    level -= cont;
                line.StatementHead = _statementHead;
                line.ParenDepthAtStart = _paren;
            }
            else
            {
                var caseFrame = Top != null && Top.Kind == FrameKind.Case && Top.HeaderDone ? Top : null;
                if (caseFrame != null)
                    caseLabelColon = FindCaseLabelColon(line.Tokens);
                level = LevelForStatementStart(first, caseLabelColon >= 0);

                if (caseFrame != null && _o.CommentBeforeCaseLabelAtLabelLevel && caseFrame.CaseInBody &&
                    line.Tokens.All(t => t.IsComment || t.Kind == TokenKind.Pragma))
                {
                    var next = NextCodeLine(lines, index);
                    if (next != null && next.Tokens[0].IsKeyword("ELSE"))
                        level = CaseElseLevel(caseFrame);
                    else if (next != null && FindCaseLabelColon(next.Tokens) >= 0)
                        level = CaseLabelLevel(caseFrame);
                }

                if (caseFrame != null && (caseLabelColon >= 0 || first.IsKeyword("ELSE")))
                {
                    line.IsCaseBranchStart = true;
                    line.PreviousCaseBranchLines = caseFrame.BranchStarted ? caseFrame.BranchLines : -1;
                    caseFrame.BranchStarted = true;
                    caseFrame.BranchLines = 0;
                }

                _statementLevel = Math.Max(0, level);
                _statementHead = line;
            }

            var countingCase = Find(FrameKind.Case);
            if (countingCase != null && countingCase.BranchStarted)
                countingCase.BranchLines++;

            line.Indent = Math.Max(0, level);
            line.IsContinuation = continuation;
            line.IsEnumMember = _part == CodePart.Declaration && !InDeclarationStatementContext && _paren == 1 &&
                                first.Kind == TokenKind.Identifier;
            line.InDeclarationSection = !continuation && InDeclarationStatementContext;
            line.IsStatement = !continuation && _part == CodePart.Implementation;
            line.IsTypeHeader = !continuation && _part == CodePart.Declaration && IsTypeStart(first);

            WalkTokens(lines, index, line.Indent, caseLabelColon);
            line.FrameId = Top?.Id ?? 0;
        }

        /// <summary>"TYPE name :" or, after a line break behind TYPE, "name :" directly inside TYPE.</summary>
        private bool IsTypeStart(Token first) =>
            first.IsKeyword("TYPE") || (Top != null && Top.Kind == FrameKind.Type && _paren == 0 && first.Kind == TokenKind.Identifier);

        private static Line NextCodeLine(List<Line> lines, int index)
        {
            for (int i = index + 1; i < lines.Count; i++)
            {
                if (lines[i].Tokens.Any(t => !t.IsComment && t.Kind != TokenKind.Pragma))
                    return lines[i];
            }
            return null;
        }

        private int LevelForStatementStart(Token first, bool isCaseLabel)
        {
            if (first.Kind == TokenKind.Keyword)
            {
                switch (first.Upper)
                {
                    case "END_IF":
                    case "ELSIF":
                        return LevelOf(Find(FrameKind.If));
                    case "THEN":
                        var ifFrame = Find(FrameKind.If);
                        return ifFrame == null ? BodyLevel() : ifFrame.Level + (_o.IndentThen ? 1 : 0);
                    case "DO":
                        var loop = Find(FrameKind.For, FrameKind.While);
                        return loop == null ? BodyLevel() : loop.Level + (_o.IndentDo ? 1 : 0);
                    case "END_CASE": return LevelOf(Find(FrameKind.Case));
                    case "END_FOR": return LevelOf(Find(FrameKind.For));
                    case "END_WHILE": return LevelOf(Find(FrameKind.While));
                    case "UNTIL":
                    case "END_REPEAT": return LevelOf(Find(FrameKind.Repeat));
                    case "END_VAR": return LevelOf(Find(FrameKind.Var));
                    case "END_STRUCT": return LevelOf(Find(FrameKind.Struct));
                    case "END_UNION": return LevelOf(Find(FrameKind.Union));
                    case "END_TYPE": return LevelOf(Find(FrameKind.Type));
                    case "__CATCH":
                    case "__FINALLY":
                    case "__ENDTRY": return LevelOf(Find(FrameKind.Try));
                    case "ELSE":
                        var f = Find(FrameKind.If, FrameKind.Case);
                        if (f == null)
                            return BodyLevel();
                        return f.Kind == FrameKind.Case ? CaseElseLevel(f) : f.Level;
                }
            }
            if (first.Kind == TokenKind.Pragma && IsEndRegion(first))
                return LevelOf(Find(FrameKind.Region));
            if (isCaseLabel)
                return CaseLabelLevel(Top);
            return BodyLevel();
        }

        private int LevelOf(Frame f) => f?.Level ?? BodyLevel();

        private static bool IsCloser(Token t)
        {
            if (t.Kind == TokenKind.Pragma)
                return IsEndRegion(t);
            if (t.Kind != TokenKind.Keyword)
                return false;
            switch (t.Upper)
            {
                case "END_IF": case "ELSIF": case "ELSE": case "END_CASE": case "END_FOR": case "END_WHILE":
                case "UNTIL": case "END_REPEAT": case "END_VAR": case "END_STRUCT": case "END_UNION": case "END_TYPE":
                case "__CATCH": case "__FINALLY": case "__ENDTRY":
                    return true;
                default:
                    return false;
            }
        }

        private static string PragmaWord(Token t)
        {
            string inner = t.Text.TrimStart('{').TrimStart();
            int end = 0;
            while (end < inner.Length && (char.IsLetterOrDigit(inner[end]) || inner[end] == '_'))
                end++;
            return inner.Substring(0, end).ToLowerInvariant();
        }

        private static bool IsRegion(Token t) => t.Kind == TokenKind.Pragma && PragmaWord(t) == "region";

        private static bool IsEndRegion(Token t) => t.Kind == TokenKind.Pragma && PragmaWord(t) == "endregion";

        /// <summary>Returns the index of the ':' that terminates a CASE label at the start of the line, or -1.</summary>
        private static int FindCaseLabelColon(List<Token> tokens)
        {
            int depth = 0;
            bool any = false;
            for (int k = 0; k < tokens.Count; k++)
            {
                var t = tokens[k];
                if (t.IsComment || t.Kind == TokenKind.Pragma)
                    continue;
                if (t.Kind == TokenKind.Operator)
                {
                    switch (t.Text)
                    {
                        case "(": case "[": depth++; continue;
                        case ")": case "]": depth--; continue;
                        case ":": return depth == 0 && any ? k : -1;
                        case ";": case ":=": case "=>": return -1;
                    }
                    if (StLanguage.IsAssignmentOperator(t))
                        return -1; // S= R= REF=
                }
                if (t.Kind == TokenKind.Keyword && !t.Is("TRUE") && !t.Is("FALSE"))
                    return -1;
                any = true;
            }
            return -1;
        }

        /// <summary>Line break before THEN / DO / STRUCT when configured (the keyword starts a new line).</summary>
        private bool SplitBefore(List<Token> tokens, int k)
        {
            var t = tokens[k];
            if (t.Kind != TokenKind.Keyword)
                return false;
            if (t.Is("THEN"))
                return _o.LineBreakBeforeThen;
            if (t.Is("DO"))
                return _o.LineBreakBeforeDo && Top != null && (Top.Kind == FrameKind.For || Top.Kind == FrameKind.While) && !Top.HeaderDone;
            if (t.Is("STRUCT") || t.Is("UNION"))
                return _o.StructOnNewLine && _part == CodePart.Declaration;
            return false;
        }

        private void WalkTokens(List<Line> lines, int index, int lineLevel, int caseLabelColon)
        {
            var line = lines[index];
            var tokens = line.Tokens;
            bool opensBlock = false;
            bool maySplit = !line.Verbatim && !line.NoSplit;

            for (int k = 0; k < tokens.Count; k++)
            {
                var t = tokens[k];

                if (maySplit && k > 0 && SplitBefore(tokens, k) && tokens.Take(k).Any(x => !x.IsComment))
                {
                    MoveToNewLine(lines, index, k);
                    break;
                }

                int splitAfter = -1;
                bool structural = false;

                if (t.IsComment)
                    continue;

                if (t.Kind == TokenKind.Pragma)
                {
                    if (IsRegion(t))
                    {
                        Push(FrameKind.Region, lineLevel);
                        opensBlock = true;
                    }
                    else if (IsEndRegion(t))
                    {
                        Pop(FrameKind.Region);
                    }
                    continue;
                }

                opensBlock = false;

                if (t.Kind == TokenKind.Operator)
                {
                    switch (t.Text)
                    {
                        case "(":
                        case "[":
                            _paren++;
                            break;
                        case ")":
                        case "]":
                            _paren = Math.Max(0, _paren - 1);
                            break;
                        case ":=":
                        case "=>":
                            if (_paren > 0)
                                ArgumentAssignments.Add(t);
                            break;
                        case ";":
                            if (_paren == 0)
                            {
                                _open = false;
                                _conditionOpen = false;
                                structural = true;
                                if (_o.OneStatementPerLine)
                                    splitAfter = k;
                            }
                            break;
                        case ":":
                            if (k == caseLabelColon)
                            {
                                _open = false;
                                structural = true;
                                opensBlock = true;
                                Top.CaseInBody = true;
                                CaseLabelColons.Add(t);
                                if (_o.CaseStatementOnNewLine)
                                    splitAfter = k;
                            }
                            break;
                        case "-":
                        case "+":
                            if (IsUnaryContext(_prevSignificant))
                                UnaryOperators.Add(t);
                            break;
                    }
                }
                else if (t.Kind == TokenKind.Keyword)
                {
                    int keywordSplit = -1;
                    structural = HandleKeyword(tokens, ref k, lineLevel, ref keywordSplit, ref opensBlock);
                    if (maySplit)
                        splitAfter = keywordSplit;
                    t = tokens[k];
                }

                if (!structural && StatementContext)
                    _open = true;
                _prevSignificant = t;

                if (splitAfter >= 0 && maySplit && HasCodeAfter(tokens, splitAfter))
                {
                    MoveToNewLine(lines, index, splitAfter + 1);
                    break;
                }
            }

            line.OpensBlock = opensBlock;
        }

        private static void MoveToNewLine(List<Line> lines, int index, int from)
        {
            var tokens = lines[index].Tokens;
            var rest = tokens.GetRange(from, tokens.Count - from);
            tokens.RemoveRange(from, tokens.Count - from);
            lines.Insert(index + 1, new Line { Tokens = rest, BlankBefore = 0, OriginalText = string.Empty, WasMultiline = lines[index].WasMultiline });
        }

        /// <summary>Updates the block structure for a keyword. Returns true when the keyword ends a statement.</summary>
        private bool HandleKeyword(List<Token> tokens, ref int k, int lineLevel, ref int splitAfter, ref bool opensBlock)
        {
            var t = tokens[k];
            bool expand = _o.ExpandSingleLineBlocks;
            string word = t.Upper;

            if (StLanguage.VarSectionKeywords.Contains(word))
            {
                Push(FrameKind.Var, lineLevel);
                _open = false;
                int j = k + 1;
                while (j < tokens.Count && (tokens[j].IsComment || (tokens[j].Kind == TokenKind.Keyword && StLanguage.VarModifiers.Contains(tokens[j].Text))))
                    j++;
                // stop before trailing comments so they stay on the header line
                int last = j - 1;
                while (last > k && tokens[last].IsComment)
                    last--;
                k = last;
                splitAfter = k;
                opensBlock = true;
                return true;
            }

            switch (word)
            {
                case "IF":
                    Push(FrameKind.If, lineLevel);
                    _conditionOpen = true;
                    return false;
                case "THEN":
                    _open = false;
                    _conditionOpen = false;
                    opensBlock = true;
                    if (expand)
                        splitAfter = k;
                    return true;
                case "ELSIF":
                    _conditionOpen = true;
                    return false;
                case "ELSE":
                    var f = Find(FrameKind.If, FrameKind.Case);
                    if (f != null && f.Kind == FrameKind.Case)
                    {
                        f.CaseInBody = true;
                        f.InElse = true;
                    }
                    _open = false;
                    opensBlock = true;
                    if (expand)
                        splitAfter = k;
                    return true;
                case "CASE":
                    Push(FrameKind.Case, lineLevel);
                    return false;
                case "OF":
                    if (Top != null && Top.Kind == FrameKind.Case && !Top.HeaderDone)
                    {
                        Top.HeaderDone = true;
                        _open = false;
                        opensBlock = true;
                        if (expand)
                            splitAfter = k;
                        return true;
                    }
                    return false;
                case "FOR":
                    Push(FrameKind.For, lineLevel);
                    return false;
                case "WHILE":
                    Push(FrameKind.While, lineLevel);
                    _conditionOpen = true;
                    return false;
                case "DO":
                    if (Top != null && (Top.Kind == FrameKind.For || Top.Kind == FrameKind.While) && !Top.HeaderDone)
                    {
                        Top.HeaderDone = true;
                        _open = false;
                        _conditionOpen = false;
                        opensBlock = true;
                        if (expand)
                            splitAfter = k;
                        return true;
                    }
                    return false;
                case "REPEAT":
                    Push(FrameKind.Repeat, lineLevel);
                    _open = false;
                    opensBlock = true;
                    if (expand)
                        splitAfter = k;
                    return true;
                case "UNTIL":
                    _conditionOpen = true;
                    return false;
                case "END_IF":
                    Pop(FrameKind.If);
                    return EndKeyword(tokens, ref k, ref splitAfter);
                case "END_CASE":
                    Pop(FrameKind.Case);
                    return EndKeyword(tokens, ref k, ref splitAfter);
                case "END_FOR":
                    Pop(FrameKind.For);
                    return EndKeyword(tokens, ref k, ref splitAfter);
                case "END_WHILE":
                    Pop(FrameKind.While);
                    return EndKeyword(tokens, ref k, ref splitAfter);
                case "END_REPEAT":
                    Pop(FrameKind.Repeat);
                    return EndKeyword(tokens, ref k, ref splitAfter);
                case "END_VAR":
                    Pop(FrameKind.Var);
                    _open = false;
                    splitAfter = k;
                    return true;
                case "STRUCT":
                    Push(FrameKind.Struct, lineLevel);
                    _open = false;
                    opensBlock = true;
                    splitAfter = k;
                    return true;
                case "UNION":
                    Push(FrameKind.Union, lineLevel);
                    _open = false;
                    opensBlock = true;
                    splitAfter = k;
                    return true;
                case "TYPE":
                    if (_part != CodePart.Declaration)
                        return false;
                    Push(FrameKind.Type, lineLevel);
                    if (_o.LineBreakAfterTypeKeyword)
                        splitAfter = k;
                    return false;
                case "END_TYPE":
                    Pop(FrameKind.Type);
                    _open = false;
                    splitAfter = k;
                    return true;
                case "END_STRUCT":
                    Pop(FrameKind.Struct);
                    _open = false;
                    splitAfter = k;
                    return true;
                case "END_UNION":
                    Pop(FrameKind.Union);
                    _open = false;
                    splitAfter = k;
                    return true;
                case "__TRY":
                    Push(FrameKind.Try, lineLevel);
                    _open = false;
                    opensBlock = true;
                    if (expand)
                        splitAfter = k;
                    return true;
                case "__CATCH":
                    // __CATCH(exc): the parenthesised exception variable belongs to the header
                    if (k + 1 < tokens.Count && tokens[k + 1].IsOperator("("))
                    {
                        int close = k + 1;
                        while (close < tokens.Count && !tokens[close].IsOperator(")"))
                            close++;
                        if (close < tokens.Count)
                            k = close;
                    }
                    _open = false;
                    opensBlock = true;
                    if (expand)
                        splitAfter = k;
                    return true;
                case "__FINALLY":
                    _open = false;
                    opensBlock = true;
                    if (expand)
                        splitAfter = k;
                    return true;
                case "__ENDTRY":
                    Pop(FrameKind.Try);
                    return EndKeyword(tokens, ref k, ref splitAfter);
            }
            return false;
        }

        private bool EndKeyword(List<Token> tokens, ref int k, ref int splitAfter)
        {
            _open = false;
            _conditionOpen = false;
            var keyword = tokens[k];
            bool hasSemicolon = k + 1 < tokens.Count && tokens[k + 1].IsOperator(";");
            if (StLanguage.EndKeywordsWithOptionalSemicolon.Contains(keyword.Text))
            {
                if (_o.EndKeywordSemicolon == SemicolonStyle.Add && !hasSemicolon)
                {
                    tokens.Insert(k + 1, new Token(TokenKind.Operator, ";", -1));
                    hasSemicolon = true;
                }
                else if (_o.EndKeywordSemicolon == SemicolonStyle.Remove && hasSemicolon)
                {
                    tokens.RemoveAt(k + 1);
                    hasSemicolon = false;
                }
            }
            if (hasSemicolon)
                k++;
            if (_o.ExpandSingleLineBlocks || _o.OneStatementPerLine)
                splitAfter = k;
            return true;
        }

        private static bool IsUnaryContext(Token previous)
        {
            if (previous == null)
                return true;
            if (previous.Kind == TokenKind.Operator)
                return previous.Text != ")" && previous.Text != "]" && previous.Text != "^";
            if (previous.Kind == TokenKind.Keyword)
                return !(previous.Is("TRUE") || previous.Is("FALSE") || previous.Is("THIS") || previous.Is("SUPER"));
            return false;
        }

        private static bool HasCodeAfter(List<Token> tokens, int index)
        {
            for (int i = index + 1; i < tokens.Count; i++)
            {
                if (!tokens[i].IsComment)
                    return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ joining

        /// <summary>
        /// Joins a statement that spans several lines into one logical line when a line break option asks for it:
        /// wrapped calls, initializers and implicit enums (their layout is then decided by the renderer), and any
        /// simple statement when <see cref="FormatterOptions.KeepExistingLineBreaks"/> is off.
        /// </summary>
        private void TryJoinStatement(List<Line> lines, int index)
        {
            var first = lines[index];
            if (first.Tokens[0].IsComment || first.Tokens[0].Kind == TokenKind.Pragma)
                return;

            if (_part == CodePart.Implementation)
            {
                if (_o.CallLineBreakMode == LineBreakMode.Preserve && _o.KeepExistingLineBreaks)
                    return;
                if (!JoinUntilSemicolon(lines, index, out var merged, out int last))
                    return;
                if (_o.CallLineBreakMode != LineBreakMode.Preserve && ListShape.Call(merged) != null)
                {
                    Replace(lines, index, merged, last);
                    return;
                }
                if (!_o.KeepExistingLineBreaks && IsSimpleStatement(merged) && FitsOnOneLine(merged, BodyLevel()))
                    Replace(lines, index, merged, last);
                return;
            }

            if (InDeclarationStatementContext)
            {
                if (_o.InitializerLineBreakMode == LineBreakMode.Preserve)
                    return;
                if (JoinUntilSemicolon(lines, index, out var merged, out int last) && ListShape.Initializer(merged) != null)
                    Replace(lines, index, merged, last);
                return;
            }

            if (IsTypeStart(first.Tokens[0]) && _o.EnumLineBreakMode != LineBreakMode.Preserve)
            {
                if (JoinUntilSemicolon(lines, index, out var merged, out int last) && ListShape.Enum(merged) != null)
                    Replace(lines, index, merged, last);
            }
        }

        private static void Replace(List<Line> lines, int index, List<Token> merged, int last)
        {
            lines[index].Tokens = merged;
            lines[index].WasMultiline = true;
            lines.RemoveRange(index + 1, last - index);
        }

        /// <summary>
        /// Collects the tokens from line <paramref name="index"/> up to the line with the ';' that ends the statement
        /// (at depth 0). Fails for single line statements, comments before the ';', blank lines or verbatim lines.
        /// </summary>
        private static bool JoinUntilSemicolon(List<Line> lines, int index, out List<Token> merged, out int last)
        {
            merged = null;
            last = index;
            int depth = 0;
            if (EndsStatement(lines[index].Tokens, ref depth, out bool firstHasComment) || firstHasComment || depth < 0)
                return false;

            var tokens = new List<Token>(lines[index].Tokens);
            for (int j = index + 1; j < lines.Count && j < index + 500; j++)
            {
                var next = lines[j];
                if (next.Verbatim || next.BlankBefore > 0)
                    return false;
                bool ends = EndsStatement(next.Tokens, ref depth, out bool hasComment);
                if (hasComment && !ends)
                    return false;
                tokens.AddRange(next.Tokens);
                if (ends)
                {
                    if (!EndsWithSemicolonAndComments(tokens))
                        return false;
                    merged = tokens;
                    last = j;
                    return true;
                }
                if (depth < 0)
                    return false;
            }
            return false;
        }

        /// <summary>No control keywords, pragmas or CASE labels (a bare ':').</summary>
        private static bool IsSimpleStatement(List<Token> tokens) =>
            tokens.All(t => t.Kind != TokenKind.Keyword || ExpressionKeywords.Contains(t.Text)) &&
            !tokens.Any(t => t.Kind == TokenKind.Pragma || t.IsOperator(":"));

        private bool FitsOnOneLine(List<Token> tokens, int level)
        {
            if (_o.MaxLineLength <= 0)
                return true;
            int width = level * Math.Max(1, _o.IndentSize) + tokens.Sum(t => t.Text.Length) + tokens.Count - 1;
            return width <= _o.MaxLineLength;
        }

        /// <summary>Scans a line; true when it contains the ';' that ends the statement (at depth 0).</summary>
        private static bool EndsStatement(List<Token> tokens, ref int depth, out bool commentBeforeEnd)
        {
            commentBeforeEnd = false;
            for (int i = 0; i < tokens.Count; i++)
            {
                var t = tokens[i];
                if (t.IsComment || t.Kind == TokenKind.Pragma)
                {
                    commentBeforeEnd = true;
                    continue;
                }
                if (t.Kind != TokenKind.Operator)
                    continue;
                if (t.Text == "(" || t.Text == "[")
                    depth++;
                else if (t.Text == ")" || t.Text == "]")
                    depth--;
                else if (t.Text == ";" && depth == 0)
                {
                    // only comments may follow the terminating semicolon
                    for (int j = i + 1; j < tokens.Count; j++)
                    {
                        if (!tokens[j].IsComment)
                        {
                            commentBeforeEnd = true;
                            return true;
                        }
                    }
                    return true;
                }
            }
            return false;
        }

        private static bool EndsWithSemicolonAndComments(List<Token> tokens)
        {
            int i = tokens.Count - 1;
            while (i >= 0 && tokens[i].IsComment)
                i--;
            return i >= 0 && tokens[i].IsOperator(";");
        }

        // ------------------------------------------------------------------ simple IF on one line

        /// <summary>
        /// SimpleIfOnSingleLine: 'IF cond THEN stmt; END_IF' with one simple statement stays on one line (or is
        /// joined from three lines) when it fits into MaxLineLength.
        /// </summary>
        private void TryKeepSimpleIf(List<Line> lines, int index)
        {
            var line = lines[index];
            if (!line.Tokens[0].IsKeyword("IF"))
                return;

            if (IsSimpleIf(line.Tokens))
            {
                if (FitsOnOneLine(CodeOf(line.Tokens), BodyLevel()))
                    line.NoSplit = true;
                return;
            }

            // IF cond THEN / statement; / END_IF
            if (index + 2 >= lines.Count)
                return;
            var body = lines[index + 1];
            var end = lines[index + 2];
            if (body.Verbatim || end.Verbatim || body.BlankBefore > 0 || end.BlankBefore > 0 ||
                line.Tokens.Any(t => t.IsComment) || body.Tokens.Any(t => t.IsComment))
                return;
            var merged = line.Tokens.Concat(body.Tokens).Concat(end.Tokens).ToList();
            if (!IsSimpleIf(merged) || !FitsOnOneLine(CodeOf(merged), BodyLevel()))
                return;
            line.Tokens = merged;
            line.NoSplit = true;
            lines.RemoveRange(index + 1, 2);
        }

        private static List<Token> CodeOf(List<Token> tokens) => tokens.Where(t => !t.IsComment).ToList();

        private static bool IsSimpleIf(List<Token> tokens)
        {
            var code = CodeOf(tokens);
            if (code.Count < 6 || !code[0].IsKeyword("IF") || code.Any(t => t.Kind == TokenKind.Pragma))
                return false;
            if (tokens.Take(tokens.FindLastIndex(t => !t.IsComment) + 1).Any(t => t.IsComment))
                return false; // comments only at the end

            int then = code.FindIndex(t => t.IsKeyword("THEN"));
            if (then < 2)
                return false;
            for (int i = 1; i < then; i++)
            {
                if (code[i].Kind == TokenKind.Keyword && !ExpressionKeywords.Contains(code[i].Text))
                    return false;
            }

            int semicolon = -1, depth = 0;
            for (int i = then + 1; i < code.Count; i++)
            {
                var t = code[i];
                if (t.IsOperator("(") || t.IsOperator("[")) depth++;
                else if (t.IsOperator(")") || t.IsOperator("]")) depth--;
                else if (depth == 0 && t.IsOperator(";"))
                {
                    semicolon = i;
                    break;
                }
                else if (t.Kind == TokenKind.Keyword && !ExpressionKeywords.Contains(t.Text))
                    return false;
            }
            if (semicolon <= then + 1 || semicolon + 1 >= code.Count || !code[semicolon + 1].IsKeyword("END_IF"))
                return false;
            int rest = code.Count - (semicolon + 2);
            return rest == 0 || (rest == 1 && code[code.Count - 1].IsOperator(";"));
        }
    }
}
