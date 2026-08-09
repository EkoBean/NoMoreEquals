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

    /// <summary>One-line feedback, rendered wherever <see cref="StatusSlot"/> says.</summary>
    public string StatusMessage { get; private set; } = string.Empty;

    /// <summary>Which part of the UI the current <see cref="StatusMessage"/> belongs to.</summary>
    public StatusSlot StatusSlot { get; private set; } = StatusSlot.Window;

    /// <summary>
    /// Replace the visible feedback. One message at a time, as before — the slot only
    /// decides where it is drawn, not how many can be on screen.
    /// </summary>
    public void SetStatus(StatusSlot slot, string message)
    {
        this.StatusSlot = slot;
        this.StatusMessage = message;
    }

    /// <summary>Whether the current message is one <paramref name="slot"/> should draw.</summary>
    public bool HasStatusFor(StatusSlot slot)
        => this.StatusSlot == slot && this.StatusMessage.Length > 0;

    /// <summary>Persist a setting that does not affect the conversion tables.</summary>
    public void Save() => this.plugin.ConfigService.Save();

    /// <summary>Persist a setting and recompute the conversion tables.</summary>
    public void ApplyAndSave()
    {
        this.plugin.ConfigService.Save();
        this.plugin.RebuildAll();
    }
}
