namespace FastChatFilter;

/// <summary>
/// Options for loading a ProfanityFilter.
/// </summary>
public sealed class LoadOptions
{
    /// <summary>
    /// Default options with normalization enabled.
    /// </summary>
    public static LoadOptions Default { get; } = new();

    /// <summary>
    /// Enable text normalization (lowercase conversion).
    /// When enabled, both the dictionary words and input text are normalized before matching.
    /// Default: true
    /// </summary>
    public bool EnableNormalization { get; init; } = true;
}
