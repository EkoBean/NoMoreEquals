using System;
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
/// agree. Aliases that circulate among players but appear in neither (<c>/beginner</c>,
/// <c>/cwls1</c>) are deliberately absent: an entry here silently changes what the
/// plugin rewrites, so it needs a source. <c>/quickchat</c> is excluded for the same
/// reason as <c>/gearset</c> — its argument is a preset id.
/// </para>
/// </summary>
internal static class ChatChannelCommands
{
    private static readonly HashSet<string> Tokens =
        new(StringComparer.OrdinalIgnoreCase)
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
        };

    /// <summary>
    /// Whether <paramref name="token"/> — a command name with its leading '/' already
    /// stripped — sends its argument to a chat channel. Case-insensitive, because the
    /// game accepts <c>/P</c> as readily as <c>/p</c>.
    /// </summary>
    public static bool IsChatChannel(string token) => Tokens.Contains(token);
}
