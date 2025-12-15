using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
#if NET8_0_OR_GREATER
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
#endif

namespace FastChatFilter.Hash;

/// <summary>
/// High-performance CRC32 hash implementation.
/// Uses hardware acceleration (CRC32C) when available.
/// </summary>
public static class Crc32
{
    private const uint Seed = 0xFFFFFFFF;

    // CRC32 lookup table for software fallback
    private static readonly uint[] Table = GenerateTable();

    private static uint[] GenerateTable()
    {
        const uint polynomial = 0xEDB88320;
        var table = new uint[256];

        for (uint i = 0; i < 256; i++)
        {
            uint crc = i;
            for (int j = 0; j < 8; j++)
            {
                crc = (crc & 1) != 0 ? (crc >> 1) ^ polynomial : crc >> 1;
            }
            table[i] = crc;
        }

        return table;
    }

    /// <summary>
    /// Compute CRC32 hash of a character span.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint Compute(ReadOnlySpan<char> text)
    {
        if (text.IsEmpty)
            return 0;

        var bytes = MemoryMarshal.AsBytes(text);
        return ComputeBytes(bytes);
    }

    /// <summary>
    /// Compute CRC32 hash of a string.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint Compute(string text)
    {
        if (string.IsNullOrEmpty(text))
            return 0;

        return Compute(text.AsSpan());
    }

    /// <summary>
    /// Compute CRC32 hash of a byte span.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint ComputeBytes(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty)
            return 0;

#if NET8_0_OR_GREATER
        if (Sse42.IsSupported)
        {
            return ComputeHardware(data);
        }
#endif
        return ComputeSoftware(data);
    }

#if NET8_0_OR_GREATER
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint ComputeHardware(ReadOnlySpan<byte> data)
    {
        uint crc = Seed;
        int offset = 0;
        int length = data.Length;

        // Process 8 bytes at a time using CRC32C instruction
        if (Sse42.X64.IsSupported)
        {
            while (length - offset >= 8)
            {
                ulong value = MemoryMarshal.Read<ulong>(data.Slice(offset, 8));
                crc = (uint)Sse42.X64.Crc32(crc, value);
                offset += 8;
            }
        }

        // Process 4 bytes at a time
        while (length - offset >= 4)
        {
            uint value = MemoryMarshal.Read<uint>(data.Slice(offset, 4));
            crc = Sse42.Crc32(crc, value);
            offset += 4;
        }

        // Process remaining bytes
        while (offset < length)
        {
            crc = Sse42.Crc32(crc, data[offset]);
            offset++;
        }

        return crc ^ Seed;
    }
#endif

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint ComputeSoftware(ReadOnlySpan<byte> data)
    {
        uint crc = Seed;

        foreach (byte b in data)
        {
            crc = Table[(crc ^ b) & 0xFF] ^ (crc >> 8);
        }

        return crc ^ Seed;
    }
}
