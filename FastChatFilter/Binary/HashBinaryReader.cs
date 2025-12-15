using System;
using System.IO;
using System.Runtime.InteropServices;
using FastChatFilter.Hash;

namespace FastChatFilter.Binary;

/// <summary>
/// Reads CRC32 hash-based binary filter files.
/// </summary>
internal static class HashBinaryReader
{
    /// <summary>
    /// Load HashSet32 from file.
    /// </summary>
    public static HashSet32 Load(string path)
    {
        if (string.IsNullOrEmpty(path))
            throw new ArgumentNullException(nameof(path));

        byte[] data = File.ReadAllBytes(path);
        return LoadFromBytes(data);
    }

    /// <summary>
    /// Load HashSet32 from stream.
    /// </summary>
    public static HashSet32 Load(Stream stream)
    {
        if (stream == null)
            throw new ArgumentNullException(nameof(stream));

        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return LoadFromBytes(ms.ToArray());
    }

    /// <summary>
    /// Load HashSet32 from byte array.
    /// </summary>
    public static HashSet32 LoadFromBytes(byte[] data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        if (data.Length < BinaryHeader.SizeInBytes)
            throw new InvalidDataException("Data is too small to contain a valid header.");

        var header = MemoryMarshal.Read<BinaryHeader>(data.AsSpan(0, BinaryHeader.SizeInBytes));

        if (!header.IsValid)
            throw new InvalidDataException($"Invalid magic number or unsupported version. Expected FCF2, got 0x{header.Magic:X8}");

        // CRC32 format: header contains hash count, min/max word lengths
        int hashCount = header.HashCount;
        int minLength = header.MinWordLength;
        int maxLength = header.MaxWordLength;

        int expectedSize = BinaryHeader.SizeInBytes + (hashCount * sizeof(uint));
        if (data.Length < expectedSize)
            throw new InvalidDataException($"Data size mismatch. Expected at least {expectedSize} bytes, got {data.Length}.");

        // Read hash array
        var hashes = new uint[hashCount];
        int offset = BinaryHeader.SizeInBytes;

        for (int i = 0; i < hashCount; i++)
        {
            hashes[i] = MemoryMarshal.Read<uint>(data.AsSpan(offset, sizeof(uint)));
            offset += sizeof(uint);
        }

        return HashSet32.FromSortedHashes(hashes, minLength, maxLength);
    }
}
