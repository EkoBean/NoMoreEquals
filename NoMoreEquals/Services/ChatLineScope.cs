using NoMoreEquals.Data;

namespace NoMoreEquals.Services;

/// <summary>
/// How much of a chat line this plugin is allowed to touch.
/// <para>
/// Replaces a pair of loose predicates (<c>IsInsideCommandToken</c> plus
/// <c>GetChatBodyStart</c>) that had to be called together and could disagree — which is
/// why every caller ended up carrying its own copy of the reasoning. One type answers the
/// whole question, so a caller cannot hold half of the answer.
/// </para>
/// </summary>
/// <param name="Convertible">False means the plugin does not act on this line at all.</param>
/// <param name="BodyStart">
/// Index the convertible part starts at: 0 for an ordinary line, just past the command
/// token's trailing space for a chat channel command. Meaningless when
/// <paramref name="Convertible"/> is false.
/// </param>
internal readonly record struct ChatLineScope(bool Convertible, int BodyStart)
{
    private static readonly ChatLineScope Blocked = new(false, 0);
    private static readonly ChatLineScope WholeLine = new(true, 0);

    public static ChatLineScope Of(string? line)
    {
        if (string.IsNullOrEmpty(line) || line[0] != '/')
            return WholeLine;

        // Halfwidth space only. A CJK IME will happily put U+3000 here, but the game
        // cannot parse that line either, and a command the game rejects is not one this
        // plugin has any reason to act on.
        var space = line.IndexOf(' ');

        // No separator yet: the line could still become either a chat message or a
        // /gearset call, so nothing may happen to it.
        if (space < 0)
            return Blocked;

        return ChatChannelCommands.IsChatChannel(line[1..space])
            ? new ChatLineScope(true, space + 1)
            : Blocked;
    }
}
