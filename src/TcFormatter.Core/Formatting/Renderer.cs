using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TcFormatter.Lexing;

namespace TcFormatter.Formatting
{
    /// <summary>Turns processed <see cref="Line"/>s into physical text lines: spacing, casing, alignment, wrapping.</summary>
    internal sealed class Renderer
    {
        private static readonly HashSet<string> BinaryOperators = new HashSet<string>
        {
            "+", "-", "*", "/", "**", "=", "<>", "<", ">", "<=", ">=", "&",
        };

        private readonly FormatterOptions _o;
        private readonly HashSet<Token> _unary;
        private readonly HashSet<Token> _caseColons;
        private readonly HashSet<Token> _argumentAssignments;
        private readonly HashSet<Token> _spaceBefore;

        public Renderer(FormatterOptions options, HashSet<Token> unary, HashSet<Token> caseColons,
                        HashSet<Token> argumentAssignments, HashSet<Token> spaceBefore)
        {
            _o = options;
            _unary = unary;
            _caseColons = caseColons;
            _argumentAssignments = argumentAssignments;
            _spaceBefore = spaceBefore;
        }

        private string Assign => _o.SpaceAroundAssignment ? " := " : ":=";

        private sealed class RLine
        {
            public Line Source;
            public string Indent;
            public int IndentWidth;
            public int Blank;
            public List<Token> Code;
            public List<Token> Comments;
            public string CodeText;
            public string CommentText;
            public DeclParts Decl;
            public EnumParts Enum;
            public string AssignLeft;
            public string AssignRight;
            public List<string> Wrapped;
            public int CommentColumn = -1;

            public bool IsCommentOnly => Code.Count == 0;
        }

        private sealed class DeclParts
        {
            public string Names;
            public string Type;
            public string Init;
        }

        private sealed class EnumParts
        {
            public string Name;
            public string Value;
            public bool Comma;
        }

        public List<string> Render(List<Line> lines)
        {
            var rendered = new List<RLine>(lines.Count);
            Line previous = null;
            foreach (var line in lines)
            {
                var r = new RLine { Source = line };
                r.Blank = ComputeBlankLines(line, previous);
                previous = line;
                if (!line.Verbatim)
                    Prepare(r);
                rendered.Add(r);
            }

            AlignDeclarations(rendered);
            AlignEnums(rendered);
            WrapCalls(rendered);
            AlignAssignments(rendered);
            AlignCodeComments(rendered);

            var output = new List<string>();
            foreach (var r in rendered)
            {
                for (int b = 0; b < r.Blank; b++)
                    output.Add(string.Empty);

                if (r.Source.Verbatim)
                {
                    output.Add(r.Source.OriginalText);
                    continue;
                }
                if (r.Wrapped != null)
                {
                    output.AddRange(r.Wrapped);
                    continue;
                }
                output.Add(Compose(r));
            }
            return output;
        }

        private int ComputeBlankLines(Line line, Line previous)
        {
            if (previous == null)
                return 0;
            if (line.Verbatim && previous.Verbatim)
                return line.BlankBefore;
            int blank = Math.Min(line.BlankBefore, Math.Max(0, _o.MaxConsecutiveBlankLines));
            if (_o.RemoveBlankLinesAtBlockStart && previous.OpensBlock && !previous.Verbatim)
                blank = 0;
            if (_o.RemoveBlankLinesAtBlockEnd && line.StartsWithCloser && !line.Verbatim)
                blank = 0;
            return blank;
        }

        private void Prepare(RLine r)
        {
            var line = r.Source;
            r.Indent = IndentString(line.Indent);
            r.IndentWidth = line.Indent * Math.Max(1, _o.IndentSize);

            int lastCode = line.Tokens.Count - 1;
            while (lastCode >= 0 && line.Tokens[lastCode].IsComment)
                lastCode--;
            r.Code = line.Tokens.Take(lastCode + 1).ToList();
            r.Comments = line.Tokens.Skip(lastCode + 1).ToList();
            if (r.Code.Count == 0)
            {
                r.Code = r.Comments;
                r.Comments = new List<Token>();
            }

            r.CodeText = Join(r.Code, 0, r.Code.Count);
            r.CommentText = r.Comments.Count == 0 ? null : Join(r.Comments, 0, r.Comments.Count);

            if (_o.AlignContinuationToAssignment && line.IsContinuation && line.ParenDepthAtStart == 0 && line.StatementHead != null)
            {
                // continue under the ':=' of the statement's first line
                string target = AssignmentTarget(line.StatementHead.Tokens);
                if (target != null)
                {
                    int pad = target.Length + (_o.SpaceAroundAssignment ? 1 : 0);
                    r.Indent = IndentString(line.StatementHead.Indent) + new string(' ', pad);
                    r.IndentWidth = line.StatementHead.Indent * Math.Max(1, _o.IndentSize) + pad;
                }
            }

            if (line.InDeclarationSection)
                r.Decl = ParseDeclaration(r.Code);
            else if (line.IsEnumMember)
                r.Enum = ParseEnum(r.Code);
            else if (line.IsStatement)
                ParseAssignment(r);
        }

        private string IndentString(int levels)
        {
            if (levels <= 0)
                return string.Empty;
            return _o.IndentStyle == IndentStyle.Tabs
                ? new string('\t', levels)
                : new string(' ', levels * Math.Max(1, _o.IndentSize));
        }

        private string Compose(RLine r)
        {
            string code = r.CodeText;
            if (r.CommentText == null)
                return r.Indent + code;
            if (code.Length == 0)
                return r.Indent + r.CommentText;
            int column = r.CommentColumn >= 0 ? r.CommentColumn : r.IndentWidth + code.Length + Math.Max(1, _o.SpacesBeforeTrailingComment);
            int pad = Math.Max(Math.Max(1, _o.SpacesBeforeTrailingComment), column - r.IndentWidth - code.Length);
            return r.Indent + code + new string(' ', pad) + r.CommentText;
        }

        // ------------------------------------------------------------------ declarations

        private DeclParts ParseDeclaration(List<Token> code)
        {
            if (code.Count < 4 || !code[code.Count - 1].IsOperator(";"))
                return null;
            int depth = 0, colon = -1, assign = -1;
            for (int i = 0; i < code.Count - 1; i++)
            {
                var t = code[i];
                if (t.Kind == TokenKind.Pragma || t.IsComment)
                    return null;
                if (t.Kind != TokenKind.Operator)
                {
                    if (colon < 0 && t.Kind == TokenKind.Keyword && !t.Is("AT"))
                        return null;
                    continue;
                }
                if (t.Text == "(" || t.Text == "[")
                    depth++;
                else if (t.Text == ")" || t.Text == "]")
                    depth--;
                else if (depth == 0 && t.Text == ":" && colon < 0)
                    colon = i;
                else if (depth == 0 && t.Text == ":=" && colon >= 0 && assign < 0)
                    assign = i;
                else if (depth == 0 && t.Text == ";")
                    return null;
            }
            if (colon <= 0 || colon + 1 >= code.Count - 1)
                return null;
            int typeEnd = assign >= 0 ? assign : code.Count - 1;
            if (typeEnd <= colon + 1)
                return null;
            return new DeclParts
            {
                Names = Join(code, 0, colon),
                Type = Join(code, colon + 1, typeEnd),
                Init = assign >= 0 && assign + 1 < code.Count - 1 ? Join(code, assign + 1, code.Count - 1) : null,
            };
        }

        /// <summary>Rendered text before the first top-level ':=' of a statement line, or null when it is no assignment.</summary>
        private string AssignmentTarget(List<Token> tokens)
        {
            int depth = 0;
            for (int i = 0; i < tokens.Count; i++)
            {
                var t = tokens[i];
                if (t.IsComment || t.Kind == TokenKind.Pragma)
                    return null;
                if (t.Kind == TokenKind.Keyword && !t.Is("THIS") && !t.Is("SUPER"))
                    return null;
                if (t.IsOperator("(") || t.IsOperator("["))
                    depth++;
                else if (t.IsOperator(")") || t.IsOperator("]"))
                    depth--;
                else if (depth == 0 && t.IsOperator(":="))
                    return i == 0 ? null : Join(tokens, 0, i);
                else if (depth == 0 && t.IsOperator("("))
                    return null;
            }
            return null;
        }

        private void AlignDeclarations(List<RLine> lines)
        {
            foreach (var group in Groups(lines, r => r.Decl != null))
            {
                int limit = _o.MaxAlignedNameLength;
                bool Aligned(RLine r) => limit <= 0 || r.Decl.Names.Length <= limit;

                int nameWidth = group.Where(Aligned).Select(r => r.Decl.Names.Length).DefaultIfEmpty(0).Max();
                string colon = (_o.SpaceBeforeColon ? " " : "") + ":" + (_o.SpaceAfterColon ? " " : "");

                var left = new Dictionary<RLine, string>();
                foreach (var r in group)
                {
                    string names;
                    if (_o.DeclarationTypeColumn > 0)
                    {
                        // fixed column: the type starts at DeclarationTypeColumn (1-based); longer names push it right
                        int width = _o.DeclarationTypeColumn - 1 - r.IndentWidth - colon.Length;
                        names = r.Decl.Names.PadRight(Math.Max(0, width));
                    }
                    else
                    {
                        names = _o.AlignDeclarationColons && Aligned(r) ? r.Decl.Names.PadRight(nameWidth) : r.Decl.Names;
                    }
                    left[r] = names + colon + r.Decl.Type;
                }

                int leftWidth = group.Where(r => r.Decl.Init != null && Aligned(r)).Select(r => left[r].Length).DefaultIfEmpty(0).Max();
                foreach (var r in group)
                {
                    string text = left[r];
                    if (r.Decl.Init != null)
                    {
                        if (_o.AlignDeclarationInitializers && Aligned(r))
                            text = text.PadRight(leftWidth);
                        text += Assign + r.Decl.Init;
                    }
                    r.CodeText = text + ";";
                }

                if (_o.DeclarationCommentAlignment == TrailingCommentAlignment.AlignInBlock)
                    AlignComments(group);
            }
        }

        // ------------------------------------------------------------------ enums

        private EnumParts ParseEnum(List<Token> code)
        {
            if (code.Count == 0 || code[0].Kind != TokenKind.Identifier)
                return null;
            bool comma = code[code.Count - 1].IsOperator(",");
            int end = comma ? code.Count - 1 : code.Count;
            if (end == 1)
                return new EnumParts { Name = code[0].Text, Comma = comma };
            if (end < 3 || !code[1].IsOperator(":="))
                return null;
            for (int i = 2; i < end; i++)
            {
                if (code[i].IsOperator(","))
                    return null;
            }
            return new EnumParts { Name = code[0].Text, Value = Join(code, 2, end), Comma = comma };
        }

        private void AlignEnums(List<RLine> lines)
        {
            foreach (var group in Groups(lines, r => r.Enum != null))
            {
                int width = group.Where(r => r.Enum.Value != null).Select(r => r.Enum.Name.Length).DefaultIfEmpty(0).Max();
                foreach (var r in group)
                {
                    string text = r.Enum.Name;
                    if (r.Enum.Value != null)
                        text = (_o.AlignEnumValues ? text.PadRight(width) : text) + Assign + r.Enum.Value;
                    r.CodeText = text + (r.Enum.Comma ? "," : "");
                }
                if (_o.DeclarationCommentAlignment == TrailingCommentAlignment.AlignInBlock)
                    AlignComments(group);
            }
        }

        // ------------------------------------------------------------------ statements

        private void ParseAssignment(RLine r)
        {
            var code = r.Code;
            if (code.Count < 4 || !code[code.Count - 1].IsOperator(";"))
                return;
            int depth = 0;
            for (int i = 0; i < code.Count; i++)
            {
                var t = code[i];
                if (t.Kind == TokenKind.Keyword && !(t.Is("THIS") || t.Is("SUPER")))
                    return;
                if (t.IsOperator("[")) depth++;
                else if (t.IsOperator("]")) depth--;
                else if (depth == 0 && t.IsOperator("(")) return;
                else if (depth == 0 && t.IsOperator(":="))
                {
                    if (i == 0)
                        return;
                    r.AssignLeft = Join(code, 0, i);
                    r.AssignRight = Join(code, i + 1, code.Count);
                    return;
                }
            }
        }

        private void AlignAssignments(List<RLine> lines)
        {
            if (!_o.AlignConsecutiveAssignments)
                return;
            foreach (var group in Groups(lines, r => r.AssignLeft != null && r.Wrapped == null, allowCommentLines: false))
            {
                int width = group.Max(r => r.AssignLeft.Length);
                foreach (var r in group)
                    r.CodeText = r.AssignLeft.PadRight(width) + Assign + r.AssignRight;
            }
        }

        private void AlignCodeComments(List<RLine> lines)
        {
            if (_o.CodeCommentAlignment != TrailingCommentAlignment.AlignInBlock)
                return;
            var group = new List<RLine>();
            void Flush()
            {
                if (group.Count > 1)
                    AlignComments(group);
                group.Clear();
            }
            foreach (var r in lines)
            {
                bool candidate = !r.Source.Verbatim && r.Decl == null && r.Enum == null && r.Wrapped == null &&
                                 r.CommentText != null && !r.IsCommentOnly;
                if (!candidate || r.Blank > 0)
                    Flush();
                if (candidate)
                    group.Add(r);
            }
            Flush();
        }

        private void AlignComments(List<RLine> group)
        {
            var withComments = group.Where(r => r.CommentText != null && !r.IsCommentOnly).ToList();
            if (withComments.Count == 0)
                return;
            // the comment column lies right of the widest code line of the whole block
            int column = group.Where(r => !r.IsCommentOnly).Max(r => r.IndentWidth + r.CodeText.Length) + Math.Max(1, _o.SpacesBeforeTrailingComment);
            foreach (var r in withComments)
                r.CommentColumn = column;
        }

        /// <summary>
        /// Groups consecutive lines matching <paramref name="member"/> that share indentation and frame.
        /// Comment-only lines do not interrupt a group; blank lines do when configured.
        /// </summary>
        private IEnumerable<List<RLine>> Groups(List<RLine> lines, Func<RLine, bool> member, bool allowCommentLines = true)
        {
            int i = 0;
            while (i < lines.Count)
            {
                var head = lines[i];
                if (head.Source.Verbatim || !member(head))
                {
                    i++;
                    continue;
                }
                var group = new List<RLine> { head };
                int j = i + 1;
                while (j < lines.Count)
                {
                    var x = lines[j];
                    if (x.Source.Verbatim)
                        break;
                    if (x.Blank > 0 && (_o.BlankLineBreaksAlignment || !allowCommentLines))
                        break;
                    if (member(x) && x.Indent == head.Indent && x.Source.FrameId == head.Source.FrameId)
                    {
                        group.Add(x);
                        j++;
                        continue;
                    }
                    if (allowCommentLines && (x.IsCommentOnly || (x.Code.Count > 0 && x.Code.All(t => t.Kind == TokenKind.Pragma))))
                    {
                        j++;
                        continue;
                    }
                    break;
                }
                yield return group;
                i = j;
            }
        }

        // ------------------------------------------------------------------ call wrapping

        private void WrapCalls(List<RLine> lines)
        {
            if (!_o.WrapLongCalls && _o.WrapCallsWithArgumentsAtLeast <= 0)
                return;
            foreach (var r in lines)
            {
                if (r.Source.Verbatim || !r.Source.IsStatement || r.Decl != null)
                    continue;
                var shape = CallShape.TryParse(r.Source.Tokens);
                if (shape == null || shape.Arguments.Count == 0)
                    continue;

                bool tooLong = _o.WrapLongCalls && _o.MaxLineLength > 0 && r.IndentWidth + r.CodeText.Length > _o.MaxLineLength;
                bool manyArgs = _o.WrapCallsWithArgumentsAtLeast > 0 && shape.Arguments.Count >= _o.WrapCallsWithArgumentsAtLeast;
                if (!tooLong && !manyArgs)
                    continue;

                r.Wrapped = BuildWrappedCall(r, shape);
            }
        }

        private List<string> BuildWrappedCall(RLine r, CallShape shape)
        {
            var tokens = r.Source.Tokens;
            string head = Join(tokens, 0, shape.OpenParen + 1);
            string tail = Join(tokens, shape.CloseParen, shape.CloseParen + 2);
            string spAfter = _o.SpaceAroundAssignment ? " " : "";
            string spBefore = _o.SpaceAroundAssignment && _o.SpaceBeforeArgumentAssignment ? " " : "";

            var args = new List<(string Name, string Op, string Value, string Text)>();
            foreach (var (start, end) in shape.Arguments)
            {
                int op = -1, depth = 0;
                for (int i = start; i < end; i++)
                {
                    var t = tokens[i];
                    if (t.IsOperator("(") || t.IsOperator("[")) depth++;
                    else if (t.IsOperator(")") || t.IsOperator("]")) depth--;
                    else if (depth == 0 && (t.IsOperator(":=") || t.IsOperator("=>")))
                    {
                        op = i;
                        break;
                    }
                }
                if (op > start && op + 1 < end)
                    args.Add((Join(tokens, start, op), tokens[op].Text, Join(tokens, op + 1, end), null));
                else
                    args.Add((null, null, null, Join(tokens, start, end)));
            }

            int nameWidth = _o.AlignWrappedArguments ? args.Where(a => a.Name != null).Select(a => a.Name.Length).DefaultIfEmpty(0).Max() : 0;
            var argTexts = args.Select(a => a.Name == null
                ? a.Text
                : a.Name.PadRight(nameWidth) + spBefore + a.Op + spAfter + a.Value).ToList();

            string comment = r.CommentText == null ? string.Empty : new string(' ', Math.Max(1, _o.SpacesBeforeTrailingComment)) + r.CommentText;
            var result = new List<string>();

            if (_o.CallWrapStyle == CallWrapStyle.AlignWithOpeningParenthesis)
            {
                string inside = _o.SpaceInsideParentheses ? " " : "";
                string pad = new string(' ', head.Length + inside.Length);
                for (int i = 0; i < argTexts.Count; i++)
                {
                    string prefix = i == 0 ? r.Indent + head + inside : r.Indent + pad;
                    bool last = i == argTexts.Count - 1;
                    result.Add(prefix + argTexts[i] + (last ? inside + tail + comment : ","));
                }
                return result;
            }

            string argIndent = r.Indent + IndentString(Math.Max(1, _o.ContinuationIndent));
            result.Add(r.Indent + head);
            for (int i = 0; i < argTexts.Count; i++)
            {
                bool last = i == argTexts.Count - 1;
                if (!last)
                    result.Add(argIndent + argTexts[i] + ",");
                else if (_o.WrappedCallClosingParenthesisOnNewLine)
                    result.Add(argIndent + argTexts[i]);
                else
                    result.Add(argIndent + argTexts[i] + tail + comment);
            }
            if (_o.WrappedCallClosingParenthesisOnNewLine)
                result.Add(r.Indent + tail + comment);
            return result;
        }

        // ------------------------------------------------------------------ tokens

        internal string Join(IList<Token> tokens, int start, int end)
        {
            var sb = new StringBuilder();
            Token previous = null;
            for (int i = start; i < end; i++)
            {
                var t = tokens[i];
                if (previous != null)
                    sb.Append(' ', Spaces(previous, t));
                sb.Append(TokenText(t));
                previous = t;
            }
            return sb.ToString();
        }

        private int Spaces(Token a, Token b)
        {
            int spaces = SpacingRule(a, b);
            if (spaces == 0 && WouldMerge(a, b))
                return 1;
            return spaces;
        }

        private static bool WouldMerge(Token a, Token b)
        {
            var relexed = Lexer.Tokenize(a.Text + b.Text);
            return relexed.Count != 2 || relexed[0].Text != a.Text || relexed[1].Kind != b.Kind;
        }

        private int SpacingRule(Token a, Token b)
        {
            if (a.Kind == TokenKind.Unknown || b.Kind == TokenKind.Unknown)
                return _spaceBefore.Contains(b) ? 1 : 0;
            if (a.IsComment || b.IsComment || a.Kind == TokenKind.Pragma || b.Kind == TokenKind.Pragma)
                return 1;

            string at = a.Kind == TokenKind.Operator ? a.Text : null;
            string bt = b.Kind == TokenKind.Operator ? b.Text : null;

            if (bt == ";")
                return 0;
            if (bt == ",")
                return _o.SpaceBeforeComma ? 1 : 0;
            if (at == ",")
                return _o.SpaceAfterComma ? 1 : 0;
            if (at == ";")
                return 1;

            if (_caseColons.Contains(b))
                return 0;
            if (_caseColons.Contains(a))
                return 1;
            if (bt == ":")
                return _o.SpaceBeforeColon ? 1 : 0;
            if (at == ":")
                return _o.SpaceAfterColon ? 1 : 0;

            if (at == "(" )
                return bt == ")" ? 0 : (_o.SpaceInsideParentheses ? 1 : 0);
            if (bt == ")")
                return _o.SpaceInsideParentheses ? 1 : 0;
            if (at == "[")
                return bt == "]" ? 0 : (_o.SpaceInsideBrackets ? 1 : 0);
            if (bt == "]")
                return _o.SpaceInsideBrackets ? 1 : 0;

            if (bt == "(")
            {
                if (IsOperandEnd(a) || (a.Kind == TokenKind.Keyword && a.Text.StartsWith("__", StringComparison.Ordinal)))
                    return _o.SpaceBeforeCallParenthesis ? 1 : 0;
                if (_unary.Contains(a))
                    return 0;
                return 1;
            }
            if (bt == "[")
            {
                if (a.IsKeyword("ARRAY"))
                    return _o.SpaceAfterArrayKeyword ? 1 : 0;
                if (IsOperandEnd(a) || a.Kind == TokenKind.String)
                    return 0;
                return 1;
            }

            if (at == "." || bt == "." || at == "#" || bt == "#")
                return 0;
            if (at == ".." || bt == "..")
                return _o.SpaceAroundRange ? 1 : 0;
            if (bt == "^")
                return 0;

            if (_argumentAssignments.Contains(b))
                return _o.SpaceAroundAssignment && _o.SpaceBeforeArgumentAssignment ? 1 : 0;
            if (StLanguage.IsAssignmentOperator(a) || StLanguage.IsAssignmentOperator(b))
                return _o.SpaceAroundAssignment ? 1 : 0;

            if (_unary.Contains(a))
                return 0;
            if ((at != null && BinaryOperators.Contains(at)) || (bt != null && BinaryOperators.Contains(bt)))
                return _o.SpaceAroundOperators ? 1 : 0;

            return 1;
        }

        private static bool IsOperandEnd(Token t) =>
            t.Kind == TokenKind.Identifier || t.Kind == TokenKind.TypeName ||
            (t.Kind == TokenKind.Operator && (t.Text == ")" || t.Text == "]" || t.Text == "^"));

        internal string TokenText(Token t)
        {
            switch (t.Kind)
            {
                case TokenKind.Keyword:
                    return Case(t.Text, t.Is("TRUE") || t.Is("FALSE") ? _o.LiteralCasing : _o.KeywordCasing);
                case TokenKind.TypeName:
                    return Case(t.Text, _o.TypeCasing);
                case TokenKind.Operator:
                    return t.Text.Length > 1 && char.IsLetter(t.Text[0]) ? Case(t.Text, _o.KeywordCasing) : t.Text;
                case TokenKind.Number:
                case TokenKind.DirectAddress:
                    return Case(t.Text, _o.LiteralCasing);
                case TokenKind.TimeLiteral:
                    int hash = t.Text.IndexOf('#');
                    return Case(t.Text.Substring(0, hash + 1), _o.LiteralCasing) + Case(t.Text.Substring(hash + 1), _o.TimeUnitCasing);
                case TokenKind.LineComment:
                    string text = t.Text.TrimEnd();
                    if (_o.SpaceAfterLineCommentStart && text.Length > 2 && char.IsLetterOrDigit(text[2]))
                        text = "// " + text.Substring(2);
                    return text;
                default:
                    return t.Text;
            }
        }

        private static string Case(string text, CasingStyle style)
        {
            switch (style)
            {
                case CasingStyle.Upper: return text.ToUpperInvariant();
                case CasingStyle.Lower: return text.ToLowerInvariant();
                default: return text;
            }
        }
    }
}
