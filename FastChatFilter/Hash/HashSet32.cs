using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace FastChatFilter.Hash;

/// <summary>
/// High-performance read-only hash set using CRC32 hashes.
/// Optimized for fast lookup with minimal memory allocation.
/// </summary>
internal sealed class HashSet32
{
    private readonly uint[] _hashes;
    private readonly int[] _lengthBuckets; // Min/max word lengths for quick rejection

    public int MinWordLength { get; }
    public int MaxWordLength { get; }
    public int Count => _hashes.Length;

    private HashSet32(uint[] hashes, int minLength, int maxLength)
    {
        _hashes = hashes;
        MinWordLength = minLength;
        MaxWordLength = maxLength;

        // Create length buckets for O(1) length check
        _lengthBuckets = new int[maxLength + 1];
        for (int i = minLength; i <= maxLength; i++)
        {
            _lengthBuckets[i] = 1; // Mark valid lengths
        }
    }

    /// <summary>
    /// Create HashSet32 from pre-sorted hash array and length info.
    /// </summary>
    public static HashSet32 FromSortedHashes(uint[] sortedHashes, int minLength, int maxLength)
    {
        return new HashSet32(sortedHashes, minLength, maxLength);
    }

    /// <summary>
    /// Check if a hash exists in the set using binary search.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Contains(uint hash)
    {
        return BinarySearch(hash) >= 0;
    }

    /// <summary>
    /// Check if a word length is valid (within min/max range).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsValidLength(int length)
    {
        return length >= MinWordLength && length <= MaxWordLength;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int BinarySearch(uint hash)
    {
        int left = 0;
        int right = _hashes.Length - 1;

        while (left <= right)
        {
            int mid = left + ((right - left) >> 1);
            uint midHash = _hashes[mid];

            if (midHash == hash)
                return mid;
            else if (midHash < hash)
                left = mid + 1;
            else
                right = mid - 1;
        }

        return -1;
    }
}
