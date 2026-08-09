using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace NoMoreEquals.Services;

/// <summary>
/// Tracks IME composition via window subclass + IMM.
/// Exposes live COMPSTR and committed RESULTSTR without relying on noisy game RawString.
/// <para>
/// Two independent channels hang off this type, and they have different owners:
/// </para>
/// <list type="bullet">
/// <item>
/// <b>Preview channel</b> (<see cref="PeekCommittedResults"/> /
/// <see cref="ClearPreviewResults"/>) — every RESULTSTR we saw, in Chinese form.
/// Owned by <see cref="ChatPreviewOverlay"/>. Purely cosmetic.
/// </item>
/// <item>
/// <b>Insert channel</b> (<see cref="HasPendingInsert"/> /
/// <see cref="TryConsumePendingInsert"/> / <see cref="ClearPendingInsert"/>) —
/// converted text whose original RESULTSTR we stripped from the game. Owned by
/// <see cref="ChatInputWatcher"/>. Once queued, this is the <i>only</i> copy of
/// what the player typed, so dropping it loses characters.
/// </item>
/// </list>
/// </summary>
internal sealed class ImeCompositionTracker : IDisposable
{
    private const uint WmImeStartComposition = 0x010D;
    private const uint WmImeEndComposition = 0x010E;
    private const uint WmImeComposition = 0x010F;
    private const uint WmKeyDown = 0x0100;
    private const nint VkReturn = 0x0D;
    private const long KeyRepeatFlag = 1L << 30;
    private const uint GcsCompStr = 0x0008;
    private const uint GcsResultStr = 0x0800;
    private const nuint SubclassId = 0x4E4D4531; // 'NME1'

    private readonly SubclassProc subclassProc;
    private readonly ChatImeGate gate;
    private readonly object stateLock = new();
    private IntPtr hwnd;
    private bool subclassInstalled;
    private bool wndProcComposing;
    private bool wasComposing;
    private long pendingInsertQueuedTimestamp;
    private int cachedPid;
    private string pendingResults = string.Empty;
    private string pendingInsertConverted = string.Empty;
    private string pendingInsertOriginal = string.Empty;

    public ImeCompositionTracker(ChatImeGate gate)
    {
        this.gate = gate;
        this.subclassProc = this.OnSubclass;
    }

    /// <summary>
    /// Invoked on WM_KEYDOWN Return <em>before</em> the game handles it, when IME is not composing.
    /// Used to SetText(converted) so the outgoing chat line is already converted.
    /// </summary>
    public Action? BeforeReturnKeyDown { get; set; }

    /// <summary>
    /// Optional rewrite of an IME RESULTSTR before the game commits it.
    /// Return null (or the same string) to let Imm deliver the original result.
    /// Return a different string to suppress GCS_RESULTSTR and queue InsertText instead.
    /// </summary>
    public Func<string, string?>? RewriteCommittedResult { get; set; }

    /// <summary>
    /// When false, Imm RESULTSTR/COMPSTR are ignored for chat conversion + preview
    /// (e.g. focus is in a Dalamud ImGui text field while ChatLog still reports IsActive).
    /// Owned by <see cref="ChatImeGate"/>; this type only reads it.
    /// </summary>
    public bool AcceptChatIme => this.gate.Accept;

    /// <summary>Game window we are subclassed onto, or zero. Used to sanity-check SendInput.</summary>
    public IntPtr WindowHandle => this.hwnd;

    public bool IsComposing { get; private set; }

    /// <summary>Current in-progress composition (Bopomofo / reading), from IMM only.</summary>
    public string Composition { get; private set; } = string.Empty;

    /// <summary>Age of the oldest un-flushed entry in the insert channel.</summary>
    public TimeSpan TimeSincePendingInsertQueued
    {
        get
        {
            lock (this.stateLock)
                return Elapsed(this.pendingInsertQueuedTimestamp);
        }
    }

    public bool HasPendingInsert
    {
        get
        {
            lock (this.stateLock)
                return this.pendingInsertConverted.Length > 0;
        }
    }

    /// <summary>
    /// Install or remove the window subclass. Must be driven unconditionally once per
    /// frame: everything else in the pipeline (RESULTSTR rewriting, the pre-send
    /// conversion, composition state) only happens while the subclass is installed.
    /// </summary>
    public void SyncSubclass(bool wanted)
    {
        if (!wanted)
        {
            this.RemoveSubclass();
            return;
        }

        var gameHwnd = this.FindGameWindowHandle();
        if (gameHwnd == IntPtr.Zero)
            return;

        if (this.subclassInstalled && this.hwnd == gameHwnd)
            return;

        this.RemoveSubclass();
        this.hwnd = gameHwnd;
        this.subclassInstalled = SetWindowSubclass(this.hwnd, this.subclassProc, SubclassId, IntPtr.Zero);
    }

    /// <summary>
    /// Recompute composition state from IMM. Called once per framework tick by
    /// <see cref="ChatInputWatcher"/>, which is the only owner of this state.
    /// </summary>
    public void Refresh(string? inputTextHint = null)
    {
        if (!this.AcceptChatIme)
        {
            this.wasComposing = false;
            this.wndProcComposing = false;
            this.IsComposing = false;
            this.Composition = string.Empty;
            return;
        }

        var immComp = this.PollImmComposition();
        var heuristic = ChatBufferHeuristics.TrailingBopomofo(inputTextHint);

        var composing = this.wndProcComposing || !string.IsNullOrEmpty(immComp) || heuristic.Length > 0;
        this.wasComposing = composing;
        this.IsComposing = composing;
        this.Composition = !string.IsNullOrEmpty(immComp)
            ? immComp
            : heuristic;
    }

    /// <summary>
    /// Preview channel: RESULTSTR chunks captured since the last
    /// <see cref="ClearPreviewResults"/>. Does not clear.
    /// </summary>
    public string PeekCommittedResults()
    {
        lock (this.stateLock)
            return this.pendingResults;
    }

    /// <summary>Preview channel reset. Cosmetic only — never loses player text.</summary>
    public void ClearPreviewResults()
    {
        lock (this.stateLock)
            this.pendingResults = string.Empty;
    }

    /// <summary>
    /// Insert channel: take the converted text that must be InsertText'd because we
    /// suppressed Imm's RESULTSTR. <paramref name="originalChinese"/> is what the game
    /// would have received, so the caller can fall back to it without losing input.
    /// </summary>
    public bool TryConsumePendingInsert(out string converted, out string originalChinese)
    {
        lock (this.stateLock)
        {
            converted = this.pendingInsertConverted;
            originalChinese = this.pendingInsertOriginal;
            this.pendingInsertConverted = string.Empty;
            this.pendingInsertOriginal = string.Empty;
            this.pendingInsertQueuedTimestamp = 0;
            return !string.IsNullOrEmpty(converted);
        }
    }

    /// <summary>
    /// Insert channel reset. <b>Discards player input</b> — the original RESULTSTR was
    /// already stripped from the game, so only call this once the text is provably
    /// undeliverable. <see cref="ChatInputWatcher.DropPendingInsert"/> is the only caller.
    /// </summary>
    public void ClearPendingInsert()
    {
        lock (this.stateLock)
        {
            this.pendingInsertConverted = string.Empty;
            this.pendingInsertOriginal = string.Empty;
            this.pendingInsertQueuedTimestamp = 0;
        }
    }

    public void Dispose() => this.RemoveSubclass();

    private static TimeSpan Elapsed(long timestamp)
        => timestamp == 0 ? Timeout.InfiniteTimeSpan : Stopwatch.GetElapsedTime(timestamp);

    private void QueueInsert(string original, string converted)
    {
        lock (this.stateLock)
        {
            this.pendingInsertOriginal += original;
            this.pendingInsertConverted += converted;
            if (this.pendingInsertQueuedTimestamp == 0)
                this.pendingInsertQueuedTimestamp = Stopwatch.GetTimestamp();
        }
    }

    private void AppendResult(string result)
    {
        if (string.IsNullOrEmpty(result))
            return;

        lock (this.stateLock)
            this.pendingResults += result;
    }

    private void RemoveSubclass()
    {
        if (!this.subclassInstalled || this.hwnd == IntPtr.Zero)
            return;

        _ = RemoveWindowSubclass(this.hwnd, this.subclassProc, SubclassId);
        this.subclassInstalled = false;
        this.hwnd = IntPtr.Zero;
        this.wndProcComposing = false;
    }

    private IntPtr OnSubclass(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam, nuint id, IntPtr data)
    {
        switch (msg)
        {
            // WM_SYSKEYDOWN is deliberately not handled: Alt+Enter is the game's
            // fullscreen toggle, and treating it as a send used to SetText the
            // player's half-finished line out from under them.
            case WmKeyDown:
                if (wParam == VkReturn
                    && (lParam.ToInt64() & KeyRepeatFlag) == 0
                    && !this.wndProcComposing
                    && this.AcceptChatIme)
                {
                    // IME usually eats Return for candidate confirm, so this path is mostly "send chat".
                    // Run conversion before the game reads the input buffer.
                    try
                    {
                        this.BeforeReturnKeyDown?.Invoke();
                    }
                    catch
                    {
                        // never break input
                    }
                }

                break;

            case WmImeStartComposition:
                if (this.AcceptChatIme)
                    this.wndProcComposing = true;
                break;

            case WmImeComposition:
            {
                if (!this.AcceptChatIme)
                    break;

                var flags = unchecked((uint)lParam.ToInt64());
                if ((flags & GcsCompStr) != 0)
                    this.wndProcComposing = true;

                if ((flags & GcsResultStr) != 0)
                {
                    var himc = ImmGetContext(hWnd);
                    if (himc != IntPtr.Zero)
                    {
                        try
                        {
                            var result = GetCompositionString(himc, GcsResultStr);
                            if (!string.IsNullOrEmpty(result))
                            {
                                // Preview still tracks the logical commit (Chinese form).
                                this.AppendResult(result);

                                string? rewritten = null;
                                try
                                {
                                    rewritten = this.RewriteCommittedResult?.Invoke(result);
                                }
                                catch
                                {
                                    rewritten = null;
                                }

                                if (!string.IsNullOrEmpty(rewritten)
                                    && !string.Equals(rewritten, result, StringComparison.Ordinal))
                                {
                                    // Stop the game from receiving Chinese RESULTSTR; we will
                                    // InsertText(converted) ourselves so prior text is untouched.
                                    this.QueueInsert(result, rewritten);
                                    flags &= ~GcsResultStr;

                                    // Preserve the high half of lParam; only the flag word is ours.
                                    var high = lParam.ToInt64() & unchecked((long)0xFFFFFFFF00000000);
                                    lParam = (IntPtr)(high | flags);
                                }
                            }
                        }
                        finally
                        {
                            _ = ImmReleaseContext(hWnd, himc);
                        }
                    }
                }

                break;
            }

            case WmImeEndComposition:
                this.wndProcComposing = false;
                break;
        }

        return DefSubclassProc(hWnd, msg, wParam, lParam);
    }

    private string PollImmComposition()
    {
        if (this.hwnd == IntPtr.Zero)
            this.hwnd = this.FindGameWindowHandle();

        if (this.hwnd == IntPtr.Zero)
            return string.Empty;

        var himc = ImmGetContext(this.hwnd);
        if (himc == IntPtr.Zero)
            return string.Empty;

        try
        {
            return GetCompositionString(himc, GcsCompStr);
        }
        finally
        {
            _ = ImmReleaseContext(this.hwnd, himc);
        }
    }

    private static string GetCompositionString(IntPtr himc, uint index)
    {
        var bytes = ImmGetCompositionStringW(himc, index, null, 0);
        if (bytes <= 0)
            return string.Empty;

        var buffer = new char[bytes / 2];
        var written = ImmGetCompositionStringW(himc, index, buffer, (uint)bytes);
        return written <= 0 ? string.Empty : new string(buffer, 0, written / 2);
    }

    private IntPtr FindGameWindowHandle()
    {
        var pid = Environment.ProcessId;
        if (this.hwnd != IntPtr.Zero && this.cachedPid == pid && IsWindow(this.hwnd))
            return this.hwnd;

        this.cachedPid = pid;
        var found = IntPtr.Zero;
        EnumWindows((h, lParam) =>
        {
            _ = lParam;
            GetWindowThreadProcessId(h, out var windowPid);
            if (windowPid != (uint)pid)
                return true;

            var className = new StringBuilder(64);
            if (GetClassName(h, className, className.Capacity) <= 0)
                return true;

            if (!string.Equals(className.ToString(), "FFXIVGAME", StringComparison.Ordinal))
                return true;

            if (!IsWindowVisible(h))
                return true;

            found = h;
            return false;
        }, IntPtr.Zero);

        return found;
    }

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    private delegate IntPtr SubclassProc(
        IntPtr hWnd,
        uint uMsg,
        IntPtr wParam,
        IntPtr lParam,
        nuint uIdSubclass,
        IntPtr dwRefData);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    private static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("imm32.dll")]
    private static extern IntPtr ImmGetContext(IntPtr hWnd);

    [DllImport("imm32.dll")]
    private static extern bool ImmReleaseContext(IntPtr hWnd, IntPtr hImc);

    [DllImport("imm32.dll", CharSet = CharSet.Unicode)]
    private static extern int ImmGetCompositionStringW(IntPtr hImc, uint dwIndex, char[]? lpBuf, uint dwBufLen);

    [DllImport("comctl32.dll", SetLastError = true)]
    private static extern bool SetWindowSubclass(
        IntPtr hWnd,
        SubclassProc pfnSubclass,
        nuint uIdSubclass,
        IntPtr dwRefData);

    [DllImport("comctl32.dll", SetLastError = true)]
    private static extern bool RemoveWindowSubclass(
        IntPtr hWnd,
        SubclassProc pfnSubclass,
        nuint uIdSubclass);

    [DllImport("comctl32.dll")]
    private static extern IntPtr DefSubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);
}
