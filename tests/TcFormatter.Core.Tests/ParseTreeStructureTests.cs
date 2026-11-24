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
    public void Assignment_whose_target_contains_a_call_is_aligned_with_its_neighbours()
    {
        var input = Lines("nA := 1;", "fbAxis.M_Get(1).nPos := 2;", "fb(nIn := 3);");

        // the call statement in the last line is no assignment and stays as it is
        Assert.Equal(Lines(
            "nA".PadRight("fbAxis.M_Get(1).nPos".Length) + " := 1;",
            "fbAxis.M_Get(1).nPos := 2;",
            "fb(nIn := 3);"), Impl(input, o => o.AlignConsecutiveAssignments = true));
    }

    [Fact]
    public void Statement_with_keywords_inside_its_expression_is_joined_like_any_other()
    {
        // __DELETE is part of the expression according to the parse tree; it was missing from a hand-kept keyword list
        Assert.Equal("__DELETE(pData);", Impl(Lines("__DELETE(", "pData);"), o => o.KeepExistingLineBreaks = false));
    }

    [Fact]
    public void Member_named_like_a_type_keeps_its_case()
    {
        Assert.Equal("tStart := stLog.Time;", Impl("tStart:=stLog.Time;"));
    }
}
