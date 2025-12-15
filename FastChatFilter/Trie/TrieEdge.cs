using System.Runtime.InteropServices;

namespace FastChatFilter.Trie;

/// <summary>
/// Represents an edge (transition) in the binary trie structure.
/// Fixed-size structure for efficient memory layout and binary serialization.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal readonly struct TrieEdge
{
    /// <summary>
    /// Size of the structure in bytes.
    /// </summary>
    public const int SizeInBytes = 8;

    /// <summary>
    /// The character label for this edge (UTF-16).
    /// </summary>
    public readonly char Character;

    /// <summary>
    /// Reserved for alignment padding.
    /// </summary>
    private readonly ushort _reserved;

    /// <summary>
    /// Index of the child node in the node table.
    /// </summary>
    public readonly int ChildNodeIndex;

    public TrieEdge(char character, int childNodeIndex)
    {
        Character = character;
        _reserved = 0;
        ChildNodeIndex = childNodeIndex;
    }
}
