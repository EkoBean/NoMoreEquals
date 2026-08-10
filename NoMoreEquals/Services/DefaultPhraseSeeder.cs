using System;
using System.Collections.Generic;
using NoMoreEquals.Data.Defaults;

namespace NoMoreEquals.Services;

/// <summary>
/// Copies shipped phrase defaults into the user's editable replacements.
/// </summary>
internal static class DefaultPhraseSeeder
{
    /// <summary>
    /// Seed defaults into <see cref="Configuration.PhraseReplacements"/> on startup.
    /// Skips anything already offered, so a default the user deleted stays deleted.
    /// </summary>
    /// <returns>True if configuration changed and should be saved.</returns>
    public static bool SeedInto(Configuration config) =>
        Seed(config.PhraseReplacements, config.SeededDefaultPhraseKeys, ignoreSeededKeys: false);

    /// <summary>
    /// Re-offer every shipped default, including ones already seeded before.
    /// Additive only: an entry the user still has is left exactly as it is.
    /// </summary>
    /// <returns>True if configuration changed and should be saved.</returns>
    public static bool RestoreAll(Configuration config) =>
        Seed(config.PhraseReplacements, config.SeededDefaultPhraseKeys, ignoreSeededKeys: true);

    /// <summary>
    /// Takes the lists rather than the whole <see cref="Configuration"/> so this stays
    /// independent of Dalamud's <c>IPluginConfiguration</c> and can be exercised directly.
    /// </summary>
    /// <param name="ignoreSeededKeys">
    /// False for the startup pass, which must respect deletions. True for the user-invoked
    /// restore, whose entire purpose is to re-offer what was deleted.
    /// </param>
    public static bool Seed(
        List<PhraseReplacement> phrases,
        List<string> seededKeys,
        bool ignoreSeededKeys)
    {
        var alreadySeeded = new HashSet<string>(seededKeys, StringComparer.Ordinal);
        var changed = false;

        foreach (var (from, to) in DefaultPhraseMaps.Entries)
        {
            var isNewKey = !alreadySeeded.Contains(from);
            if (!isNewKey && !ignoreSeededKeys)
                continue;

            if (!phrases.Exists(p => string.Equals(p.From, from, StringComparison.Ordinal)))
            {
                phrases.Add(new PhraseReplacement { From = from, To = to, Enabled = true });
                changed = true;
            }

            if (isNewKey)
            {
                alreadySeeded.Add(from);
                seededKeys.Add(from);
                changed = true;
            }
        }

        return changed;
    }
}
