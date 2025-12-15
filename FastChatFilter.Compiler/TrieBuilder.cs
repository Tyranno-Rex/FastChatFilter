using System;
using System.Collections.Generic;
using System.Linq;

namespace FastChatFilter.Compiler;

/// <summary>
/// Builds a trie data structure from a list of words.
/// Used by the compiler to generate binary trie files.
/// </summary>
internal sealed class TrieBuilder
{
    private readonly List<BuilderNode> _nodes = new();

    public TrieBuilder()
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
    /// Add a word to the trie.
    /// </summary>
    /// <param name="word">Word to add.</param>
    public void Add(string word)
    {
        if (string.IsNullOrEmpty(word))
            return;

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
    }

    /// <summary>
    /// Add multiple words to the trie.
    /// </summary>
    /// <param name="words">Words to add.</param>
    public void AddRange(IEnumerable<string> words)
    {
        foreach (var word in words)
        {
            Add(word);
        }
    }

    /// <summary>
    /// Build the final node and edge arrays.
    /// Edges are sorted by character for binary search during matching.
    /// </summary>
    /// <returns>Tuple of (nodes, edges) arrays.</returns>
    public (TrieNodeData[] Nodes, TrieEdgeData[] Edges) Build()
    {
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

        return (nodes, allEdges.ToArray());
    }

    private sealed class BuilderNode
    {
        public Dictionary<char, int> Children { get; } = new();
        public bool IsTerminal { get; set; }
    }
}

/// <summary>
/// Node data for serialization.
/// </summary>
internal readonly struct TrieNodeData
{
    public readonly int FirstEdgeIndex;
    public readonly ushort EdgeCount;
    public readonly bool IsTerminal;

    public TrieNodeData(int firstEdgeIndex, ushort edgeCount, bool isTerminal)
    {
        FirstEdgeIndex = firstEdgeIndex;
        EdgeCount = edgeCount;
        IsTerminal = isTerminal;
    }
}

/// <summary>
/// Edge data for serialization.
/// </summary>
internal readonly struct TrieEdgeData
{
    public readonly char Character;
    public readonly int ChildNodeIndex;

    public TrieEdgeData(char character, int childNodeIndex)
    {
        Character = character;
        ChildNodeIndex = childNodeIndex;
    }
}
