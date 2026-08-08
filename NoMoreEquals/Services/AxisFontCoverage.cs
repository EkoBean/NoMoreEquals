using System;
using System.Collections.Generic;
using Dalamud.Interface.GameFonts;
using Dalamud.Plugin.Services;

namespace NoMoreEquals.Services;

/// <summary>
/// Loads glyph coverage from the game's AXIS font (.fdt) via <see cref="IDataManager"/>.
/// </summary>
internal sealed class AxisFontCoverage
{
    private const string Axis12Path = "common/font/AXIS_12.fdt";

    private HashSet<char> glyphs = [];

    public bool Loaded { get; private set; }

    public int GlyphCount => this.glyphs.Count;

    public bool TryLoad(IDataManager dataManager, IPluginLog log)
    {
        try
        {
            var file = dataManager.GetFile(Axis12Path);
            if (file?.Data is not { Length: > 0 } data)
            {
                log.Warning($"NoMoreEquals: could not read {Axis12Path}");
                return false;
            }

            var reader = new FdtReader(data);
            var set = new HashSet<char>(reader.Glyphs.Count);
            foreach (var glyph in reader.Glyphs)
            {
                var c = glyph.Char;
                if (c != 0)
                    set.Add(c);
            }

            this.glyphs = set;
            this.Loaded = true;
            log.Information($"NoMoreEquals: loaded AXIS_12 coverage ({set.Count} glyphs)");
            return true;
        }
        catch (Exception ex)
        {
            log.Error(ex, "NoMoreEquals: failed to parse AXIS_12.fdt");
            return false;
        }
    }

    public bool Contains(char c) => this.glyphs.Contains(c);
}
