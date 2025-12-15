using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using FastChatFilter.Binary;

namespace FastChatFilter.Trie;

/// <summary>
/// Read-only binary trie data structure for fast word matching.
/// Loads from precompiled binary files for efficient runtime performance.
/// </summary>
internal sealed class BinaryTrie : IDisposable
{
    private readonly byte[] _data;
    private readonly int _nodeTableOffset;
    private readonly int _edgeTableOffset;
    private readonly int _rootNodeIndex;
    private readonly int _nodeCount;
    private readonly int _edgeCount;
    private bool _disposed;

    private BinaryTrie(byte[] data, int nodeCount, int edgeCount, int nodeTableOffset)
    {
        _data = data;
        _rootNodeIndex = 0; // Root is always at index 0
        _nodeCount = nodeCount;
        _edgeCount = edgeCount;
        _nodeTableOffset = nodeTableOffset;
        _edgeTableOffset = _nodeTableOffset + (_nodeCount * TrieNode.SizeInBytes);
    }

    /// <summary>
    /// Index of the root node.
    /// </summary>
    public int RootIndex => _rootNodeIndex;

    /// <summary>
    /// Number of nodes in the trie.
    /// </summary>
    public int NodeCount => _nodeCount;

    /// <summary>
    /// Number of edges in the trie.
    /// </summary>
    public int EdgeCount => _edgeCount;

    /// <summary>
    /// Loads a binary trie from a file path.
    /// </summary>
    /// <param name="path">Path to the .bin file.</param>
    /// <returns>A loaded BinaryTrie instance.</returns>
    public static BinaryTrie Load(string path)
    {
        if (string.IsNullOrEmpty(path))
            throw new ArgumentNullException(nameof(path));

        byte[] data = File.ReadAllBytes(path);
        return LoadFromBytes(data);
    }

    /// <summary>
    /// Loads a binary trie from a stream.
    /// </summary>
    /// <param name="stream">Stream containing the binary trie data.</param>
    /// <returns>A loaded BinaryTrie instance.</returns>
    public static BinaryTrie Load(Stream stream)
    {
        if (stream == null)
            throw new ArgumentNullException(nameof(stream));

        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        byte[] data = ms.ToArray();
        return LoadFromBytes(data);
    }

    /// <summary>
    /// Loads a binary trie from a byte array (standalone format).
    /// </summary>
    /// <param name="data">Byte array containing the binary trie data.</param>
    /// <returns>A loaded BinaryTrie instance.</returns>
    public static BinaryTrie LoadFromBytes(byte[] data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        if (data.Length < BinaryHeader.SizeInBytes)
            throw new InvalidDataException("Data is too small to contain a valid header.");

        var header = MemoryMarshal.Read<BinaryHeader>(data.AsSpan(0, BinaryHeader.SizeInBytes));

        if (!header.IsValid)
            throw new InvalidDataException($"Invalid magic number or unsupported version. Expected FCF3, got 0x{header.Magic:X8}");

        int expectedSize = BinaryHeader.SizeInBytes +
                          (header.NodeCount * TrieNode.SizeInBytes) +
                          (header.EdgeCount * TrieEdge.SizeInBytes);

        if (data.Length < expectedSize)
            throw new InvalidDataException($"Data size mismatch. Expected at least {expectedSize} bytes, got {data.Length}.");

        return new BinaryTrie(data, header.NodeCount, header.EdgeCount, BinaryHeader.SizeInBytes);
    }

    /// <summary>
    /// Loads a binary trie from a byte array with explicit node/edge counts (for hybrid format).
    /// </summary>
    public static BinaryTrie LoadFromBytes(byte[] data, int nodeCount, int edgeCount)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        return new BinaryTrie(data, nodeCount, edgeCount, BinaryHeader.SizeInBytes);
    }

    /// <summary>
    /// Gets a node by index.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TrieNode GetNode(int index)
    {
        if ((uint)index >= (uint)_nodeCount)
            throw new ArgumentOutOfRangeException(nameof(index));

        int offset = _nodeTableOffset + (index * TrieNode.SizeInBytes);
        return MemoryMarshal.Read<TrieNode>(_data.AsSpan(offset, TrieNode.SizeInBytes));
    }

    /// <summary>
    /// Gets the edges for a node as a read-only span.
    /// Zero allocation access to edge data.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<TrieEdge> GetEdges(int nodeIndex)
    {
        var node = GetNode(nodeIndex);
        if (node.EdgeCount == 0)
            return ReadOnlySpan<TrieEdge>.Empty;

        int offset = _edgeTableOffset + (node.FirstEdgeIndex * TrieEdge.SizeInBytes);
        int byteLength = node.EdgeCount * TrieEdge.SizeInBytes;

        return MemoryMarshal.Cast<byte, TrieEdge>(_data.AsSpan(offset, byteLength));
    }

    /// <summary>
    /// Gets the root node.
    /// </summary>
    public TrieNode Root => GetNode(_rootNodeIndex);

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
        }
    }
}
