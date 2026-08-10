using System.Collections.Generic;
using System.Linq;
using NoMoreEquals.Data.Defaults;
using NoMoreEquals.Services;
using Xunit;

namespace NoMoreEquals.Tests;

/// <summary>
/// Exercises the Dalamud-free core. <see cref="Configuration"/> cannot be constructed
/// here -- it implements Dalamud's IPluginConfiguration and instantiating it throws
/// FileNotFoundException for the Dalamud assembly -- so these call Seed directly.
/// </summary>
public class DefaultPhraseSeederTests
{
    /// <summary>A key that is actually shipped, so the tests move if the data changes.</summary>
    private static string FirstDefaultKey => DefaultPhraseMaps.Entries.Keys.First();

    private static string FirstDefaultValue => DefaultPhraseMaps.Entries[FirstDefaultKey];

    [Fact]
    public void Seed_PopulatesEmptyConfigAndRecordsKeys()
    {
        var phrases = new List<PhraseReplacement>();
        var seeded = new List<string>();

        var changed = DefaultPhraseSeeder.Seed(phrases, seeded, ignoreSeededKeys: false);

        Assert.True(changed);
        Assert.Equal(DefaultPhraseMaps.Entries.Count, phrases.Count);
        var rule = phrases.Single(p => p.From == FirstDefaultKey);
        Assert.Equal(FirstDefaultValue, rule.To);
        Assert.True(rule.Enabled);
        Assert.Contains(FirstDefaultKey, seeded);
    }

    [Fact]
    public void Seed_DoesNotResurrectADeletedDefault()
    {
        var phrases = new List<PhraseReplacement>();
        var seeded = new List<string>();
        DefaultPhraseSeeder.Seed(phrases, seeded, ignoreSeededKeys: false);

        // The user deletes it, then the plugin restarts.
        phrases.RemoveAll(p => p.From == FirstDefaultKey);
        var changed = DefaultPhraseSeeder.Seed(phrases, seeded, ignoreSeededKeys: false);

        Assert.False(changed);
        Assert.DoesNotContain(phrases, p => p.From == FirstDefaultKey);
    }

    [Fact]
    public void RestoreAll_AddsBackADeletedDefault()
    {
        var phrases = new List<PhraseReplacement>();
        var seeded = new List<string>();
        DefaultPhraseSeeder.Seed(phrases, seeded, ignoreSeededKeys: false);
        phrases.RemoveAll(p => p.From == FirstDefaultKey);

        var changed = DefaultPhraseSeeder.Seed(phrases, seeded, ignoreSeededKeys: true);

        Assert.True(changed);
        var rule = phrases.Single(p => p.From == FirstDefaultKey);
        Assert.Equal(FirstDefaultValue, rule.To);
    }

    [Fact]
    public void RestoreAll_PreservesAUserEditedValue()
    {
        var phrases = new List<PhraseReplacement>();
        var seeded = new List<string>();
        DefaultPhraseSeeder.Seed(phrases, seeded, ignoreSeededKeys: false);

        // The user rewrites the target and switches the rule off.
        var edited = phrases.Single(p => p.From == FirstDefaultKey);
        edited.To = "\u6E2C\u8A66"; // 測試
        edited.Enabled = false;

        var changed = DefaultPhraseSeeder.Seed(phrases, seeded, ignoreSeededKeys: true);

        Assert.False(changed);
        Assert.Equal("\u6E2C\u8A66", edited.To);
        Assert.False(edited.Enabled);
        Assert.Single(phrases, p => p.From == FirstDefaultKey);
    }
}
