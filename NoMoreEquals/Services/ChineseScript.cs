using System.Text;

namespace NoMoreEquals.Services;

/// <summary>
/// Whether a string has anything written Chinese in it.
/// <para>
/// Gates what may be entered as the source side of a user rule. A rule whose source is
/// pure ASCII has nothing to do with this plugin's job, but it still fires on every chat
/// line: a phrase rule <c>"Alt"</c> rewrites the recipient of <c>/tell Alt Ego@World</c>
/// and the message never arrives. The glyph map cannot do that — it only maps zh to jp
/// kanji — but phrase sources are free text, so they need the check.
/// </para>
/// </summary>
internal static class ChineseScript
{
    /// <summary>
    /// True when <paramref name="text"/> contains at least one Han character or one
    /// Bopomofo symbol. One is enough: the question is whether a rule has any business
    /// existing in this plugin, not whether its source is purely Chinese.
    /// </summary>
    public static bool HasChineseCharacter(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return false;

        foreach (var rune in text.EnumerateRunes())
        {
            if (IsHan(rune))
                return true;

            // Bopomofo comes from ChatBufferHeuristics rather than a second copy of the
            // ranges. Two definitions of the same concept in one codebase drift apart.
            if (rune.IsBmp && ChatBufferHeuristics.IsBopomofo((char)rune.Value))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Unicode's Han script. Deliberately not "Chinese characters": Chinese and Japanese
    /// kanji occupy the same code points, so no predicate can tell 綠 from 緑 by script,
    /// and none needs to.
    /// <para>
    /// Runes, not chars — Extension B and later live outside the BMP, and a char-wise
    /// scan sees only surrogate halves there.
    /// </para>
    /// </summary>
    private static bool IsHan(Rune rune) => rune.Value switch
    {
        >= 0x3400 and <= 0x4DBF => true,    // Extension A
        >= 0x4E00 and <= 0x9FFF => true,    // Unified
        >= 0xF900 and <= 0xFAFF => true,    // Compatibility
        >= 0x20000 and <= 0x3FFFF => true,  // Extension B onwards
        _ => false,
    };
}
