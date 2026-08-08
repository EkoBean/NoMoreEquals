using System.Collections.Generic;
using System.Text;

namespace NoMoreEquals.Services;

internal static class KanjiConverter
{
    /// <summary>
    /// Replace characters according to <paramref name="map"/>.
    /// Returns the original string instance when nothing changes.
    /// </summary>
    public static string Convert(string input, IReadOnlyDictionary<char, char> map)
    {
        if (string.IsNullOrEmpty(input) || map.Count == 0)
            return input;

        StringBuilder? sb = null;
        for (var i = 0; i < input.Length; i++)
        {
            var c = input[i];
            if (map.TryGetValue(c, out var replacement) && replacement != c)
            {
                sb ??= new StringBuilder(input.Length).Append(input, 0, i);
                sb.Append(replacement);
            }
            else
            {
                sb?.Append(c);
            }
        }

        return sb?.ToString() ?? input;
    }

    /// <summary>
    /// Full chat pipeline: phrase replacements, then single-character glyph map.
    /// </summary>
    public static string ConvertAll(
        string input,
        PhraseReplacementService phrases,
        IReadOnlyDictionary<char, char> glyphMap)
    {
        var afterPhrases = phrases.Apply(input);
        return Convert(afterPhrases, glyphMap);
    }
}
