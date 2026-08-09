using System;
using System.IO;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.ManagedFontAtlas;
using Dalamud.Plugin;

namespace NoMoreEquals.Services;

/// <summary>
/// Live preview above ChatLog. Built from an Imm-backed committed shadow + live COMPSTR,
/// never from noisy in-IME RawString. Rendered with a CJK system font so text stays readable.
/// <para>
/// Strictly a view. It owns <see cref="committedShadow"/> and the preview channel
/// (<see cref="ImeCompositionTracker.ClearPreviewResults"/>) and nothing else: it does
/// not set the IME gate, does not drive <see cref="ImeCompositionTracker.Refresh"/>, and
/// must never touch the insert channel. It previously did all three, which is why a
/// display checkbox could change — or completely disable — input behaviour.
/// </para>
/// </summary>
internal sealed class ChatPreviewOverlay : IDisposable
{
    private const float VerticalGap = 4f;

    private readonly Configuration configuration;
    private readonly ConversionGate gate;
    private readonly KanjiMapService mapService;
    private readonly PhraseReplacementService phraseService;
    private readonly ChatInputAccessor accessor;
    private readonly ImeCompositionTracker imeTracker;
    private readonly IFontHandle previewFont;

    /// <summary>Committed Unicode text we trust (not game RawString during IME).</summary>
    private string committedShadow = string.Empty;

    /// <summary>How much of PeekCommittedResults we have already appended to the shadow.</summary>
    private string lastSeenPendingResults = string.Empty;

    public ChatPreviewOverlay(
        Configuration configuration,
        ConversionGate gate,
        KanjiMapService mapService,
        PhraseReplacementService phraseService,
        ChatInputAccessor accessor,
        ImeCompositionTracker imeTracker,
        IDalamudPluginInterface pluginInterface)
    {
        this.configuration = configuration;
        this.gate = gate;
        this.mapService = mapService;
        this.phraseService = phraseService;
        this.accessor = accessor;
        this.imeTracker = imeTracker;
        this.previewFont = CreateCjkFontHandle(pluginInterface);
    }

    public void Dispose() => this.previewFont.Dispose();

    public unsafe void Draw()
    {
        if (!this.gate.IsArmed || !this.configuration.ShowChatPreviewOverlay)
            return;

        try
        {
            if (!this.accessor.TryGetActiveInput(out var input))
            {
                this.ResetShadow();
                return;
            }

            if (!ChatInputAccessor.TryGetScreenRect(input, out var x, out var y, out var width, out _))
                return;

            var raw = ChatInputAccessor.GetText(input);
            this.UpdateShadow(raw);

            // Raw, not the shadow. The '=' noise that makes raw untrustworthy elsewhere
            // cannot reach the part this question depends on — the leading "/token " is
            // ASCII the game renders fine — whereas the shadow is blanked outright once
            // IsBufferBlank trips, and ChatLineScope reads an empty line as convertible.
            // That combination drew a preview over "/ac" plus four CJK characters.
            //
            // Deliberately no ResetShadow() here. The shadow has to keep tracking the
            // buffer while the preview is withheld, so that deleting the "/gearset "
            // brings the preview back on the same frame instead of flickering.
            if (!this.gate.ShouldConvert(raw))
                return;

            var preview = this.BuildPreview();
            if (string.IsNullOrEmpty(preview))
                return;

            using (this.previewFont.Push())
            {
                var previewHeight = ImGui.GetTextLineHeight() + (ImGui.GetStyle().WindowPadding.Y * 2f);
                var pos = new Vector2(x, y - previewHeight - VerticalGap);
                var size = new Vector2(Math.Max(width, 120f), previewHeight);

                ImGui.SetNextWindowPos(pos, ImGuiCond.Always);
                ImGui.SetNextWindowSize(size, ImGuiCond.Always);
                const ImGuiWindowFlags flags =
                    ImGuiWindowFlags.NoTitleBar
                    | ImGuiWindowFlags.NoResize
                    | ImGuiWindowFlags.NoMove
                    | ImGuiWindowFlags.NoScrollbar
                    | ImGuiWindowFlags.NoScrollWithMouse
                    | ImGuiWindowFlags.NoCollapse
                    | ImGuiWindowFlags.NoSavedSettings
                    | ImGuiWindowFlags.NoFocusOnAppearing
                    | ImGuiWindowFlags.NoNav
                    | ImGuiWindowFlags.NoInputs
                    | ImGuiWindowFlags.NoBackground;

                if (!ImGui.Begin("##NoMoreEqualsChatPreview", flags))
                {
                    ImGui.End();
                    return;
                }

                var dl = ImGui.GetWindowDrawList();
                var min = ImGui.GetWindowPos();
                var max = min + ImGui.GetWindowSize();
                dl.AddRectFilled(min, max, ImGui.GetColorU32(new Vector4(0.05f, 0.05f, 0.08f, 0.90f)), 4f);
                dl.AddRect(min, max, ImGui.GetColorU32(new Vector4(0.55f, 0.75f, 0.95f, 0.55f)), 4f);

                ImGui.SetCursorPos(ImGui.GetStyle().WindowPadding);
                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.95f, 0.97f, 1f, 1f));
                ImGui.TextUnformatted(preview);
                ImGui.PopStyleColor();
                ImGui.End();
            }
        }
        catch
        {
            // Preview is best-effort.
        }
    }

    private void UpdateShadow(string raw)
    {
        // ChatLog still "active" but keystrokes belong to ImGui — freeze on chat
        // RawString only. Composition state itself is refreshed once per framework
        // tick by ChatInputWatcher; reading it a phase later is at most one frame stale.
        if (!this.imeTracker.AcceptChatIme)
        {
            this.imeTracker.ClearPreviewResults();
            this.lastSeenPendingResults = string.Empty;
            this.committedShadow = IsBufferBlank(raw) ? string.Empty : raw;
            return;
        }

        var chatEmpty = IsBufferBlank(raw);

        // Lifecycle: an empty chat box destroys the preview strings. Anything still
        // queued for InsertText is ChatInputWatcher's business, not ours.
        if (chatEmpty)
        {
            this.committedShadow = string.Empty;
            this.lastSeenPendingResults = string.Empty;
            if (!this.imeTracker.IsComposing)
                this.imeTracker.ClearPreviewResults();

            return;
        }

        // Peek only — ChatInputWatcher owns the separate insert channel.
        var pending = this.imeTracker.PeekCommittedResults();
        if (string.IsNullOrEmpty(pending))
        {
            this.lastSeenPendingResults = string.Empty;
        }
        else if (pending.StartsWith(this.lastSeenPendingResults, StringComparison.Ordinal)
                 && pending.Length > this.lastSeenPendingResults.Length)
        {
            this.committedShadow += pending[this.lastSeenPendingResults.Length..];
            this.lastSeenPendingResults = pending;
        }
        else if (!string.Equals(pending, this.lastSeenPendingResults, StringComparison.Ordinal))
        {
            this.committedShadow += pending;
            this.lastSeenPendingResults = pending;
        }

        if (this.imeTracker.IsComposing)
        {
            // While composing: never trust RawString (it is where the '=' noise comes from).
            return;
        }

        // Idle: sync from RawString when it looks like real text.
        this.committedShadow = raw;
    }

    private void ResetShadow()
    {
        this.committedShadow = string.Empty;
        this.lastSeenPendingResults = string.Empty;
        this.imeTracker.ClearPreviewResults();
    }

    /// <summary>
    /// A buffer we cannot read as text: genuinely empty, or the '=' soup the game puts
    /// in RawString for glyphs AXIS cannot render.
    /// </summary>
    private static bool IsBufferBlank(string raw)
        => string.IsNullOrEmpty(raw) || ChatBufferHeuristics.IsMostlyEqualsNoise(raw);

    private string BuildPreview()
    {
        // ConvertChatLine, not ConvertAll: a "/p ..." line shows its command token
        // verbatim and its message body converted, matching what will actually be sent.
        var convertedCommitted = KanjiConverter.ConvertChatLine(
            this.committedShadow,
            this.phraseService,
            this.mapService.Active);

        // Only attach live COMPSTR when IME is actually targeting ChatLog.
        var composition = this.imeTracker.AcceptChatIme && this.imeTracker.IsComposing
            ? this.imeTracker.Composition
            : string.Empty;
        if (string.IsNullOrEmpty(composition))
            return convertedCommitted;

        return string.Concat(convertedCommitted, composition);
    }

    private static IFontHandle CreateCjkFontHandle(IDalamudPluginInterface pluginInterface)
    {
        return pluginInterface.UiBuilder.FontAtlas.NewDelegateFontHandle(e =>
        {
            e.OnPreBuild(tk =>
            {
                var fonts = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
                var candidates = new[]
                {
                    Path.Combine(fonts, "msjh.ttc"),
                    Path.Combine(fonts, "msjhbd.ttc"),
                    Path.Combine(fonts, "msyh.ttc"),
                    Path.Combine(fonts, "mingliu.ttc"),
                    Path.Combine(fonts, "NotoSansCJK-Regular.ttc"),
                };

                foreach (var path in candidates)
                {
                    if (!File.Exists(path))
                        continue;

                    try
                    {
                        // msjh / msyh typically expose the needed CJK face at index 0.
                        var config = new SafeFontConfig { SizePx = 18 };
                        tk.Font = tk.AddFontFromFile(path, config);
                        return;
                    }
                    catch
                    {
                        // try next
                    }
                }

                // Fallback: Dalamud default (still better than AXIS for preview readability).
                tk.AddDalamudDefaultFont(18);
            });
        });
    }
}
