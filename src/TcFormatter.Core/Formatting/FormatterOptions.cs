using System.ComponentModel;

namespace TcFormatter.Formatting
{
    public enum IndentStyle { Tabs, Spaces }

    public enum LineEndingStyle { Auto, CrLf, Lf }

    public enum CasingStyle { Preserve, Upper, Lower }

    public enum SemicolonStyle { Preserve, Add, Remove }

    public enum FinalNewlineStyle { Preserve, Add, Remove }

    public enum TrailingCommentAlignment { None, AlignInBlock }

    public enum CallWrapStyle { OnePerLineIndented, AlignWithOpeningParenthesis }

    /// <summary>
    /// All formatting options. Every property maps 1:1 to a key in the "format" section of .tcformat.json
    /// (camelCase) and to an entry on the IDE options page.
    /// </summary>
    public sealed class FormatterOptions
    {
        // ---------------------------------------------------------------- Indentation & whitespace
        [Category("1 Indentation"), Description("Indent with tabs or spaces. Alignment inside lines always uses spaces.")]
        public IndentStyle IndentStyle { get; set; } = IndentStyle.Tabs;

        [Category("1 Indentation"), Description("Number of spaces per indentation level (also the tab width used to compute line length).")]
        public int IndentSize { get; set; } = 4;

        [Category("1 Indentation"), Description("Extra indentation levels for continuation lines of a statement that spans several lines.")]
        public int ContinuationIndent { get; set; } = 1;

        [Category("1 Indentation"), Description("Indent the members of VAR...END_VAR sections.")]
        public bool IndentVarBlocks { get; set; } = true;

        [Category("1 Indentation"), Description("Indent the members of STRUCT/UNION definitions.")]
        public bool IndentStructMembers { get; set; } = true;

        [Category("1 Indentation"), Description("Indent CASE labels relative to CASE.")]
        public bool IndentCaseLabels { get; set; } = true;

        [Category("1 Indentation"), Description("Indent statements below a CASE label relative to the label.")]
        public bool IndentCaseBody { get; set; } = true;

        [Category("1 Indentation"), Description("Indent code between {region} and {endregion} pragmas.")]
        public bool IndentRegions { get; set; } = false;

        // ---------------------------------------------------------------- Lines
        [Category("2 Lines"), Description("Line ending of the result. Auto keeps the dominant line ending of the input (CRLF when none).")]
        public LineEndingStyle LineEnding { get; set; } = LineEndingStyle.Auto;

        [Category("2 Lines"), Description("Maximum number of consecutive blank lines that are kept.")]
        public int MaxConsecutiveBlankLines { get; set; } = 1;

        [Category("2 Lines"), Description("Remove blank lines directly after a block opener (THEN, DO, VAR, ...).")]
        public bool RemoveBlankLinesAtBlockStart { get; set; } = true;

        [Category("2 Lines"), Description("Remove blank lines directly before a block closer (END_IF, ELSE, END_VAR, ...).")]
        public bool RemoveBlankLinesAtBlockEnd { get; set; } = true;

        [Category("2 Lines"), Description("Whether the text ends with a line break.")]
        public FinalNewlineStyle FinalNewline { get; set; } = FinalNewlineStyle.Preserve;

        [Category("2 Lines"), Description("Put every statement on its own line (splits 'a := 1; b := 2;').")]
        public bool OneStatementPerLine { get; set; } = true;

        [Category("2 Lines"), Description("Expand single-line blocks like 'IF a THEN b := 1; END_IF' into several lines.")]
        public bool ExpandSingleLineBlocks { get; set; } = true;

        [Category("2 Lines"), Description("Put the statement after a CASE label on its own line.")]
        public bool CaseStatementOnNewLine { get; set; } = true;

        [Category("2 Lines"), Description("Maximum line length used for wrapping calls (0 = no limit).")]
        public int MaxLineLength { get; set; } = 120;

        // ---------------------------------------------------------------- Spacing
        [Category("3 Spacing"), Description("Space around assignments := => S= R= REF=.")]
        public bool SpaceAroundAssignment { get; set; } = true;

        [Category("3 Spacing"), Description("Space around binary operators + - * / ** = <> < > <= >= &.")]
        public bool SpaceAroundOperators { get; set; } = true;

        [Category("3 Spacing"), Description("Space after a comma.")]
        public bool SpaceAfterComma { get; set; } = true;

        [Category("3 Spacing"), Description("Space before a comma.")]
        public bool SpaceBeforeComma { get; set; } = false;

        [Category("3 Spacing"), Description("Space directly inside parentheses: '( a )'.")]
        public bool SpaceInsideParentheses { get; set; } = false;

        [Category("3 Spacing"), Description("Space directly inside brackets: '[ i ]'.")]
        public bool SpaceInsideBrackets { get; set; } = false;

        [Category("3 Spacing"), Description("Space between a called name and its '(': 'fbTimer (IN := x)'.")]
        public bool SpaceBeforeCallParenthesis { get; set; } = false;

        [Category("3 Spacing"), Description("Space between ARRAY and '[': 'ARRAY [1..10]'.")]
        public bool SpaceAfterArrayKeyword { get; set; } = false;

        [Category("3 Spacing"), Description("Space around the range operator '..'.")]
        public bool SpaceAroundRange { get; set; } = false;

        [Category("3 Spacing"), Description("Space before ':' in declarations ('nValue : INT').")]
        public bool SpaceBeforeColon { get; set; } = true;

        [Category("3 Spacing"), Description("Space after ':' in declarations.")]
        public bool SpaceAfterColon { get; set; } = true;

        [Category("3 Spacing"), Description("Insert a space after '//' when the comment starts with a letter or digit.")]
        public bool SpaceAfterLineCommentStart { get; set; } = true;

        [Category("3 Spacing"), Description("Minimum spaces between code and a trailing comment.")]
        public int SpacesBeforeTrailingComment { get; set; } = 1;

        // ---------------------------------------------------------------- Casing
        [Category("4 Casing"), Description("Casing of keywords (IF, THEN, VAR, AND, ...).")]
        public CasingStyle KeywordCasing { get; set; } = CasingStyle.Upper;

        [Category("4 Casing"), Description("Casing of elementary data types (BOOL, INT, STRING, ...).")]
        public CasingStyle TypeCasing { get; set; } = CasingStyle.Upper;

        [Category("4 Casing"), Description("Casing of literal prefixes and hex digits (TRUE, 16#FF, T#1S, INT#5).")]
        public CasingStyle LiteralCasing { get; set; } = CasingStyle.Upper;

        [Category("4 Casing"), Description("Casing of time literal units (T#1s500ms vs T#1S500MS).")]
        public CasingStyle TimeUnitCasing { get; set; } = CasingStyle.Preserve;

        // ---------------------------------------------------------------- Declarations
        [Category("5 Declarations"), Description("Align the ':' of consecutive declarations.")]
        public bool AlignDeclarationColons { get; set; } = true;

        [Category("5 Declarations"), Description("Align the ':=' initializers of consecutive declarations.")]
        public bool AlignDeclarationInitializers { get; set; } = true;

        [Category("5 Declarations"), Description("Align trailing comments of consecutive declarations.")]
        public TrailingCommentAlignment DeclarationCommentAlignment { get; set; } = TrailingCommentAlignment.AlignInBlock;

        [Category("5 Declarations"), Description("Blank lines interrupt an alignment block.")]
        public bool BlankLineBreaksAlignment { get; set; } = true;

        [Category("5 Declarations"), Description("Declarations whose name is longer than this are not used to compute alignment (0 = no limit).")]
        public int MaxAlignedNameLength { get; set; } = 40;

        [Category("5 Declarations"), Description("Align the ':=' of enum members.")]
        public bool AlignEnumValues { get; set; } = true;

        // ---------------------------------------------------------------- Statements
        [Category("6 Statements"), Description("Semicolon after END_IF / END_CASE / END_FOR / END_WHILE / END_REPEAT.")]
        public SemicolonStyle EndKeywordSemicolon { get; set; } = SemicolonStyle.Preserve;

        [Category("6 Statements"), Description("Align trailing comments of consecutive code lines.")]
        public TrailingCommentAlignment CodeCommentAlignment { get; set; } = TrailingCommentAlignment.None;

        [Category("6 Statements"), Description("Align ':=' of consecutive simple assignments.")]
        public bool AlignConsecutiveAssignments { get; set; } = false;

        [Category("6 Statements"), Description("Wrap calls with named arguments that exceed MaxLineLength (one argument per line).")]
        public bool WrapLongCalls { get; set; } = true;

        [Category("6 Statements"), Description("Always wrap calls with at least this many named arguments (0 = only when too long).")]
        public int WrapCallsWithArgumentsAtLeast { get; set; } = 0;

        [Category("6 Statements"), Description("Layout of wrapped calls.")]
        public CallWrapStyle CallWrapStyle { get; set; } = CallWrapStyle.OnePerLineIndented;

        [Category("6 Statements"), Description("Put the closing parenthesis of a wrapped call on its own line.")]
        public bool WrappedCallClosingParenthesisOnNewLine { get; set; } = false;

        [Category("6 Statements"), Description("Align ':=' and '=>' of the arguments of wrapped calls.")]
        public bool AlignWrappedArguments { get; set; } = true;

        [Category("6 Statements"), Description("Re-join call statements that were wrapped over several lines (without comments inside) before deciding how to wrap them. Makes call layout fully deterministic, like Black.")]
        public bool RejoinWrappedCalls { get; set; } = true;

        public FormatterOptions Clone() => (FormatterOptions)MemberwiseClone();
    }
}
