using System;
using System.Collections.Frozen;
using System.Collections.Generic;

namespace NoMoreEquals.Data;

/// <summary>
/// The slash commands whose argument is ordinary chat text.
/// <para>
/// This plugin only has business touching a line that ends up in a chat channel.
/// <c>/gearset change 綠</c> and <c>/target 綠</c> take identifiers, not messages, and a
/// glyph substitution there breaks the command outright — so the whitelist is the gate,
/// not the leading slash.
/// </para>
/// <para>
/// Sourced from the Lodestone Eorzea Database and the FFXIV Console Games Wiki, which
/// agree with each other, plus in-game verification for <c>/beginner</c> and <c>/b</c>,
/// which both omit but which do work. An entry here silently changes what the plugin
/// rewrites, so each one needs one of those two kinds of evidence — <c>/cwls</c>
/// circulates widely among players and was tried on that strength, but the game rejects
/// it. <c>/quickchat</c> is excluded for the same reason as <c>/gearset</c>: its argument
/// is a preset id, not a message.
/// </para>
/// <para>
/// Both linkshell families stop at 8 because the game only ever has eight of each.
/// </para>
/// </summary>
internal static class ChatChannelCommands
{
    /// <summary>
    /// Frozen, not a plain <see cref="HashSet{T}"/>: this set is read from both the window
    /// procedure's thread and the render thread and is never written after startup, so the
    /// immutability the readers rely on is worth stating in the type rather than leaving
    /// to convention. Faster lookups come free with it.
    /// </summary>
    private static readonly FrozenSet<string> Tokens =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "say", "s",
            "yell", "y",
            "shout", "sh",
            "tell", "t",
            "reply", "r",
            "party", "p",
            "alliance", "a",
            "freecompany", "fc",
            "pvpteam", "pt",
            "novice", "n",
            "beginner", "b",
            "echo", "e",
            "emote", "em",
            "linkshell", "l",
            "linkshell1", "l1",
            "linkshell2", "l2",
            "linkshell3", "l3",
            "linkshell4", "l4",
            "linkshell5", "l5",
            "linkshell6", "l6",
            "linkshell7", "l7",
            "linkshell8", "l8",
            "cwlinkshell", "cwl",
            "cwlinkshell1", "cwl1",
            "cwlinkshell2", "cwl2",
            "cwlinkshell3", "cwl3",
            "cwlinkshell4", "cwl4",
            "cwlinkshell5", "cwl5",
            "cwlinkshell6", "cwl6",
            "cwlinkshell7", "cwl7",
            "cwlinkshell8", "cwl8",
        }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// How many tokens the whitelist holds. Exposed so a test can pin it: a token that
    /// goes missing already breaks the plugin visibly, but a token added without a source
    /// widens what gets rewritten and would otherwise land in silence.
    /// </summary>
    public static int Count => Tokens.Count;

    /// <summary>
    /// Whether <paramref name="token"/> — a command name with its leading '/' already
    /// stripped — sends its argument to a chat channel. Case-insensitive, because the
    /// game accepts <c>/P</c> as readily as <c>/p</c>.
    /// </summary>
    public static bool IsChatChannel(string token) => Tokens.Contains(token);
}
