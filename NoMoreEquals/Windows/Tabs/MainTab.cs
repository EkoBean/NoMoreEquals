using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using NoMoreEquals.Localization;
using NoMoreEquals.Services;

namespace NoMoreEquals.Windows.Tabs;

/// <summary>
/// Everyday settings: a live conversion preview plus CRUD for phrase
/// replacements and single-character glyph overrides.
/// </summary>
internal sealed class MainTab
{
    /// <summary>Preview try-out block width as a fraction of the content area.</summary>
    private const float PreviewContentWidthRatio = 0.8f;

    private readonly ConfigWindowContext context;

    private string newGlyphFrom = string.Empty;
    private string newGlyphTo = string.Empty;
    private string newPhraseFrom = string.Empty;
    private string newPhraseTo = string.Empty;
    private string previewInput = string.Empty;

    public MainTab(ConfigWindowContext context)
    {
        this.context = context;
    }

    public void Draw(UiStrings t)
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
        UiHelpers.SectionTitle(t.Preview);
        ImGui.TextWrapped(t.PreviewInputHint);

        var availX = ImGui.GetContentRegionAvail().X;
        var width = availX * PreviewContentWidthRatio;
        var startX = ImGui.GetCursorPosX() + ((availX - width) * 0.5f);

        ImGui.SetCursorPosX(startX);
        ImGui.SetNextItemWidth(width);
        ImGui.InputTextWithHint("##preview", t.PreviewHelp, ref this.previewInput, 512);

        var previewOut = KanjiConverter.ConvertAll(
            this.previewInput,
            this.context.Plugin.PhraseService,
            this.context.Plugin.MapService.Active);

        ImGui.SetCursorPosX(startX);
        ImGui.BeginGroup();
        ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + width);
        ImGui.TextDisabled(t.PreviewResult);
        if (string.IsNullOrWhiteSpace(this.previewInput))
            ImGui.TextDisabled("—");
        else
            ImGui.TextUnformatted(previewOut); // never TextWrapped: player text may contain '%'

        ImGui.PopTextWrapPos();
        ImGui.EndGroup();
    }

    private void DrawPhraseSection(UiStrings t)
    {
        UiHelpers.SectionTitle(t.PhraseSectionTitle);
        ImGui.TextWrapped(t.PhraseSectionHelp);

        var half = UiHelpers.HalfFieldWidth();
        ImGui.SetNextItemWidth(half);
        ImGui.InputTextWithHint("##phraseFrom", t.PhraseFrom, ref this.newPhraseFrom, 64);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(half);
        ImGui.InputTextWithHint("##phraseTo", t.PhraseTo, ref this.newPhraseTo, 64);
        if (ImGui.Button($"{t.AddPhrase}##addPhrase", new Vector2(-1, 0)))
            this.TryAddPhrase(t);

        var listHeight = Math.Max(ImGui.GetContentRegionAvail().Y, 80f);
        ImGui.BeginChild("##phraseList", new Vector2(0, listHeight), true);

        var phrases = this.context.Configuration.PhraseReplacements;
        var removeAt = -1;
        for (var i = 0; i < phrases.Count; i++)
        {
            var rule = phrases[i];
            var on = rule.Enabled;
            if (ImGui.Checkbox($"##phraseOn{i}", ref on))
            {
                rule.Enabled = on;
                this.context.ApplyAndSave();
            }

            ImGui.SameLine();
            ImGui.TextUnformatted($"{rule.From} → {rule.To}");
            ImGui.SameLine();
            if (ImGui.SmallButton($"{t.Delete}##phraseDel{i}"))
                removeAt = i;
        }

        if (removeAt >= 0)
        {
            phrases.RemoveAt(removeAt);
            this.context.ApplyAndSave();
            this.context.StatusMessage = t.PhraseRemoved;
        }

        if (phrases.Count == 0)
            ImGui.TextDisabled(t.NoPhrasesYet);

        ImGui.EndChild();
    }

    private void DrawGlyphSection(UiStrings t)
    {
        UiHelpers.SectionTitle(t.GlyphSectionTitle);
        ImGui.TextWrapped(t.GlyphSectionHelp);

        var half = UiHelpers.HalfFieldWidth();
        ImGui.SetNextItemWidth(half);
        ImGui.InputTextWithHint("##glyphFrom", t.GlyphFrom, ref this.newGlyphFrom, 8);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(half);
        ImGui.InputTextWithHint("##glyphTo", t.GlyphTo, ref this.newGlyphTo, 8);
        if (ImGui.Button($"{t.AddGlyph}##addGlyph", new Vector2(-1, 0)))
            this.TryAddGlyph(t);

        var listHeight = Math.Max(ImGui.GetContentRegionAvail().Y, 80f);
        ImGui.BeginChild("##glyphList", new Vector2(0, listHeight), true);

        var mappings = this.context.Configuration.CustomMappings;
        List<string>? toRemove = null;
        foreach (var (from, to) in mappings.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            ImGui.TextUnformatted($"{from} → {to}");
            ImGui.SameLine();
            if (ImGui.SmallButton($"{t.Delete}##glyphDel{from}"))
                (toRemove ??= []).Add(from);
        }

        if (toRemove is not null)
        {
            foreach (var key in toRemove)
                mappings.Remove(key);

            this.context.ApplyAndSave();
        }

        if (mappings.Count == 0)
            ImGui.TextDisabled(t.NoGlyphOverridesYet);

        ImGui.EndChild();
    }

    private void TryAddPhrase(UiStrings t)
    {
        var from = this.newPhraseFrom.Trim();
        var to = this.newPhraseTo.Trim();

        if (string.IsNullOrEmpty(from))
        {
            this.context.StatusMessage = t.PhraseEmptyError;
            return;
        }

        // Ahead of the same-as check so that "abc" -> "abc" reports the real problem.
        if (!ChineseScript.HasChineseCharacter(from))
        {
            this.context.StatusMessage = t.SourceNeedChineseError;
            return;
        }

        if (string.Equals(from, to, StringComparison.Ordinal))
        {
            this.context.StatusMessage = t.PhraseSameError;
            return;
        }

        var phrases = this.context.Configuration.PhraseReplacements;
        var existing = phrases.FindIndex(p => string.Equals(p.From, from, StringComparison.Ordinal));
        if (existing >= 0)
            phrases[existing].To = to;
        else
            phrases.Add(new PhraseReplacement { From = from, To = to, Enabled = true });

        this.context.ApplyAndSave();
        this.newPhraseFrom = string.Empty;
        this.newPhraseTo = string.Empty;
        this.context.StatusMessage = string.Format(t.PhraseAdded, from, to);
    }

    private void TryAddGlyph(UiStrings t)
    {
        if (!KanjiMapService.TryParseSingleChar(this.newGlyphFrom, out var zh)
            || !KanjiMapService.TryParseSingleChar(this.newGlyphTo, out var jp)
            || zh == jp)
        {
            this.context.StatusMessage = t.GlyphNeedSingleChar;
            return;
        }

        if (!ChineseScript.HasChineseCharacter(zh.ToString()))
        {
            this.context.StatusMessage = t.SourceNeedChineseError;
            return;
        }

        this.context.Configuration.CustomMappings[zh.ToString()] = jp.ToString();
        this.context.ApplyAndSave();
        this.newGlyphFrom = string.Empty;
        this.newGlyphTo = string.Empty;
        this.context.StatusMessage = string.Format(t.GlyphAdded, zh, jp);
    }
}
