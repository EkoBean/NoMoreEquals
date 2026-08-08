using System.Collections.Generic;

namespace NoMoreEquals.Data.DefaultMissingGlyphs;

/// <summary>
/// Shipped missing-glyph defaults that are <b>not</b> OpenCC/variant pairs.
/// <see cref="Services.DefaultGlyphSeeder"/> copies these into the user's editable
/// mappings so they appear on the Main tab like any other customization.
/// <para>
/// To add more defaults: create a module (like <see cref="CommonParticles"/>),
/// then append its <c>Entries</c> to <see cref="Modules"/>.
/// </para>
/// </summary>
internal static class DefaultMissingGlyphMaps
{
    /// <summary>
    /// Curated packs. Append new module <c>Entries</c> here when adding defaults.
    /// </summary>
    private static readonly IReadOnlyList<IReadOnlyDictionary<char, char>> Modules =
    [
        CommonParticles.Entries,
        // e.g. CommonPronouns.Entries,
    ];

    /// <summary>All curated default pairs (later modules override earlier on key clash).</summary>
    public static IReadOnlyDictionary<char, char> Entries { get; } = Merge(Modules);

    private static Dictionary<char, char> Merge(IReadOnlyList<IReadOnlyDictionary<char, char>> modules)
    {
        var map = new Dictionary<char, char>();
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
