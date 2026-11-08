using TcFormatter.Formatting;

namespace TcFormatter.Core.Tests;

public class StatementTests : FormatterTestBase
{
    [Theory]
    [InlineData(SemicolonStyle.Preserve, "IF a THEN\n    b := 1;\nEND_IF;\nWHILE c DO\n    d := 2;\nEND_WHILE")]
    [InlineData(SemicolonStyle.Add, "IF a THEN\n    b := 1;\nEND_IF;\nWHILE c DO\n    d := 2;\nEND_WHILE;")]
    [InlineData(SemicolonStyle.Remove, "IF a THEN\n    b := 1;\nEND_IF\nWHILE c DO\n    d := 2;\nEND_WHILE")]
    public void Controls_semicolon_after_end_keywords(SemicolonStyle style, string expected)
    {
        var input = "IF a THEN\nb := 1;\nEND_IF;\nWHILE c DO\nd := 2;\nEND_WHILE";
        Assert.Equal(expected, Impl(input, o => o.EndKeywordSemicolon = style));
    }

    [Fact]
    public void Wraps_calls_that_are_too_long()
    {
        var input = "fbAxisMove(Axis := stAxis, Execute := bMoveStart, Position := fTarget, Velocity := fVelocity, Done => bMoveDone);";

        Assert.Equal(Lines(
            "fbAxisMove(",
            "    Axis     := stAxis,",
            "    Execute  := bMoveStart,",
            "    Position := fTarget,",
            "    Velocity := fVelocity,",
            "    Done     => bMoveDone);"), Impl(input, o => o.MaxLineLength = 80));
    }

    [Fact]
    public void Wrapped_call_layout_options()
    {
        var input = "fbTimer(IN := bStart, PT := T#5S);";

        Assert.Equal(Lines(
            "fbTimer(",
            "    IN := bStart,",
            "    PT := T#5S",
            ");"), Impl(input, o => { o.MaxCallArgumentsOnSingleLine = 1; o.WrappedCallClosingParenthesisOnNewLine = true; }));

        Assert.Equal(Lines(
            "fbTimer(IN := bStart,",
            "        PT := T#5S);"), Impl(input, o => { o.MaxCallArgumentsOnSingleLine = 1; o.CallWrapStyle = CallWrapStyle.AlignWithOpeningParenthesis; }));
    }

    [Fact]
    public void Rejoins_a_wrapped_call_that_fits_on_one_line()
    {
        var input = Lines("fbTimer(", "IN := bStart,", "PT := T#5S);");
        Assert.Equal("fbTimer(IN := bStart, PT := T#5S);", Impl(input));
    }

    [Fact]
    public void Keeps_wrapped_calls_with_comments_inside()
    {
        var input = Lines("fbTimer(", "IN := bStart, // start", "PT := T#5S);");
        Assert.Equal(Lines("fbTimer(", "    IN := bStart, // start", "    PT := T#5S);"), Impl(input));
    }

    [Fact]
    public void Aligns_consecutive_assignments_when_enabled()
    {
        var input = Lines("a := 1;", "bLonger := 2;", "", "c := 3;");
        Assert.Equal(Lines("a       := 1;", "bLonger := 2;", "", "c := 3;"), Impl(input, o => o.AlignConsecutiveAssignments = true));
        Assert.Equal(Lines("a := 1;", "bLonger := 2;", "", "c := 3;"), Impl(input));
    }

    [Fact]
    public void Aligns_trailing_code_comments_when_enabled()
    {
        var input = Lines("a := 1; // one", "bLonger := 2; // two");
        Assert.Equal(Lines("a := 1;       // one", "bLonger := 2; // two"),
            Impl(input, o => o.CodeCommentAlignment = TrailingCommentAlignment.AlignInBlock));
    }

    [Fact]
    public void Indents_regions_when_enabled()
    {
        var input = Lines("{region \"Init\"}", "a := 1;", "{endregion}");
        Assert.Equal(input, Impl(input));
        Assert.Equal(Lines("{region \"Init\"}", "    a := 1;", "{endregion}"), Impl(input, o => o.IndentRegions = true));
    }

    [Fact]
    public void Formats_try_catch_blocks()
    {
        var input = Lines("__TRY", "a := b / c;", "__CATCH(exc)", "a := 0;", "__ENDTRY");
        Assert.Equal(Lines("__TRY", "    a := b / c;", "__CATCH(exc)", "    a := 0;", "__ENDTRY"), Impl(input));
    }

    [Fact]
    public void Refuses_to_change_code_when_the_safety_check_fails()
    {
        // unbalanced and odd code must never be destroyed
        var input = "IF a THEN\nx := (1 + ;\nEND_CASE\n@@@ ;";
        var result = StFormatter.Format(input, CodePart.Implementation, Options());
        Assert.Null(result.Error);
        Assert.Equal(StFormatter.Format(result.Text, CodePart.Implementation, Options()).Text, result.Text);
    }
}

public class SingleLineBlockTests : FormatterTestBase
{
    [Theory]
    [InlineData("IF bStart THEN nCount := 1; END_IF")]
    [InlineData("IF bA THEN nX := 1; ELSE nX := 2; END_IF")]
    [InlineData("IF bA THEN nX := 1; ELSIF bB THEN nX := 2; END_IF")]
    [InlineData("FOR i := 1 TO 3 DO nSum := nSum + i; END_FOR")]
    [InlineData("WHILE bRun DO nCount := nCount + 1; END_WHILE")]
    public void Keeps_single_line_blocks_intact_when_expanding_is_off(string input)
    {
        Assert.Equal(input, Impl(input, o => o.ExpandSingleLineBlocks = false));
    }

    [Fact]
    public void Still_splits_statements_around_a_single_line_block()
    {
        Assert.Equal(
            Lines("nA := 1;", "IF bStart THEN nCount := 1; END_IF", "nB := 2;"),
            Impl("nA := 1; IF bStart THEN nCount := 1; END_IF nB := 2;", o => o.ExpandSingleLineBlocks = false));
    }
}
