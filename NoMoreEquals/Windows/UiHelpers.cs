using System;
using Dalamud.Bindings.ImGui;

namespace NoMoreEquals.Windows;

/// <summary>Small ImGui layout helpers shared by the config tabs.</summary>
internal static class UiHelpers
{
    /// <summary>
    /// Relative scale for section titles (Preview, Phrase replacements, …).
    /// 1.0 = default UI font; try 1.15–1.35 for larger headers.
    /// </summary>
    private const float SectionTitleScale = 1.25f;

    public static void SectionTitle(string text)
    {
        ImGui.SetWindowFontScale(SectionTitleScale);
        ImGui.TextUnformatted(text);
        ImGui.SetWindowFontScale(1f);
    }

    /// <summary>Width for two input fields sitting side by side on one row.</summary>
    public static float HalfFieldWidth()
    {
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        return Math.Max((ImGui.GetContentRegionAvail().X - spacing) * 0.5f, 60f);
    }
}
