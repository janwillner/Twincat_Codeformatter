using TcFormatter.Naming;

namespace TcFormatter.Core.Tests;

public class NamingLeadingUnderscoreTests
{
    [Fact]
    public void Leading_underscore_is_not_part_of_the_prefix_when_allowed()
    {
        var rules = new NamingRules { AllowLeadingUnderscore = true };
        Assert.Empty(NamingChecker.Check("VAR\n_bBusy : BOOL;\nEND_VAR", rules));
    }

    [Fact]
    public void Suggestion_keeps_an_allowed_leading_underscore()
    {
        var rules = new NamingRules { AllowLeadingUnderscore = true };
        var d = Assert.Single(NamingChecker.Check("VAR\n_Busy : BOOL;\nEND_VAR", rules));
        Assert.Equal("_bBusy", d.Suggestion);
    }

    [Fact]
    public void Message_shows_the_type_as_written()
    {
        var d = Assert.Single(NamingChecker.Check("VAR\nValues : ARRAY[1..3] OF INT;\nEND_VAR", new NamingRules()));
        Assert.Contains("ARRAY[1..3] OF INT", d.Message);
    }
}
