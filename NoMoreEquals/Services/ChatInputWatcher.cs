using System;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace NoMoreEquals.Services;

/// <summary>
/// Watches the ChatLog text input and applies phrase + glyph conversion while typing.
/// </summary>
internal sealed class ChatInputWatcher : IDisposable
{
    private const string ChatLogAddonName = "ChatLog";

    private readonly Configuration configuration;
    private readonly KanjiMapService mapService;
    private readonly PhraseReplacementService phraseService;
    private readonly IFramework framework;
    private readonly IGameGui gameGui;
    private readonly IPluginLog log;

    private string lastWritten = string.Empty;
    private bool writing;

    public ChatInputWatcher(
        Configuration configuration,
        KanjiMapService mapService,
        PhraseReplacementService phraseService,
        IFramework framework,
        IGameGui gameGui,
        IPluginLog log)
    {
        this.configuration = configuration;
        this.mapService = mapService;
        this.phraseService = phraseService;
        this.framework = framework;
        this.gameGui = gameGui;
        this.log = log;

        this.framework.Update += this.OnFrameworkUpdate;
    }

    public void Dispose()
    {
        this.framework.Update -= this.OnFrameworkUpdate;
    }

    private unsafe void OnFrameworkUpdate(IFramework _)
    {
        if (!this.configuration.Enabled || this.writing)
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
                return;

            var text = GetTextInputString(input);
            if (string.IsNullOrEmpty(text))
            {
                this.lastWritten = string.Empty;
                return;
            }

            if (this.configuration.SkipSlashCommands && text.StartsWith('/'))
                return;

            // Avoid fighting ourselves after a SetText.
            if (text == this.lastWritten)
                return;

            var converted = KanjiConverter.ConvertAll(text, this.phraseService, this.mapService.Active);
            if (ReferenceEquals(converted, text) || converted == text)
            {
                this.lastWritten = text;
                return;
            }

            this.writing = true;
            try
            {
                input->SetText(converted);
                this.lastWritten = converted;
            }
            finally
            {
                this.writing = false;
            }
        }
        catch (Exception ex)
        {
            this.log.Debug(ex, "NoMoreEquals: chat input conversion skipped");
            this.writing = false;
        }
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
