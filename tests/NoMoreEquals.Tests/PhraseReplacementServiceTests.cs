using NoMoreEquals;
using NoMoreEquals.Services;
using Xunit;

namespace NoMoreEquals.Tests;

public class PhraseReplacementServiceTests
{
    private const string You = "你";        // 你
    private const string YouPolite = "您";  // 您
    private const string Good = "好";       // 好
    private const string Ni = "尼";         // 尼

    [Fact]
    public void Apply_PrefersTheLongestMatch()
    {
        var service = Build(
            Rule(You, Ni),
            Rule(You + Good, YouPolite + Good));

        Assert.Equal(YouPolite + Good, service.Apply(You + Good));
        Assert.Equal(Ni, service.Apply(You));
    }

    [Fact]
    public void Apply_DoesNotRewriteItsOwnOutput()
    {
        // a -> b, b -> c must yield "b", not "c": matches are non-overlapping and
        // consumed left to right.
        var service = Build(Rule("a", "b"), Rule("b", "c"));
        Assert.Equal("bc", service.Apply("ab"));
    }

    [Fact]
    public void Apply_ReturnsSameInstanceWhenNothingMatches()
    {
        var service = Build(Rule(You, Ni));
        const string input = "nothing to do";
        Assert.Same(input, service.Apply(input));
    }

    [Fact]
    public void Apply_WithNoRulesIsIdentity()
    {
        var service = Build();
        Assert.Equal(0, service.EnabledCount);
        Assert.Equal(You, service.Apply(You));
    }

    [Fact]
    public void Rebuild_SkipsDisabledEmptyAndNoOpRules()
    {
        var service = Build(
            new PhraseReplacement { From = You, To = Ni, Enabled = false },
            new PhraseReplacement { From = string.Empty, To = Ni, Enabled = true },
            new PhraseReplacement { From = Good, To = Good, Enabled = true });

        Assert.Equal(0, service.EnabledCount);
        Assert.Equal(You + Good, service.Apply(You + Good));
    }

    [Fact]
    public void Rebuild_ReplacesThePreviousRuleSet()
    {
        var service = Build(Rule(You, Ni));
        service.Rebuild(new[] { Rule(Good, Ni) });

        Assert.Equal(1, service.EnabledCount);
        Assert.Equal(You, service.Apply(You));
        Assert.Equal(Ni, service.Apply(Good));
    }

    private static PhraseReplacement Rule(string from, string to)
        => new() { From = from, To = to, Enabled = true };

    private static PhraseReplacementService Build(params PhraseReplacement[] rules)
    {
        var service = new PhraseReplacementService();
        service.Rebuild(rules);
        return service;
    }
}
