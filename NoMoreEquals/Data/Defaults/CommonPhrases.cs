using System.Collections.Generic;

namespace NoMoreEquals.Data.Defaults;

/// <summary>
/// Multi-character wording defaults seeded into the user's phrase replacements.
/// Unlike <see cref="CommonParticles"/> these go through
/// <see cref="Services.PhraseReplacementService"/>, so each entry gets its own
/// enable toggle and participates in longest-match-first replacement.
/// </summary>
internal static class CommonPhrases
{
    public static IReadOnlyDictionary<string, string> Entries { get; } = new Dictionary<string, string>
    {
        ["\u5496\u5561"] = "\u73C8\u7432", // 咖啡 -> 珈琲
    };
}
