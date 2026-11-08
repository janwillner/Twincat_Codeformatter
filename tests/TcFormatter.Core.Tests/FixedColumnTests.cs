using TcFormatter.Formatting;

namespace TcFormatter.Core.Tests;

/// <summary>Fixed columns for declaration types and trailing comments ("variable at tab 1, type at tab 10").</summary>
public class FixedColumnTests : FormatterTestBase
{
    private static void TabsAtFixedColumns(FormatterOptions o)
    {
        o.IndentStyle = IndentStyle.Tabs;
        o.AlignWithTabs = true;
        o.DeclarationTypeColumn = 43;   // ':' on the tab stop at column 41, type at 43
        o.DeclarationCommentColumn = 53;
    }

    [Fact]
    public void Type_and_comment_columns_are_reached_with_tabs()
    {
        var input = Lines(
            "VAR_INPUT",
            "vi_b_Enable : BOOL; // routine enable",
            "vi_lr_SetState:LREAL;   // setpoint",
            "vi_lr_VeryLongNameForTheTypeColumnTest : LREAL; // long name",
            "lr_a0 : ARRAY[0..MAX_NUM_NOTCHFILTERS] OF LREAL; // long type",
            "END_VAR");

        Assert.Equal(Lines(
            "VAR_INPUT",
            "\tvi_b_Enable\t\t\t\t\t\t\t: BOOL;\t\t// routine enable",
            "\tvi_lr_SetState\t\t\t\t\t\t: LREAL;\t// setpoint",
            "\tvi_lr_VeryLongNameForTheTypeColumnTest\t: LREAL;\t// long name",
            "\tlr_a0\t\t\t\t\t\t\t\t: ARRAY[0..MAX_NUM_NOTCHFILTERS] OF LREAL;\t// long type",
            "END_VAR"), Decl(input, TabsAtFixedColumns));
    }

    [Fact]
    public void Comment_column_works_with_spaces()
    {
        var input = Lines("VAR", "bStart : BOOL; // start", "nCounterValueWithALongName : INT; // counter", "END_VAR");
        Assert.Equal(Lines(
            "VAR",
            "    bStart : BOOL;           // start",
            "    nCounterValueWithALongName : INT; // counter",
            "END_VAR"), Decl(input, o => { o.AlignDeclarationColons = false; o.DeclarationCommentColumn = 30; }));
    }

    [Fact]
    public void Comment_column_takes_precedence_over_block_alignment()
    {
        var input = Lines("VAR", "a : BOOL; // a", "bLonger : ARRAY[1..100] OF BOOL; // b", "END_VAR");
        var result = Decl(input, TabsAtFixedColumns).Split('\n');
        Assert.Equal("\ta\t\t\t\t\t\t\t\t\t: BOOL;\t\t// a", result[1]);
    }
}

public class CommentBeforeElseTests : FormatterTestBase
{
    private const string Input = "IF bA THEN\nnX := 1;\n(* second case *)\nELSIF bB THEN\nnX := 2;\n// otherwise\nELSE\nnX := 3;\nEND_IF";

    [Fact]
    public void Comments_before_ELSIF_and_ELSE_belong_to_the_branch_by_default()
    {
        Assert.Equal(Lines(
            "IF bA THEN",
            "    nX := 1;",
            "    (* second case *)",
            "ELSIF bB THEN",
            "    nX := 2;",
            "    // otherwise",
            "ELSE",
            "    nX := 3;",
            "END_IF"), Impl(Input));
    }

    [Fact]
    public void Comments_before_ELSIF_and_ELSE_can_get_their_indentation()
    {
        Assert.Equal(Lines(
            "IF bA THEN",
            "    nX := 1;",
            "(* second case *)",
            "ELSIF bB THEN",
            "    nX := 2;",
            "// otherwise",
            "ELSE",
            "    nX := 3;",
            "END_IF"), Impl(Input, o => o.CommentBeforeElseAtElseLevel = true));
    }
}
