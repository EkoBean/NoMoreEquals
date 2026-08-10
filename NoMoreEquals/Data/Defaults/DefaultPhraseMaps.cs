using System.Collections.Generic;

namespace NoMoreEquals.Data.Defaults;

/// <summary>
/// Shipped multi-character wording defaults.
/// <see cref="Services.DefaultPhraseSeeder"/> copies these into the user's editable
/// phrase replacements so they appear on the Main tab like any other customization.
/// <para>
/// To add more defaults: create a module (like <see cref="CommonPhrases"/>),
/// then append its <c>Entries</c> to <see cref="Modules"/>.
/// </para>
/// </summary>
internal static class DefaultPhraseMaps
{
    /// <summary>Curated packs. Append new module <c>Entries</c> here when adding defaults.</summary>
    private static readonly IReadOnlyList<IReadOnlyDictionary<string, string>> Modules =
    [
        CommonPhrases.Entries,
    ];

    /// <summary>All curated default pairs (later modules override earlier on key clash).</summary>
    public static IReadOnlyDictionary<string, string> Entries { get; } = Merge(Modules);

    private static Dictionary<string, string> Merge(IReadOnlyList<IReadOnlyDictionary<string, string>> modules)
    {
        var map = new Dictionary<string, string>();
        foreach (var module in modules)
        {
            foreach (var (from, to) in module)
            {
                if (from != to)
                    map[from] = to;
            }
        }

        return map;
    }
}
