using System;
using System.Collections.Generic;
using System.Text;

namespace NoMoreEquals.Services;

internal static class KanjiConverter
{
    /// <summary>
    /// Full chat pipeline: phrase replacements, then single-character glyph map.
    /// </summary>
    public static string ConvertAll(
        string input,
        PhraseReplacementService phrases,
        IReadOnlyDictionary<char, char> glyphMap)
    {
        return ConvertGlyphs(phrases.Apply(input), glyphMap);
    }

    /// <summary>
    /// Convert a whole chat line, preserving a leading slash command verbatim.
    /// <para>
    /// Both conversion layers used to bail out entirely on a leading '/', which
    /// silently disabled the plugin for <c>/say</c>, <c>/party</c>, <c>/tell</c> and
    /// every other channel command — i.e. for most of what players actually send.
    /// Only the command token itself needs protecting; everything after the first
    /// space is ordinary message text.
    /// </para>
    /// </summary>
    public static string ConvertChatLine(
        string input,
        PhraseReplacementService phrases,
        IReadOnlyDictionary<char, char> glyphMap)
    {
        var bodyStart = GetChatBodyStart(input);
        if (bodyStart == 0)
            return ConvertAll(input, phrases, glyphMap);

        if (bodyStart >= input.Length)
            return input;

        var body = input[bodyStart..];
        var converted = ConvertAll(body, phrases, glyphMap);
        return ReferenceEquals(converted, body)
            ? input
            : string.Concat(input.AsSpan(0, bodyStart), converted);
    }

    /// <summary>
    /// Index at which the convertible part of a chat line starts: 0 for an ordinary
    /// line, just past the command token's trailing space for a slash command, and
    /// <c>input.Length</c> for a command that has no arguments yet.
    /// </summary>
    public static int GetChatBodyStart(string input)
    {
        if (string.IsNullOrEmpty(input) || input[0] != '/')
            return 0;

        var space = input.IndexOf(' ');
        return space < 0 ? input.Length : space + 1;
    }

    /// <summary>
    /// True while the caret is still (necessarily) inside a slash command's own token,
    /// i.e. the line starts with '/' and has no argument separator yet. Used by the
    /// Imm layer, which sees a committed chunk but not a caret position.
    /// </summary>
    public static bool IsInsideCommandToken(string input)
        => !string.IsNullOrEmpty(input) && input[0] == '/' && input.IndexOf(' ') < 0;

    /// <summary>
    /// Replace characters according to <paramref name="map"/>.
    /// Returns the original string instance when nothing changes.
    /// </summary>
    private static string ConvertGlyphs(string input, IReadOnlyDictionary<char, char> map)
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
}
