using System.Runtime.InteropServices;

namespace FastChatFilter.Binary;

/// <summary>
/// Binary file header structure for FastChatFilter dictionary files.
/// Version 3: Hybrid format (Trie + CRC32 hash verification).
///
/// File Layout:
/// [Header: 32 bytes]
/// [Trie Nodes: NodeCount * 8 bytes]
/// [Trie Edges: EdgeCount * 8 bytes]
/// [CRC32 Hashes: HashCount * 4 bytes]
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal readonly struct BinaryHeader
{
    /// <summary>
    /// Magic number "FCF3" in little-endian (0x33464346) for Hybrid format.
    /// </summary>
    public const int MagicValue = 0x33464346; // "FCF3"

    /// <summary>
    /// Current binary format version.
    /// </summary>
    public const ushort CurrentVersion = 3;

    /// <summary>
    /// Size of the header in bytes.
    /// </summary>
    public const int SizeInBytes = 32;

    /// <summary>
    /// Magic number for file validation.
    /// </summary>
    public readonly int Magic;

    /// <summary>
    /// Format version number.
    /// </summary>
    public readonly ushort Version;

    /// <summary>
    /// Reserved flags for future use.
    /// </summary>
    public readonly ushort Flags;

    /// <summary>
    /// Number of nodes in the trie.
    /// </summary>
    public readonly int NodeCount;

    /// <summary>
    /// Number of edges in the trie.
    /// </summary>
    public readonly int EdgeCount;

    /// <summary>
    /// Number of CRC32 hashes for verification.
    /// </summary>
    public readonly int HashCount;

    /// <summary>
    /// Minimum word length in the dictionary.
    /// </summary>
    public readonly int MinWordLength;

    /// <summary>
    /// Maximum word length in the dictionary.
    /// </summary>
    public readonly int MaxWordLength;

    /// <summary>
    /// Reserved for future use (to make header 32 bytes).
    /// </summary>
    private readonly int _reserved;

    public BinaryHeader(int nodeCount, int edgeCount, int hashCount, int minWordLength, int maxWordLength, ushort flags = 0)
    {
        Magic = MagicValue;
        Version = CurrentVersion;
        Flags = flags;
        NodeCount = nodeCount;
        EdgeCount = edgeCount;
        HashCount = hashCount;
        MinWordLength = minWordLength;
        MaxWordLength = maxWordLength;
        _reserved = 0;
    }

    public bool IsValid => Magic == MagicValue && Version <= CurrentVersion;
}
