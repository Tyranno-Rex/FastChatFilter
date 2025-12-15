using System;
using System.Collections.Generic;
using System.Linq;
using FastChatFilter.Hash;

namespace FastChatFilter.Compiler;

/// <summary>
/// Builds hybrid data structure (Trie + CRC32 hashes) from a list of words.
/// </summary>
internal sealed class HybridBuilder
{
    private readonly List<BuilderNode> _nodes = new();
    private readonly HashSet<uint> _hashes = new();
    private readonly List<int> _wordLengths = new();

    public HybridBuilder()
    {
        // Root node at index 0
        _nodes.Add(new BuilderNode());
    }

    /// <summary>
    /// Number of nodes in the trie.
    /// </summary>
    public int NodeCount => _nodes.Count;

    /// <summary>
    /// Total number of edges in the trie.
    /// </summary>
    public int EdgeCount => _nodes.Sum(n => n.Children.Count);

    /// <summary>
    /// Number of unique hashes.
    /// </summary>
    public int HashCount => _hashes.Count;

    /// <summary>
    /// Minimum word length.
    /// </summary>
    public int MinWordLength => _wordLengths.Count > 0 ? _wordLengths.Min() : 0;

    /// <summary>
    /// Maximum word length.
    /// </summary>
    public int MaxWordLength => _wordLengths.Count > 0 ? _wordLengths.Max() : 0;

    /// <summary>
    /// Add a word to both trie and hash set.
    /// </summary>
    public void Add(string word)
    {
        if (string.IsNullOrEmpty(word))
            return;

        // Add to Trie
        int currentIndex = 0; // Root

        foreach (char c in word)
        {
            var current = _nodes[currentIndex];

            if (!current.Children.TryGetValue(c, out int childIndex))
            {
                childIndex = _nodes.Count;
                _nodes.Add(new BuilderNode());
                current.Children[c] = childIndex;
            }

            currentIndex = childIndex;
        }

        _nodes[currentIndex].IsTerminal = true;

        // Add to Hash Set
        uint hash = ComputeCrc32(word);
        if (_hashes.Add(hash))
        {
            _wordLengths.Add(word.Length);
        }
    }

    /// <summary>
    /// Add multiple words.
    /// </summary>
    public void AddRange(IEnumerable<string> words)
    {
        foreach (var word in words)
        {
            Add(word);
        }
    }

    /// <summary>
    /// Build the final node, edge, and hash arrays.
    /// </summary>
    public (TrieNodeData[] Nodes, TrieEdgeData[] Edges, uint[] Hashes) Build()
    {
        // Build Trie
        var nodes = new TrieNodeData[_nodes.Count];
        var allEdges = new List<TrieEdgeData>();

        for (int i = 0; i < _nodes.Count; i++)
        {
            var builderNode = _nodes[i];
            var sortedChildren = builderNode.Children
                .OrderBy(kvp => kvp.Key)
                .ToList();

            int firstEdgeIndex = allEdges.Count;

            foreach (var (c, childIndex) in sortedChildren)
            {
                allEdges.Add(new TrieEdgeData(c, childIndex));
            }

            nodes[i] = new TrieNodeData(
                firstEdgeIndex,
                (ushort)sortedChildren.Count,
                builderNode.IsTerminal);
        }

        // Build Hash Set (sorted for binary search)
        var hashes = _hashes.ToArray();
        Array.Sort(hashes);

        return (nodes, allEdges.ToArray(), hashes);
    }

    /// <summary>
    /// Compute CRC32 hash of a string using the runtime Crc32 class.
    /// </summary>
    private static uint ComputeCrc32(string text)
    {
        return Crc32.Compute(text);
    }

    private sealed class BuilderNode
    {
        public Dictionary<char, int> Children { get; } = new();
        public bool IsTerminal { get; set; }
    }
}
