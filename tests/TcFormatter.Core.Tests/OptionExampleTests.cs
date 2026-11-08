using System.Reflection;
using TcFormatter.Formatting;
using TcFormatter.Naming;

namespace TcFormatter.Core.Tests;

/// <summary>Every option has a before/after example, and the example really shows the effect of the option.</summary>
public class OptionExampleTests
{
    public static IEnumerable<object[]> FormatOptions() =>
        typeof(FormatterOptions).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanWrite)
            .Select(p => new object[] { p.Name });

    public static IEnumerable<object[]> NamingOptions() =>
        typeof(NamingRules).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanWrite)
            .Select(p => new object[] { p.Name });

    [Theory]
    [MemberData(nameof(FormatOptions))]
    public void Every_format_option_has_an_example_that_shows_its_effect(string option)
    {
        Assert.True(FormatterOptionExamples.Has(option), $"no example for {option}");

        var current = new FormatterOptions();
        var preview = FormatterOptionExamples.Preview(option, current);
        Assert.False(string.IsNullOrWhiteSpace(preview.Before));
        Assert.Null(preview.Error);

        var differs = FormatterOptionExamples.Alternatives(option, current)
            .Select(alternative => FormatterOptionExamples.Preview(option, alternative))
            .Any(p => p.After != preview.After);
        Assert.True(differs, $"the example of {option} looks the same for every value:\n{preview.After}");
    }

    [Theory]
    [MemberData(nameof(NamingOptions))]
    public void Every_naming_option_has_an_example_that_shows_its_effect(string option)
    {
        Assert.True(NamingOptionExamples.Has(option), $"no example for {option}");

        var current = new NamingRules();
        var preview = NamingOptionExamples.Preview(option, current);
        Assert.False(string.IsNullOrWhiteSpace(preview.Before));

        var differs = NamingOptionExamples.Alternatives(option, current)
            .Select(alternative => NamingOptionExamples.Preview(option, alternative))
            .Any(p => p.After != preview.After);
        Assert.True(differs, $"the example of {option} looks the same for every value:\n{preview.After}");
    }

    [Fact]
    public void Preview_uses_the_current_value()
    {
        var spaces = FormatterOptionExamples.Preview(nameof(FormatterOptions.SpaceAroundOperators), new FormatterOptions());
        var none = FormatterOptionExamples.Preview(nameof(FormatterOptions.SpaceAroundOperators), new FormatterOptions { SpaceAroundOperators = false });

        Assert.Contains("a + b", spaces.After);
        Assert.Contains("a+b", none.After);
        Assert.Equal(spaces.Before, none.Before);
    }

    [Fact]
    public void Tabs_and_line_ends_are_made_visible()
    {
        var tabs = FormatterOptionExamples.Preview(nameof(FormatterOptions.IndentStyle), new FormatterOptions { IndentStyle = IndentStyle.Tabs });
        Assert.Contains("→", tabs.After);

        var lineEnds = FormatterOptionExamples.Preview(nameof(FormatterOptions.LineEnding), new FormatterOptions { LineEnding = LineEndingStyle.CrLf });
        Assert.Contains("CRLF", lineEnds.After);
    }

    [Fact]
    public void Naming_preview_shows_the_suggested_names_and_messages()
    {
        var preview = NamingOptionExamples.Preview(nameof(NamingRules.TypePrefixes), new NamingRules());
        Assert.Contains("Speed : LREAL", preview.Before);
        Assert.Contains("fSpeed : LREAL", preview.After);
        Assert.Contains("TCN002", preview.After);
    }
}
