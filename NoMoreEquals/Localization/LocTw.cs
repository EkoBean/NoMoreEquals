namespace NoMoreEquals.Localization;

/// <summary>
/// Traditional Chinese UI copy for Dalamud language codes <c>tw</c> and <c>zh</c>.
/// Edit this file to change Chinese strings.
/// </summary>
internal static class LocTw
{
    public static UiStrings Create() => new()
    {
        WindowTitle = "NoMoreEquals 不要等號",
        EnableChatConversion = "開啟 \"NoMoreEquals 不要等號\"",
        SkipSlashCommands = "略過以 / 開頭的指令列",
        StatusCounts = "缺字對照：{0}　｜　替代詞彙：{1}",
        TabMain = "首頁",
        TabAdvanced = "進階",
        Preview = "轉換預覽",
        PreviewInputHint = "這裡不會反映遊戲內缺字。",
        PreviewResult = "轉換結果",
        PreviewHelp =
            "請輸入文字",
        PhraseSectionTitle = "替代詞彙（自訂）",
        PhraseSectionHelp =
            "用在日文／字體都無法直接表達的說法，例如「你」→「尼」、「懂」→「明白」。" +
            "會先做詞彙替換，再做缺字漢字轉換。較長的詞優先匹配。",
        PhraseFrom = "原文",
        PhraseTo = "替換為",
        AddPhrase = "新增詞彙",
        NoPhrasesYet = "尚未新增替代詞彙。",
        GlyphSectionTitle = "缺字漢字對照",
        GlyphSectionHelp =
            "內建對照會把遊戲字體顯示不出來的中文漢字換成日文漢字（例如 綠→緑）。" +
            "字體裡本來就有的字（圖、邊、國、學…）不會動。" +
            "下方可再加單字覆寫。",
        GlyphFrom = "原字",
        GlyphTo = "換成",
        AddGlyph = "新增單字",
        NoGlyphOverridesYet = "尚未新增單字覆寫（內建缺字對照仍會生效）。",
        Delete = "刪除",
        AdvancedHelp =
            "AXIS 是 FFXIV 聊天／介面用的系統字體。外掛啟動時會自動讀取字體裡有哪些字，" +
            "只替換真正缺字的漢字。一般使用者不必手動重建。",
        UseLiveFontFilter = "依目前遊戲字體過濾缺字對照",
        RebuildFromFont = "重新讀取遊戲字體並重建對照",
        AdvancedOverviewHelp =
            "展開下方區塊，可瀏覽此外掛目前載入的全部替代詞彙與缺字對照。",
        AdvancedPhraseOverview = "替代詞彙（{0}）",
        AdvancedGlyphOverview = "缺字漢字對照（{0}）",
        AdvancedOverviewEmpty = "目前沒有載入任何項目。",
        AdvancedOverviewSource = "來源",
        AdvancedOverviewBuiltIn = "內建",
        AdvancedOverviewCustom = "自訂",
        PhraseEmptyError = "原文不能是空的。",
        PhraseSameError = "原文與替換後相同，無需新增。",
        PhraseAdded = "已設定詞彙：{0} → {1}",
        PhraseRemoved = "已刪除替代詞彙。",
        GlyphAdded = "已新增單字：{0} → {1}",
        GlyphNeedSingleChar = "單字對照兩邊都必須剛好一個字，且不相同。",
        RebuiltStatus = "已重建。缺字對照 {0}，替代詞彙 {1}",
    };
}
