using System;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading;
using Dalamud.Bindings.ImGui;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;
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
/// </summary>
internal sealed class ChatInputWatcher : IDisposable
{
    private const string ChatLogAddonName = "ChatLog";
    private const ushort VirtualKeyRight = 0x27;
    private const uint InputKeyboard = 1;
    private const uint KeyEventKeyUp = 0x0002;

    /// <summary>Let ENDCOMPOSITION settle before InsertText.</summary>
    private static readonly TimeSpan PostCommitDelay = TimeSpan.FromMilliseconds(40);

    /// <summary>IME confirm Enter must not be treated as chat-send.</summary>
    private static readonly TimeSpan SuppressSendAfterIme = TimeSpan.FromMilliseconds(250);

    private readonly Configuration configuration;
    private readonly KanjiMapService mapService;
    private readonly PhraseReplacementService phraseService;
    private readonly IFramework framework;
    private readonly IGameGui gameGui;
    private readonly IPluginLog log;
    private readonly ImeCompositionTracker imeTracker;

    private bool writing;

    public ChatInputWatcher(
        Configuration configuration,
        KanjiMapService mapService,
        PhraseReplacementService phraseService,
        IFramework framework,
        IGameGui gameGui,
        IPluginLog log,
        ImeCompositionTracker imeTracker)
    {
        this.configuration = configuration;
        this.mapService = mapService;
        this.phraseService = phraseService;
        this.framework = framework;
        this.gameGui = gameGui;
        this.log = log;
        this.imeTracker = imeTracker;

        this.imeTracker.RewriteCommittedResult = this.TryRewriteResult;
        this.framework.Update += this.OnFrameworkUpdate;
        this.imeTracker.BeforeReturnKeyDown = this.ConvertBeforeSend;
    }

    public void Dispose()
    {
        this.framework.Update -= this.OnFrameworkUpdate;
        if (ReferenceEquals(this.imeTracker.BeforeReturnKeyDown, (Action)this.ConvertBeforeSend))
            this.imeTracker.BeforeReturnKeyDown = null;

        if (ReferenceEquals(this.imeTracker.RewriteCommittedResult, (Func<string, string?>)this.TryRewriteResult))
            this.imeTracker.RewriteCommittedResult = null;
    }

    private unsafe string? TryRewriteResult(string result)
    {
        if (!this.configuration.Enabled || !this.imeTracker.AcceptChatIme)
            return null;

        if (this.mapService.Active.Count == 0 && this.phraseService.EnabledCount == 0)
            return null;

        if (this.configuration.SkipSlashCommands)
        {
            // Slash commands are decided from the buffer; if the line already starts with '/',
            // leave Imm alone so we never rewrite command arguments mid-IME.
            try
            {
                if (this.TryGetActiveInput(out var input))
                {
                    var text = GetTextInputString(input);
                    if (!string.IsNullOrEmpty(text) && text.StartsWith('/'))
                        return null;
                }
            }
            catch
            {
                // fall through and convert
            }
        }

        var converted = KanjiConverter.ConvertAll(result, this.phraseService, this.mapService.Active);
        return converted == result ? null : converted;
    }

    private unsafe void OnFrameworkUpdate(IFramework frameworkParam)
    {
        _ = frameworkParam;
        this.UpdateChatImeGate();

        if (!this.configuration.Enabled || this.writing)
            return;

        if (this.mapService.Active.Count == 0 && this.phraseService.EnabledCount == 0)
            return;

        try
        {
            if (!this.imeTracker.AcceptChatIme)
            {
                // ImGui (or non-chat) owns keyboard/IME — never InsertText into ChatLog.
                if (this.imeTracker.HasPendingInsert)
                    this.imeTracker.ClearPendingCommits();
                return;
            }

            if (!this.imeTracker.HasPendingInsert)
                return;

            if (!this.TryGetActiveInput(out var input))
                return;

            var textHint = GetTextInputString(input);
            this.imeTracker.Refresh(
                ImeCompositionTracker.IsMostlyEqualsNoise(textHint) ? null : textHint);

            // Never insert while still composing.
            if (this.imeTracker.IsComposing)
                return;

            // Empty chat is OK when we suppressed RESULTSTR and still need to InsertText
            // the first glyph. Only drop the queue if it has gone stale.
            var chatEmpty = string.IsNullOrEmpty(textHint)
                            || ImeCompositionTracker.IsMostlyEqualsNoise(textHint);
            if (chatEmpty)
            {
                var sinceEnd = this.imeTracker.TimeSinceCompositionEnded;
                if (sinceEnd == Timeout.InfiniteTimeSpan
                    || sinceEnd > TimeSpan.FromMilliseconds(500))
                {
                    this.imeTracker.ClearPendingCommits();
                    return;
                }
            }

            var sinceEndCommit = this.imeTracker.TimeSinceCompositionEnded;
            if (sinceEndCommit != Timeout.InfiniteTimeSpan && sinceEndCommit < PostCommitDelay)
                return;

            if (!this.imeTracker.TryConsumePendingInsert(out var converted, out var original))
                return;

            if (string.IsNullOrEmpty(converted))
                return;

            var text = GetTextInputString(input);

            // If Imm still delivered the Chinese (strip failed / TSF path), do not
            // InsertText on top (would duplicate) and do not backspace/SetText the line.
            // Send-time SetText remains the safety net.
            if (!string.IsNullOrEmpty(original) && text.EndsWith(original, StringComparison.Ordinal))
            {
                this.log.Debug("NoMoreEquals: RESULTSTR suppress missed; leaving buffer untouched");
                return;
            }

            if (text.EndsWith(converted, StringComparison.Ordinal))
                return;

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

            // InsertText leaves the caret at the start of the inserted span; push it to the end
            // (same approach QuickSymbols uses after chat InsertText).
            var caretMoves = GetCaretMoveCount(converted);
            if (caretMoves > 0)
            {
                this.framework.RunOnTick(
                    () => this.AdvanceChatCaretRight(caretMoves),
                    delayTicks: 1);
            }
        }
        catch (Exception ex)
        {
            this.log.Debug(ex, "NoMoreEquals: pending InsertText skipped");
            this.writing = false;
        }
    }

    private unsafe void AdvanceChatCaretRight(int caretMoves)
    {
        try
        {
            if (!this.TryGetActiveInput(out _))
                return;

            SendKeyPress(VirtualKeyRight, caretMoves);
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

    private unsafe void UpdateChatImeGate()
    {
        var accept = false;
        try
        {
            if (this.configuration.Enabled
                && this.TryGetActiveInput(out _)
                && !ImGui.GetIO().WantTextInput)
            {
                accept = true;
            }
        }
        catch
        {
            accept = false;
        }

        this.imeTracker.AcceptChatIme = accept;
    }

    private unsafe void ConvertBeforeSend()
    {
        if (!this.configuration.Enabled || this.writing)
            return;

        if (this.mapService.Active.Count == 0 && this.phraseService.EnabledCount == 0)
            return;

        this.imeTracker.Refresh(null);
        if (this.imeTracker.IsComposing)
            return;

        var sinceEnd = this.imeTracker.TimeSinceCompositionEnded;
        if (sinceEnd != Timeout.InfiniteTimeSpan && sinceEnd < SuppressSendAfterIme)
            return;

        try
        {
            if (!this.TryGetActiveInput(out var input))
                return;

            var text = GetTextInputString(input);
            if (string.IsNullOrEmpty(text))
                return;

            if (this.configuration.SkipSlashCommands && text.StartsWith('/'))
                return;

            if (ImeCompositionTracker.IsMostlyEqualsNoise(text))
                return;

            var converted = KanjiConverter.ConvertAll(text, this.phraseService, this.mapService.Active);
            if (converted == text)
                return;

            // Send path only — line is leaving; full SetText is acceptable here.
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

    private unsafe bool TryGetActiveInput(out AtkComponentTextInput* input)
    {
        input = null;
        var chatUnit = this.gameGui.GetAddonByName(ChatLogAddonName);
        if (chatUnit.IsNull || !chatUnit.IsReady || !chatUnit.IsVisible)
            return false;

        var chatLog = this.gameGui.GetAddonByName<AddonChatLog>(ChatLogAddonName);
        if (chatLog == null || chatLog->TextInput == null)
            return false;

        input = chatLog->TextInput;
        return input->Enabled && input->IsActive;
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
