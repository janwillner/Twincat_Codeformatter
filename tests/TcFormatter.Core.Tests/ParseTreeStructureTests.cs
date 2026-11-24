namespace TcFormatter.Core.Tests;

/// <summary>Block structure and statement ends come from the parse tree, not from how a word is spelled.</summary>
public class ParseTreeStructureTests : FormatterTestBase
{
    [Fact]
    public void Member_named_like_a_keyword_opens_no_block_and_keeps_its_case()
    {
        var input = Lines("x := E_Cmd.Repeat;", "IF stState.Then THEN", "y := 1;", "END_IF");

        Assert.Equal(Lines(
            "x := E_Cmd.Repeat;",
            "IF stState.Then THEN",
            "    y := 1;",
            "END_IF"), Impl(input));
    }

    [Fact]
    public void Member_named_like_a_type_keeps_its_case()
    {
        Assert.Equal("tStart := stLog.Time;", Impl("tStart:=stLog.Time;"));
    }
}
