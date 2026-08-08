using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using NoMoreEquals.Data.DefaultMissingGlyphs;
using NoMoreEquals.Localization;
using NoMoreEquals.Services;
using NoMoreEquals.Windows;

namespace NoMoreEquals;

public sealed class Plugin : IDalamudPlugin
{
    private const string CommandName = "/nme";
    private const string CommandNameLong = "/nomoreequals";

    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IGameGui GameGui { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    public Configuration Configuration { get; }

    internal KanjiMapService MapService { get; } = new();

    internal PhraseReplacementService PhraseService { get; } = new();

    private readonly AxisFontCoverage fontCoverage = new();
    private readonly ChatInputWatcher chatWatcher;
    private readonly WindowSystem windowSystem = new("NoMoreEquals");
    private readonly ConfigWindow configWindow;

    public Plugin()
    {
        this.Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        this.Configuration.CustomMappings ??= new();
        this.Configuration.SeededDefaultGlyphKeys ??= [];
        this.Configuration.PhraseReplacements ??= [];

        if (DefaultMissingGlyphMaps.SeedInto(this.Configuration))
            this.Configuration.Save();

        I18n.Apply(PluginInterface.UiLanguage);
        PluginInterface.LanguageChanged += this.OnLanguageChanged;

        this.fontCoverage.TryLoad(DataManager, Log);
        this.RebuildAll();

        this.chatWatcher = new ChatInputWatcher(
            this.Configuration,
            this.MapService,
            this.PhraseService,
            Framework,
            GameGui,
            Log);

        this.configWindow = new ConfigWindow(this);
        this.windowSystem.AddWindow(this.configWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(this.OnCommand)
        {
            HelpMessage = "Open NoMoreEquals settings.",
        });
        CommandManager.AddHandler(CommandNameLong, new CommandInfo(this.OnCommand)
        {
            HelpMessage = "Open NoMoreEquals settings.",
        });

        PluginInterface.UiBuilder.Draw += this.windowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi += this.ToggleConfigUi;

        Log.Information(
            $"NoMoreEquals ready. lang={I18n.CurrentLangCode}, glyphs={this.MapService.ActiveCount}, phrases={this.PhraseService.EnabledCount}, AXIS={(this.fontCoverage.Loaded ? this.fontCoverage.GlyphCount.ToString() : "n/a")}");
    }

    public void Dispose()
    {
        PluginInterface.LanguageChanged -= this.OnLanguageChanged;
        PluginInterface.UiBuilder.Draw -= this.windowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi -= this.ToggleConfigUi;

        this.chatWatcher.Dispose();
        this.windowSystem.RemoveAllWindows();
        this.configWindow.Dispose();

        CommandManager.RemoveHandler(CommandName);
        CommandManager.RemoveHandler(CommandNameLong);
    }

    internal void RebuildAll()
    {
        if (this.Configuration.UseLiveFontFilter && !this.fontCoverage.Loaded)
            this.fontCoverage.TryLoad(DataManager, Log);

        this.MapService.Rebuild(this.Configuration, this.fontCoverage);
        this.PhraseService.Rebuild(this.Configuration);
        Log.Information(
            $"NoMoreEquals rebuilt. Glyphs={this.MapService.ActiveCount}, phrases={this.PhraseService.EnabledCount}");
    }

    /// <summary>Legacy name kept for call sites that only touch the glyph map.</summary>
    internal void RebuildMap() => this.RebuildAll();

    private void OnLanguageChanged(string langCode)
    {
        I18n.Apply(langCode);
        this.configWindow.RefreshWindowTitle();
        Log.Information($"NoMoreEquals language -> {I18n.CurrentLangCode} (chinese={I18n.IsChinese})");
    }

    private void OnCommand(string command, string args)
    {
        this.ToggleConfigUi();
    }

    public void ToggleConfigUi() => this.configWindow.Toggle();
}
