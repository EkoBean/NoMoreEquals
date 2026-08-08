using System;
using System.IO;
using System.Numerics;
using System.Threading;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.ManagedFontAtlas;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace NoMoreEquals.Services;

/// <summary>
/// Live preview above ChatLog. Built from an Imm-backed committed shadow + live COMPSTR,
/// never from noisy in-IME RawString. Rendered with a CJK system font so text stays readable.
/// </summary>
internal sealed class ChatPreviewOverlay : IDisposable
{
    private const string ChatLogAddonName = "ChatLog";
    private const float VerticalGap = 4f;

    private readonly Configuration configuration;
    private readonly KanjiMapService mapService;
    private readonly PhraseReplacementService phraseService;
    private readonly IGameGui gameGui;
    private readonly ImeCompositionTracker imeTracker;
    private readonly IFontHandle previewFont;

    /// <summary>Committed Unicode text we trust (not game RawString during IME).</summary>
    private string committedShadow = string.Empty;

    /// <summary>How much of PeekCommittedResults we have already appended to the shadow.</summary>
    private string lastSeenPendingResults = string.Empty;

    public ChatPreviewOverlay(
        Configuration configuration,
        KanjiMapService mapService,
        PhraseReplacementService phraseService,
        IGameGui gameGui,
        ImeCompositionTracker imeTracker,
        IDalamudPluginInterface pluginInterface)
    {
        this.configuration = configuration;
        this.mapService = mapService;
        this.phraseService = phraseService;
        this.gameGui = gameGui;
        this.imeTracker = imeTracker;
        this.previewFont = CreateCjkFontHandle(pluginInterface);
    }

    public void Dispose() => this.previewFont.Dispose();

    public unsafe void Draw()
    {
        if (!this.configuration.Enabled || !this.configuration.ShowChatPreviewOverlay)
            return;

        if (this.mapService.Active.Count == 0 && this.phraseService.EnabledCount == 0)
            return;

        try
        {
            var chatUnit = this.gameGui.GetAddonByName(ChatLogAddonName);
            if (chatUnit.IsNull || !chatUnit.IsReady || !chatUnit.IsVisible)
                return;

            var chatLog = this.gameGui.GetAddonByName<AddonChatLog>(ChatLogAddonName);
            if (chatLog == null || chatLog->TextInput == null)
                return;

            var input = chatLog->TextInput;
            if (!input->Enabled || !input->IsActive)
            {
                this.imeTracker.AcceptChatIme = false;
                this.ClearPreviewLifecycle();
                return;
            }

            // Prefer UiBuilder.Draw timing: WantTextInput is authoritative here.
            this.imeTracker.AcceptChatIme = !ImGui.GetIO().WantTextInput;

            if (!TryGetInputScreenRect(input, out var x, out var y, out var width, out _))
                return;

            var raw = GetTextInputString(input);
            // Slash commands are never converted; hide preview for those lines.
            if (!string.IsNullOrEmpty(raw) && raw.StartsWith('/'))
            {
                this.ClearPreviewLifecycle();
                return;
            }

            this.UpdateShadow(raw);
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
        // Gate first so Imm composition from Dalamud text fields is not treated as chat IME.
        this.imeTracker.Refresh(
            this.imeTracker.AcceptChatIme && !ImeCompositionTracker.IsMostlyEqualsNoise(raw)
                ? raw
                : null);

        // ChatLog still "active" but keystrokes belong to ImGui — freeze on chat RawString only.
        if (!this.imeTracker.AcceptChatIme)
        {
            this.imeTracker.ClearPeekResults();
            if (string.IsNullOrEmpty(raw) || ImeCompositionTracker.IsMostlyEqualsNoise(raw))
            {
                this.committedShadow = string.Empty;
                this.lastSeenPendingResults = string.Empty;
            }
            else
            {
                this.committedShadow = raw;
                this.lastSeenPendingResults = string.Empty;
            }

            return;
        }

        var chatEmpty = string.IsNullOrEmpty(raw)
                        || ImeCompositionTracker.IsMostlyEqualsNoise(raw);

        // Lifecycle: empty chat box destroys preview strings.
        // Keep a brief pending InsertText alive (first glyph on a blank line).
        if (chatEmpty && !this.imeTracker.IsComposing)
        {
            this.committedShadow = string.Empty;
            this.lastSeenPendingResults = string.Empty;
            this.imeTracker.ClearPeekResults();

            var sinceEnd = this.imeTracker.TimeSinceCompositionEnded;
            var insertStale = !this.imeTracker.HasPendingInsert
                              || sinceEnd == Timeout.InfiniteTimeSpan
                              || sinceEnd > TimeSpan.FromMilliseconds(500);
            if (insertStale)
                this.imeTracker.ClearPendingCommits();

            return;
        }

        if (chatEmpty && this.imeTracker.IsComposing)
        {
            // First syllable on an empty line: show COMPSTR only, no stale committed text.
            this.committedShadow = string.Empty;
            this.lastSeenPendingResults = string.Empty;
            return;
        }

        // Peek only — ChatInputWatcher may Consume / InsertText separately.
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

    private void ClearPreviewLifecycle()
    {
        this.committedShadow = string.Empty;
        this.lastSeenPendingResults = string.Empty;
        this.imeTracker.ClearPendingCommits();
    }

    private string BuildPreview()
    {
        var convertedCommitted = KanjiConverter.ConvertAll(
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

    private static unsafe bool TryGetInputScreenRect(
        AtkComponentTextInput* input,
        out float x,
        out float y,
        out float width,
        out float height)
    {
        x = y = width = height = 0;
        var owner = input->AtkComponentInputBase.OwnerNode;
        if (owner == null)
            return false;

        var node = (AtkResNode*)owner;
        x = node->ScreenX;
        y = node->ScreenY;
        width = node->Width * Math.Max(node->ScaleX, 0.01f);
        height = node->Height * Math.Max(node->ScaleY, 0.01f);
        return width > 1f && height > 1f;
    }

    private static unsafe string GetTextInputString(AtkComponentTextInput* input)
    {
        if (input == null)
            return string.Empty;

        var raw = input->AtkComponentInputBase.RawString.ToString();
        if (!string.IsNullOrEmpty(raw))
            return raw;

        return input->AtkComponentInputBase.EvaluatedString.ToString();
    }
}
