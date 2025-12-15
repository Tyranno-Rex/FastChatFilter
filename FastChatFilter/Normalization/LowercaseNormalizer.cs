using System;

namespace FastChatFilter.Normalization;

/// <summary>
/// Normalizer that converts text to lowercase.
/// Uses invariant culture for consistent results across locales.
/// </summary>
internal sealed class LowercaseNormalizer : ITextNormalizer
{
    /// <summary>
    /// Normalize input text to lowercase into output buffer.
    /// </summary>
    public int Normalize(ReadOnlySpan<char> input, Span<char> output)
    {
        if (output.Length < input.Length)
            throw new ArgumentException("Output buffer is too small.", nameof(output));

        for (int i = 0; i < input.Length; i++)
        {
            output[i] = char.ToLowerInvariant(input[i]);
        }

        return input.Length;
    }

    /// <summary>
    /// Normalize string to lowercase (allocates new string).
    /// </summary>
    public string Normalize(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input ?? string.Empty;

        return input.ToLowerInvariant();
    }
}
