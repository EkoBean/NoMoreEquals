using System.Collections.Generic;

namespace NoMoreEquals.Data.DefaultMissingGlyphs;

/// <summary>
/// Shipped missing-glyph defaults that are <b>not</b> OpenCC/variant pairs.
/// Seeded into <see cref="Configuration.CustomMappings"/> so they appear on the
/// Main tab and stay fully editable (change / delete) like user customizations.
/// <para>
/// To add more defaults: create a module (like <see cref="CommonParticles"/>),
/// then append its <c>Entries</c> to <see cref="Modules"/>. New keys are seeded
/// once per install; keys the user already deleted are not re-added.
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

    /// <summary>
    /// Copy not-yet-seeded defaults into <see cref="Configuration.CustomMappings"/>.
    /// Does not overwrite an existing custom entry. Marks each key as seeded so
    /// a user who deletes a default will not get it back on the next launch.
    /// </summary>
    /// <returns>True if configuration changed and should be saved.</returns>
    public static bool SeedInto(Configuration config)
    {
        config.CustomMappings ??= new Dictionary<string, string>();
        config.SeededDefaultGlyphKeys ??= [];

        var alreadySeeded = new HashSet<string>(config.SeededDefaultGlyphKeys);
        var changed = false;

        foreach (var (from, to) in Entries)
        {
            var key = from.ToString();
            if (alreadySeeded.Contains(key))
                continue;

            if (!config.CustomMappings.ContainsKey(key))
                config.CustomMappings[key] = to.ToString();

            config.SeededDefaultGlyphKeys.Add(key);
            changed = true;
        }

        return changed;
    }

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
