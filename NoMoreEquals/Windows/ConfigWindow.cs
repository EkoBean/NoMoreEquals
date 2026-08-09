using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using NoMoreEquals.Localization;
using NoMoreEquals.Windows.Tabs;

namespace NoMoreEquals.Windows;

/// <summary>
/// Window frame: global toggles, tab bar and status line. Tab bodies live in
/// <see cref="MainTab"/> and <see cref="AdvancedTab"/>.
/// </summary>
internal sealed class ConfigWindow : Window
{
    private readonly ConfigWindowContext context;
    private readonly MainTab mainTab;
    private readonly AdvancedTab advancedTab;

    public ConfigWindow(Plugin plugin)
        : base("NoMoreEquals###NoMoreEqualsConfig")
    {
        this.context = new ConfigWindowContext(plugin);
        this.mainTab = new MainTab(this.context);
        this.advancedTab = new AdvancedTab(this.context);

        this.Size = new Vector2(720, 560);
        this.SizeCondition = ImGuiCond.FirstUseEver;
        this.SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(640, 420),
            MaximumSize = new Vector2(1200, 1000),
        };

        this.RefreshWindowTitle();
    }

    public void RefreshWindowTitle()
    {
        this.WindowName = $"{I18n.T.WindowTitle}###NoMoreEqualsConfig";
    }

    public override void Draw()
    {
        var t = I18n.T;
        var config = this.context.Configuration;

        var enabled = config.Enabled;
        if (ImGui.Checkbox(t.EnableChatConversion, ref enabled))
        {
            config.Enabled = enabled;
            this.context.Save();
        }

        // TEMPORARY — the overlay is still being worked on, so its switch is shown off and
        // locked. Delete this block and uncomment the one below to turn it back on.
        var showPreview = false;
        ImGui.BeginDisabled();
        ImGui.Checkbox($"{t.ShowChatPreviewOverlay} (WIP)", ref showPreview);
        ImGui.EndDisabled();

        // var showPreview = config.ShowChatPreviewOverlay;
        // if (ImGui.Checkbox(t.ShowChatPreviewOverlay, ref showPreview))
        // {
        //     config.ShowChatPreviewOverlay = showPreview;
        //     this.context.Save();
        // }

        ImGui.TextDisabled(string.Format(
            t.StatusCounts,
            this.context.Plugin.MapService.ActiveCount,
            this.context.Plugin.PhraseService.EnabledCount));

        ImGui.Spacing();
        if (ImGui.BeginTabBar("##NoMoreEqualsTabs"))
        {
            if (ImGui.BeginTabItem(t.TabMain))
            {
                this.mainTab.Draw(t);
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem(t.TabAdvanced))
            {
                this.advancedTab.Draw(t);
                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }
    }
}
