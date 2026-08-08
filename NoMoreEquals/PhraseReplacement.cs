using System;

namespace NoMoreEquals;

/// <summary>A single user-defined wording replacement applied before glyph conversion.</summary>
[Serializable]
public sealed class PhraseReplacement
{
    public string From { get; set; } = string.Empty;

    public string To { get; set; } = string.Empty;

    public bool Enabled { get; set; } = true;
}
