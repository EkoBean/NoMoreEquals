using System;
using System.Collections.Generic;
using Dalamud.Configuration;

namespace NoMoreEquals;

/// <summary>
/// Persisted user settings. Plain data only — saving is owned by
/// <see cref="Services.ConfigService"/>.
/// </summary>
[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = CurrentVersion;

    /// <summary>Latest on-disk schema version understood by this build.</summary>
    public const int CurrentVersion = 3;

    /// <summary>When false, chat input is left unchanged.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// When true, draw the live conversion preview above the chat input box.
    /// </summary>
    public bool ShowChatPreviewOverlay { get; set; } = true;

    /// <summary>
    /// When true, rebuild the active glyph map from candidates + live AXIS glyphs on load.
    /// When false, use the shipped AXIS-filtered <see cref="Data.KanjiMap"/> only.
    /// </summary>
    public bool UseLiveFontFilter { get; set; } = true;

    /// <summary>
    /// Editable single-character glyph overrides shown on the Main tab
    /// (includes shipped defaults such as 啊→阿 after first seed).
    /// </summary>
    public Dictionary<string, string> CustomMappings { get; set; } = new();

    /// <summary>
    /// Curated default glyph keys already offered to this install.
    /// Prevents re-adding a default the user deleted, while still allowing
    /// newly shipped defaults to be seeded on upgrade.
    /// </summary>
    public List<string> SeededDefaultGlyphKeys { get; set; } = [];

    /// <summary>
    /// User phrase / wording replacements (e.g. 你→尼, 懂→明白).
    /// Applied before single-character glyph conversion.
    /// </summary>
    public List<PhraseReplacement> PhraseReplacements { get; set; } = [];

    /// <summary>
    /// Repairs a config deserialized from disk. Collections can come back null from an
    /// older or hand-edited file, so every consumer downstream can assume non-null.
    /// Schema changes so far have only added fields with defaults, so stamping the
    /// version is enough; a real migration would branch on the incoming value here.
    /// </summary>
    public void Normalize()
    {
        this.CustomMappings ??= new Dictionary<string, string>();
        this.SeededDefaultGlyphKeys ??= [];
        this.PhraseReplacements ??= [];
        this.PhraseReplacements.RemoveAll(p => p is null);
        this.Version = CurrentVersion;
    }
}
