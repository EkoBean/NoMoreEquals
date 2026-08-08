using System;
using System.Collections.Generic;
using Dalamud.Configuration;

namespace NoMoreEquals;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 2;

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

    /// <summary>Single-character glyph overrides (e.g. rare missing hanzi → Japanese form).</summary>
    public Dictionary<string, string> CustomMappings { get; set; } = new();

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
