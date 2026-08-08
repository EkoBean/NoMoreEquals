using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using NoMoreEquals.Localization;
using NoMoreEquals.Services;

namespace NoMoreEquals.Windows;

public sealed class ConfigWindow : Window, IDisposable
{
    /// <summary>
    /// Relative scale for section titles (Preview, Phrase replacements, …).
    /// 1.0 = default UI font; try 1.15–1.35 for larger headers.
    /// </summary>
    private const float SectionTitleScale = 1.25f;

    /// <summary>Preview try-out block width as a fraction of the content area.</summary>
    private const float PreviewContentWidthRatio = 0.8f;

    private readonly Plugin plugin;
    private readonly Configuration configuration;

    private string newGlyphFrom = string.Empty;
    private string newGlyphTo = string.Empty;
    private string newPhraseFrom = string.Empty;
    private string newPhraseTo = string.Empty;
    private string previewInput = string.Empty;
    private string statusMessage = string.Empty;

    public ConfigWindow(Plugin plugin)
        : base("NoMoreEquals###NoMoreEqualsConfig")
    {
        this.plugin = plugin;
        this.configuration = plugin.Configuration;

        this.Size = new Vector2(720, 560);
        this.SizeCondition = ImGuiCond.FirstUseEver;
        this.SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(640, 420),
            MaximumSize = new Vector2(1200, 1000),
        };

        this.RefreshWindowTitle();
    }

    public void Dispose()
    {
    }

    public void RefreshWindowTitle()
    {
        this.WindowName = $"{I18n.T.WindowTitle}###NoMoreEqualsConfig";
    }

    public override void Draw()
    {
        var t = I18n.T;

        var enabled = this.configuration.Enabled;
        if (ImGui.Checkbox(t.EnableChatConversion, ref enabled))
        {
            this.configuration.Enabled = enabled;
            this.configuration.Save();
        }

        var skipSlash = this.configuration.SkipSlashCommands;
        if (ImGui.Checkbox(t.SkipSlashCommands, ref skipSlash))
        {
            this.configuration.SkipSlashCommands = skipSlash;
            this.configuration.Save();
        }

        ImGui.TextDisabled(string.Format(
            t.StatusCounts,
            this.plugin.MapService.ActiveCount,
            this.plugin.PhraseService.EnabledCount));

        ImGui.Spacing();
        if (ImGui.BeginTabBar("##NoMoreEqualsTabs"))
        {
            if (ImGui.BeginTabItem(t.TabMain))
            {
                this.DrawMainTab(t);
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem(t.TabAdvanced))
            {
                this.DrawAdvancedTab(t);
                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }

        if (!string.IsNullOrEmpty(this.statusMessage))
        {
            ImGui.Spacing();
            ImGui.TextWrapped(this.statusMessage);
        }
    }

    private void DrawMainTab(UiStrings t)
    {
        this.DrawPreviewSection(t);

        ImGui.Separator();
        ImGui.Spacing();

        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var avail = ImGui.GetContentRegionAvail();
        var colWidth = Math.Max((avail.X - spacing) * 0.5f, 200f);
        var colHeight = Math.Max(avail.Y, 260f);

        ImGui.BeginChild("##phraseCol", new Vector2(colWidth, colHeight), true);
        this.DrawPhraseSection(t);
        ImGui.EndChild();

        ImGui.SameLine();

        ImGui.BeginChild("##glyphCol", new Vector2(colWidth, colHeight), true);
        this.DrawGlyphSection(t);
        ImGui.EndChild();
    }

    private void DrawPreviewSection(UiStrings t)
    {
        DrawSectionTitle(t.Preview);
        ImGui.TextWrapped(t.PreviewInputHint);

        var availX = ImGui.GetContentRegionAvail().X;
        var width = availX * PreviewContentWidthRatio;
        var startX = ImGui.GetCursorPosX() + (availX - width) * 0.5f;

        ImGui.SetCursorPosX(startX);
        ImGui.SetNextItemWidth(width);
        ImGui.InputTextWithHint("##preview", t.PreviewHelp, ref this.previewInput, 512);

        var previewOut = KanjiConverter.ConvertAll(
            this.previewInput,
            this.plugin.PhraseService,
            this.plugin.MapService.Active);

        ImGui.SetCursorPosX(startX);
        ImGui.BeginGroup();
        ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + width);
        ImGui.TextDisabled(t.PreviewResult);
        if (string.IsNullOrWhiteSpace(this.previewInput))
            ImGui.TextDisabled("—");
        else
            ImGui.TextWrapped(previewOut);
        ImGui.PopTextWrapPos();
        ImGui.EndGroup();
    }

    /// <summary>Draw a section header. Tweak <see cref="SectionTitleScale"/> to change size.</summary>
    private static void DrawSectionTitle(string text)
    {
        ImGui.SetWindowFontScale(SectionTitleScale);
        ImGui.TextUnformatted(text);
        ImGui.SetWindowFontScale(1f);
    }

    private void DrawAdvancedTab(UiStrings t)
    {
        ImGui.TextWrapped(t.AdvancedHelp);
        ImGui.Spacing();

        var liveFilter = this.configuration.UseLiveFontFilter;
        if (ImGui.Checkbox(t.UseLiveFontFilter, ref liveFilter))
        {
            this.configuration.UseLiveFontFilter = liveFilter;
            this.configuration.Save();
            this.plugin.RebuildAll();
        }

        if (ImGui.Button(t.RebuildFromFont))
        {
            this.plugin.RebuildAll();
            this.statusMessage = string.Format(
                t.RebuiltStatus,
                this.plugin.MapService.ActiveCount,
                this.plugin.PhraseService.EnabledCount);
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        ImGui.TextWrapped(t.AdvancedOverviewHelp);

        this.DrawLoadedPhraseOverview(t);
        this.DrawLoadedGlyphOverview(t);
    }

    private void DrawLoadedPhraseOverview(UiStrings t)
    {
        var phrases = this.plugin.PhraseService.Active;
        var label = string.Format(t.AdvancedPhraseOverview, phrases.Count);
        if (!ImGui.CollapsingHeader($"{label}###loadedPhrases"))
            return;

        if (phrases.Count == 0)
        {
            ImGui.TextDisabled(t.AdvancedOverviewEmpty);
            return;
        }

        ImGui.BeginChild("##loadedPhraseTable", new Vector2(0, 160), true);
        var flags = ImGuiTableFlags.Borders
                    | ImGuiTableFlags.RowBg
                    | ImGuiTableFlags.SizingStretchProp
                    | ImGuiTableFlags.ScrollY;
        if (ImGui.BeginTable("##loadedPhrases", 2, flags))
        {
            ImGui.TableSetupColumn(t.PhraseFrom);
            ImGui.TableSetupColumn(t.PhraseTo);
            ImGui.TableHeadersRow();

            for (var i = 0; i < phrases.Count; i++)
            {
                var rule = phrases[i];
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
        var map = this.plugin.MapService.Active;
        var label = string.Format(t.AdvancedGlyphOverview, map.Count);
        if (!ImGui.CollapsingHeader($"{label}###loadedGlyphs"))
            return;

        if (map.Count == 0)
        {
            ImGui.TextDisabled(t.AdvancedOverviewEmpty);
            return;
        }

        this.configuration.CustomMappings ??= new Dictionary<string, string>();
        var customKeys = new HashSet<char>();
        foreach (var key in this.configuration.CustomMappings.Keys)
        {
            if (KanjiMapService.TryParseSingleChar(key, out var c))
                customKeys.Add(c);
        }

        ImGui.BeginChild("##loadedGlyphTable", new Vector2(0, 220), true);
        var flags = ImGuiTableFlags.Borders
                    | ImGuiTableFlags.RowBg
                    | ImGuiTableFlags.SizingStretchProp
                    | ImGuiTableFlags.ScrollY;
        if (ImGui.BeginTable("##loadedGlyphs", 3, flags))
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

    private void DrawPhraseSection(UiStrings t)
    {
        DrawSectionTitle(t.PhraseSectionTitle);
        ImGui.TextWrapped(t.PhraseSectionHelp);

        var half = HalfFieldWidth();
        ImGui.SetNextItemWidth(half);
        ImGui.InputTextWithHint("##phraseFrom", t.PhraseFrom, ref this.newPhraseFrom, 64);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(half);
        ImGui.InputTextWithHint("##phraseTo", t.PhraseTo, ref this.newPhraseTo, 64);
        if (ImGui.Button($"{t.AddPhrase}##addPhrase", new Vector2(-1, 0)))
            this.TryAddPhrase(t);

        var listHeight = Math.Max(ImGui.GetContentRegionAvail().Y, 80f);
        ImGui.BeginChild("##phraseList", new Vector2(0, listHeight), true);

        this.configuration.PhraseReplacements ??= [];
        var removeAt = -1;
        for (var i = 0; i < this.configuration.PhraseReplacements.Count; i++)
        {
            var rule = this.configuration.PhraseReplacements[i];
            var on = rule.Enabled;
            if (ImGui.Checkbox($"##phraseOn{i}", ref on))
            {
                rule.Enabled = on;
                this.configuration.Save();
                this.plugin.RebuildAll();
            }

            ImGui.SameLine();
            ImGui.TextUnformatted($"{rule.From} → {rule.To}");
            ImGui.SameLine();
            if (ImGui.SmallButton($"{t.Delete}##phraseDel{i}"))
                removeAt = i;
        }

        if (removeAt >= 0)
        {
            this.configuration.PhraseReplacements.RemoveAt(removeAt);
            this.configuration.Save();
            this.plugin.RebuildAll();
            this.statusMessage = t.PhraseRemoved;
        }

        if (this.configuration.PhraseReplacements.Count == 0)
            ImGui.TextDisabled(t.NoPhrasesYet);

        ImGui.EndChild();
    }

    private void DrawGlyphSection(UiStrings t)
    {
        DrawSectionTitle(t.GlyphSectionTitle);
        ImGui.TextWrapped(t.GlyphSectionHelp);

        var half = HalfFieldWidth();
        ImGui.SetNextItemWidth(half);
        ImGui.InputTextWithHint("##glyphFrom", t.GlyphFrom, ref this.newGlyphFrom, 8);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(half);
        ImGui.InputTextWithHint("##glyphTo", t.GlyphTo, ref this.newGlyphTo, 8);
        if (ImGui.Button($"{t.AddGlyph}##addGlyph", new Vector2(-1, 0)))
            this.TryAddGlyph(t);

        var listHeight = Math.Max(ImGui.GetContentRegionAvail().Y, 80f);
        ImGui.BeginChild("##glyphList", new Vector2(0, listHeight), true);

        this.configuration.CustomMappings ??= new Dictionary<string, string>();
        var toRemove = new List<string>();
        foreach (var (from, to) in this.configuration.CustomMappings.OrderBy(kv => kv.Key))
        {
            ImGui.TextUnformatted($"{from} → {to}");
            ImGui.SameLine();
            if (ImGui.SmallButton($"{t.Delete}##glyphDel{from}"))
                toRemove.Add(from);
        }

        foreach (var key in toRemove)
        {
            this.configuration.CustomMappings.Remove(key);
            this.configuration.Save();
            this.plugin.RebuildAll();
        }

        if (this.configuration.CustomMappings.Count == 0)
            ImGui.TextDisabled(t.NoGlyphOverridesYet);

        ImGui.EndChild();
    }

    private static float HalfFieldWidth()
    {
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        return Math.Max((ImGui.GetContentRegionAvail().X - spacing) * 0.5f, 60f);
    }

    private void TryAddPhrase(UiStrings t)
    {
        var from = this.newPhraseFrom.Trim();
        var to = this.newPhraseTo.Trim();

        if (string.IsNullOrEmpty(from))
        {
            this.statusMessage = t.PhraseEmptyError;
            return;
        }

        if (string.Equals(from, to, StringComparison.Ordinal))
        {
            this.statusMessage = t.PhraseSameError;
            return;
        }

        this.configuration.PhraseReplacements ??= [];
        var existing = this.configuration.PhraseReplacements
            .FindIndex(p => string.Equals(p.From, from, StringComparison.Ordinal));
        if (existing >= 0)
            this.configuration.PhraseReplacements[existing].To = to;
        else
            this.configuration.PhraseReplacements.Add(new PhraseReplacement { From = from, To = to, Enabled = true });

        this.configuration.Save();
        this.plugin.RebuildAll();
        this.newPhraseFrom = string.Empty;
        this.newPhraseTo = string.Empty;
        this.statusMessage = string.Format(t.PhraseAdded, from, to);
    }

    private void TryAddGlyph(UiStrings t)
    {
        if (KanjiMapService.TryParseSingleChar(this.newGlyphFrom, out var zh)
            && KanjiMapService.TryParseSingleChar(this.newGlyphTo, out var jp)
            && zh != jp)
        {
            this.configuration.CustomMappings ??= new Dictionary<string, string>();
            this.configuration.CustomMappings[zh.ToString()] = jp.ToString();
            this.configuration.Save();
            this.plugin.RebuildAll();
            this.newGlyphFrom = string.Empty;
            this.newGlyphTo = string.Empty;
            this.statusMessage = string.Format(t.GlyphAdded, zh, jp);
        }
        else
        {
            this.statusMessage = t.GlyphNeedSingleChar;
        }
    }
}
