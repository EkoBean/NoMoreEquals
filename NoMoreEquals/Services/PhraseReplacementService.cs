using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NoMoreEquals.Services;

/// <summary>
/// Applies user phrase replacements, longest match first, left-to-right, non-overlapping.
/// </summary>
internal sealed class PhraseReplacementService
{
    private List<PhraseReplacement> ordered = [];

    public int EnabledCount => this.ordered.Count;

    /// <summary>Active (enabled) phrase rules, longest-match order.</summary>
    public IReadOnlyList<PhraseReplacement> Active => this.ordered;

    public void Rebuild(Configuration config)
    {
        this.ordered = (config.PhraseReplacements ?? [])
            .Where(p => p.Enabled
                        && !string.IsNullOrEmpty(p.From)
                        && p.To is not null
                        && !string.Equals(p.From, p.To, StringComparison.Ordinal))
            .OrderByDescending(p => p.From.Length)
            .ThenBy(p => p.From, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// Replace phrases. Returns the original string instance when nothing changes.
    /// </summary>
    public string Apply(string input)
    {
        if (string.IsNullOrEmpty(input) || this.ordered.Count == 0)
            return input;

        StringBuilder? sb = null;
        var i = 0;
        while (i < input.Length)
        {
            var matched = false;
            foreach (var rule in this.ordered)
            {
                var from = rule.From;
                if (from.Length == 0 || i + from.Length > input.Length)
                    continue;

                if (string.CompareOrdinal(input, i, from, 0, from.Length) != 0)
                    continue;

                sb ??= new StringBuilder(input.Length).Append(input, 0, i);
                sb.Append(rule.To);
                i += from.Length;
                matched = true;
                break;
            }

            if (matched)
                continue;

            sb?.Append(input[i]);
            i++;
        }

        return sb?.ToString() ?? input;
    }
}
