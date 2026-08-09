using System;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace NoMoreEquals.Services;

/// <summary>
/// The single point of access to the game's ChatLog text input.
/// <para>
/// The input pipeline, the IME gate and the preview overlay all go through this
/// type, so they can never disagree about whether chat is focused or what it
/// currently contains — previously each of them carried its own copy of the
/// addon lookup and of <c>GetTextInputString</c>.
/// </para>
/// </summary>
internal sealed class ChatInputAccessor
{
    private const string ChatLogAddonName = "ChatLog";

    private readonly IGameGui gameGui;

    public ChatInputAccessor(IGameGui gameGui)
    {
        this.gameGui = gameGui;
    }

    /// <summary>
    /// True when ChatLog is on screen and its text field currently has focus.
    /// <paramref name="input"/> is only non-null when this returns true.
    /// </summary>
    public unsafe bool TryGetActiveInput(out AtkComponentTextInput* input)
    {
        input = null;

        var chatUnit = this.gameGui.GetAddonByName(ChatLogAddonName);
        if (chatUnit.IsNull || !chatUnit.IsReady || !chatUnit.IsVisible)
            return false;

        var chatLog = this.gameGui.GetAddonByName<AddonChatLog>(ChatLogAddonName);
        if (chatLog == null || chatLog->TextInput == null)
            return false;

        var candidate = chatLog->TextInput;
        if (!candidate->Enabled || !candidate->IsActive)
            return false;

        input = candidate;
        return true;
    }

    /// <summary>Focus check for callers that do not need the pointer.</summary>
    public unsafe bool IsInputActive() => this.TryGetActiveInput(out _);

    /// <summary>
    /// Current buffer contents, or the empty string when chat is not focused.
    /// Never throws — every caller treats an unreadable buffer as empty.
    /// </summary>
    public unsafe string GetActiveText()
    {
        try
        {
            return this.TryGetActiveInput(out var input) ? GetText(input) : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    public static unsafe string GetText(AtkComponentTextInput* input)
    {
        if (input == null)
            return string.Empty;

        var raw = input->AtkComponentInputBase.RawString.ToString();
        if (!string.IsNullOrEmpty(raw))
            return raw;

        return input->AtkComponentInputBase.EvaluatedString.ToString();
    }

    /// <summary>Screen-space rect of the input node, for positioning the preview.</summary>
    public static unsafe bool TryGetScreenRect(
        AtkComponentTextInput* input,
        out float x,
        out float y,
        out float width,
        out float height)
    {
        x = y = width = height = 0;
        if (input == null)
            return false;

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
}
