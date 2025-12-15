using System;

namespace FastChatFilter.Normalization;

/// <summary>
/// Interface for text normalization.
/// Implementations should minimize allocations.
/// </summary>
internal interface ITextNormalizer
{
    /// <summary>
    /// Normalize input text into output buffer.
    /// </summary>
    /// <param name="input">Input text to normalize.</param>
    /// <param name="output">Output buffer (must be at least input.Length).</param>
    /// <returns>Number of characters written to output.</returns>
    int Normalize(ReadOnlySpan<char> input, Span<char> output);

    /// <summary>
    /// Normalize string (allocates new string).
    /// Used primarily for compile-time normalization.
    /// </summary>
    /// <param name="input">Input string to normalize.</param>
    /// <returns>Normalized string.</returns>
    string Normalize(string input);
}
