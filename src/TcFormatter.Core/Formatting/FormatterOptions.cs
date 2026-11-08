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

    /// <summary>Space before the colon of CASE labels.</summary>
    public enum CaseLabelColonSpace { None, Always, AfterNames }

    /// <summary>How lists in parentheses (calls, enums, initializers) are broken over lines.</summary>
    public enum LineBreakMode
    {
        /// <summary>Keep the line breaks as written.</summary>
        Preserve,

        /// <summary>One line unless it is too long or has too many elements; wrapped lists are re-joined.</summary>
        Long,

        /// <summary>Like Long, but lists that are already wrapped stay wrapped.</summary>
        LongOrMultiline,

        /// <summary>Always one element per line.</summary>
        Always,

        /// <summary>Always one line.</summary>
        Never,
    }

    /// <summary>
    /// All formatting options. Every property maps 1:1 to a key in the "format" section of .tcformat.json
    /// (camelCase) and to an entry on the IDE options page.
    /// </summary>
    public sealed class FormatterOptions
    {
        // ---------------------------------------------------------------- Indentation & whitespace
        [Category("1 Indentation"), Description("Indent with tabs or spaces. Alignment inside lines uses spaces unless AlignWithTabs is on.")]
        public IndentStyle IndentStyle { get; set; } = IndentStyle.Tabs;

        [Category("1 Indentation"), Description("Number of spaces per indentation level (also the tab width used to compute line length).")]
        public int IndentSize { get; set; } = 4;

        [Category("1 Indentation"), Description("Extra indentation levels for continuation lines of a statement that spans several lines.")]
        public int ContinuationIndent { get; set; } = 1;

        [Category("1 Indentation"), Description("Indent continuation lines of IF / ELSIF / WHILE / UNTIL conditions. Off: they continue directly under the IF.")]
        public bool IndentConditionContinuation { get; set; } = true;

        [Category("1 Indentation"), Description("Align continuation lines of an assignment under its ':=' (e.g. 'bOk := bA' / '       AND bB;').")]
        public bool AlignContinuationToAssignment { get; set; } = false;

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

        [Category("3 Spacing"), Description("Space before ':=' and '=>' of call arguments. Off gives the TwinCAT F2 style 'fbTimer(IN:= bStart, Q=> bDone)'.")]
        public bool SpaceBeforeArgumentAssignment { get; set; } = true;

        [Category("3 Spacing"), Description("Keep a space after ':=' / '=>' of call arguments without a value, as written by the TwinCAT input assistant: 'fbTimer(IN:= , Q=> )'.")]
        public bool SpaceAfterEmptyArgumentAssignment { get; set; } = false;

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

        [Category("3 Spacing"), Description("Text at the end of lines of multi-line comments that is aligned at CommentLineEndMarkerColumn, e.g. '<br>' in help headers. Empty = off.")]
        public string CommentLineEndMarker { get; set; } = "";

        [Category("3 Spacing"), Description("Column (1-based, tabs count as IndentSize) of CommentLineEndMarker; longer lines move the marker to the next tab stop (or one space further). With AlignWithTabs the gap is filled with tabs. 0 = off. Example: tab stop 37 with tab width 4 = 149.")]
        public int CommentLineEndMarkerColumn { get; set; } = 0;

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

        [Category("5 Declarations"), Description("Put the data type of declarations at this fixed column (1-based, tabs count as IndentSize). 0 = align automatically. Example: type at tab 10 with tab width 4 = 37. With AlignWithTabs the names are padded with tabs when the ':' falls on a tab stop (': ' on tab stop 41 -> 43).")]
        public int DeclarationTypeColumn { get; set; } = 0;

        [Category("5 Declarations"), Description("Indent comments between the POU header (FUNCTION_BLOCK, METHOD, ...) and the first VAR block by one level.")]
        public bool IndentCommentsAfterPouHeader { get; set; } = false;

        [Category("5 Declarations"), Description("Put trailing comments of declarations at this fixed column (1-based, tabs count as IndentSize); longer lines move the comment to the next tab stop (or one space further). 0 = use DeclarationCommentAlignment.")]
        public int DeclarationCommentColumn { get; set; } = 0;

        [Category("5 Declarations"), Description("Align the ':=' of enum members.")]
        public bool AlignEnumValues { get; set; } = true;

        // ---------------------------------------------------------------- Statements
        [Category("6 Statements"), Description("Semicolon after END_IF / END_CASE / END_FOR / END_WHILE / END_REPEAT.")]
        public SemicolonStyle EndKeywordSemicolon { get; set; } = SemicolonStyle.Preserve;

        [Category("6 Statements"), Description("Align trailing comments of consecutive code lines.")]
        public TrailingCommentAlignment CodeCommentAlignment { get; set; } = TrailingCommentAlignment.None;

        [Category("6 Statements"), Description("Align ':=' of consecutive simple assignments.")]
        public bool AlignConsecutiveAssignments { get; set; } = false;

        [Category("6 Statements"), Description("Line breaking of call statements with arguments. Long: one line unless too long (wrapped calls are re-joined, like Black). LongOrMultiline: also keep calls that are already wrapped wrapped. Always: one argument per line. Never: always one line. Preserve: keep the existing line breaks.")]
        public LineBreakMode CallLineBreakMode { get; set; } = LineBreakMode.Long;

        [Category("6 Statements"), Description("Maximum number of call arguments on a single line; calls with more arguments are wrapped (0 = no limit).")]
        public int MaxCallArgumentsOnSingleLine { get; set; } = 0;

        [Category("6 Statements"), Description("Layout of wrapped calls.")]
        public CallWrapStyle CallWrapStyle { get; set; } = CallWrapStyle.OnePerLineIndented;

        [Category("6 Statements"), Description("Put the closing parenthesis of a wrapped call on its own line.")]
        public bool WrappedCallClosingParenthesisOnNewLine { get; set; } = false;

        [Category("6 Statements"), Description("Align ':=' and '=>' of the arguments of wrapped calls and initializers.")]
        public bool AlignWrappedArguments { get; set; } = true;

        [Category("6 Statements"), Description("Keep 'IF a THEN b; END_IF' with a single statement on one line (and join such IF statements) when it fits into MaxLineLength.")]
        public bool SimpleIfOnSingleLine { get; set; } = false;

        [Category("6 Statements"), Description("Put THEN on its own line.")]
        public bool LineBreakBeforeThen { get; set; } = false;

        [Category("6 Statements"), Description("Indent THEN when it is on its own line.")]
        public bool IndentThen { get; set; } = false;

        [Category("6 Statements"), Description("Put DO of FOR / WHILE on its own line.")]
        public bool LineBreakBeforeDo { get; set; } = false;

        [Category("6 Statements"), Description("Indent DO when it is on its own line.")]
        public bool IndentDo { get; set; } = false;

        [Category("6 Statements"), Description("Keep line breaks inside statements. Off: statements without comments or control keywords are joined into one line when they fit into MaxLineLength.")]
        public bool KeepExistingLineBreaks { get; set; } = true;

        // ---------------------------------------------------------------- CASE
        [Category("7 CASE"), Description("Indent ELSE of a CASE statement like the labels. Off: ELSE is aligned with CASE.")]
        public bool IndentCaseElse { get; set; } = true;

        [Category("7 CASE"), Description("Space before the ':' of CASE labels. AfterNames: only after symbolic labels ('SEQ.INIT :' but '10:').")]
        public CaseLabelColonSpace SpaceBeforeCaseLabelColon { get; set; } = CaseLabelColonSpace.None;

        [Category("7 CASE"), Description("A comment directly before a CASE label or ELSE gets the indentation of the label.")]
        public bool CommentBeforeCaseLabelAtLabelLevel { get; set; } = false;

        [Category("6 Statements"), Description("A comment directly before ELSIF / ELSE of an IF statement gets the indentation of ELSIF / ELSE (e.g. a section header comment).")]
        public bool CommentBeforeElseAtElseLevel { get; set; } = false;

        [Category("7 CASE"), Description("Minimum blank lines after a CASE branch that consists of a single line.")]
        public int BlankLinesAfterSingleLineCase { get; set; } = 0;

        [Category("7 CASE"), Description("Minimum blank lines after a CASE branch that spans several lines.")]
        public int BlankLinesAfterMultiLineCase { get; set; } = 0;

        // ---------------------------------------------------------------- Types and initializers
        [Category("8 Types and initializers"), Description("Put STRUCT / UNION on the line after 'TYPE name :'. Off: 'TYPE name : STRUCT'.")]
        public bool StructOnNewLine { get; set; } = true;

        [Category("8 Types and initializers"), Description("Line breaking of implicit enums 'TYPE E : (A, B) END_TYPE' (see CallLineBreakMode for the values).")]
        public LineBreakMode EnumLineBreakMode { get; set; } = LineBreakMode.Preserve;

        [Category("8 Types and initializers"), Description("Maximum number of enum values on a single line (0 = no limit).")]
        public int MaxEnumValuesOnSingleLine { get; set; } = 0;

        [Category("8 Types and initializers"), Description("Line break before the '(' of a wrapped enum.")]
        public bool LineBreakBeforeEnumOpenParenthesis { get; set; } = true;

        [Category("8 Types and initializers"), Description("Line break after the '(' of a wrapped enum.")]
        public bool LineBreakAfterEnumOpenParenthesis { get; set; } = true;

        [Category("8 Types and initializers"), Description("Line break before the ')' of a wrapped enum.")]
        public bool LineBreakBeforeEnumCloseParenthesis { get; set; } = true;

        [Category("8 Types and initializers"), Description("Line breaking of structure, array and function block initializers in declarations (see CallLineBreakMode for the values).")]
        public LineBreakMode InitializerLineBreakMode { get; set; } = LineBreakMode.Preserve;

        [Category("8 Types and initializers"), Description("Maximum number of initializer values on a single line (0 = no limit).")]
        public int MaxInitializerArgumentsOnSingleLine { get; set; } = 0;

        [Category("8 Types and initializers"), Description("Put the closing parenthesis / bracket of a wrapped initializer on its own line.")]
        public bool InitializerClosingParenthesisOnNewLine { get; set; } = false;

        // ---------------------------------------------------------------- more lines / spacing / alignment
        [Category("2 Lines"), Description("Keep existing blank lines (up to MaxConsecutiveBlankLines). Off: blank lines are removed except where other options add them.")]
        public bool KeepExistingBlankLines { get; set; } = true;

        [Category("2 Lines"), Description("Blank lines at the start of the text.")]
        public int BlankLinesAtFileStart { get; set; } = 0;

        [Category("2 Lines"), Description("Minimum blank lines before IF / CASE / FOR / WHILE / REPEAT statements (not at the start of a block).")]
        public int BlankLinesBeforeStatementWithBody { get; set; } = 0;

        [Category("2 Lines"), Description("Minimum blank lines after END_IF / END_CASE / END_FOR / END_WHILE / END_REPEAT (not at the end of a block).")]
        public int BlankLinesAfterStatementWithBody { get; set; } = 0;

        [Category("2 Lines"), Description("Minimum blank lines before a statement that spans several lines.")]
        public int BlankLinesBeforeMultiLineStatement { get; set; } = 0;

        [Category("2 Lines"), Description("Minimum blank lines after a statement that spans several lines.")]
        public int BlankLinesAfterMultiLineStatement { get; set; } = 0;

        [Category("2 Lines"), Description("Minimum blank lines before a single line statement.")]
        public int BlankLinesBeforeSingleLineStatement { get; set; } = 0;

        [Category("2 Lines"), Description("Minimum blank lines after a single line statement.")]
        public int BlankLinesAfterSingleLineStatement { get; set; } = 0;

        [Category("2 Lines"), Description("Minimum blank lines before a comment line.")]
        public int BlankLinesBeforeSingleLineComment { get; set; } = 0;

        [Category("2 Lines"), Description("Minimum blank lines after a comment line.")]
        public int BlankLinesAfterSingleLineComment { get; set; } = 0;

        [Category("2 Lines"), Description("Minimum blank lines before a comment that spans several lines.")]
        public int BlankLinesBeforeMultiLineComment { get; set; } = 0;

        [Category("2 Lines"), Description("Minimum blank lines after a comment that spans several lines.")]
        public int BlankLinesAfterMultiLineComment { get; set; } = 0;

        [Category("2 Lines"), Description("Minimum blank lines before a single line declaration.")]
        public int BlankLinesBeforeSingleLineDeclaration { get; set; } = 0;

        [Category("2 Lines"), Description("Minimum blank lines after a single line declaration.")]
        public int BlankLinesAfterSingleLineDeclaration { get; set; } = 0;

        [Category("2 Lines"), Description("Minimum blank lines before a declaration that spans several lines.")]
        public int BlankLinesBeforeMultiLineDeclaration { get; set; } = 0;

        [Category("2 Lines"), Description("Minimum blank lines after a declaration that spans several lines.")]
        public int BlankLinesAfterMultiLineDeclaration { get; set; } = 0;

        [Category("2 Lines"), Description("Minimum blank lines after END_VAR.")]
        public int BlankLinesAfterVarBlocks { get; set; } = 0;

        [Category("8 Types and initializers"), Description("Put the type name on the line after TYPE.")]
        public bool LineBreakAfterTypeKeyword { get; set; } = false;

        [Category("8 Types and initializers"), Description("Indent everything between TYPE and END_TYPE.")]
        public bool IndentTypeDeclaration { get; set; } = false;

        [Category("8 Types and initializers"), Description("Line break after the '(' / '[' of a wrapped initializer. Off: the first value follows the parenthesis and the others are aligned under it.")]
        public bool LineBreakAfterInitializerOpenParenthesis { get; set; } = true;

        [Category("3 Spacing"), Description("With 'SpaceInsideParentheses': also put spaces between nested parentheses '( ( a ) )'.")]
        public bool SpaceBetweenNestedParentheses { get; set; } = true;

        [Category("5 Declarations"), Description("Align columns (declarations, initializers, enum values, comments) with tabs on tab stops instead of spaces. Only with IndentStyle = Tabs.")]
        public bool AlignWithTabs { get; set; } = false;

        public FormatterOptions Clone() => (FormatterOptions)MemberwiseClone();
    }
}
