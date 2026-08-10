using System;
using System.Collections.Generic;
using NoMoreEquals.Data.Defaults;

namespace NoMoreEquals.Services;

/// <summary>
/// Copies shipped glyph defaults into the user's editable mappings on first sight.
/// </summary>
internal static class DefaultGlyphSeeder
{
    /// <summary>
    /// Seed not-yet-offered defaults into <see cref="Configuration.CustomMappings"/>.
    /// Does not overwrite an existing custom entry. Marks each key as seeded so
    /// a user who deletes a default will not get it back on the next launch.
    /// </summary>
    /// <returns>True if configuration changed and should be saved.</returns>
    public static bool SeedInto(Configuration config) =>
        Seed(config.CustomMappings, config.SeededDefaultGlyphKeys, ignoreSeededKeys: false);

    /// <summary>
    /// Re-offer every shipped default, including ones already seeded before.
    /// Additive only: an entry the user still has is left exactly as it is.
    /// </summary>
    /// <returns>True if configuration changed and should be saved.</returns>
    public static bool RestoreAll(Configuration config) =>
        Seed(config.CustomMappings, config.SeededDefaultGlyphKeys, ignoreSeededKeys: true);

    /// <summary>
    /// Takes the collections rather than the whole <see cref="Configuration"/> so this stays
    /// independent of Dalamud's <c>IPluginConfiguration</c> and can be exercised directly.
    /// </summary>
    /// <param name="ignoreSeededKeys">
    /// False for the startup pass, which must respect deletions. True for the user-invoked
    /// restore, whose entire purpose is to re-offer what was deleted.
    /// </param>
    public static bool Seed(
        Dictionary<string, string> mappings,
        List<string> seededKeys,
        bool ignoreSeededKeys)
    {
        var alreadySeeded = new HashSet<string>(seededKeys, StringComparer.Ordinal);
        var changed = false;

        foreach (var (from, to) in DefaultGlyphMaps.Entries)
        {
            var key = from.ToString();
            var isNewKey = !alreadySeeded.Contains(key);
            if (!isNewKey && !ignoreSeededKeys)
                continue;

            if (!mappings.ContainsKey(key))
            {
                mappings[key] = to.ToString();
                changed = true;
            }

            if (isNewKey)
            {
                alreadySeeded.Add(key);
                seededKeys.Add(key);
                changed = true;
            }
        }

        return changed;
    }
}
