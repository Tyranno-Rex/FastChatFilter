using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace FastChatFilter.Compiler;

/// <summary>
/// Writes trie data to binary format.
/// </summary>
internal static class BinaryTrieWriter
{
    /// <summary>
    /// Magic number "FCF1" in little-endian.
    /// </summary>
    private const int MagicValue = 0x31464346;

    /// <summary>
    /// Current binary format version.
    /// </summary>
    private const ushort CurrentVersion = 1;

    /// <summary>
    /// Header size in bytes.
    /// </summary>
    private const int HeaderSize = 32;

    /// <summary>
    /// Write trie data to a stream.
    /// </summary>
    public static async Task WriteAsync(Stream stream, TrieBuilder builder)
    {
        var (nodes, edges) = builder.Build();

        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);

        // Write header
        WriteHeader(writer, nodes.Length, edges.Length);

        // Write nodes
        foreach (var node in nodes)
        {
            writer.Write(node.FirstEdgeIndex);
            writer.Write(node.EdgeCount);
            writer.Write((ushort)(node.IsTerminal ? 1 : 0));
        }

        // Write edges
        foreach (var edge in edges)
        {
            // Write character as ushort (2 bytes, fixed size) instead of char (variable UTF-8)
            writer.Write((ushort)edge.Character);
            writer.Write((ushort)0); // Reserved padding
            writer.Write(edge.ChildNodeIndex);
        }

        await stream.FlushAsync();
    }

    /// <summary>
    /// Write trie data to a file.
    /// </summary>
    public static async Task WriteAsync(string path, TrieBuilder builder)
    {
        await using var stream = File.Create(path);
        await WriteAsync(stream, builder);
    }

    private static void WriteHeader(BinaryWriter writer, int nodeCount, int edgeCount)
    {
        writer.Write(MagicValue);           // 4 bytes - Magic
        writer.Write(CurrentVersion);       // 2 bytes - Version
        writer.Write((ushort)0);            // 2 bytes - Flags
        writer.Write(nodeCount);            // 4 bytes - NodeCount
        writer.Write(edgeCount);            // 4 bytes - EdgeCount
        writer.Write(0);                    // 4 bytes - RootNodeIndex (always 0)

        // Reserved bytes (12 bytes to make header 32 bytes total)
        writer.Write(0);                    // 4 bytes
        writer.Write(0);                    // 4 bytes
        writer.Write(0);                    // 4 bytes
    }
}
