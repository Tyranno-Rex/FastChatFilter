using System;
using System.Buffers;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using FastChatFilter.Binary;
using FastChatFilter.Hash;
using FastChatFilter.Matching;
using FastChatFilter.Normalization;
using FastChatFilter.Trie;

namespace FastChatFilter;

/// <summary>
/// High-performance profanity filter using Trie + CRC32 hybrid matching.
///
/// Flow:
/// 1. Sliding window over input text
/// 2. Trie traversal for fast partial matching
/// 3. When terminal node reached, verify with CRC32 hash
/// 4. Hash mismatch = false positive removal
/// 5. Hash match = confirmed profanity
///
/// Thread-safe for concurrent reads after initialization.
/// </summary>
public sealed class ProfanityFilter : IDisposable
{
    private readonly BinaryTrie _trie;
    private readonly HashSet32 _hashSet;
    private readonly HybridMatcher _matcher;
    private readonly ITextNormalizer? _normalizer;
    private readonly LoadOptions _options;
    private bool _disposed;

    private const int StackAllocThreshold = 512;
    private const int MaxMatchesPerCall = 256;

    private ProfanityFilter(
        BinaryTrie trie,
        HashSet32 hashSet,
        ITextNormalizer? normalizer,
        LoadOptions options)
    {
        _trie = trie;
        _hashSet = hashSet;
        _matcher = new HybridMatcher(trie, hashSet);
        _normalizer = normalizer;
        _options = options;
    }

    /// <summary>
    /// Load filter from binary file.
    /// </summary>
    /// <param name="binaryPath">Path to .bin file compiled by FastChatFilter.Compiler.</param>
    /// <param name="options">Load options (optional).</param>
    /// <returns>A new ProfanityFilter instance.</returns>
    public static ProfanityFilter Load(string binaryPath, LoadOptions? options = null)
    {
        if (string.IsNullOrEmpty(binaryPath))
            throw new ArgumentNullException(nameof(binaryPath));

        options ??= LoadOptions.Default;

        var (trie, hashSet) = HybridBinaryReader.Load(binaryPath);
        var normalizer = options.EnableNormalization
            ? CreateDefaultNormalizer()
            : null;

        return new ProfanityFilter(trie, hashSet, normalizer, options);
    }

    /// <summary>
    /// Load filter from stream (for embedded resources).
    /// </summary>
    /// <param name="stream">Stream containing binary data.</param>
    /// <param name="options">Load options (optional).</param>
    /// <returns>A new ProfanityFilter instance.</returns>
    public static ProfanityFilter Load(Stream stream, LoadOptions? options = null)
    {
        if (stream == null)
            throw new ArgumentNullException(nameof(stream));

        options ??= LoadOptions.Default;

        var (trie, hashSet) = HybridBinaryReader.Load(stream);
        var normalizer = options.EnableNormalization
            ? CreateDefaultNormalizer()
            : null;

        return new ProfanityFilter(trie, hashSet, normalizer, options);
    }

    /// <summary>
    /// Load filter from byte array.
    /// </summary>
    /// <param name="data">Byte array containing binary data.</param>
    /// <param name="options">Load options (optional).</param>
    /// <returns>A new ProfanityFilter instance.</returns>
    public static ProfanityFilter LoadFromBytes(byte[] data, LoadOptions? options = null)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        options ??= LoadOptions.Default;

        var (trie, hashSet) = HybridBinaryReader.LoadFromBytes(data);
        var normalizer = options.EnableNormalization
            ? CreateDefaultNormalizer()
            : null;

        return new ProfanityFilter(trie, hashSet, normalizer, options);
    }

    /// <summary>
    /// Check if text contains profanity.
    /// Uses Trie for fast traversal, CRC32 for verification.
    /// </summary>
    /// <param name="text">Text to check.</param>
    /// <returns>True if profanity is found.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Contains(ReadOnlySpan<char> text)
    {
        if (text.IsEmpty)
            return false;

        if (_normalizer == null)
        {
            return _matcher.Contains(text);
        }

        // Normalize into stack buffer for small texts
        if (text.Length <= StackAllocThreshold)
        {
            Span<char> buffer = stackalloc char[text.Length];
            int normalizedLength = _normalizer.Normalize(text, buffer);
            return _matcher.Contains(buffer.Slice(0, normalizedLength));
        }
        else
        {
            // Rent from array pool for large texts
            char[] rentedBuffer = ArrayPool<char>.Shared.Rent(text.Length);
            try
            {
                int normalizedLength = _normalizer.Normalize(text, rentedBuffer);
                return _matcher.Contains(rentedBuffer.AsSpan(0, normalizedLength));
            }
            finally
            {
                ArrayPool<char>.Shared.Return(rentedBuffer);
            }
        }
    }

    /// <summary>
    /// Check if text contains profanity (string overload).
    /// </summary>
    /// <param name="text">Text to check.</param>
    /// <returns>True if profanity is found.</returns>
    public bool Contains(string text)
    {
        if (string.IsNullOrEmpty(text))
            return false;
        return Contains(text.AsSpan());
    }

    /// <summary>
    /// Mask profanity in text.
    /// Returns new string with profanity replaced by mask character.
    /// </summary>
    /// <param name="text">Text to mask.</param>
    /// <param name="maskChar">Character to use for masking. Default: '*'</param>
    /// <param name="options">Masking options (optional).</param>
    /// <returns>Text with profanity masked.</returns>
    public string Mask(string text, char maskChar = '*', MaskOptions? options = null)
    {
        if (string.IsNullOrEmpty(text))
            return text ?? string.Empty;

        options ??= MaskOptions.Default;

        ReadOnlySpan<char> span = text.AsSpan();
        ReadOnlySpan<char> normalizedSpan = span;
        char[]? normalizedBuffer = null;

        if (_normalizer != null)
        {
            normalizedBuffer = ArrayPool<char>.Shared.Rent(text.Length);
            int len = _normalizer.Normalize(span, normalizedBuffer);
            normalizedSpan = normalizedBuffer.AsSpan(0, len);
        }

        try
        {
            // Find all matches
            Span<MatchResult> matches = stackalloc MatchResult[MaxMatchesPerCall];
            int matchCount = _matcher.FindAll(normalizedSpan, matches);

            if (matchCount == 0)
                return text;

            // Build result with masking
            return BuildMaskedString(text, matches.Slice(0, matchCount), maskChar, options);
        }
        finally
        {
            if (normalizedBuffer != null)
            {
                ArrayPool<char>.Shared.Return(normalizedBuffer);
            }
        }
    }

    /// <summary>
    /// Get all matches in text.
    /// </summary>
    /// <param name="text">Text to search.</param>
    /// <param name="results">Buffer to store match results.</param>
    /// <returns>Number of matches found.</returns>
    public int FindMatches(ReadOnlySpan<char> text, Span<MatchResult> results)
    {
        if (text.IsEmpty || results.IsEmpty)
            return 0;

        if (_normalizer == null)
        {
            return _matcher.FindAll(text, results);
        }

        if (text.Length <= StackAllocThreshold)
        {
            Span<char> buffer = stackalloc char[text.Length];
            int normalizedLength = _normalizer.Normalize(text, buffer);
            return _matcher.FindAll(buffer.Slice(0, normalizedLength), results);
        }
        else
        {
            char[] rentedBuffer = ArrayPool<char>.Shared.Rent(text.Length);
            try
            {
                int normalizedLength = _normalizer.Normalize(text, rentedBuffer);
                return _matcher.FindAll(rentedBuffer.AsSpan(0, normalizedLength), results);
            }
            finally
            {
                ArrayPool<char>.Shared.Return(rentedBuffer);
            }
        }
    }

    private static string BuildMaskedString(
        string original,
        ReadOnlySpan<MatchResult> matches,
        char maskChar,
        MaskOptions options)
    {
        if (options.FixedMask != null)
        {
            return BuildMaskedStringWithFixedMask(original, matches, options.FixedMask);
        }

        return BuildMaskedStringPreserveLength(original, matches, maskChar);
    }

    private static string BuildMaskedStringPreserveLength(
        string original,
        ReadOnlySpan<MatchResult> matches,
        char maskChar)
    {
#if NET8_0_OR_GREATER
        var matchArray = matches.ToArray();
        return string.Create(original.Length, (Original: original, Matches: matchArray, MaskChar: maskChar),
            static (span, state) =>
            {
                state.Original.AsSpan().CopyTo(span);
                foreach (var match in state.Matches)
                {
                    for (int i = match.StartIndex; i < match.EndIndex && i < span.Length; i++)
                    {
                        span[i] = state.MaskChar;
                    }
                }
            });
#else
        var sb = new StringBuilder(original);
        foreach (var match in matches)
        {
            for (int i = match.StartIndex; i < match.EndIndex && i < sb.Length; i++)
            {
                sb[i] = maskChar;
            }
        }
        return sb.ToString();
#endif
    }

    private static string BuildMaskedStringWithFixedMask(
        string original,
        ReadOnlySpan<MatchResult> matches,
        string fixedMask)
    {
        var sb = new StringBuilder();
        int lastEnd = 0;

        foreach (var match in matches)
        {
            if (match.StartIndex > lastEnd)
            {
                sb.Append(original, lastEnd, match.StartIndex - lastEnd);
            }
            sb.Append(fixedMask);
            lastEnd = match.EndIndex;
        }

        if (lastEnd < original.Length)
        {
            sb.Append(original, lastEnd, original.Length - lastEnd);
        }

        return sb.ToString();
    }

    private static ITextNormalizer CreateDefaultNormalizer()
    {
        return new LowercaseNormalizer();
    }

    /// <summary>
    /// Releases resources used by this filter.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _trie.Dispose();
            _disposed = true;
        }
    }
}
