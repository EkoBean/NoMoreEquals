using System.Collections.Generic;

namespace NoMoreEquals.Data.DefaultMissingGlyphs;

/// <summary>
/// High-frequency particles / sentence endings that AXIS lacks,
/// mapped to near-homophone glyphs that do render.
/// </summary>
internal static class CommonParticles
{
    public static IReadOnlyDictionary<char, char> Entries { get; } = new Dictionary<char, char>
    {
        ['\u554A'] = '\u963F', // 啊 -> 阿
        ['\u55CE'] = '\u561B', // 嗎 -> 嘛
        ['\u5594'] = '\u54E6', // 喔 -> 哦
    };
}
