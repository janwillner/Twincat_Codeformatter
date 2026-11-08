using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using TcFormatter.Lexing;

namespace TcFormatter.Formatting
{
    /// <summary>
    /// Token based formatter. Keeps the user's line structure (except for statement splitting and call
    /// re-wrapping) and recomputes indentation, spacing, casing, alignment and blank lines.
    /// </summary>
    internal sealed class FormatEngine
    {
        private static readonly Regex FormatOff = new Regex(@"tcfmt\s*:\s*off\b", RegexOptions.IgnoreCase);
        private static readonly Regex FormatOn = new Regex(@"tcfmt\s*:\s*on\b", RegexOptions.IgnoreCase);

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

        private static void MarkVerbatim(List<Line> lines)
        {
            bool off = false;
            foreach (var line in lines)
            {
                bool hasOff = line.Tokens.Any(t => t.IsComment && FormatOff.IsMatch(t.Text));
                bool hasOn = line.Tokens.Any(t => t.IsComment && FormatOn.IsMatch(t.Text));
                if (off && !hasOn)
                    line.Verbatim = true;
                if (hasOn)
                    off = false;
                if (hasOff)
                    off = true;
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
                case FrameKind.Case: return top.CaseInBody ? CaseBodyLevel(top) : CaseLabelLevel(top);
                default: return top.Level + 1;
            }
        }

        private int CaseLabelLevel(Frame f) => f.Level + (_o.IndentCaseLabels ? 1 : 0);

        private int CaseBodyLevel(Frame f) => CaseLabelLevel(f) + (_o.IndentCaseBody ? 1 : 0);

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

            if (!line.Verbatim && !_open && _paren == 0 && _part == CodePart.Implementation && _o.RejoinWrappedCalls)
                TryJoinCallStatement(lines, index);

            var first = line.Tokens[0];
            line.StartsWithCloser = IsCloser(first);
            if (line.StartsWithCloser && _paren == 0)
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
                if (Top != null && Top.Kind == FrameKind.Case && Top.HeaderDone)
                    caseLabelColon = FindCaseLabelColon(line.Tokens);
                level = LevelForStatementStart(first, caseLabelColon >= 0);
                _statementLevel = Math.Max(0, level);
                _statementHead = line;
            }

            line.Indent = Math.Max(0, level);
            line.IsContinuation = continuation;
            line.IsEnumMember = _part == CodePart.Declaration && !InDeclarationStatementContext && _paren == 1 &&
                                first.Kind == TokenKind.Identifier;
            line.InDeclarationSection = !continuation && InDeclarationStatementContext;
            line.IsStatement = !continuation && _part == CodePart.Implementation;

            WalkTokens(lines, index, line.Indent, caseLabelColon);
            line.FrameId = Top?.Id ?? 0;
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
                    case "END_CASE": return LevelOf(Find(FrameKind.Case));
                    case "END_FOR": return LevelOf(Find(FrameKind.For));
                    case "END_WHILE": return LevelOf(Find(FrameKind.While));
                    case "UNTIL":
                    case "END_REPEAT": return LevelOf(Find(FrameKind.Repeat));
                    case "END_VAR": return LevelOf(Find(FrameKind.Var));
                    case "END_STRUCT": return LevelOf(Find(FrameKind.Struct));
                    case "END_UNION": return LevelOf(Find(FrameKind.Union));
                    case "__CATCH":
                    case "__FINALLY":
                    case "__ENDTRY": return LevelOf(Find(FrameKind.Try));
                    case "ELSE":
                        var f = Find(FrameKind.If, FrameKind.Case);
                        if (f == null)
                            return BodyLevel();
                        return f.Kind == FrameKind.Case ? CaseLabelLevel(f) : f.Level;
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
                case "UNTIL": case "END_REPEAT": case "END_VAR": case "END_STRUCT": case "END_UNION":
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
                    if (t.Text.EndsWith("=", StringComparison.Ordinal) && char.IsLetter(t.Text[0]))
                        return -1; // S= R= REF=
                }
                if (t.Kind == TokenKind.Keyword && !t.Is("TRUE") && !t.Is("FALSE"))
                    return -1;
                any = true;
            }
            return -1;
        }

        private void WalkTokens(List<Line> lines, int index, int lineLevel, int caseLabelColon)
        {
            var line = lines[index];
            var tokens = line.Tokens;
            bool opensBlock = false;

            for (int k = 0; k < tokens.Count; k++)
            {
                var t = tokens[k];
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
                else if (t.Kind == TokenKind.Keyword && !line.Verbatim)
                {
                    structural = HandleKeyword(tokens, ref k, lineLevel, ref splitAfter, ref opensBlock);
                    t = tokens[k];
                }
                else if (t.Kind == TokenKind.Keyword)
                {
                    int dummy = -1;
                    structural = HandleKeyword(tokens, ref k, lineLevel, ref dummy, ref opensBlock);
                    t = tokens[k];
                }

                if (!structural && StatementContext)
                    _open = true;
                _prevSignificant = t;

                if (splitAfter >= 0 && !line.Verbatim && HasCodeAfter(tokens, splitAfter))
                {
                    var rest = tokens.GetRange(splitAfter + 1, tokens.Count - splitAfter - 1);
                    tokens.RemoveRange(splitAfter + 1, tokens.Count - splitAfter - 1);
                    lines.Insert(index + 1, new Line { Tokens = rest, BlankBefore = 0, OriginalText = string.Empty });
                    break;
                }
            }

            line.OpensBlock = opensBlock;
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
                        f.CaseInBody = true;
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

        // ------------------------------------------------------------------ call re-joining

        /// <summary>
        /// Joins a call statement that spans several lines (and contains no comments before its final ';')
        /// into one logical line, so the renderer can decide deterministically how to wrap it.
        /// </summary>
        private static void TryJoinCallStatement(List<Line> lines, int index)
        {
            var first = lines[index];
            int depth = 0;
            if (EndsStatement(first.Tokens, ref depth, out bool firstHasComment) || firstHasComment || depth <= 0)
                return;

            var merged = new List<Token>(first.Tokens);
            int last = index;
            for (int j = index + 1; j < lines.Count && j < index + 500; j++)
            {
                var next = lines[j];
                if (next.Verbatim || next.BlankBefore > 0)
                    return;
                bool ends = EndsStatement(next.Tokens, ref depth, out bool hasComment);
                if (hasComment && !ends)
                    return;
                merged.AddRange(next.Tokens);
                last = j;
                if (ends)
                    break;
                if (depth < 0)
                    return;
            }
            if (last == index || !EndsWithSemicolonAndComments(merged))
                return;
            if (CallShape.TryParse(merged) == null)
                return;

            first.Tokens = merged;
            lines.RemoveRange(index + 1, last - index);
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
    }
}
