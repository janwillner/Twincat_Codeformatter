using TcFormatter.Formatting;

namespace TcFormatter.Core.Tests;

/// <summary>Code the parser does not accept is never formatted (like Black): the input comes back unchanged with an error.</summary>
public class SyntaxErrorTests
{
    [Theory]
    [InlineData("IF bStart THEN\nnCount:=nCount+1;\n", CodePart.Implementation, "line 3, column 1")]
    [InlineData("VAR\nnA:INT\nEND_VAR", CodePart.Declaration, "line 3, column 1")]
    public void Code_with_a_syntax_error_is_left_unchanged(string code, CodePart part, string position)
    {
        var result = StFormatter.Format(code, part, new FormatterOptions());

        Assert.Equal(code, result.Text);
        Assert.False(result.Changed);
        Assert.NotNull(result.Error);
        Assert.StartsWith("Syntax error, code left unchanged: " + position, result.Error);
    }
}
