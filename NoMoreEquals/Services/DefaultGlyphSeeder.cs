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
    public static bool SeedInto(Configuration config)
    {
        var alreadySeeded = new HashSet<string>(config.SeededDefaultGlyphKeys);
        var changed = false;

        foreach (var (from, to) in DefaultGlyphMaps.Entries)
        {
            var key = from.ToString();
            if (!alreadySeeded.Add(key))
                continue;

            if (!config.CustomMappings.ContainsKey(key))
                config.CustomMappings[key] = to.ToString();

            config.SeededDefaultGlyphKeys.Add(key);
            changed = true;
        }

        return changed;
    }
}
