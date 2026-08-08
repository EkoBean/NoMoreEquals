using Dalamud.Plugin;

namespace NoMoreEquals.Services;

/// <summary>
/// Owns loading and persisting <see cref="Configuration"/>, keeping Dalamud's
/// plugin interface out of the config type itself.
/// </summary>
internal sealed class ConfigService
{
    private readonly IDalamudPluginInterface pluginInterface;

    public ConfigService(IDalamudPluginInterface pluginInterface)
    {
        this.pluginInterface = pluginInterface;
        this.Config = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        this.Config.Normalize();
    }

    public Configuration Config { get; }

    public void Save() => this.pluginInterface.SavePluginConfig(this.Config);
}
