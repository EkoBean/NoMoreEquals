namespace NoMoreEquals.Services;

/// <summary>
/// The single answer to "may this plugin act right now, and on this line?".
/// <para>
/// Exists to stop the same question being answered in three places. Before this type,
/// <c>NoRulesActive</c> had two independent implementations — one on
/// <see cref="ChatInputWatcher"/>, one written out by hand inside
/// <see cref="ChatPreviewOverlay"/> — and the slash command whitelist would have made
/// that three. Each call site now keeps only the condition that is genuinely its own
/// (a re-entrancy flag, the IME gate, a display setting).
/// </para>
/// <para>
/// Deliberately not consulted on the pending-insert flush path. Once a commit has been
/// suppressed the original is gone from the game, so the queued text is the only copy of
/// what the player typed; the gate closing mid-flight must fall back to that original,
/// never discard it. See <see cref="ChatInputWatcher"/>.
/// </para>
/// </summary>
internal sealed class ConversionGate
{
    private readonly Configuration configuration;
    private readonly KanjiMapService mapService;
    private readonly PhraseReplacementService phraseService;

    public ConversionGate(
        Configuration configuration,
        KanjiMapService mapService,
        PhraseReplacementService phraseService)
    {
        this.configuration = configuration;
        this.mapService = mapService;
        this.phraseService = phraseService;
    }

    /// <summary>Conversion is switched on and at least one rule could fire.</summary>
    public bool IsArmed
        => this.configuration.Enabled
           && (this.mapService.ActiveCount > 0 || this.phraseService.EnabledCount > 0);

    /// <summary>
    /// <see cref="IsArmed"/>, and <paramref name="line"/> is one the plugin is allowed to
    /// touch at all — an ordinary line, or a chat channel command that has reached its
    /// message body.
    /// </summary>
    public bool ShouldConvert(string? line)
        => this.IsArmed && ChatLineScope.Of(line).Convertible;
}
