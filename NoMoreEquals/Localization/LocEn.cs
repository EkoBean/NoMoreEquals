namespace NoMoreEquals.Localization;

/// <summary>
/// Default (English) UI copy. Edit this file to change English strings.
/// </summary>
internal static class LocEn
{
    public static UiStrings Create() => new()
    {
        WindowTitle = "NoMoreEquals 不要等號",
        EnableChatConversion = "Enable NoMoreEquals",
        SkipSlashCommands = "Skip lines starting with /",
        StatusCounts = "Glyph mappings: {0}  |  Phrase replacements: {1}",
        TabMain = "Main",
        TabAdvanced = "Advanced",
        Preview = "Preview",
        PreviewInputHint = "This will not reflect in-game missing glyphs.",
        PreviewResult = "Converted",
        PreviewHelp =
            "Type something here.",
        PhraseSectionTitle = "Custom phrases",
        PhraseSectionHelp =
            "For those characters that don't have Japanese Kanji. \nCustomize your glyph replacements here.",
        PhraseFrom = "From",
        PhraseTo = "To",
        AddPhrase = "Add phrase",
        NoPhrasesYet = "No phrase customizations yet.",
        GlyphSectionTitle = "Missing-glyph mappings",
        GlyphSectionHelp =
            "Built-in variant maps (e.g. 綠→緑) apply automatically. " +
            "Below are editable overrides — starters like 啊→阿 / 嗎→嘛 are pre-filled; change or delete freely.",
        GlyphFrom = "From",
        GlyphTo = "To",
        AddGlyph = "Add character",
        NoGlyphOverridesYet = "No custom character overrides yet.",
        Delete = "Delete",
        AdvancedHelp =
            "AXIS is FFXIV's main UI/chat font. On startup the plugin reads which glyphs exist and only " +
            "converts Chinese forms that are actually missing. Most users never need this tab.",
        UseLiveFontFilter = "Filter glyph map using the current game font",
        RebuildFromFont = "Reload game font and rebuild glyph map",
        AdvancedOverviewHelp =
            "Expand the sections below to browse every phrase and glyph mapping currently loaded by the plugin.",
        AdvancedPhraseOverview = "Custom phrases ({0})",
        AdvancedGlyphOverview = "Missing-glyph mappings ({0})",
        AdvancedOverviewEmpty = "Nothing loaded.",
        AdvancedOverviewSource = "Source",
        AdvancedOverviewBuiltIn = "Built-in",
        AdvancedOverviewCustom = "Custom",
        PhraseEmptyError = "From text cannot be empty.",
        PhraseSameError = "From and To are identical; nothing to add.",
        PhraseAdded = "Phrase set: {0} → {1}",
        PhraseRemoved = "Phrase removed.",
        GlyphAdded = "Character mapping added: {0} → {1}",
        GlyphNeedSingleChar = "Each side must be exactly one distinct character.",
        RebuiltStatus = "Rebuilt. Glyph mappings: {0}, phrases: {1}",
    };
}
