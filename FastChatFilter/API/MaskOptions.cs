namespace FastChatFilter;

/// <summary>
/// Options for masking profanity in text.
/// </summary>
public sealed class MaskOptions
{
    /// <summary>
    /// Default masking options.
    /// </summary>
    public static MaskOptions Default { get; } = new();

    /// <summary>
    /// Preserve original string length when masking.
    /// When true, each character is replaced with the mask character.
    /// When false, the entire match is replaced with a single mask character.
    /// Default: true
    /// </summary>
    public bool PreserveLength { get; init; } = true;

    /// <summary>
    /// Fixed mask string to use instead of repeating the mask character.
    /// When set, this string replaces each match regardless of match length.
    /// Example: "***" for all matches regardless of length.
    /// Default: null (use maskChar repeated for match length)
    /// </summary>
    public string? FixedMask { get; init; }
}
