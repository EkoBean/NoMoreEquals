using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using NoMoreEquals.Localization;
using NoMoreEquals.Services;
using NoMoreEquals.Windows;

namespace NoMoreEquals;

public sealed class Plugin : IDalamudPlugin
{
    private const string CommandName = "/nme";
    private const string CommandNameLong = "/nomoreequals";
    private const string CommandHelp = "Open NoMoreEquals settings.";

    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IGameGui GameGui { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    private readonly AxisFontCoverage fontCoverage = new();
    private readonly ConversionGate conversionGate;
    private readonly ChatInputAccessor chatAccessor;
    private readonly ChatImeGate imeGate;
    private readonly ImeCompositionTracker imeTracker;
    private readonly ChatInputWatcher chatWatcher;
    private readonly ChatPreviewOverlay chatPreview;
    private readonly WindowSystem windowSystem = new("NoMoreEquals");
    private readonly ConfigWindow configWindow;

    public Plugin()
    {
        this.ConfigService = new ConfigService(PluginInterface);

        if (DefaultGlyphSeeder.SeedInto(this.Configuration))
            this.ConfigService.Save();

        I18n.Apply(PluginInterface.UiLanguage);
        PluginInterface.LanguageChanged += this.OnLanguageChanged;

        this.fontCoverage.TryLoad(DataManager, Log);
        this.RebuildAll();

        this.conversionGate = new ConversionGate(this.Configuration, this.MapService, this.PhraseService);
        this.chatAccessor = new ChatInputAccessor(GameGui);
        this.imeGate = new ChatImeGate(this.chatAccessor);
        this.imeTracker = new ImeCompositionTracker(this.imeGate);

        this.chatWatcher = new ChatInputWatcher(
            this.Configuration,
            this.conversionGate,
            this.MapService,
            this.PhraseService,
            this.chatAccessor,
            Framework,
            Log,
            this.imeTracker);

        this.chatPreview = new ChatPreviewOverlay(
            this.Configuration,
            this.conversionGate,
            this.MapService,
            this.PhraseService,
            this.chatAccessor,
            this.imeTracker,
            PluginInterface);

        this.configWindow = new ConfigWindow(this);
        this.windowSystem.AddWindow(this.configWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(this.OnCommand) { HelpMessage = CommandHelp });
        CommandManager.AddHandler(CommandNameLong, new CommandInfo(this.OnCommand) { HelpMessage = CommandHelp });

        PluginInterface.UiBuilder.Draw += this.OnDraw;
        PluginInterface.UiBuilder.OpenConfigUi += this.ToggleConfigUi;

        Log.Information(
            $"NoMoreEquals ready. lang={I18n.CurrentLangCode}, glyphs={this.MapService.ActiveCount}, phrases={this.PhraseService.EnabledCount}, AXIS={(this.fontCoverage.Loaded ? this.fontCoverage.GlyphCount.ToString() : "n/a")}");
    }

    internal ConfigService ConfigService { get; }

    internal Configuration Configuration => this.ConfigService.Config;

    internal KanjiMapService MapService { get; } = new();

    internal PhraseReplacementService PhraseService { get; } = new();

    public void Dispose()
    {
        PluginInterface.LanguageChanged -= this.OnLanguageChanged;
        PluginInterface.UiBuilder.Draw -= this.OnDraw;
        PluginInterface.UiBuilder.OpenConfigUi -= this.ToggleConfigUi;

        // Close the gate first: every message-layer handler reads it, so this stops
        // the window procedure doing any work while the rest is torn down.
        this.imeGate.Close();
        this.chatWatcher.Dispose();
        this.chatPreview.Dispose();
        this.imeTracker.Dispose();
        this.windowSystem.RemoveAllWindows();

        CommandManager.RemoveHandler(CommandName);
        CommandManager.RemoveHandler(CommandNameLong);
    }

    private void OnDraw()
    {
        // Single evaluation point for the IME gate, before anything else draws:
        // ImGui's WantTextInput is only authoritative inside the ImGui frame.
        this.imeGate.Update();

        this.windowSystem.Draw();
        this.chatPreview.Draw();
    }

    /// <summary>Recomputes both conversion tables from the current configuration.</summary>
    internal void RebuildAll()
    {
        if (this.Configuration.UseLiveFontFilter && !this.fontCoverage.Loaded)
            this.fontCoverage.TryLoad(DataManager, Log);

        this.MapService.Rebuild(this.Configuration, this.fontCoverage);
        this.PhraseService.Rebuild(this.Configuration.PhraseReplacements);
        Log.Information(
            $"NoMoreEquals rebuilt. Glyphs={this.MapService.ActiveCount}, phrases={this.PhraseService.EnabledCount}");
    }

    public void ToggleConfigUi() => this.configWindow.Toggle();

    private void OnLanguageChanged(string langCode)
    {
        I18n.Apply(langCode);
        this.configWindow.RefreshWindowTitle();
        Log.Information($"NoMoreEquals language -> {I18n.CurrentLangCode}");
    }

    private void OnCommand(string command, string args) => this.ToggleConfigUi();
}
