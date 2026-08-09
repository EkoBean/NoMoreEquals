namespace NoMoreEquals.Windows;

/// <summary>
/// Shared state and commands handed to each config tab, so tabs never reach
/// into the plugin's services directly.
/// </summary>
internal sealed class ConfigWindowContext
{
    private readonly Plugin plugin;

    public ConfigWindowContext(Plugin plugin)
    {
        this.plugin = plugin;
    }

    public Plugin Plugin => this.plugin;

    public Configuration Configuration => this.plugin.Configuration;

    /// <summary>Persist a setting that does not affect the conversion tables.</summary>
    public void Save() => this.plugin.ConfigService.Save();

    /// <summary>Persist a setting and recompute the conversion tables.</summary>
    public void ApplyAndSave()
    {
        this.plugin.ConfigService.Save();
        this.plugin.RebuildAll();
    }
}
