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
    /// Convert a whole chat line, honouring <see cref="ChatLineScope"/>: a line the
    /// plugin may not touch comes back unchanged, and a chat channel command keeps its
    /// command token verbatim while its message body is converted.
    /// <para>
    /// Asks the scope itself rather than trusting the caller to have asked. Every caller
    /// does gate on <see cref="ConversionGate.ShouldConvert"/> first, but this is the
    /// function that actually rewrites player text, so it does not get to assume that.
    /// </para>
    /// </summary>
    public static string ConvertChatLine(
        string input,
        PhraseReplacementService phrases,
        IReadOnlyDictionary<char, char> glyphMap)
    {
        var scope = ChatLineScope.Of(input);
        if (!scope.Convertible)
            return input;

        if (scope.BodyStart == 0)
            return ConvertAll(input, phrases, glyphMap);

        // A command with its separator typed but no body yet ("/p ").
        if (scope.BodyStart >= input.Length)
            return input;

        var body = input[scope.BodyStart..];
        var converted = ConvertAll(body, phrases, glyphMap);
        return ReferenceEquals(converted, body)
            ? input
            : string.Concat(input.AsSpan(0, scope.BodyStart), converted);
    }

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
