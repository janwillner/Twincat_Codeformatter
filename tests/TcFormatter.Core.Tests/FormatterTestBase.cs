using TcFormatter.Formatting;

namespace TcFormatter.Core.Tests;

public abstract class FormatterTestBase
{
    /// <summary>Readable defaults for tests: 4 spaces, LF.</summary>
    protected static FormatterOptions Options(Action<FormatterOptions>? configure = null)
    {
        var o = new FormatterOptions { IndentStyle = IndentStyle.Spaces, IndentSize = 4, LineEnding = LineEndingStyle.Lf };
        configure?.Invoke(o);
        return o;
    }

    protected static string Impl(string code, Action<FormatterOptions>? configure = null) =>
        Run(code, CodePart.Implementation, configure);

    protected static string Decl(string code, Action<FormatterOptions>? configure = null) =>
        Run(code, CodePart.Declaration, configure);

    private static string Run(string code, CodePart part, Action<FormatterOptions>? configure)
    {
        var options = Options(configure);
        var result = StFormatter.Format(code, part, options);
        Assert.True(result.Error is null, result.Error);

        // every result must be stable: formatting it again changes nothing
        var second = StFormatter.Format(result.Text, part, options);
        Assert.Equal(result.Text, second.Text);
        return result.Text;
    }

    /// <summary>Joins lines with LF so expectations can be written as arrays.</summary>
    protected static string Lines(params string[] lines) => string.Join("\n", lines);
}
