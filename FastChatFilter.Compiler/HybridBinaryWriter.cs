using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace FastChatFilter.Compiler;

/// <summary>
/// Writes hybrid data (Trie + CRC32 hashes) to binary format.
///
/// File Layout:
/// [Header: 32 bytes]
/// [Trie Nodes: NodeCount * 8 bytes]
/// [Trie Edges: EdgeCount * 8 bytes]
/// [CRC32 Hashes: HashCount * 4 bytes]
/// </summary>
internal static class HybridBinaryWriter
{
    /// <summary>
    /// Magic number "FCF3" in little-endian for Hybrid format.
    /// </summary>
    private const int MagicValue = 0x33464346; // "FCF3"

    /// <summary>
    /// Current binary format version.
    /// </summary>
    private const ushort CurrentVersion = 3;

    /// <summary>
    /// Write hybrid data to a stream.
    /// </summary>
    public static async Task WriteAsync(Stream stream, HybridBuilder builder)
    {
        var (nodes, edges, hashes) = builder.Build();

        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);

        // Write header
        WriteHeader(writer, nodes.Length, edges.Length, hashes.Length,
                    builder.MinWordLength, builder.MaxWordLength);

        // Write Trie nodes
        foreach (var node in nodes)
        {
            writer.Write(node.FirstEdgeIndex);
            writer.Write(node.EdgeCount);
            writer.Write((ushort)(node.IsTerminal ? 1 : 0));
        }

        // Write Trie edges
        foreach (var edge in edges)
        {
            writer.Write((ushort)edge.Character); // Fixed 2 bytes
            writer.Write((ushort)0);              // Padding
            writer.Write(edge.ChildNodeIndex);
        }

        // Write CRC32 hashes (sorted)
        foreach (var hash in hashes)
        {
            writer.Write(hash);
        }

        await stream.FlushAsync();
    }

    /// <summary>
    /// Write hybrid data to a file.
    /// </summary>
    public static async Task WriteAsync(string path, HybridBuilder builder)
    {
        await using var stream = File.Create(path);
        await WriteAsync(stream, builder);
    }

    private static void WriteHeader(BinaryWriter writer, int nodeCount, int edgeCount,
                                    int hashCount, int minWordLength, int maxWordLength)
    {
        writer.Write(MagicValue);           // 4 bytes - Magic "FCF3"
        writer.Write(CurrentVersion);       // 2 bytes - Version
        writer.Write((ushort)0);            // 2 bytes - Flags
        writer.Write(nodeCount);            // 4 bytes - NodeCount
        writer.Write(edgeCount);            // 4 bytes - EdgeCount
        writer.Write(hashCount);            // 4 bytes - HashCount
        writer.Write(minWordLength);        // 4 bytes - MinWordLength
        writer.Write(maxWordLength);        // 4 bytes - MaxWordLength
        // Total: 28 bytes, need 4 more for 32 byte header
        writer.Write(0);                    // 4 bytes - Reserved
    }
}
