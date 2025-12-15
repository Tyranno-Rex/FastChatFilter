using System;
using System.IO;
using System.Runtime.InteropServices;
using FastChatFilter.Hash;
using FastChatFilter.Trie;

namespace FastChatFilter.Binary;

/// <summary>
/// Reads hybrid binary filter files (Trie + CRC32 hash).
/// </summary>
internal static class HybridBinaryReader
{
    /// <summary>
    /// Load hybrid data from file.
    /// </summary>
    public static (BinaryTrie Trie, HashSet32 HashSet) Load(string path)
    {
        if (string.IsNullOrEmpty(path))
            throw new ArgumentNullException(nameof(path));

        byte[] data = File.ReadAllBytes(path);
        return LoadFromBytes(data);
    }

    /// <summary>
    /// Load hybrid data from stream.
    /// </summary>
    public static (BinaryTrie Trie, HashSet32 HashSet) Load(Stream stream)
    {
        if (stream == null)
            throw new ArgumentNullException(nameof(stream));

        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return LoadFromBytes(ms.ToArray());
    }

    /// <summary>
    /// Load hybrid data from byte array.
    /// </summary>
    public static (BinaryTrie Trie, HashSet32 HashSet) LoadFromBytes(byte[] data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        if (data.Length < BinaryHeader.SizeInBytes)
            throw new InvalidDataException("Data is too small to contain a valid header.");

        var header = MemoryMarshal.Read<BinaryHeader>(data.AsSpan(0, BinaryHeader.SizeInBytes));

        if (!header.IsValid)
            throw new InvalidDataException($"Invalid magic number or unsupported version. Expected FCF3, got 0x{header.Magic:X8}");

        int trieDataSize = (header.NodeCount * TrieNode.SizeInBytes) + (header.EdgeCount * TrieEdge.SizeInBytes);
        int hashDataSize = header.HashCount * sizeof(uint);
        int expectedSize = BinaryHeader.SizeInBytes + trieDataSize + hashDataSize;

        if (data.Length < expectedSize)
            throw new InvalidDataException($"Data size mismatch. Expected at least {expectedSize} bytes, got {data.Length}.");

        // Load Trie
        var trie = BinaryTrie.LoadFromBytes(data, header.NodeCount, header.EdgeCount);

        // Load Hash Set
        int hashOffset = BinaryHeader.SizeInBytes + trieDataSize;
        var hashes = new uint[header.HashCount];

        for (int i = 0; i < header.HashCount; i++)
        {
            hashes[i] = MemoryMarshal.Read<uint>(data.AsSpan(hashOffset + (i * sizeof(uint)), sizeof(uint)));
        }

        var hashSet = HashSet32.FromSortedHashes(hashes, header.MinWordLength, header.MaxWordLength);

        return (trie, hashSet);
    }
}
