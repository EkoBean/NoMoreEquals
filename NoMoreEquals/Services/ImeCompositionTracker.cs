using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace NoMoreEquals.Services;

/// <summary>
/// Tracks IME composition via window subclass + IMM.
/// Exposes live COMPSTR and committed RESULTSTR without relying on noisy game RawString.
/// </summary>
internal sealed class ImeCompositionTracker : IDisposable
{
    private const uint WmImeStartComposition = 0x010D;
    private const uint WmImeEndComposition = 0x010E;
    private const uint WmImeComposition = 0x010F;
    private const uint WmKeyDown = 0x0100;
    private const uint WmSysKeyDown = 0x0104;
    private const nint VkReturn = 0x0D;
    private const uint GcsCompStr = 0x0008;
    private const uint GcsResultStr = 0x0800;
    private const nuint SubclassId = 0x4E4D4531; // 'NME1'

    private readonly SubclassProc subclassProc;
    private readonly object gate = new();
    private IntPtr hwnd;
    private bool subclassInstalled;
    private bool wndProcComposing;
    private bool wasComposing;
    private long compositionEndedTimestamp;
    private int cachedPid;
    private string pendingResults = string.Empty;
    private string pendingInsertConverted = string.Empty;
    private string pendingInsertOriginal = string.Empty;

    public ImeCompositionTracker()
    {
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
    /// Updated each framework tick from ChatInputWatcher.
    /// </summary>
    public volatile bool AcceptChatIme;

    public bool IsComposing { get; private set; }

    /// <summary>Current in-progress composition (Bopomofo / reading), from IMM only.</summary>
    public string Composition { get; private set; } = string.Empty;

    public TimeSpan TimeSinceCompositionEnded
    {
        get
        {
            if (this.compositionEndedTimestamp == 0)
                return Timeout.InfiniteTimeSpan;

            return Stopwatch.GetElapsedTime(this.compositionEndedTimestamp);
        }
    }

    public void Refresh(string? inputTextHint = null)
    {
        this.EnsureSubclass();

        if (!this.AcceptChatIme)
        {
            if (this.wasComposing)
                this.compositionEndedTimestamp = Stopwatch.GetTimestamp();

            this.wasComposing = false;
            this.wndProcComposing = false;
            this.IsComposing = false;
            this.Composition = string.Empty;
            return;
        }

        var immComp = this.PollImmComposition();
        var heuristic = HeuristicBopomofo(inputTextHint);

        var composing = this.wndProcComposing || !string.IsNullOrEmpty(immComp) || heuristic.Length > 0;
        if (this.wasComposing && !composing)
            this.compositionEndedTimestamp = Stopwatch.GetTimestamp();

        this.wasComposing = composing;
        this.IsComposing = composing;
        this.Composition = !string.IsNullOrEmpty(immComp)
            ? immComp
            : heuristic;
    }

    /// <summary>Peek RESULTSTR chunks captured since last consume (does not clear).</summary>
    public string PeekCommittedResults()
    {
        lock (this.gate)
            return this.pendingResults;
    }

    /// <summary>Drain RESULTSTR chunks captured from WM_IME_COMPOSITION since last call.</summary>
    public string ConsumeCommittedResults()
    {
        lock (this.gate)
        {
            var result = this.pendingResults;
            this.pendingResults = string.Empty;
            return result;
        }
    }

    /// <summary>
    /// Converted text that should be InsertText'd because we suppressed Imm's RESULTSTR.
    /// </summary>
    public bool TryConsumePendingInsert(out string converted, out string originalChinese)
    {
        lock (this.gate)
        {
            converted = this.pendingInsertConverted;
            originalChinese = this.pendingInsertOriginal;
            this.pendingInsertConverted = string.Empty;
            this.pendingInsertOriginal = string.Empty;
            return !string.IsNullOrEmpty(converted);
        }
    }

    public bool HasPendingInsert
    {
        get
        {
            lock (this.gate)
                return this.pendingInsertConverted.Length > 0;
        }
    }

    /// <summary>Drop queued Imm RESULTSTR peek state used by the preview shadow.</summary>
    public void ClearPeekResults()
    {
        lock (this.gate)
            this.pendingResults = string.Empty;
    }

    /// <summary>Drop queued RESULTSTR / InsertText state (e.g. chat buffer was cleared for real).</summary>
    public void ClearPendingCommits()
    {
        lock (this.gate)
        {
            this.pendingResults = string.Empty;
            this.pendingInsertConverted = string.Empty;
            this.pendingInsertOriginal = string.Empty;
        }
    }

    public void Dispose() => this.RemoveSubclass();

    private void QueueInsert(string original, string converted)
    {
        lock (this.gate)
        {
            this.pendingInsertOriginal += original;
            this.pendingInsertConverted += converted;
        }
    }

    private void AppendResult(string result)
    {
        if (string.IsNullOrEmpty(result))
            return;

        lock (this.gate)
            this.pendingResults += result;
    }

    private void EnsureSubclass()
    {
        var gameHwnd = this.FindGameWindowHandle();
        if (gameHwnd == IntPtr.Zero)
            return;

        if (this.subclassInstalled && this.hwnd == gameHwnd)
            return;

        this.RemoveSubclass();
        this.hwnd = gameHwnd;
        this.subclassInstalled = SetWindowSubclass(this.hwnd, this.subclassProc, SubclassId, IntPtr.Zero);
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
            case WmKeyDown:
            case WmSysKeyDown:
                if (wParam == VkReturn && !this.wndProcComposing && this.AcceptChatIme)
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
                var flags = unchecked((uint)lParam.ToInt64());
                if ((flags & GcsCompStr) != 0 && this.AcceptChatIme)
                    this.wndProcComposing = true;

                if ((flags & GcsResultStr) != 0 && this.AcceptChatIme)
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
                                    lParam = unchecked((IntPtr)(nint)flags);
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
                this.compositionEndedTimestamp = Stopwatch.GetTimestamp();
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

    private static string HeuristicBopomofo(string? inputText)
    {
        if (string.IsNullOrEmpty(inputText))
            return string.Empty;

        // Only use heuristic when the trailing run is purely Bopomofo — never treat '=' noise as composition.
        var end = inputText.Length;
        var start = end;
        while (start > 0 && IsBopomofo(inputText[start - 1]))
            start--;

        if (start == end)
            return string.Empty;

        // Reject if the hint string is dominated by '=' (game RawString corruption).
        if (IsMostlyEqualsNoise(inputText))
            return string.Empty;

        return inputText[start..];
    }

    internal static bool IsMostlyEqualsNoise(string s)
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

    private static bool IsBopomofo(char c)
        => c is (>= '\u3105' and <= '\u312F')
            or (>= '\u31A0' and <= '\u31BF')
            or '\u02C7' or '\u02C9' or '\u02CA' or '\u02CB' or '\u02D9';

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
