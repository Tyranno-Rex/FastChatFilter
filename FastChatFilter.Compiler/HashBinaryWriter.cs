using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace FastChatFilter.Compiler;

/// <summary>
/// Writes CRC32 hash data to binary format.
/// </summary>
internal static class HashBinaryWriter
{
    /// <summary>
    /// Magic number "FCF2" in little-endian for CRC32 hash format.
    /// </summary>
    private const int MagicValue = 0x32464346; // "FCF2"

    /// <summary>
    /// Current binary format version.
    /// </summary>
    private const ushort CurrentVersion = 2;

    /// <summary>
    /// Header size in bytes.
    /// </summary>
    private const int HeaderSize = 32;

    /// <summary>
    /// Write hash data to a stream.
    /// </summary>
    public static async Task WriteAsync(Stream stream, HashBuilder builder)
    {
        var hashes = builder.Build();

        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);

        // Write header
        WriteHeader(writer, hashes.Length, builder.MinWordLength, builder.MaxWordLength);

        // Write sorted hashes
        foreach (var hash in hashes)
        {
            writer.Write(hash);
        }

        await stream.FlushAsync();
    }

    /// <summary>
    /// Write hash data to a file.
    /// </summary>
    public static async Task WriteAsync(string path, HashBuilder builder)
    {
        await using var stream = File.Create(path);
        await WriteAsync(stream, builder);
    }

    private static void WriteHeader(BinaryWriter writer, int hashCount, int minWordLength, int maxWordLength)
    {
        writer.Write(MagicValue);           // 4 bytes - Magic "FCF2"
        writer.Write(CurrentVersion);       // 2 bytes - Version
        writer.Write((ushort)0);            // 2 bytes - Flags
        writer.Write(hashCount);            // 4 bytes - HashCount
        writer.Write(minWordLength);        // 4 bytes - MinWordLength
        writer.Write(maxWordLength);        // 4 bytes - MaxWordLength

        // Reserved bytes (12 bytes to make header 32 bytes total)
        writer.Write(0);                    // 4 bytes
        writer.Write(0);                    // 4 bytes
        writer.Write(0);                    // 4 bytes
    }
}
