using TcFormatter.Formatting;

namespace TcFormatter.Core.Tests;

public class LabelTests : FormatterTestBase
{
    [Fact]
    public void Jump_labels_end_the_statement()
    {
        var input = Lines("IF bA THEN", "JMP lbl_End;", "END_IF", "lbl_End:", "(* call *)", "fbErr(", "nA := 1);");
        Assert.Equal(Lines(
            "IF bA THEN",
            "    JMP lbl_End;",
            "END_IF",
            "lbl_End:",
            "(* call *)",
            "fbErr(",
            "    nA := 1);"), Impl(input, o => o.CallLineBreakMode = LineBreakMode.Preserve));
    }

    [Theory]
    [InlineData(CaseLabelColonSpace.None, "SEQ.INIT:", "10:")]
    [InlineData(CaseLabelColonSpace.Always, "SEQ.INIT :", "10 :")]
    [InlineData(CaseLabelColonSpace.AfterNames, "SEQ.INIT :", "10:")]
    public void Space_before_the_colon_of_case_labels(CaseLabelColonSpace style, string named, string numeric)
    {
        var input = Lines("CASE nSeq OF", "SEQ.INIT:", "nA := 1;", "10 :", "nA := 2;", "END_CASE");
        var result = Impl(input, o => o.SpaceBeforeCaseLabelColon = style).Split('\n');
        Assert.Equal("    " + named, result[1]);
        Assert.Equal("    " + numeric, result[3]);
    }

    [Fact]
    public void Empty_call_arguments_can_keep_the_space_of_the_input_assistant()
    {
        var input = "fbTimer(IN:=bStart, PT:=, Q=>);";
        Assert.Equal("fbTimer(IN:= bStart, PT:=, Q=>);", Impl(input, o => o.SpaceBeforeArgumentAssignment = false));
        Assert.Equal("fbTimer(IN:= bStart, PT:= , Q=> );",
            Impl(input, o => { o.SpaceBeforeArgumentAssignment = false; o.SpaceAfterEmptyArgumentAssignment = true; }));
    }
}

public class PouHeaderCommentTests : FormatterTestBase
{
    private const string Input = "(* file header *)\nFUNCTION_BLOCK FB_Test\n(* template comment\n   second line *)\n// note\nVAR_INPUT\n(* inside *)\nbA : BOOL;\nEND_VAR\n(* between blocks *)\nVAR\nbB : BOOL;\nEND_VAR";

    [Fact]
    public void Comments_after_the_header_stay_at_column_zero_by_default()
    {
        var lines = Decl(Input).Split('\n');
        Assert.Equal("(* template comment", lines[2]);
        Assert.Equal("// note", lines[4]);
    }

    [Fact]
    public void Comments_between_header_and_first_VAR_can_be_indented()
    {
        var lines = Decl(Input, o => o.IndentCommentsAfterPouHeader = true).Split('\n');
        Assert.Equal("(* file header *)", lines[0]);
        Assert.Equal("    (* template comment", lines[2]);
        Assert.Equal("// note".PadLeft(11), lines[4]);
        Assert.Equal("(* between blocks *)", lines[9]);
    }
}
