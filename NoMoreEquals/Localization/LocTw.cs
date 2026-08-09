namespace NoMoreEquals.Localization;

/// <summary>
/// Traditional Chinese UI copy for Dalamud language code <c>tw</c>.
/// Edit this file to change Chinese strings.
/// </summary>
internal static class LocTw
{
    public static UiStrings Create() => new()
    {
        WindowTitle = "NoMoreEquals 不要等號",
        EnableChatConversion = "開啟 \"NoMoreEquals 不要等號\"",
        ShowChatPreviewOverlay = "顯示聊天框轉換預覽",
        StatusCounts = "缺字對照：{0}　｜　替代詞彙：{1}",
        TabMain = "首頁",
        TabAdvanced = "進階",
        Preview = "字符轉換預覽",
        PreviewInputHint = "這裡不會反映遊戲內缺字，僅供效果預覽。",
        PreviewResult = "轉換結果",
        PreviewHelp =
            "請輸入文字",
        PhraseSectionTitle = "自訂替換詞",
        PhraseSectionHelp =
            "有些國字沒有對應的日文漢字(例如：你、潢、懂)，可以自定義使用者替換詞。\n",
        PhraseFrom = "原文",
        PhraseTo = "替換為",
        AddPhrase = "新增詞彙",
        NoPhrasesYet = "尚未新增替代詞彙。",
        GlyphSectionTitle = "自訂缺字轉換",
        GlyphSectionHelp =
            "內建異體字轉換會自動生效。" +
            "下方是可自行增刪缺字替換。",
        GlyphFrom = "原字",
        GlyphTo = "換成",
        AddGlyph = "新增文字",
        NoGlyphOverridesYet = "尚未新增文字替換。",
        Delete = "刪除",
        AdvancedHelp =
            "AXIS 是 FFXIV 聊天／介面用的系統字體。外掛啟動時會自動讀取字體裡有哪些字，" +
            "若更換字體包更新才需重建對照。",
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
        GlyphNeedSingleChar = "單字對照兩邊都必須剛好一個字，且不相同。",
        SourceNeedChineseError = "原文必須包含中文字，這個插件只轉換中文字形。",
    };
}
