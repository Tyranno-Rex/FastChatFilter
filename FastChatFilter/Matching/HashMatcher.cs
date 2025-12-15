using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using FastChatFilter.Hash;
using FastChatFilter.Normalization;

namespace FastChatFilter.Matching;

/// <summary>
/// High-performance matcher using CRC32 hash lookup.
/// Scans text for all possible substrings and checks against hash set.
/// </summary>
internal sealed class HashMatcher
{
    private readonly HashSet32 _hashSet;
    private readonly ITextNormalizer? _normalizer;

    public HashMatcher(HashSet32 hashSet, ITextNormalizer? normalizer = null)
    {
        _hashSet = hashSet ?? throw new ArgumentNullException(nameof(hashSet));
        _normalizer = normalizer;
    }

    /// <summary>
    /// Check if text contains any words in the hash set.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Contains(ReadOnlySpan<char> text)
    {
        if (text.IsEmpty)
            return false;

        int minLen = _hashSet.MinWordLength;
        int maxLen = _hashSet.MaxWordLength;

        // Quick rejection: text shorter than minimum word
        if (text.Length < minLen)
            return false;

        // Scan all possible starting positions
        for (int start = 0; start <= text.Length - minLen; start++)
        {
            // Check all possible lengths from this position
            int maxPossibleLen = Math.Min(maxLen, text.Length - start);

            for (int len = minLen; len <= maxPossibleLen; len++)
            {
                var substring = text.Slice(start, len);
                uint hash = ComputeHash(substring);

                if (_hashSet.Contains(hash))
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Find all matches in text.
    /// </summary>
    public int FindAll(ReadOnlySpan<char> text, Span<MatchResult> results)
    {
        if (text.IsEmpty || results.IsEmpty)
            return 0;

        int matchCount = 0;
        int minLen = _hashSet.MinWordLength;
        int maxLen = _hashSet.MaxWordLength;

        if (text.Length < minLen)
            return 0;

        int start = 0;
        while (start <= text.Length - minLen && matchCount < results.Length)
        {
            int matchedLength = 0;

            // Find longest match at this position
            int maxPossibleLen = Math.Min(maxLen, text.Length - start);
            for (int len = maxPossibleLen; len >= minLen; len--)
            {
                var substring = text.Slice(start, len);
                uint hash = ComputeHash(substring);

                if (_hashSet.Contains(hash))
                {
                    matchedLength = len;
                    break; // Found longest match
                }
            }

            if (matchedLength > 0)
            {
                results[matchCount++] = new MatchResult(start, matchedLength);
                start += matchedLength; // Skip past match
            }
            else
            {
                start++;
            }
        }

        return matchCount;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private uint ComputeHash(ReadOnlySpan<char> text)
    {
        if (_normalizer == null)
        {
            return Crc32.Compute(text);
        }

        // Normalize before hashing
        const int StackAllocThreshold = 128;

        if (text.Length <= StackAllocThreshold)
        {
            Span<char> buffer = stackalloc char[text.Length];
            int len = _normalizer.Normalize(text, buffer);
            return Crc32.Compute(buffer.Slice(0, len));
        }
        else
        {
            char[] rentedBuffer = ArrayPool<char>.Shared.Rent(text.Length);
            try
            {
                int len = _normalizer.Normalize(text, rentedBuffer);
                return Crc32.Compute(rentedBuffer.AsSpan(0, len));
            }
            finally
            {
                ArrayPool<char>.Shared.Return(rentedBuffer);
            }
        }
    }
}
