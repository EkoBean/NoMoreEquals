using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace NoMoreEquals.Services;

/// <summary>
/// In-game conversion without touching already-committed text:
/// <list type="bullet">
/// <item>IME RESULTSTR is rewritten at the Imm message layer (GCS_RESULTSTR stripped).</item>
/// <item>Converted glyphs are appended via InsertText only — never SetText while typing.</item>
/// <item>No synthetic backspace (that wiped the line after IME Enter).</item>
/// </list>
/// SetText is reserved for Enter-to-send as a last-resort safety net.
/// <para>
/// This type is the sole owner of the tracker's <i>insert channel</i> and of its
/// composition state. Because stripping GCS_RESULTSTR removes the player's text from
/// the game, a queued insert is the only remaining copy of it: every path below either
/// delivers it or goes through <see cref="DropPendingInsert"/>, which is the single
/// place allowed to give up on one.
/// </para>
/// </summary>
internal sealed class ChatInputWatcher : IDisposable
{
    private const ushort VirtualKeyEnd = 0x23;
    private const ushort VirtualKeyRight = 0x27;
    private const uint InputKeyboard = 1;
    private const uint KeyEventKeyUp = 0x0002;

    /// <summary>
    /// Framework ticks to let pass after a commit becomes deliverable, so the IME's own
    /// teardown messages are all pumped before we touch the buffer.
    /// <para>
    /// This replaces a 40ms wall clock. One frame is a real boundary — the message queue
    /// has been drained and the game has completed a tick — whereas 40ms was a number
    /// found by trial against an older design that still used SetText and synthetic
    /// backspace. Every millisecond spent here is a window in which a non-IME keystroke
    /// (space, an ASCII letter) reaches the chat buffer ahead of our converted text and
    /// comes out in the wrong order, so this is the smallest value with a rationale
    /// behind it. <c>IsComposing</c> is the guard that actually keeps us out of the
    /// IME's way; this is only settle time on top of it.
    /// </para>
    /// <para>
    /// If ordering still slips, 0 is the next thing to try: it inserts on the first tick
    /// after the commit. If characters start getting eaten or the line gets wiped
    /// instead, the settle time was load-bearing after all and this needs to go back up.
    /// </para>
    /// </summary>
    private const int SettleTicks = 1;

    /// <summary>
    /// How long a suppressed commit may wait for a chat box it can be delivered into.
    /// Generous on purpose — waiting out a detour into a Dalamud window is far better
    /// than losing the characters, which is what the old unconditional clear did.
    /// </summary>
    private static readonly TimeSpan PendingInsertLifetime = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Shorter window for "chat is not focused at all". Long enough to ride out a
    /// one-frame blip around an IME commit, short enough that a genuinely closed chat
    /// box does not leak the queued text into whatever line is typed next.
    /// </summary>
    private static readonly TimeSpan ChatGoneGrace = TimeSpan.FromMilliseconds(250);

    private readonly Configuration configuration;
    private readonly ConversionGate gate;
    private readonly KanjiMapService mapService;
    private readonly PhraseReplacementService phraseService;
    private readonly ChatInputAccessor accessor;
    private readonly IFramework framework;
    private readonly IPluginLog log;
    private readonly ImeCompositionTracker imeTracker;

    private bool writing;

    /// <summary>Ticks the current pending insert has spent deliverable but unsent.</summary>
    private int settledTicks;

    public ChatInputWatcher(
        Configuration configuration,
        ConversionGate gate,
        KanjiMapService mapService,
        PhraseReplacementService phraseService,
        ChatInputAccessor accessor,
        IFramework framework,
        IPluginLog log,
        ImeCompositionTracker imeTracker)
    {
        this.configuration = configuration;
        this.gate = gate;
        this.mapService = mapService;
        this.phraseService = phraseService;
        this.accessor = accessor;
        this.framework = framework;
        this.log = log;
        this.imeTracker = imeTracker;

        this.imeTracker.RewriteCommittedResult = this.TryRewriteResult;
        this.imeTracker.BeforeReturnKeyDown = this.ConvertBeforeSend;
        this.framework.Update += this.OnFrameworkUpdate;
    }

    public void Dispose()
    {
        this.framework.Update -= this.OnFrameworkUpdate;

        // Delegate value equality, not ReferenceEquals: a method group conversion
        // allocates a fresh delegate every time, so ReferenceEquals here was always
        // false and these handlers were never actually unhooked.
        if (this.imeTracker.BeforeReturnKeyDown == (Action)this.ConvertBeforeSend)
            this.imeTracker.BeforeReturnKeyDown = null;

        if (this.imeTracker.RewriteCommittedResult == (Func<string, string?>)this.TryRewriteResult)
            this.imeTracker.RewriteCommittedResult = null;
    }

    private string? TryRewriteResult(string result)
    {
        if (!this.imeTracker.AcceptChatIme)
            return null;

        // The gate is asked about the whole line, but the conversion runs on the chunk
        // the IME just committed: the line decides whether we may act at all, the chunk
        // is the text there is to act on. Returning null leaves GCS_RESULTSTR in place,
        // so the game receives exactly what the player typed and this plugin never
        // touched the line.
        if (!this.gate.ShouldConvert(this.accessor.GetActiveText()))
            return null;

        var converted = KanjiConverter.ConvertAll(result, this.phraseService, this.mapService.Active);
        return string.Equals(converted, result, StringComparison.Ordinal) ? null : converted;
    }

    private unsafe void OnFrameworkUpdate(IFramework frameworkParam)
    {
        _ = frameworkParam;

        // Unconditional, every tick. The subclass is what makes the whole pipeline
        // exist — RESULTSTR rewriting, the pre-send conversion, composition state —
        // so installing it must never depend on a display setting. It used to be
        // reached only through the preview overlay's draw path, which meant turning
        // the overlay off silently disabled conversion entirely.
        this.imeTracker.SyncSubclass(this.configuration.Enabled);

        if (this.writing)
            return;

        // Conversion off and nothing in flight: stop polling IMM entirely. An in-flight
        // commit still drains below, because its original was already taken from the game.
        if (!this.configuration.Enabled && !this.imeTracker.HasPendingInsert)
            return;

        try
        {
            var hasInput = this.accessor.TryGetActiveInput(out var input);
            var textHint = hasInput ? ChatInputAccessor.GetText(input) : string.Empty;

            this.imeTracker.Refresh(
                ChatBufferHeuristics.IsMostlyEqualsNoise(textHint) ? null : textHint);

            this.FlushPendingInsert(hasInput, input);
        }
        catch (Exception ex)
        {
            this.log.Debug(ex, "NoMoreEquals: framework tick skipped");
            this.writing = false;
        }
    }

    private unsafe void FlushPendingInsert(bool hasInput, AtkComponentTextInput* input)
    {
        if (!this.imeTracker.HasPendingInsert)
        {
            this.settledTicks = 0;
            return;
        }

        if (!hasInput)
        {
            // Chat lost focus. Ride out a transient blip, but do not hold the text long
            // enough that it could surface on a different line later.
            this.ExpirePendingInsert(ChatGoneGrace, "chat input is no longer focused");
            return;
        }

        if (this.imeTracker.IsComposing || !this.imeTracker.AcceptChatIme)
        {
            // Not deliverable this tick: still mid-composition, or a Dalamud text
            // field owns the keyboard so InsertText would go to the wrong place.
            // Wait — the player usually clicks straight back — but not forever.
            this.ExpirePendingInsert(PendingInsertLifetime, "undeliverable");
            return;
        }

        // Deliverable from here: composition is over and chat owns the keyboard. Let the
        // settle ticks pass, then insert on the very next one.
        if (this.settledTicks++ < SettleTicks)
            return;

        this.settledTicks = 0;

        if (!this.imeTracker.TryConsumePendingInsert(out var converted, out var original))
            return;

        // Conversion may have been switched off, or the last rule deleted, while this
        // commit was in flight. Deliver what the game would have received rather than
        // honour the toggle by throwing the player's characters away.
        //
        // IsArmed, never ShouldConvert: the line may since have become "/gearset ...",
        // but this text was taken out of the game before that happened and is the only
        // copy of it. The gate decides whether to convert, never whether to deliver.
        if (!this.gate.IsArmed)
            converted = original;

        if (string.IsNullOrEmpty(converted))
            return;

        var text = ChatInputAccessor.GetText(input);

        // If Imm still delivered the Chinese (strip failed / TSF path), the text is
        // already in the buffer: do not InsertText on top (would duplicate) and do not
        // backspace/SetText the line. Send-time SetText remains the safety net.
        if (!string.IsNullOrEmpty(original) && text.EndsWith(original, StringComparison.Ordinal))
        {
            this.log.Debug("NoMoreEquals: RESULTSTR suppress missed; leaving buffer untouched");
            return;
        }

        // There is deliberately no "the buffer already ends with `converted`, so skip"
        // check here. It cannot tell "we already inserted this" from "the player typed
        // the same word twice", and the queue has been consumed by this point, so the
        // second 黒色 of 黒色黒色 was silently destroyed. It is also unnecessary: the
        // consume above is atomic, so one commit cannot be flushed twice, and the game
        // never receives our converted form — only the original Chinese, which the
        // check above covers.

        // Decide this before inserting: afterwards the buffer string is not reliably
        // refreshed within the same frame, but CursorPos read now is the real pre-insert
        // caret.
        var appendingAtEnd = CaretIsAtEndOf(input, text);

        this.writing = true;
        try
        {
            // Append only — previously committed characters are never rewritten.
            input->InsertText(converted, false);
        }
        finally
        {
            this.writing = false;
        }

        this.AdvanceCaretAfterInsert(converted, appendingAtEnd);
    }

    private void ExpirePendingInsert(TimeSpan grace, string reason)
    {
        var age = this.imeTracker.TimeSincePendingInsertQueued;
        if (age != Timeout.InfiniteTimeSpan && age > grace)
            this.DropPendingInsert($"{reason}, {age.TotalMilliseconds:F0}ms after commit");
    }

    /// <summary>
    /// Last resort: give up on a commit we can no longer deliver.
    /// <para>
    /// Nothing about the message is recorded — not the text, not its length. This
    /// plugin converts glyphs; it never writes chat content anywhere, and a diagnostic
    /// is not a good enough reason to start. The reason string describes only the
    /// pipeline state that caused the drop.
    /// </para>
    /// </summary>
    private void DropPendingInsert(string reason)
    {
        this.imeTracker.ClearPendingInsert();
        this.log.Debug($"NoMoreEquals: dropped a suppressed IME commit ({reason})");
    }

    /// <summary>
    /// Whether the caret sits at the end of <paramref name="text"/>, i.e. this commit is
    /// being appended rather than inserted back into the middle of a line.
    /// <para>
    /// Accepts either candidate unit for <c>CursorPos</c> — the game's own unit is not
    /// documented and writing the field turned out not to move the caret, so it cannot
    /// be probed. Both readings agreeing on "at the end" is enough, and being wrong only
    /// costs a caret jump the player can undo, never text.
    /// </para>
    /// <para>
    /// Known limitation: this measures RawString, which the game fills with '=' for
    /// glyphs AXIS cannot render, so its length does not always agree with what CursorPos
    /// counts. When they disagree an end-of-line append is misread as a mid-line one and
    /// falls back to N × Right — the caret still lands correctly, just with the old
    /// latency, so it is left alone. Fixing it properly means feeding this an Imm-backed
    /// shadow of the buffer instead of RawString.
    /// </para>
    /// </summary>
    private static unsafe bool CaretIsAtEndOf(AtkComponentTextInput* input, string text)
    {
        var pos = input->AtkComponentInputBase.CursorPos;
        return pos == CountRunes(text) || pos == Encoding.UTF8.GetByteCount(text);
    }

    /// <summary>
    /// Push the caret past the text we just inserted. <c>InsertText</c> leaves it at the
    /// <i>start</i> of the inserted span.
    /// <para>
    /// Synthesised keys rather than a direct write: <c>CursorPos</c> is an output of the
    /// game's input handling, not an input to it. Writing all three of CursorPos /
    /// SelectionStart / SelectionEnd leaves the values in place — an immediate read-back
    /// agrees, and so does one taken a frame later — but the caret node is only
    /// repositioned when the game processes real input, so nothing moves on screen.
    /// </para>
    /// <para>
    /// End instead of N × Right whenever the commit landed at the end of the line, which
    /// is the overwhelmingly common case: one event instead of one per character, and it
    /// lands correctly without having to know what unit the caret counts in.
    /// </para>
    /// </summary>
    private void AdvanceCaretAfterInsert(string inserted, bool appendingAtEnd)
    {
        if (appendingAtEnd)
        {
            this.SendCaretKey(VirtualKeyEnd, 1);
            return;
        }

        var caretMoves = GetCaretMoveCount(inserted);
        if (caretMoves > 0)
            this.SendCaretKey(VirtualKeyRight, caretMoves);
    }

    private void SendCaretKey(ushort virtualKey, int pressCount)
    {
        try
        {
            if (!this.accessor.IsInputActive())
                return;

            // SendInput delivers to the OS foreground window, not to the addon checked
            // above. Without this, losing focus at the wrong moment sprays keys into
            // whatever application took over.
            var gameWindow = this.imeTracker.WindowHandle;
            if (gameWindow == IntPtr.Zero || GetForegroundWindow() != gameWindow)
                return;

            SendKeyPress(virtualKey, pressCount);
        }
        catch (Exception ex)
        {
            this.log.Debug(ex, "NoMoreEquals: caret advance skipped");
        }
    }

    private static int GetCaretMoveCount(string text)
    {
        if (string.IsNullOrEmpty(text))
            return 0;

        return Math.Clamp(StringInfo.ParseCombiningCharacters(text).Length, 1, 128);
    }

    private static int CountRunes(string text)
    {
        var count = 0;
        foreach (var _ in text.EnumerateRunes())
            count++;

        return count;
    }

    private static void SendKeyPress(ushort virtualKey, int pressCount)
    {
        if (pressCount <= 0)
            return;

        var inputs = new Input[Math.Clamp(pressCount, 1, 128) * 2];
        for (var i = 0; i < inputs.Length; i += 2)
        {
            inputs[i] = Input.Keyboard(virtualKey, 0);
            inputs[i + 1] = Input.Keyboard(virtualKey, KeyEventKeyUp);
        }

        _ = SendInput((uint)inputs.Length, ref inputs[0], Marshal.SizeOf<Input>());
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint Type;
        public InputUnion Union;

        public static Input Keyboard(ushort virtualKey, uint flags)
            => new()
            {
                Type = InputKeyboard,
                Union = new InputUnion
                {
                    KeyboardInput = new KeyboardInput
                    {
                        VirtualKey = virtualKey,
                        ScanCode = 0,
                        Flags = flags,
                        Time = 0,
                        ExtraInfo = UIntPtr.Zero,
                    },
                },
            };
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    private struct InputUnion
    {
        [FieldOffset(0)]
        public KeyboardInput KeyboardInput;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput
    {
        public ushort VirtualKey;
        public ushort ScanCode;
        public uint Flags;
        public uint Time;
        public UIntPtr ExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint numberOfInputs, ref Input inputs, int sizeOfInputStructure);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    /// <summary>
    /// Runs from the window procedure on WM_KEYDOWN Return, before the game reads the
    /// input buffer. Full-line SetText is only acceptable here because the line is
    /// about to leave.
    /// </summary>
    private unsafe void ConvertBeforeSend()
    {
        // Stays ahead of Refresh: that call recomputes composition state, so moving this
        // check below it would change when that state is recomputed. ShouldConvert cannot
        // be asked yet — there is no line to ask about until the buffer has been read.
        if (!this.gate.IsArmed || this.writing)
            return;

        this.imeTracker.Refresh(null);
        if (this.imeTracker.IsComposing)
            return;

        // An IME confirm-Enter is identified by an in-flight commit, not by a wall
        // clock. The previous blanket 250 ms suppression also disabled this safety net
        // for anyone who pressed Enter promptly — exactly when it is needed, because a
        // phrase rule spanning two IME commits can only be applied to the whole line.
        if (this.imeTracker.HasPendingInsert)
            return;

        try
        {
            if (!this.accessor.TryGetActiveInput(out var input))
                return;

            var text = ChatInputAccessor.GetText(input);
            if (string.IsNullOrEmpty(text))
                return;

            if (!this.gate.ShouldConvert(text))
                return;

            // No '=' noise check here: composition has ended, so RawString is the real
            // buffer. The old density heuristic rejected ordinary text such as "a==b",
            // and rejected hardest exactly when a line was mostly missing glyphs.
            var converted = KanjiConverter.ConvertChatLine(
                text,
                this.phraseService,
                this.mapService.Active);
            if (string.Equals(converted, text, StringComparison.Ordinal))
                return;

            this.writing = true;
            try
            {
                input->SetText(converted);
            }
            finally
            {
                this.writing = false;
            }
        }
        catch (Exception ex)
        {
            this.log.Debug(ex, "NoMoreEquals: pre-send conversion skipped");
            this.writing = false;
        }
    }
}
