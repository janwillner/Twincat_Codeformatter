using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace TcFormatter.Formatting
{
    /// <summary>Before / after preview of one option.</summary>
    public sealed class OptionPreview
    {
        public OptionPreview(string before, string after, string error = null)
        {
            Before = before;
            After = after;
            Error = error;
        }

        /// <summary>Example code before formatting (tabs shown as →).</summary>
        public string Before { get; }

        /// <summary>The example formatted with the given settings.</summary>
        public string After { get; }

        public string Error { get; }
    }

    /// <summary>
    /// A small code example for every <see cref="FormatterOptions"/> property. The "after" text is produced by the
    /// real formatter with the current settings, so the preview is always correct for every value.
    /// </summary>
    public static class FormatterOptionExamples
    {
        private sealed class Example
        {
            public CodePart Part;
            public string Code;
            public Action<FormatterOptions> Prepare;
            public int[] IntAlternatives;
            public string[] StringAlternatives;
            public bool ShowLineEnds;
        }

        private static readonly Dictionary<string, Example> Examples = new Dictionary<string, Example>(StringComparer.OrdinalIgnoreCase);

        private const string IfBlock = "IF bStart THEN\nnCount := nCount + 1;\nEND_IF";
        private const string CaseBlock = "CASE nState OF\n1:\nnStep := 10;\n2:\nnStep := 20;\nEND_CASE";
        private const string WrappedCall = "fbTimer(\nIN := bStart,\nPT := T#1S);";
        private const string ShortCall = "fbTimer(IN := bStart, PT := T#1S);";
        private const string EnumType = "TYPE E_Mode :\n(\nIdle := 0,\nRunning := 10\n);\nEND_TYPE";
        private const string Initializer = "VAR\nstCfg : ST_Cfg := (nSpeed := 100, bEnable := TRUE);\nEND_VAR";
        private const string Struct = "TYPE ST_Motor :\nSTRUCT\nbOn : BOOL;\nfSpeed : LREAL;\nEND_STRUCT\nEND_TYPE";
        private const string Declarations = "VAR\nbStart : BOOL;\nnCounter : INT := 5;\nEND_VAR";

        static FormatterOptionExamples()
        {
            // ---------------------------------------------------------------- indentation
            I(nameof(FormatterOptions.IndentStyle), IfBlock);
            I(nameof(FormatterOptions.IndentSize), IfBlock, ints: 2);
            I(nameof(FormatterOptions.ContinuationIndent), "bResult := bA AND bB\nOR bC;", ints: 2);
            I(nameof(FormatterOptions.IndentConditionContinuation), "IF bA\nAND bB THEN\nnCount := 1;\nEND_IF");
            I(nameof(FormatterOptions.AlignContinuationToAssignment), "bResult := bA AND bB\nOR bC;");
            D(nameof(FormatterOptions.IndentVarBlocks), Declarations);
            D(nameof(FormatterOptions.IndentStructMembers), Struct);
            I(nameof(FormatterOptions.IndentCaseLabels), CaseBlock);
            I(nameof(FormatterOptions.IndentCaseBody), CaseBlock);
            I(nameof(FormatterOptions.IndentRegions), "{region \"Init\"}\nnCount := 0;\n{endregion}");

            // ---------------------------------------------------------------- lines
            I(nameof(FormatterOptions.LineEnding), "nA := 1;\r\nnB := 2;", lineEnds: true);
            I(nameof(FormatterOptions.MaxConsecutiveBlankLines), "nA := 1;\n\n\n\nnB := 2;", ints: 2);
            I(nameof(FormatterOptions.RemoveBlankLinesAtBlockStart), "IF bStart THEN\n\nnCount := 1;\nEND_IF");
            I(nameof(FormatterOptions.RemoveBlankLinesAtBlockEnd), "IF bStart THEN\nnCount := 1;\n\nEND_IF");
            I(nameof(FormatterOptions.FinalNewline), "nA := 1;", lineEnds: true);
            I(nameof(FormatterOptions.OneStatementPerLine), "nA := 1; nB := 2;");
            I(nameof(FormatterOptions.ExpandSingleLineBlocks), "IF bStart THEN nCount := 1; END_IF");
            I(nameof(FormatterOptions.CaseStatementOnNewLine), "CASE nState OF\n1: nStep := 10;\nEND_CASE");
            I(nameof(FormatterOptions.MaxLineLength), "fbAxis(Axis := stAxis, Execute := bStart, Position := fTarget, Velocity := fVelocity);", ints: 60);
            I(nameof(FormatterOptions.KeepExistingBlankLines), "nA := 1;\n\nnB := 2;");
            I(nameof(FormatterOptions.BlankLinesAtFileStart), "nA := 1;", ints: 1);

            // ---------------------------------------------------------------- blank line minimums
            const string body = "nA := 1;\nIF bStart THEN\nnCount := 1;\nEND_IF\nnB := 2;";
            const string multi = "nA := 1;\nnSum := nX +\nnY;\nnB := 2;";
            const string single = "nA := 1;\nnB := 2;";
            const string comment = "nA := 1;\n// explanation\nnB := 2;";
            const string multiComment = "nA := 1;\n(* first line\n   second line *)\nnB := 2;";
            const string decls = "VAR\nnA : INT;\naData : ARRAY[1..2] OF INT := [\n1, 2];\nnB : INT;\nEND_VAR";
            I(nameof(FormatterOptions.BlankLinesBeforeStatementWithBody), body, ints: 1);
            I(nameof(FormatterOptions.BlankLinesAfterStatementWithBody), body, ints: 1);
            I(nameof(FormatterOptions.BlankLinesBeforeMultiLineStatement), multi, ints: 1);
            I(nameof(FormatterOptions.BlankLinesAfterMultiLineStatement), multi, ints: 1);
            I(nameof(FormatterOptions.BlankLinesBeforeSingleLineStatement), single, ints: 1);
            I(nameof(FormatterOptions.BlankLinesAfterSingleLineStatement), single, ints: 1);
            I(nameof(FormatterOptions.BlankLinesBeforeSingleLineComment), comment, ints: 1);
            I(nameof(FormatterOptions.BlankLinesAfterSingleLineComment), comment, ints: 1);
            I(nameof(FormatterOptions.BlankLinesBeforeMultiLineComment), multiComment, ints: 1);
            I(nameof(FormatterOptions.BlankLinesAfterMultiLineComment), multiComment, ints: 1);
            D(nameof(FormatterOptions.BlankLinesBeforeSingleLineDeclaration), "VAR\nnA : INT;\nnB : INT;\nEND_VAR", ints: 1);
            D(nameof(FormatterOptions.BlankLinesAfterSingleLineDeclaration), "VAR\nnA : INT;\nnB : INT;\nEND_VAR", ints: 1);
            D(nameof(FormatterOptions.BlankLinesBeforeMultiLineDeclaration), decls, ints: 1);
            D(nameof(FormatterOptions.BlankLinesAfterMultiLineDeclaration), decls, ints: 1);
            D(nameof(FormatterOptions.BlankLinesAfterVarBlocks), "VAR_INPUT\nbStart : BOOL;\nEND_VAR\nVAR\nnCount : INT;\nEND_VAR", ints: 1);

            // ---------------------------------------------------------------- spacing
            I(nameof(FormatterOptions.SpaceAroundAssignment), "nCount:=nCount+1;");
            I(nameof(FormatterOptions.SpaceBeforeArgumentAssignment), "fbTimer(IN:=bStart, Q=>bDone);");
            I(nameof(FormatterOptions.SpaceAroundOperators), "nSum:=a+b*c;");
            I(nameof(FormatterOptions.SpaceAfterComma), "nMax := MAX(nA,nB);");
            I(nameof(FormatterOptions.SpaceBeforeComma), "nMax := MAX(nA,nB);");
            I(nameof(FormatterOptions.SpaceInsideParentheses), "nSum := (a + b) * c;");
            I(nameof(FormatterOptions.SpaceInsideBrackets), "nValue := aData[i];");
            I(nameof(FormatterOptions.SpaceBeforeCallParenthesis), "fbTimer(IN := bStart);");
            D(nameof(FormatterOptions.SpaceAfterArrayKeyword), "VAR\naData : ARRAY[1..3] OF INT;\nEND_VAR");
            D(nameof(FormatterOptions.SpaceAroundRange), "VAR\naData : ARRAY[1..3] OF INT;\nEND_VAR");
            D(nameof(FormatterOptions.SpaceBeforeColon), "VAR\nnCount:INT;\nEND_VAR");
            D(nameof(FormatterOptions.SpaceAfterColon), "VAR\nnCount:INT;\nEND_VAR");
            I(nameof(FormatterOptions.SpaceAfterLineCommentStart), "nCount := 1; //start value");
            const string header = "(*\nDESCRIPTION:<br>\nMotor control\t\t<br>\n*)\nFUNCTION_BLOCK FB_Motor\nVAR\nEND_VAR";
            D(nameof(FormatterOptions.CommentLineEndMarker), header, o => o.CommentLineEndMarkerColumn = 25);
            Examples[nameof(FormatterOptions.CommentLineEndMarker)].StringAlternatives = new[] { "<br>" };
            D(nameof(FormatterOptions.CommentLineEndMarkerColumn), header, o => o.CommentLineEndMarker = "<br>", ints: 25);
            I(nameof(FormatterOptions.SpacesBeforeTrailingComment), "nCount := 1; // start value", ints: 4);
            I(nameof(FormatterOptions.SpaceBetweenNestedParentheses), "nSum := ((a + b) * c);", o => o.SpaceInsideParentheses = true);

            // ---------------------------------------------------------------- casing
            I(nameof(FormatterOptions.KeywordCasing), "If bA And Not bB Then\nnCount := 1;\nEnd_If");
            D(nameof(FormatterOptions.TypeCasing), "VAR\nnCount : Int;\nsName : String;\nEND_VAR");
            I(nameof(FormatterOptions.LiteralCasing), "bOk := True;\nnMask := 16#fF;\ntWait := t#1s;");
            I(nameof(FormatterOptions.TimeUnitCasing), "tWait := T#1S500ms;");

            // ---------------------------------------------------------------- declarations
            D(nameof(FormatterOptions.AlignDeclarationColons), Declarations);
            D(nameof(FormatterOptions.AlignDeclarationInitializers), "VAR\nbStart : BOOL := TRUE;\nnCounter : INT := 5;\nEND_VAR");
            D(nameof(FormatterOptions.DeclarationCommentAlignment), "VAR\nbStart : BOOL; // start\nnCounterValue : INT; // counter\nEND_VAR");
            D(nameof(FormatterOptions.BlankLineBreaksAlignment), "VAR\nnA : INT;\nbLonger : BOOL;\n\nnX : INT;\nEND_VAR");
            D(nameof(FormatterOptions.MaxAlignedNameLength), "VAR\nnA : INT;\nbAVeryLongNameForTheExample : BOOL;\nnB : INT;\nEND_VAR", ints: 10);
            D(nameof(FormatterOptions.DeclarationTypeColumn), Declarations, ints: 25);
            D(nameof(FormatterOptions.IndentCommentsAfterPouHeader), "FUNCTION_BLOCK FB_Motor\n(* template note *)\nVAR_INPUT\nbStart : BOOL;\nEND_VAR");
            D(nameof(FormatterOptions.DeclarationCommentColumn), "VAR\nbStart : BOOL; // start\nnCounterValue : INT; // counter\nEND_VAR", ints: 41);
            D(nameof(FormatterOptions.AlignEnumValues), EnumType);
            D(nameof(FormatterOptions.AlignWithTabs), "VAR\nbStart : BOOL; // start\nnCounterValue : INT := 5;\nEND_VAR", o => o.IndentStyle = IndentStyle.Tabs);

            // ---------------------------------------------------------------- statements
            I(nameof(FormatterOptions.EndKeywordSemicolon), "IF bStart THEN\nnCount := 1;\nEND_IF;\nWHILE bRun DO\nnCount := 2;\nEND_WHILE");
            I(nameof(FormatterOptions.CodeCommentAlignment), "nA := 1; // one\nnLonger := 2; // two");
            I(nameof(FormatterOptions.AlignConsecutiveAssignments), "nA := 1;\nnLonger := 2;");
            I(nameof(FormatterOptions.CallLineBreakMode), WrappedCall);
            I(nameof(FormatterOptions.MaxCallArgumentsOnSingleLine), ShortCall, ints: 1);
            I(nameof(FormatterOptions.CallWrapStyle), ShortCall, o => o.CallLineBreakMode = LineBreakMode.Always);
            I(nameof(FormatterOptions.WrappedCallClosingParenthesisOnNewLine), ShortCall, o => o.CallLineBreakMode = LineBreakMode.Always);
            I(nameof(FormatterOptions.AlignWrappedArguments), "fbAxis(Axis := stAxis, Execute := bStart);", o => o.CallLineBreakMode = LineBreakMode.Always);
            I(nameof(FormatterOptions.SimpleIfOnSingleLine), "IF bStart THEN nCount := 1; END_IF");
            I(nameof(FormatterOptions.LineBreakBeforeThen), "IF bA AND bB THEN nCount := 1; END_IF");
            I(nameof(FormatterOptions.IndentThen), "IF bA AND bB THEN nCount := 1; END_IF", o => o.LineBreakBeforeThen = true);
            I(nameof(FormatterOptions.LineBreakBeforeDo), "FOR i := 1 TO 3 DO nSum := nSum + i; END_FOR");
            I(nameof(FormatterOptions.IndentDo), "FOR i := 1 TO 3 DO nSum := nSum + i; END_FOR", o => o.LineBreakBeforeDo = true);
            I(nameof(FormatterOptions.KeepExistingLineBreaks), "nSum := nA +\nnB;");

            // ---------------------------------------------------------------- CASE
            I(nameof(FormatterOptions.IndentCaseElse), "CASE nState OF\n1:\nnStep := 10;\nELSE\nnStep := 0;\nEND_CASE");
            I(nameof(FormatterOptions.SpaceBeforeCaseLabelColon), "CASE nSeq OF\nSEQ.INIT:\nnStep := 10;\n20 :\nnStep := 30;\nEND_CASE");
            I(nameof(FormatterOptions.SpaceAfterEmptyArgumentAssignment), "fbTimer(IN:= bStart, PT:= , Q=> );", o => o.SpaceBeforeArgumentAssignment = false);
            I(nameof(FormatterOptions.CommentBeforeElseAtElseLevel), "IF bA THEN\nnX := 1;\n// otherwise\nELSE\nnX := 2;\nEND_IF");
            I(nameof(FormatterOptions.CommentBeforeCaseLabelAtLabelLevel), "CASE nState OF\n1:\nnStep := 10;\n// second step\n2:\nnStep := 20;\nEND_CASE");
            I(nameof(FormatterOptions.BlankLinesAfterSingleLineCase), "CASE nState OF\n1: nStep := 10;\n2: nStep := 20;\nEND_CASE", o => o.CaseStatementOnNewLine = false, ints: 1);
            I(nameof(FormatterOptions.BlankLinesAfterMultiLineCase), "CASE nState OF\n1:\nnStep := 10;\nnNext := 2;\n2:\nnStep := 20;\nEND_CASE", ints: 1);

            // ---------------------------------------------------------------- types and initializers
            D(nameof(FormatterOptions.StructOnNewLine), "TYPE ST_Motor : STRUCT\nbOn : BOOL;\nEND_STRUCT\nEND_TYPE");
            D(nameof(FormatterOptions.LineBreakAfterTypeKeyword), Struct);
            D(nameof(FormatterOptions.IndentTypeDeclaration), Struct, o => o.LineBreakAfterTypeKeyword = true);
            D(nameof(FormatterOptions.EnumLineBreakMode), "TYPE E_Dir :\n(\nLeft,\nRight\n);\nEND_TYPE");
            D(nameof(FormatterOptions.MaxEnumValuesOnSingleLine), "TYPE E_Dir : (Left, Right, Up, Down);\nEND_TYPE", o => o.EnumLineBreakMode = LineBreakMode.Long, ints: 2);
            D(nameof(FormatterOptions.LineBreakBeforeEnumOpenParenthesis), EnumType, o => o.EnumLineBreakMode = LineBreakMode.Always);
            D(nameof(FormatterOptions.LineBreakAfterEnumOpenParenthesis), EnumType, o => o.EnumLineBreakMode = LineBreakMode.Always);
            D(nameof(FormatterOptions.LineBreakBeforeEnumCloseParenthesis), EnumType, o => o.EnumLineBreakMode = LineBreakMode.Always);
            D(nameof(FormatterOptions.InitializerLineBreakMode), Initializer);
            D(nameof(FormatterOptions.MaxInitializerArgumentsOnSingleLine), Initializer, o => o.InitializerLineBreakMode = LineBreakMode.Long, ints: 1);
            D(nameof(FormatterOptions.LineBreakAfterInitializerOpenParenthesis), Initializer, o => o.InitializerLineBreakMode = LineBreakMode.Always);
            D(nameof(FormatterOptions.InitializerClosingParenthesisOnNewLine), Initializer, o => o.InitializerLineBreakMode = LineBreakMode.Always);
        }

        private static void I(string option, string code, Action<FormatterOptions> prepare = null, int ints = 0, bool lineEnds = false) =>
            Add(option, CodePart.Implementation, code, prepare, ints, lineEnds);

        private static void D(string option, string code, Action<FormatterOptions> prepare = null, int ints = 0, bool lineEnds = false) =>
            Add(option, CodePart.Declaration, code, prepare, ints, lineEnds);

        private static void I(string option, string code, int ints) => I(option, code, null, ints);

        private static void D(string option, string code, int ints) => D(option, code, null, ints);

        private static void Add(string option, CodePart part, string code, Action<FormatterOptions> prepare, int ints, bool lineEnds) =>
            Examples[option] = new Example
            {
                Part = part,
                Code = code,
                Prepare = prepare,
                IntAlternatives = ints == 0 ? null : new[] { ints },
                ShowLineEnds = lineEnds,
            };

        public static bool Has(string option) => option != null && Examples.ContainsKey(option);

        /// <summary>Formats the example of <paramref name="option"/> with <paramref name="current"/>.</summary>
        public static OptionPreview Preview(string option, FormatterOptions current)
        {
            if (!Has(option))
                return null;
            var example = Examples[option];
            var options = (current ?? new FormatterOptions()).Clone();
            example.Prepare?.Invoke(options);
            if (!example.ShowLineEnds && options.LineEnding == LineEndingStyle.Auto)
                options.LineEnding = LineEndingStyle.Lf;

            var result = StFormatter.Format(example.Code, example.Part, options);
            int tab = Math.Max(1, options.IndentSize);
            return new OptionPreview(
                Visualize(example.Code, tab, example.ShowLineEnds),
                Visualize(result.Text, tab, example.ShowLineEnds),
                result.Error);
        }

        /// <summary>Copies of <paramref name="current"/> with other values of <paramref name="option"/> (used by tests and tools).</summary>
        public static IEnumerable<FormatterOptions> Alternatives(string option, FormatterOptions current) =>
            DescribeAlternatives(option, current).Select(a => a.Options);

        /// <summary>Like <see cref="Alternatives"/>, with a short text of the changed setting.</summary>
        public static IEnumerable<(string Label, FormatterOptions Options)> DescribeAlternatives(string option, FormatterOptions current)
        {
            var property = typeof(FormatterOptions).GetProperty(option, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (property == null)
                yield break;
            object value = property.GetValue(current);
            IEnumerable<object> others;
            if (property.PropertyType == typeof(bool))
                others = new object[] { !(bool)value };
            else if (property.PropertyType.IsEnum)
                others = Enum.GetValues(property.PropertyType).Cast<object>().Where(v => !v.Equals(value));
            else if (property.PropertyType == typeof(int))
            {
                // the example's value and the default; the next value only when both equal the current one
                var candidates = new List<int>();
                if (Examples.TryGetValue(option, out var e) && e.IntAlternatives != null)
                    candidates.AddRange(e.IntAlternatives);
                candidates.Add((int)property.GetValue(new FormatterOptions()));
                var distinct = candidates.Where(v => v != (int)value).Distinct().ToList();
                if (distinct.Count == 0)
                    distinct.Add((int)value + 1);
                others = distinct.Cast<object>();
            }
            else if (property.PropertyType == typeof(string))
            {
                // the example's value and the default (empty = off)
                var candidates = new List<string>();
                if (Examples.TryGetValue(option, out var e) && e.StringAlternatives != null)
                    candidates.AddRange(e.StringAlternatives);
                candidates.Add((string)property.GetValue(new FormatterOptions()) ?? string.Empty);
                others = candidates.Where(v => v != (string)value).Distinct().Cast<object>();
            }
            else
                yield break;

            foreach (var other in others)
            {
                var copy = current.Clone();
                property.SetValue(copy, other);
                yield return ($"{option} = {other}", copy);
            }
        }

        /// <summary>The description shown in the settings (from the [Description] attribute).</summary>
        public static string Description(string option) =>
            typeof(FormatterOptions).GetProperty(option, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)?
                .GetCustomAttribute<System.ComponentModel.DescriptionAttribute>()?.Description;

        /// <summary>All options that have an example.</summary>
        public static IEnumerable<string> Options =>
            typeof(FormatterOptions).GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(p => p.Name).Where(Has);

        /// <summary>Shows tabs as "→" (up to the next tab stop) and, if requested, the line endings.</summary>
        public static string Visualize(string text, int tabWidth, bool showLineEnds)
        {
            var sb = new StringBuilder();
            int column = 0;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '\t')
                {
                    int next = (column / tabWidth + 1) * tabWidth;
                    sb.Append('→').Append(' ', next - column - 1);
                    column = next;
                }
                else if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                {
                    sb.Append(showLineEnds ? " [CRLF]" : string.Empty).Append(Environment.NewLine);
                    i++;
                    column = 0;
                }
                else if (c == '\n')
                {
                    sb.Append(showLineEnds ? " [LF]" : string.Empty).Append(Environment.NewLine);
                    column = 0;
                }
                else
                {
                    sb.Append(c);
                    column++;
                }
            }
            if (showLineEnds && text.Length > 0 && text[text.Length - 1] != '\n')
                sb.Append(" [no line break at the end]");
            return sb.ToString();
        }
    }
}
