using System;
using System.Runtime.CompilerServices;
using FastChatFilter.Trie;

namespace FastChatFilter.Matching;

/// <summary>
/// High-performance matcher using binary trie.
/// Provides zero-allocation Contains method for hot path operations.
/// </summary>
internal sealed class TrieMatcher
{
    private readonly BinaryTrie _trie;

    public TrieMatcher(BinaryTrie trie)
    {
        _trie = trie ?? throw new ArgumentNullException(nameof(trie));
    }

    /// <summary>
    /// Check if text contains any words in the trie.
    /// Zero allocation on hot path.
    /// </summary>
    /// <param name="text">Text to search.</param>
    /// <returns>True if any word is found.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Contains(ReadOnlySpan<char> text)
    {
        for (int startIndex = 0; startIndex < text.Length; startIndex++)
        {
            if (MatchFromPosition(text, startIndex, out _))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Find all matches in text.
    /// </summary>
    /// <param name="text">Text to search.</param>
    /// <param name="results">Buffer to store results.</param>
    /// <returns>Number of matches found.</returns>
    public int FindAll(ReadOnlySpan<char> text, Span<MatchResult> results)
    {
        int matchCount = 0;
        int startIndex = 0;

        while (startIndex < text.Length && matchCount < results.Length)
        {
            if (MatchFromPosition(text, startIndex, out int matchLength))
            {
                results[matchCount++] = new MatchResult(startIndex, matchLength);
                startIndex += matchLength; // Skip past match to avoid overlapping
            }
            else
            {
                startIndex++;
            }
        }
        return matchCount;
    }

    /// <summary>
    /// Find all matches in text, including overlapping matches.
    /// </summary>
    /// <param name="text">Text to search.</param>
    /// <param name="results">Buffer to store results.</param>
    /// <returns>Number of matches found.</returns>
    public int FindAllOverlapping(ReadOnlySpan<char> text, Span<MatchResult> results)
    {
        int matchCount = 0;

        for (int startIndex = 0; startIndex < text.Length && matchCount < results.Length; startIndex++)
        {
            if (MatchFromPosition(text, startIndex, out int matchLength))
            {
                results[matchCount++] = new MatchResult(startIndex, matchLength);
            }
        }
        return matchCount;
    }

    /// <summary>
    /// Try to match a word starting at given position.
    /// Uses longest match strategy.
    /// </summary>
    /// <param name="text">Text to search.</param>
    /// <param name="startIndex">Position to start matching from.</param>
    /// <param name="matchLength">Length of the longest match found.</param>
    /// <returns>True if a match was found.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool MatchFromPosition(ReadOnlySpan<char> text, int startIndex, out int matchLength)
    {
        matchLength = 0;
        int currentNodeIndex = _trie.RootIndex;
        int longestMatch = 0;

        for (int i = startIndex; i < text.Length; i++)
        {
            char c = text[i];

            // Binary search through edges (edges are sorted by character)
            int childIndex = FindChildNode(currentNodeIndex, c);

            if (childIndex < 0)
                break; // No transition for this character

            currentNodeIndex = childIndex;
            var node = _trie.GetNode(currentNodeIndex);

            if (node.IsTerminal)
            {
                longestMatch = i - startIndex + 1;
            }
        }

        matchLength = longestMatch;
        return longestMatch > 0;
    }

    /// <summary>
    /// Binary search for child node by character.
    /// Edges are sorted by character for O(log k) lookup where k is edge count.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int FindChildNode(int nodeIndex, char c)
    {
        ReadOnlySpan<TrieEdge> edges = _trie.GetEdges(nodeIndex);

        int left = 0;
        int right = edges.Length - 1;

        while (left <= right)
        {
            int mid = (left + right) >> 1;
            char midChar = edges[mid].Character;

            if (midChar == c)
                return edges[mid].ChildNodeIndex;
            else if (midChar < c)
                left = mid + 1;
            else
                right = mid - 1;
        }

        return -1; // Not found
    }
}
