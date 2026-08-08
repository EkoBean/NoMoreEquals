using System;

namespace NoMoreEquals.Localization;

/// <summary>
/// Resolves UI language from Dalamud's <c>UiLanguage</c>.
/// Default is English. Chinese UI is used for Dalamud codes <c>tw</c> (Traditional) and <c>zh</c> (Simplified).
/// Note: Dalamud does not use <c>zh-TW</c>; Traditional Chinese is <c>tw</c>.
/// </summary>
internal static class I18n
{
    public static UiStrings T { get; private set; } = LocEn.Create();

    public static string CurrentLangCode { get; private set; } = "en";

    public static bool IsChinese => CurrentLangCode is "tw" or "zh";

    public static void Apply(string? langCode)
    {
        var code = string.IsNullOrWhiteSpace(langCode) ? "en" : langCode.Trim().ToLowerInvariant();
        CurrentLangCode = code;
        T = IsChineseLanguage(code) ? LocTw.Create() : LocEn.Create();
    }

    public static bool IsChineseLanguage(string? langCode)
    {
        if (string.IsNullOrWhiteSpace(langCode))
            return false;

        var code = langCode.Trim().ToLowerInvariant();
        // Dalamud: tw = Traditional Chinese, zh = Simplified Chinese.
        // Also accept common CultureInfo-style aliases just in case.
        return code is "tw" or "zh" or "zh-tw" or "zh-hant" or "zh-cn" or "zh-hans";
    }
}
