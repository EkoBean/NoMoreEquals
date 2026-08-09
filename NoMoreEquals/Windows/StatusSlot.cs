namespace NoMoreEquals.Windows;

/// <summary>
/// Where a piece of feedback belongs on screen. Feedback used to be rendered in one place
/// at the bottom of the window regardless of what produced it, which put an error about
/// the phrase form as far from the button that raised it as the layout allows.
/// </summary>
internal enum StatusSlot
{
    /// <summary>Bottom of the config window. Anything without a home of its own.</summary>
    Window,

    /// <summary>Above the phrase section's add button.</summary>
    Phrase,

    /// <summary>Above the glyph section's add button.</summary>
    Glyph,
}
