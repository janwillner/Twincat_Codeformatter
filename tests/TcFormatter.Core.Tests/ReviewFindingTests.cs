using TcFormatter.Configuration;
using TcFormatter.Formatting;
using TcFormatter.TwinCatFiles;

namespace TcFormatter.Core.Tests;

/// <summary>Regression tests for findings of the code review.</summary>
public class ReviewFindingTests
{
    private static FormatterOptions Defaults => new();

    [Theory]
    [InlineData("bStart := 1;", "bstart := 1;")]                 // identifier casing is never changed by the formatter
    [InlineData("s := 'a  b';", "s := 'a b';")]                  // string contents
    [InlineData("(* a\n   b *)", "(* a\nb *)")]                  // inside multi-line comments
    [InlineData("x := 1; // a  b", "x := 1; // a b")]            // inside line comments
    public void Safety_check_detects_changes_the_formatter_never_makes(string original, string changed)
    {
        Assert.NotNull(Equivalence.FindDifference(original, changed, Defaults));
    }

    [Theory]
    [InlineData("if a then b := 1; end_if", "IF a THEN b := 1; END_IF")]
    [InlineData("x := 1; //comment   ", "x := 1; // comment")]
    [InlineData("(* a\r\n   b *)", "(* a\n   b *)")]
    public void Safety_check_accepts_what_the_formatter_does(string original, string changed)
    {
        Assert.Null(Equivalence.FindDifference(original, changed, Defaults));
    }

    [Fact]
    public void Safety_check_allows_end_semicolons_only_when_configured()
    {
        Assert.NotNull(Equivalence.FindDifference("END_IF", "END_IF;", Defaults));
        Assert.Null(Equivalence.FindDifference("END_IF", "END_IF;", new FormatterOptions { EndKeywordSemicolon = SemicolonStyle.Add }));
        Assert.Null(Equivalence.FindDifference("END_IF;", "END_IF", new FormatterOptions { EndKeywordSemicolon = SemicolonStyle.Remove }));
    }

    [Fact]
    public void Lint_positions_refer_to_the_formatted_file()
    {
        var xml =
            "<TcPlcObject>\r\n  <POU Name=\"FB_A\">\r\n" +
            "    <Declaration><![CDATA[FUNCTION_BLOCK FB_A\r\nVAR a:INT; b:INT; c:INT; END_VAR\r\n]]></Declaration>\r\n" +
            "    <Method Name=\"Run\">\r\n" +
            "      <Declaration><![CDATA[METHOD Run\r\nVAR\r\nSpeed : LREAL;\r\nEND_VAR\r\n]]></Declaration>\r\n" +
            "    </Method>\r\n  </POU>\r\n</TcPlcObject>";

        var result = TcFileProcessor.Process(xml, new TcFormatConfig(), format: true, lint: true);

        var speed = Assert.Single(result.Naming, n => n.Diagnostic.Name == "Speed");
        var lines = result.Text.Replace("\r\n", "\n").Split('\n');
        Assert.Contains("Speed : LREAL;", lines[speed.FileLine - 1]);
        Assert.Equal('S', lines[speed.FileLine - 1][speed.FileColumn - 1]);
    }

    [Fact]
    public void Invalid_ignore_patterns_are_reported_and_skipped()
    {
        var config = TcFormatConfig.Parse("""{ "naming": { "ignoreNames": [ "^ok$", "([unclosed" ] } }""", out var warnings);

        Assert.Contains(warnings, w => w.Contains("([unclosed"));
        Assert.Equal(new[] { "^ok$" }, config.Naming.IgnoreNames);
    }

    [Fact]
    public void Null_removes_a_default_table_entry()
    {
        var config = TcFormatConfig.Parse("""{ "naming": { "typePrefixes": { "I_*": null, "TON": null } } }""", out var warnings);

        Assert.Empty(warnings);
        Assert.False(config.Naming.TypePrefixes.ContainsKey("I_*"));
        Assert.False(config.Naming.TypePrefixes.ContainsKey("TON"));
        Assert.True(config.Naming.TypePrefixes.ContainsKey("BOOL"));
    }

    [Fact]
    public void Empty_object_names_do_not_crash()
    {
        Assert.Empty(Naming.NamingChecker.CheckObjectName("", "METHOD", new Naming.NamingRules()));
        Assert.Empty(Naming.NamingChecker.Check("", new Naming.NamingRules(), "", "ACTION"));
    }
}
