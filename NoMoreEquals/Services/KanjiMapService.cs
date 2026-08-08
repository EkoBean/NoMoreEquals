using System.Collections.Generic;
using System.Linq;
using NoMoreEquals.Data;

namespace NoMoreEquals.Services;

/// <summary>
/// Builds the effective zh→jp map:
/// AXIS-filtered OpenCC/variant built-ins + user/custom (incl. seeded defaults).
/// </summary>
internal sealed class KanjiMapService
{
    private Dictionary<char, char> active = new();

    public IReadOnlyDictionary<char, char> Active => this.active;

    public int ActiveCount => this.active.Count;

    public void Rebuild(Configuration config, AxisFontCoverage? coverage)
    {
        Dictionary<char, char> map;

        if (config.UseLiveFontFilter && coverage is { Loaded: true })
        {
            map = KanjiCandidates.Entries
                .Where(kv => !coverage.Contains(kv.Key) && coverage.Contains(kv.Value))
                .ToDictionary(kv => kv.Key, kv => kv.Value);
        }
        else
        {
            map = new Dictionary<char, char>(KanjiMap.Entries);
        }

        foreach (var (from, to) in config.CustomMappings)
        {
            if (TryParseSingleChar(from, out var zh) && TryParseSingleChar(to, out var jp) && zh != jp)
                map[zh] = jp;
        }

        this.active = map;
    }

    public static bool TryParseSingleChar(string? text, out char c)
    {
        c = default;
        if (string.IsNullOrEmpty(text))
            return false;

        var enumerator = text.EnumerateRunes();
        if (!enumerator.MoveNext())
            return false;

        var rune = enumerator.Current;
        if (enumerator.MoveNext())
            return false;

        if (rune.Value > 0xFFFF)
            return false;

        c = (char)rune.Value;
        return true;
    }
}
