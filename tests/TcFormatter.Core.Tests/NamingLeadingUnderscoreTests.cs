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

public class NamingUnknownElementTypeTests
{
    [Fact]
    public void Arrays_and_references_of_unknown_types_are_not_checked_without_an_own_prefix()
    {
        var rules = new NamingRules
        {
            ArrayPrefix = "", ArrayIncludesElementPrefix = true,
            ReferencePrefix = "", ReferenceIncludesTargetPrefix = true,
        };
        rules.TypePrefixes.Remove("FB_*");
        var decl = "VAR\nfb_Counters : ARRAY[1..10] OF FB_Counter;\nfb_Other : REFERENCE TO FB_Counter;\nfValues : ARRAY[1..3] OF LREAL;\nValues : ARRAY[1..3] OF LREAL;\nEND_VAR";

        var d = Assert.Single(NamingChecker.Check(decl, rules));
        Assert.Equal("Values", d.Name);
        Assert.Equal("fValues", d.Suggestion);
    }
}
