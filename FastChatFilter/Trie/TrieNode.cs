using System.Runtime.InteropServices;

namespace FastChatFilter.Trie;

/// <summary>
/// Represents a node in the binary trie structure.
/// Fixed-size structure for efficient memory layout and binary serialization.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal readonly struct TrieNode
{
    /// <summary>
    /// Size of the structure in bytes.
    /// </summary>
    public const int SizeInBytes = 8;

    /// <summary>
    /// Flag indicating this node is a terminal (end of a word).
    /// </summary>
    private const ushort TerminalFlag = 1;

    /// <summary>
    /// Index of the first edge in the edge table.
    /// </summary>
    public readonly int FirstEdgeIndex;

    /// <summary>
    /// Number of child edges for this node.
    /// </summary>
    public readonly ushort EdgeCount;

    /// <summary>
    /// Bit flags. Bit 0: IsTerminal.
    /// </summary>
    public readonly ushort Flags;

    /// <summary>
    /// Gets whether this node marks the end of a word.
    /// </summary>
    public bool IsTerminal => (Flags & TerminalFlag) != 0;

    public TrieNode(int firstEdgeIndex, ushort edgeCount, bool isTerminal)
    {
        FirstEdgeIndex = firstEdgeIndex;
        EdgeCount = edgeCount;
        Flags = isTerminal ? TerminalFlag : (ushort)0;
    }
}
