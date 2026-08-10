using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using NoMoreEquals.Localization;
using NoMoreEquals.Services;

namespace NoMoreEquals.Windows.Tabs;

/// <summary>
/// Font-filter controls plus read-only tables of everything currently loaded.
/// </summary>
internal sealed class AdvancedTab
{
    private const ImGuiTableFlags TableFlags = ImGuiTableFlags.Borders
                                               | ImGuiTableFlags.RowBg
                                               | ImGuiTableFlags.SizingStretchProp
                                               | ImGuiTableFlags.ScrollY;

    private readonly ConfigWindowContext context;

    /// <summary>Result of the last restore click, or empty. Cleared on any mouse press.</summary>
    private string restoreMessage = string.Empty;

    public AdvancedTab(ConfigWindowContext context)
    {
        this.context = context;
    }

    public void Draw(UiStrings t)
    {
        ImGui.TextWrapped(t.AdvancedHelp);
        ImGui.Spacing();

        var config = this.context.Configuration;
        var liveFilter = config.UseLiveFontFilter;
        if (ImGui.Checkbox(t.UseLiveFontFilter, ref liveFilter))
        {
            config.UseLiveFontFilter = liveFilter;
            this.context.ApplyAndSave();
        }

        // No confirmation message: the counts at the top of the window are recomputed by
        // the rebuild and already say what happened.
        if (ImGui.Button(t.RebuildFromFont))
            this.context.Plugin.RebuildAll();

        // Same dismissal rule as the Main tab's errors: any mouse press clears the last
        // result. ImGui activates a button on release, so the press that begins a click on
        // Restore clears the old message a frame before the release raises a new one.
        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            this.restoreMessage = string.Empty;

        if (ImGui.Button(t.RestoreDefaults))
        {
            var before = config.CustomMappings.Count + config.PhraseReplacements.Count;

            // | not ||: both passes must run.
            var added = DefaultGlyphSeeder.RestoreAll(config)
                        | DefaultPhraseSeeder.RestoreAll(config);

            var delta = config.CustomMappings.Count + config.PhraseReplacements.Count - before;

            this.restoreMessage = delta > 0
                ? string.Format(t.RestoreDefaultsDone, delta)
                : t.RestoreDefaultsNothing;

            if (added)
                this.context.ApplyAndSave();
        }

        if (this.restoreMessage.Length > 0)
            UiHelpers.StatusText(this.restoreMessage);

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        ImGui.TextWrapped(t.AdvancedOverviewHelp);

        this.DrawLoadedPhraseOverview(t);
        this.DrawLoadedGlyphOverview(t);
    }

    private void DrawLoadedPhraseOverview(UiStrings t)
    {
        var phrases = this.context.Plugin.PhraseService.Active;
        var label = string.Format(t.AdvancedPhraseOverview, phrases.Count);
        if (!ImGui.CollapsingHeader($"{label}###loadedPhrases"))
            return;

        if (phrases.Count == 0)
        {
            ImGui.TextDisabled(t.AdvancedOverviewEmpty);
            return;
        }

        ImGui.BeginChild("##loadedPhraseTable", new Vector2(0, 160), true);
        if (ImGui.BeginTable("##loadedPhrases", 2, TableFlags))
        {
            ImGui.TableSetupColumn(t.PhraseFrom);
            ImGui.TableSetupColumn(t.PhraseTo);
            ImGui.TableHeadersRow();

            foreach (var rule in phrases)
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(rule.From);
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(rule.To);
            }

            ImGui.EndTable();
        }

        ImGui.EndChild();
    }

    private void DrawLoadedGlyphOverview(UiStrings t)
    {
        var map = this.context.Plugin.MapService.Active;
        var label = string.Format(t.AdvancedGlyphOverview, map.Count);
        if (!ImGui.CollapsingHeader($"{label}###loadedGlyphs"))
            return;

        if (map.Count == 0)
        {
            ImGui.TextDisabled(t.AdvancedOverviewEmpty);
            return;
        }

        var customKeys = new HashSet<char>();
        foreach (var key in this.context.Configuration.CustomMappings.Keys)
        {
            if (KanjiMapService.TryParseSingleChar(key, out var c))
                customKeys.Add(c);
        }

        ImGui.BeginChild("##loadedGlyphTable", new Vector2(0, 220), true);
        if (ImGui.BeginTable("##loadedGlyphs", 3, TableFlags))
        {
            ImGui.TableSetupColumn(t.GlyphFrom);
            ImGui.TableSetupColumn(t.GlyphTo);
            ImGui.TableSetupColumn(t.AdvancedOverviewSource);
            ImGui.TableHeadersRow();

            foreach (var (from, to) in map.OrderBy(kv => kv.Key))
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(from.ToString());
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(to.ToString());
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(
                    customKeys.Contains(from) ? t.AdvancedOverviewCustom : t.AdvancedOverviewBuiltIn);
            }

            ImGui.EndTable();
        }

        ImGui.EndChild();
    }
}
