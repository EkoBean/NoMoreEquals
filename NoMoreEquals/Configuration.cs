using System;
using System.Collections.Generic;
using Dalamud.Configuration;

namespace NoMoreEquals;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 3;

    /// <summary>When false, chat input is left unchanged.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// When true, skip conversion for chat lines that start with '/'.
    /// </summary>
    public bool SkipSlashCommands { get; set; } = true;

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

    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }
}

[Serializable]
public sealed class PhraseReplacement
{
    public string From { get; set; } = string.Empty;

    public string To { get; set; } = string.Empty;

    public bool Enabled { get; set; } = true;
}
