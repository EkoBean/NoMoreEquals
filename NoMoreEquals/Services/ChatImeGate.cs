using Dalamud.Bindings.ImGui;

namespace NoMoreEquals.Services;

/// <summary>
/// The one and only writer of "IME messages currently belong to the game's chat box".
/// <para>
/// This used to be computed in two places: <see cref="ChatInputWatcher"/> during
/// <c>Framework.Update</c> — where ImGui's <c>WantTextInput</c> is a frame stale —
/// and <see cref="ChatPreviewOverlay"/> during <c>UiBuilder.Draw</c>. The window
/// procedure runs between those two phases, so on any frame where the two
/// disagreed it observed whichever answer happened to be written last.
/// </para>
/// <para>
/// It is now evaluated exactly once per frame from <c>Plugin.OnDraw</c>, before
/// anything else draws, because <c>WantTextInput</c> is only authoritative inside
/// the ImGui frame. Everyone else reads <see cref="Accept"/>.
/// </para>
/// </summary>
internal sealed class ChatImeGate
{
    private readonly ChatInputAccessor accessor;

    /// <summary>Written at Draw time, read from the window procedure.</summary>
    private volatile bool accept;

    public ChatImeGate(ChatInputAccessor accessor)
    {
        this.accessor = accessor;
    }

    /// <summary>
    /// True when the game's chat input owns the keyboard. False whenever a Dalamud
    /// ImGui text field has focus, even though ChatLog may still report IsActive.
    /// </summary>
    public bool Accept => this.accept;

    /// <summary>
    /// Call once per frame, first thing in the draw callback.
    /// <para>
    /// Deliberately does <b>not</b> consider <see cref="Configuration.Enabled"/>: this
    /// answers "where is the keyboard pointing", which is true or false regardless of
    /// our settings. Folding the toggle in here would mean a commit already stripped
    /// from the game became undeliverable the instant the player unticked the box —
    /// the callers apply that policy themselves.
    /// </para>
    /// </summary>
    public void Update()
    {
        bool next;
        try
        {
            next = this.accessor.IsInputActive() && !ImGui.GetIO().WantTextInput;
        }
        catch
        {
            next = false;
        }

        this.accept = next;
    }

    /// <summary>Close the gate on shutdown so in-flight messages stop being handled.</summary>
    public void Close() => this.accept = false;
}
