using TcFormatter.Configuration;
using TcFormatter.Formatting;

namespace TcFormatter.Core.Tests;

/// <summary>
/// Options that correspond to the publicly documented settings of STweep (stweep.com documentation and
/// release notes). Issue #12.
/// </summary>
public class StweepOptionTests : FormatterTestBase
{
    // ------------------------------------------------------------------ conditional formatting

    [Theory]
    [InlineData("{STweep.Disable}", "{STweep.Enable}")]
    [InlineData("(* STweep.Disable *)", "(* STweep.Enable *)")]
    [InlineData("// tcfmt: off", "// tcfmt: on")]
    public void Disable_and_enable_directives_keep_code_untouched(string off, string on)
    {
        var input = Lines(off, "x   :=   1;", on, "y:=2;");
        Assert.Equal(Lines(off, "x   :=   1;", on, "y := 2;"), Impl(input));
    }

    // ------------------------------------------------------------------ IF / FOR / WHILE

    [Fact]
    public void Simple_if_statements_can_stay_or_be_placed_on_a_single_line()
    {
        Assert.Equal("IF bA THEN x := 1; END_IF",
            Impl("IF bA THEN x:=1; END_IF", o => o.SimpleIfOnSingleLine = true));
        Assert.Equal("IF bA THEN x := 1; END_IF",
            Impl(Lines("IF bA THEN", "x := 1;", "END_IF"), o => o.SimpleIfOnSingleLine = true));

        // not simple: two statements, ELSE, or too long
        Assert.Equal(Lines("IF bA THEN", "    x := 1;", "    y := 2;", "END_IF"),
            Impl("IF bA THEN x := 1; y := 2; END_IF", o => o.SimpleIfOnSingleLine = true));
        Assert.Equal(Lines("IF bA THEN", "    x := 1;", "ELSE", "    x := 2;", "END_IF"),
            Impl("IF bA THEN x := 1; ELSE x := 2; END_IF", o => o.SimpleIfOnSingleLine = true));
        Assert.Equal(Lines("IF bAVeryLongCondition THEN", "    nResult := 1;", "END_IF"),
            Impl("IF bAVeryLongCondition THEN nResult := 1; END_IF", o => { o.SimpleIfOnSingleLine = true; o.MaxLineLength = 30; }));
    }

    [Fact]
    public void Then_can_get_its_own_line_and_indentation()
    {
        var input = "IF bA AND bB THEN x := 1; END_IF";
        Assert.Equal(Lines("IF bA AND bB", "THEN", "    x := 1;", "END_IF"), Impl(input, o => o.LineBreakBeforeThen = true));
        Assert.Equal(Lines("IF bA AND bB", "    THEN", "    x := 1;", "END_IF"), Impl(input, o => { o.LineBreakBeforeThen = true; o.IndentThen = true; }));
    }

    [Fact]
    public void Do_can_get_its_own_line_and_indentation()
    {
        var input = "FOR i := 1 TO 10 DO x := i; END_FOR";
        Assert.Equal(Lines("FOR i := 1 TO 10", "DO", "    x := i;", "END_FOR"), Impl(input, o => o.LineBreakBeforeDo = true));
        Assert.Equal(Lines("WHILE bRun", "    DO", "    x := 1;", "END_WHILE"),
            Impl("WHILE bRun DO x := 1; END_WHILE", o => { o.LineBreakBeforeDo = true; o.IndentDo = true; }));
    }

    // ------------------------------------------------------------------ calls

    [Fact]
    public void Call_line_break_modes()
    {
        var shortCall = "fbTimer(IN := bStart, PT := T#1S);";
        var wrapped = Lines("fbTimer(", "    IN := bStart,", "    PT := T#1S);");

        Assert.Equal(shortCall, Impl(wrapped, o => o.CallLineBreakMode = LineBreakMode.Long));
        Assert.Equal(wrapped, Impl(wrapped, o => o.CallLineBreakMode = LineBreakMode.LongOrMultiline));
        Assert.Equal(shortCall, Impl(shortCall, o => o.CallLineBreakMode = LineBreakMode.LongOrMultiline));
        Assert.Equal(wrapped, Impl(shortCall, o => o.CallLineBreakMode = LineBreakMode.Always));
        Assert.Equal(shortCall, Impl(wrapped, o => { o.CallLineBreakMode = LineBreakMode.Never; o.MaxLineLength = 10; }));
        Assert.Equal(wrapped, Impl(Lines("fbTimer(", "IN := bStart,", "PT := T#1S);"), o => o.CallLineBreakMode = LineBreakMode.Preserve));
        Assert.Equal(shortCall, Impl(shortCall, o => { o.CallLineBreakMode = LineBreakMode.Preserve; o.MaxLineLength = 10; }));
    }

    [Fact]
    public void Maximum_call_arguments_on_a_single_line()
    {
        var call = "fb(a := 1, b := 2, c := 3);";
        Assert.Equal(call, Impl(call, o => o.MaxCallArgumentsOnSingleLine = 3));
        Assert.Equal(Lines("fb(", "    a := 1,", "    b := 2,", "    c := 3);"), Impl(call, o => o.MaxCallArgumentsOnSingleLine = 2));
    }

    // ------------------------------------------------------------------ implicit enums

    private const string Enum = "TYPE E_State :\n(\nIdle := 0,\nRun := 1,\nError := 2\n);\nEND_TYPE";

    [Fact]
    public void Enums_one_value_per_line_with_configurable_parentheses()
    {
        Assert.Equal(Lines("TYPE E_State :", "(", "    Idle  := 0,", "    Run   := 1,", "    Error := 2", ");", "END_TYPE"),
            Decl(Enum, o => o.EnumLineBreakMode = LineBreakMode.Always));

        Assert.Equal(Lines("TYPE E_State : (", "    Idle  := 0,", "    Run   := 1,", "    Error := 2", ");", "END_TYPE"),
            Decl(Enum, o => { o.EnumLineBreakMode = LineBreakMode.Always; o.LineBreakBeforeEnumOpenParenthesis = false; }));

        Assert.Equal(Lines("TYPE E_State :", "(   Idle  := 0,", "    Run   := 1,", "    Error := 2);", "END_TYPE"),
            Decl(Enum, o =>
            {
                o.EnumLineBreakMode = LineBreakMode.Always;
                o.LineBreakAfterEnumOpenParenthesis = false;
                o.LineBreakBeforeEnumCloseParenthesis = false;
            }));
    }

    [Fact]
    public void Short_enums_can_be_placed_on_a_single_line()
    {
        var input = "TYPE E_Dir :\n(\nLeft,\nRight\n) INT;\nEND_TYPE";
        Assert.Equal(Lines("TYPE E_Dir : (Left, Right) INT;", "END_TYPE"),
            Decl(input, o => { o.EnumLineBreakMode = LineBreakMode.Long; o.MaxEnumValuesOnSingleLine = 3; }));
        Assert.Equal(Lines("TYPE E_Dir :", "(", "    Left,", "    Right", ") INT;", "END_TYPE"),
            Decl(input, o => { o.EnumLineBreakMode = LineBreakMode.Long; o.MaxEnumValuesOnSingleLine = 1; }));
    }

    [Fact]
    public void Enums_with_comments_inside_keep_their_lines()
    {
        var input = "TYPE E_State :\n(\nIdle := 0, // waiting\nRun := 1\n);\nEND_TYPE";
        Assert.Equal(Lines("TYPE E_State :", "(", "    Idle := 0, // waiting", "    Run  := 1", ");", "END_TYPE"),
            Decl(input, o => o.EnumLineBreakMode = LineBreakMode.Never));
    }

    // ------------------------------------------------------------------ initializers

    [Fact]
    public void Initializers_can_be_wrapped()
    {
        var input = Lines("VAR", "stCfg : ST_Cfg := (nSpeed := 100, bEnable := TRUE);", "fbAxis : FB_Axis(nId := 1, sName := 'X');", "aData : ARRAY[1..3] OF INT := [1, 2, 3];", "END_VAR");

        Assert.Equal(Lines(
            "VAR",
            "    stCfg : ST_Cfg := (",
            "        nSpeed  := 100,",
            "        bEnable := TRUE);",
            "    fbAxis : FB_Axis(",
            "        nId   := 1,",
            "        sName := 'X');",
            "    aData : ARRAY[1..3] OF INT := [",
            "        1,",
            "        2,",
            "        3];",
            "END_VAR"), Decl(input, o => o.InitializerLineBreakMode = LineBreakMode.Always));

        Assert.Equal(Lines(
            "VAR",
            "    stCfg : ST_Cfg := (",
            "        nSpeed  := 100,",
            "        bEnable := TRUE",
            "    );",
            "END_VAR"), Decl(Lines("VAR", "stCfg : ST_Cfg := (nSpeed := 100, bEnable := TRUE);", "END_VAR"),
                o => { o.InitializerLineBreakMode = LineBreakMode.Always; o.InitializerClosingParenthesisOnNewLine = true; }));
    }

    [Fact]
    public void Wrapped_initializers_are_joined_when_they_fit()
    {
        var input = Lines("VAR", "aData : ARRAY[1..3] OF INT := [", "1, 2,", "3];", "n : INT;", "END_VAR");
        Assert.Equal(Lines("VAR", "    aData : ARRAY[1..3] OF INT := [1, 2, 3];", "    n     : INT;", "END_VAR"),
            Decl(input, o => o.InitializerLineBreakMode = LineBreakMode.Long));
        Assert.Equal(Lines("VAR", "    aData : ARRAY[1..3] OF INT := [1, 2, 3];", "    n     : INT;", "END_VAR"),
            Decl(input, o => { o.InitializerLineBreakMode = LineBreakMode.Long; o.MaxInitializerArgumentsOnSingleLine = 3; }));
        Assert.Equal(Lines("VAR", "    aData : ARRAY[1..3] OF INT := [", "        1,", "        2,", "        3];", "    n : INT;", "END_VAR"),
            Decl(input, o => { o.InitializerLineBreakMode = LineBreakMode.Long; o.MaxInitializerArgumentsOnSingleLine = 2; }));
    }

    // ------------------------------------------------------------------ blank lines and line breaks

    [Fact]
    public void Existing_blank_lines_can_be_removed()
    {
        Assert.Equal(Lines("a := 1;", "b := 2;"), Impl(Lines("a := 1;", "", "b := 2;"), o => o.KeepExistingBlankLines = false));
    }

    [Fact]
    public void Existing_line_breaks_can_be_removed()
    {
        var input = Lines("nResult := nA +", "nB;", "IF bA", "AND bB THEN", "x := 1;", "END_IF");
        Assert.Equal(Lines("nResult := nA + nB;", "IF bA", "    AND bB THEN", "    x := 1;", "END_IF"),
            Impl(input, o => o.KeepExistingLineBreaks = false));
        Assert.Equal(Lines("nResult := nA +", "    nB;", "IF bA", "    AND bB THEN", "    x := 1;", "END_IF"), Impl(input));
    }

    [Fact]
    public void Blank_lines_after_case_branches()
    {
        var input = Lines("CASE n OF", "1: a := 1;", "2:", "b := 1;", "c := 2;", "3: d := 1;", "ELSE", "e := 0;", "END_CASE");
        Assert.Equal(Lines(
            "CASE n OF",
            "    1: a := 1;",
            "    2:",
            "        b := 1;",
            "        c := 2;",
            "",
            "",
            "    3: d := 1;",
            "    ELSE",
            "        e := 0;",
            "END_CASE"), Impl(input, o =>
            {
                o.CaseStatementOnNewLine = false;
                o.BlankLinesAfterSingleLineCase = 0;
                o.BlankLinesAfterMultiLineCase = 2;
                o.MaxConsecutiveBlankLines = 2;
            }));
    }

    [Fact]
    public void Blank_lines_at_file_start()
    {
        Assert.Equal(Lines("", "a := 1;"), Impl(Lines("", "", "a := 1;"), o => o.BlankLinesAtFileStart = 1));
    }

    // ------------------------------------------------------------------ CASE

    [Fact]
    public void Else_of_case_can_be_outdented()
    {
        var input = Lines("CASE n OF", "1:", "a := 1;", "ELSE", "b := 0;", "END_CASE");
        Assert.Equal(Lines("CASE n OF", "    1:", "        a := 1;", "ELSE", "    b := 0;", "END_CASE"),
            Impl(input, o => o.IndentCaseElse = false));
    }

    [Fact]
    public void Comment_before_a_case_label_can_use_the_label_indentation()
    {
        var input = Lines("CASE n OF", "1:", "a := 1;", "// second branch", "2:", "b := 1;", "END_CASE");
        Assert.Equal(Lines("CASE n OF", "    1:", "        a := 1;", "    // second branch", "    2:", "        b := 1;", "END_CASE"),
            Impl(input, o => o.CommentBeforeCaseLabelAtLabelLevel = true));
        Assert.Equal(Lines("CASE n OF", "    1:", "        a := 1;", "        // second branch", "    2:", "        b := 1;", "END_CASE"),
            Impl(input));
    }

    // ------------------------------------------------------------------ TYPE

    [Fact]
    public void Struct_keyword_on_its_own_line_or_after_type()
    {
        var sameLine = Lines("TYPE ST_A : STRUCT", "n : INT;", "END_STRUCT", "END_TYPE");
        var ownLine = Lines("TYPE ST_A :", "STRUCT", "    n : INT;", "END_STRUCT", "END_TYPE");
        Assert.Equal(ownLine, Decl(sameLine));
        Assert.Equal(Lines("TYPE ST_A : STRUCT", "    n : INT;", "END_STRUCT", "END_TYPE"), Decl(ownLine, o => o.StructOnNewLine = false));
    }

    // ------------------------------------------------------------------ spaces and alignment

    [Fact]
    public void Spaces_between_nested_parentheses_are_optional()
    {
        Assert.Equal("x := ( ( a + b ) * c );", Impl("x := ((a + b) * c);", o => o.SpaceInsideParentheses = true));
        Assert.Equal("x := (( a + b ) * c );", Impl("x := ((a + b) * c);", o => { o.SpaceInsideParentheses = true; o.SpaceBetweenNestedParentheses = false; }));
    }

    [Fact]
    public void Columns_can_be_aligned_with_tabs()
    {
        var input = Lines("VAR", "bStart : BOOL; // start", "nCounterValue : INT := 5;", "END_VAR");
        Assert.Equal(Lines(
            "VAR",
            "\tbStart\t\t\t: BOOL;\t\t\t// start",
            "\tnCounterValue\t: INT\t:= 5;",
            "END_VAR"), Decl(input, o => { o.IndentStyle = IndentStyle.Tabs; o.AlignWithTabs = true; }));
    }

    // ------------------------------------------------------------------ blank lines around statements (minimums)

    [Fact]
    public void Blank_lines_before_and_after_statements_with_body()
    {
        var input = Lines("a := 1;", "IF bA THEN", "b := 1;", "END_IF", "c := 1;", "FOR i := 1 TO 2 DO", "d := i;", "END_FOR");
        Assert.Equal(Lines(
            "a := 1;",
            "",
            "IF bA THEN",
            "    b := 1;",
            "END_IF",
            "",
            "c := 1;",
            "",
            "FOR i := 1 TO 2 DO",
            "    d := i;",
            "END_FOR"), Impl(input, o => { o.BlankLinesBeforeStatementWithBody = 1; o.BlankLinesAfterStatementWithBody = 1; }));
    }

    [Fact]
    public void No_blank_lines_are_added_at_block_borders()
    {
        var input = Lines("IF bA THEN", "IF bB THEN", "x := 1;", "END_IF", "END_IF");
        Assert.Equal(Lines("IF bA THEN", "    IF bB THEN", "        x := 1;", "    END_IF", "END_IF"),
            Impl(input, o => { o.BlankLinesBeforeStatementWithBody = 1; o.BlankLinesAfterStatementWithBody = 1; }));
    }

    [Fact]
    public void Blank_lines_after_multi_line_statements_and_attached_comments()
    {
        var input = Lines("nA := nB +", "nC;", "nD := 1;", "// explain", "IF bA THEN", "x := 1;", "END_IF");
        Assert.Equal(Lines(
            "nA := nB +",
            "    nC;",
            "",
            "nD := 1;",
            "",
            "// explain",
            "IF bA THEN",
            "    x := 1;",
            "END_IF"), Impl(input, o => { o.BlankLinesAfterMultiLineStatement = 1; o.BlankLinesBeforeStatementWithBody = 1; }));
    }

    [Fact]
    public void Blank_lines_after_var_blocks()
    {
        var input = Lines("VAR_INPUT", "a : INT;", "END_VAR", "VAR", "b : INT;", "END_VAR");
        Assert.Equal(Lines("VAR_INPUT", "    a : INT;", "END_VAR", "", "VAR", "    b : INT;", "END_VAR"),
            Decl(input, o => o.BlankLinesAfterVarBlocks = 1));
    }

    // ------------------------------------------------------------------ TYPE keyword

    [Fact]
    public void Line_break_after_type_and_indented_type_declaration()
    {
        var input = Lines("TYPE ST_A :", "STRUCT", "n : INT;", "END_STRUCT", "END_TYPE");
        Assert.Equal(Lines("TYPE", "    ST_A :", "    STRUCT", "        n : INT;", "    END_STRUCT", "END_TYPE"),
            Decl(input, o => { o.LineBreakAfterTypeKeyword = true; o.IndentTypeDeclaration = true; }));
        Assert.Equal(Lines("TYPE", "ST_A :", "STRUCT", "    n : INT;", "END_STRUCT", "END_TYPE"),
            Decl(input, o => o.LineBreakAfterTypeKeyword = true));
    }

    [Fact]
    public void Enums_after_a_type_line_break_are_still_laid_out()
    {
        var input = "TYPE E_Dir :\n(\nLeft,\nRight\n);\nEND_TYPE";
        Assert.Equal(Lines("TYPE", "    E_Dir : (Left, Right);", "END_TYPE"),
            Decl(input, o => { o.LineBreakAfterTypeKeyword = true; o.IndentTypeDeclaration = true; o.EnumLineBreakMode = LineBreakMode.Long; }));
    }

    [Fact]
    public void Initializer_can_keep_its_first_element_after_the_parenthesis()
    {
        var input = Lines("VAR", "stCfg : ST_Cfg := (nSpeed := 100, bEnable := TRUE);", "END_VAR");
        Assert.Equal(Lines(
            "VAR",
            "    stCfg : ST_Cfg := (nSpeed  := 100,",
            "                       bEnable := TRUE);",
            "END_VAR"), Decl(input, o => { o.InitializerLineBreakMode = LineBreakMode.Always; o.LineBreakAfterInitializerOpenParenthesis = false; }));
    }

    // ------------------------------------------------------------------ configuration compatibility

    [Fact]
    public void Former_call_wrapping_keys_are_still_understood()
    {
        var config = TcFormatConfig.Parse("""{ "format": { "wrapLongCalls": true, "rejoinWrappedCalls": false, "wrapCallsWithArgumentsAtLeast": 4 } }""", out var warnings);
        Assert.Empty(warnings);
        Assert.Equal(LineBreakMode.LongOrMultiline, config.Format.CallLineBreakMode);
        Assert.Equal(3, config.Format.MaxCallArgumentsOnSingleLine);

        config = TcFormatConfig.Parse("""{ "format": { "wrapLongCalls": false } }""", out warnings);
        Assert.Empty(warnings);
        Assert.Equal(LineBreakMode.Never, config.Format.CallLineBreakMode);
    }
}
