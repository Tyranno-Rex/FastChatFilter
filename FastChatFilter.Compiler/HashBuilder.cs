using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace FastChatFilter.Compiler;

/// <summary>
/// Builds a CRC32 hash set from a list of words.
/// </summary>
internal sealed class HashBuilder
{
    private readonly HashSet<uint> _hashes = new();
    private readonly List<int> _wordLengths = new();

    /// <summary>
    /// Number of unique hashes.
    /// </summary>
    public int HashCount => _hashes.Count;

    /// <summary>
    /// Minimum word length.
    /// </summary>
    public int MinWordLength => _wordLengths.Count > 0 ? _wordLengths.Min() : 0;

    /// <summary>
    /// Maximum word length.
    /// </summary>
    public int MaxWordLength => _wordLengths.Count > 0 ? _wordLengths.Max() : 0;

    /// <summary>
    /// Add a word to the hash set.
    /// </summary>
    public void Add(string word)
    {
        if (string.IsNullOrEmpty(word))
            return;

        uint hash = ComputeCrc32(word);
        if (_hashes.Add(hash))
        {
            _wordLengths.Add(word.Length);
        }
    }

    /// <summary>
    /// Add multiple words.
    /// </summary>
    public void AddRange(IEnumerable<string> words)
    {
        foreach (var word in words)
        {
            Add(word);
        }
    }

    /// <summary>
    /// Build sorted hash array for binary output.
    /// </summary>
    public uint[] Build()
    {
        var sorted = _hashes.ToArray();
        Array.Sort(sorted);
        return sorted;
    }

    /// <summary>
    /// Compute CRC32 hash of a string.
    /// </summary>
    private static uint ComputeCrc32(string text)
    {
        var bytes = MemoryMarshal.AsBytes(text.AsSpan());
        return ComputeCrc32Bytes(bytes);
    }

    private static uint ComputeCrc32Bytes(ReadOnlySpan<byte> data)
    {
        const uint seed = 0xFFFFFFFF;
        const uint polynomial = 0xEDB88320;

        uint crc = seed;

        foreach (byte b in data)
        {
            crc ^= b;
            for (int i = 0; i < 8; i++)
            {
                crc = (crc & 1) != 0 ? (crc >> 1) ^ polynomial : crc >> 1;
            }
        }

        return crc ^ seed;
    }
}
