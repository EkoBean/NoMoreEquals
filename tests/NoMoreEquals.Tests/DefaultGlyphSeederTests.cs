using System.Collections.Generic;
using System.Linq;
using NoMoreEquals.Data.Defaults;
using NoMoreEquals.Services;
using Xunit;

namespace NoMoreEquals.Tests;

/// <summary>
/// Same Dalamud constraint as <see cref="DefaultPhraseSeederTests"/>: these call the
/// list-based core, never <see cref="Configuration"/>.
/// </summary>
public class DefaultGlyphSeederTests
{
    private static string FirstDefaultKey => DefaultGlyphMaps.Entries.Keys.First().ToString();

    private static string FirstDefaultValue => DefaultGlyphMaps.Entries.Values.First().ToString();

    [Fact]
    public void Seed_PopulatesEmptyConfigAndRecordsKeys()
    {
        var mappings = new Dictionary<string, string>();
        var seeded = new List<string>();

        var changed = DefaultGlyphSeeder.Seed(mappings, seeded, ignoreSeededKeys: false);

        Assert.True(changed);
        Assert.Equal(DefaultGlyphMaps.Entries.Count, mappings.Count);
        Assert.Equal(FirstDefaultValue, mappings[FirstDefaultKey]);
        Assert.Contains(FirstDefaultKey, seeded);
    }

    [Fact]
    public void Seed_DoesNotResurrectADeletedDefault()
    {
        var mappings = new Dictionary<string, string>();
        var seeded = new List<string>();
        DefaultGlyphSeeder.Seed(mappings, seeded, ignoreSeededKeys: false);

        mappings.Remove(FirstDefaultKey);
        var changed = DefaultGlyphSeeder.Seed(mappings, seeded, ignoreSeededKeys: false);

        Assert.False(changed);
        Assert.False(mappings.ContainsKey(FirstDefaultKey));
    }

    [Fact]
    public void RestoreAll_AddsBackADeletedDefault()
    {
        var mappings = new Dictionary<string, string>();
        var seeded = new List<string>();
        DefaultGlyphSeeder.Seed(mappings, seeded, ignoreSeededKeys: false);
        mappings.Remove(FirstDefaultKey);

        var changed = DefaultGlyphSeeder.Seed(mappings, seeded, ignoreSeededKeys: true);

        Assert.True(changed);
        Assert.Equal(FirstDefaultValue, mappings[FirstDefaultKey]);
    }

    [Fact]
    public void RestoreAll_PreservesAUserEditedValue()
    {
        var mappings = new Dictionary<string, string>();
        var seeded = new List<string>();
        DefaultGlyphSeeder.Seed(mappings, seeded, ignoreSeededKeys: false);

        mappings[FirstDefaultKey] = "\u6E2C"; // 測

        var changed = DefaultGlyphSeeder.Seed(mappings, seeded, ignoreSeededKeys: true);

        Assert.False(changed);
        Assert.Equal("\u6E2C", mappings[FirstDefaultKey]);
    }
}
