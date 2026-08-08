namespace NoMoreEquals.Localization;

/// <summary>
/// Resolves UI language from Dalamud's <c>UiLanguage</c>. Default is English.
/// Dalamud uses <c>tw</c> for Traditional Chinese and <c>zh</c> for Simplified;
/// only <c>tw</c> gets the Chinese UI, matching the plugin's stated support.
/// </summary>
internal static class I18n
{
    private const string TraditionalChinese = "tw";
    private const string English = "en";

    public static UiStrings T { get; private set; } = LocEn.Create();

    public static string CurrentLangCode { get; private set; } = English;

    public static void Apply(string? langCode)
    {
        CurrentLangCode = string.IsNullOrWhiteSpace(langCode)
            ? English
            : langCode.Trim().ToLowerInvariant();

        T = CurrentLangCode == TraditionalChinese ? LocTw.Create() : LocEn.Create();
    }
}
