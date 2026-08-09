using System;

namespace NoMoreEquals.Services;

/// <summary>
/// Pure text heuristics for reading the game's chat buffer. Split out of
/// <see cref="ImeCompositionTracker"/>: these are string questions, not IME or Win32
/// ones, and keeping them free of any dependency makes them directly testable.
/// </summary>
internal static class ChatBufferHeuristics
{
    /// <summary>
    /// True when the buffer looks like the '=' soup the game writes into RawString for
    /// glyphs AXIS cannot render, rather than text we can reason about.
    /// <para>
    /// This is a density guess and it is wrong at the edges — <c>"a==b"</c> trips it —
    /// so only ask it while an IME composition is in progress, where RawString is
    /// genuinely untrustworthy. Once composition has ended, read the buffer directly.
    /// </para>
    /// </summary>
    public static bool IsMostlyEqualsNoise(string? s)
    {
        if (string.IsNullOrEmpty(s) || s.Length < 3)
            return false;

        var eq = 0;
        foreach (var c in s)
        {
            if (c == '=')
                eq++;
        }

        return eq * 2 >= s.Length;
    }

    /// <summary>
    /// The trailing run of Bopomofo in <paramref name="inputText"/>, used as a fallback
    /// reading when IMM reports no composition string. Empty when the buffer ends in
    /// anything else, or looks like '=' noise.
    /// </summary>
    public static string TrailingBopomofo(string? inputText)
    {
        if (string.IsNullOrEmpty(inputText))
            return string.Empty;

        // Only use the heuristic when the trailing run is purely Bopomofo —
        // never treat '=' noise as composition.
        var end = inputText.Length;
        var start = end;
        while (start > 0 && IsBopomofo(inputText[start - 1]))
            start--;

        if (start == end)
            return string.Empty;

        if (IsMostlyEqualsNoise(inputText))
            return string.Empty;

        return inputText[start..];
    }

    public static bool IsBopomofo(char c)
        => c is (>= '\u3105' and <= '\u312F')
            or (>= '\u31A0' and <= '\u31BF')
            or '\u02C7' or '\u02C9' or '\u02CA' or '\u02CB' or '\u02D9';
}
