using TcFormatter.Formatting;

namespace TcFormatter.Core.Tests;

public class LinesTests : FormatterTestBase
{
    [Fact]
    public void Limits_blank_lines_and_removes_them_at_block_borders()
    {
        var input = Lines(
            "",
            "x := 1;",
            "",
            "",
            "",
            "IF a THEN",
            "",
            "y := 2;",
            "",
            "END_IF",
            "",
            "");

        Assert.Equal(Lines(
            "x := 1;",
            "",
            "IF a THEN",
            "    y := 2;",
            "END_IF",
            ""), Impl(input));
    }

    [Fact]
    public void Keeps_more_blank_lines_when_configured()
    {
        Assert.Equal("a := 1;\n\n\nb := 2;", Impl("a := 1;\n\n\n\n\nb := 2;", o => o.MaxConsecutiveBlankLines = 2));
    }

    [Fact]
    public void Removes_trailing_whitespace()
    {
        Assert.Equal("a := 1; // c\nb := 2;", Impl("a := 1;    // c   \nb := 2;\t "));
    }

    [Theory]
    [InlineData(LineEndingStyle.CrLf, "a := 1;\nb := 2;", "a := 1;\r\nb := 2;")]
    [InlineData(LineEndingStyle.Lf, "a := 1;\r\nb := 2;", "a := 1;\nb := 2;")]
    [InlineData(LineEndingStyle.Auto, "a := 1;\r\nb := 2;\r\nc := 3;\nd := 4;", "a := 1;\r\nb := 2;\r\nc := 3;\r\nd := 4;")]
    public void Normalises_line_endings(LineEndingStyle style, string input, string expected)
    {
        Assert.Equal(expected, Impl(input, o => o.LineEnding = style));
    }

    [Theory]
    [InlineData(FinalNewlineStyle.Preserve, "a := 1;\n\n", "a := 1;\n")]
    [InlineData(FinalNewlineStyle.Preserve, "a := 1;", "a := 1;")]
    [InlineData(FinalNewlineStyle.Add, "a := 1;", "a := 1;\n")]
    [InlineData(FinalNewlineStyle.Remove, "a := 1;\n", "a := 1;")]
    public void Handles_final_newline(FinalNewlineStyle style, string input, string expected)
    {
        Assert.Equal(expected, Impl(input, o => o.FinalNewline = style));
    }

    [Fact]
    public void Splits_several_statements_on_one_line()
    {
        Assert.Equal(Lines("a := 1;", "b := 2; // both"), Impl("a := 1; b := 2; // both"));
        Assert.Equal("a := 1; b := 2;", Impl("a := 1; b := 2;", o => o.OneStatementPerLine = false));
    }

    [Fact]
    public void Expands_single_line_blocks()
    {
        Assert.Equal(Lines(
            "IF a THEN",
            "    b := 1;",
            "ELSE",
            "    b := 2;",
            "END_IF"), Impl("IF a THEN b := 1; ELSE b := 2; END_IF"));

        Assert.Equal(Lines(
            "FOR i := 1 TO 3 DO",
            "    n := n + i;",
            "END_FOR;",
            "x := 1;"), Impl("FOR i := 1 TO 3 DO n := n + i; END_FOR; x := 1;"));
    }

    [Fact]
    public void Puts_case_statements_below_their_label()
    {
        Assert.Equal(Lines(
            "CASE n OF",
            "    1:",
            "        x := 1;",
            "    2:",
            "        x := 2;",
            "END_CASE"), Impl("CASE n OF\n1: x := 1;\n2: x := 2;\nEND_CASE"));

        Assert.Equal(Lines(
            "CASE n OF",
            "    1: x := 1;",
            "END_CASE"), Impl("CASE n OF\n1: x := 1;\nEND_CASE", o => o.CaseStatementOnNewLine = false));
    }

    [Fact]
    public void Splits_var_sections_written_on_one_line()
    {
        Assert.Equal(Lines(
            "VAR_INPUT",
            "    bStart : BOOL;",
            "END_VAR",
            "VAR CONSTANT",
            "    cMax : INT := 3;",
            "END_VAR"), Decl("VAR_INPUT bStart : BOOL; END_VAR VAR CONSTANT cMax : INT := 3; END_VAR"));
    }
}
