using TcFormatter.Formatting;

namespace TcFormatter.Core.Tests;

public class IndentationTests : FormatterTestBase
{
    [Fact]
    public void Indents_if_elsif_else_bodies()
    {
        var input = Lines(
            "IF bA THEN",
            "x := 1;",
            "      ELSIF bB THEN",
            "  x := 2;",
            "ELSE",
            "IF bC THEN",
            "x := 3;",
            "END_IF",
            "END_IF");

        Assert.Equal(Lines(
            "IF bA THEN",
            "    x := 1;",
            "ELSIF bB THEN",
            "    x := 2;",
            "ELSE",
            "    IF bC THEN",
            "        x := 3;",
            "    END_IF",
            "END_IF"), Impl(input));
    }

    [Fact]
    public void Indents_case_labels_and_their_bodies()
    {
        var input = Lines(
            "CASE nState OF",
            "0:",
            "x := 1;",
            "1, 2:",
            "x := 2;",
            "y := 3;",
            "E_State.Run:",
            "ELSE",
            "x := 0;",
            "END_CASE");

        Assert.Equal(Lines(
            "CASE nState OF",
            "    0:",
            "        x := 1;",
            "    1, 2:",
            "        x := 2;",
            "        y := 3;",
            "    E_State.Run:",
            "    ELSE",
            "        x := 0;",
            "END_CASE"), Impl(input));
    }

    [Fact]
    public void Indents_loops()
    {
        var input = Lines(
            "FOR i := 1 TO 10 BY 2 DO",
            "WHILE bRun DO",
            "REPEAT",
            "n := n + 1;",
            "UNTIL n > 5",
            "END_REPEAT",
            "END_WHILE",
            "END_FOR");

        Assert.Equal(Lines(
            "FOR i := 1 TO 10 BY 2 DO",
            "    WHILE bRun DO",
            "        REPEAT",
            "            n := n + 1;",
            "        UNTIL n > 5",
            "        END_REPEAT",
            "    END_WHILE",
            "END_FOR"), Impl(input));
    }

    [Fact]
    public void Indents_continuation_lines_and_parenthesised_arguments()
    {
        var input = Lines(
            "bResult := bA AND bB",
            "OR bC;",
            "fbTimer(",
            "IN := bStart,",
            "PT := T#1S",
            ");",
            "x := 1;");

        Assert.Equal(Lines(
            "bResult := bA AND bB",
            "    OR bC;",
            "fbTimer(",
            "    IN := bStart,",
            "    PT := T#1S",
            ");",
            "x := 1;"), Impl(input, o => o.CallLineBreakMode = LineBreakMode.Preserve));
    }

    [Fact]
    public void Indents_comments_and_pragmas_with_the_surrounding_code()
    {
        var input = Lines(
            "IF bA THEN",
            "// explain",
            "{warning 'todo'}",
            "x := 1; // trailing",
            "(* block",
            "   comment *)",
            "END_IF");

        Assert.Equal(Lines(
            "IF bA THEN",
            "    // explain",
            "    {warning 'todo'}",
            "    x := 1; // trailing",
            "    (* block",
            "   comment *)",
            "END_IF"), Impl(input));
    }

    [Fact]
    public void Uses_tabs_when_configured()
    {
        Assert.Equal("IF a THEN\n\tx := 1;\nEND_IF",
            Impl("IF a THEN\nx := 1;\nEND_IF", o => o.IndentStyle = IndentStyle.Tabs));
    }

    [Fact]
    public void Indents_var_sections_in_declarations()
    {
        var input = Lines(
            "FUNCTION_BLOCK FB_Motor EXTENDS FB_Base IMPLEMENTS I_Motor",
            "VAR_INPUT",
            "bStart : BOOL;",
            "END_VAR",
            "VAR CONSTANT",
            "cMax : INT := 5;",
            "END_VAR");

        Assert.Equal(Lines(
            "FUNCTION_BLOCK FB_Motor EXTENDS FB_Base IMPLEMENTS I_Motor",
            "VAR_INPUT",
            "    bStart : BOOL;",
            "END_VAR",
            "VAR CONSTANT",
            "    cMax : INT := 5;",
            "END_VAR"), Decl(input));
    }

    [Fact]
    public void Indents_struct_and_enum_types()
    {
        var input = Lines(
            "TYPE ST_Data :",
            "STRUCT",
            "nA : INT;",
            "END_STRUCT",
            "END_TYPE");

        Assert.Equal(Lines(
            "TYPE ST_Data :",
            "STRUCT",
            "    nA : INT;",
            "END_STRUCT",
            "END_TYPE"), Decl(input));

        var enumInput = Lines(
            "{attribute 'qualified_only'}",
            "TYPE E_State :",
            "(",
            "Idle := 0,",
            "Run",
            ")",
            "    ;",
            "END_TYPE");

        Assert.Equal(Lines(
            "{attribute 'qualified_only'}",
            "TYPE E_State :",
            "(",
            "    Idle := 0,",
            "    Run",
            ")",
            ";",
            "END_TYPE"), Decl(enumInput));
    }

    [Fact]
    public void Keeps_code_between_fmt_off_and_on_untouched()
    {
        var input = Lines(
            "IF a THEN",
            "// tcfmt: off",
            "x   :=   1;",
            "      y:=2;",
            "// tcfmt: on",
            "z:=3;",
            "END_IF");

        Assert.Equal(Lines(
            "IF a THEN",
            "    // tcfmt: off",
            "x   :=   1;",
            "      y:=2;",
            "    // tcfmt: on",
            "    z := 3;",
            "END_IF"), Impl(input));
    }
}
