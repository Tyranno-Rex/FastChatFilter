using System;
using System.Runtime.CompilerServices;
using FastChatFilter.Hash;
using FastChatFilter.Trie;

namespace FastChatFilter.Matching;

/// <summary>
/// Hybrid matcher using Trie traversal + CRC32 verification.
///
/// Flow:
/// 1. Sliding window over input text
/// 2. Trie traversal for fast partial matching
/// 3. When terminal node reached, verify with CRC32 hash
/// 4. Hash mismatch = false positive removal
/// 5. Hash match = confirmed profanity
/// </summary>
internal sealed class HybridMatcher
{
    private readonly BinaryTrie _trie;
    private readonly HashSet32 _hashSet;

    public HybridMatcher(BinaryTrie trie, HashSet32 hashSet)
    {
        _trie = trie ?? throw new ArgumentNullException(nameof(trie));
        _hashSet = hashSet ?? throw new ArgumentNullException(nameof(hashSet));
    }

    /// <summary>
    /// Check if text contains any profanity words.
    /// Uses Trie for fast traversal, CRC32 for verification.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Contains(ReadOnlySpan<char> text)
    {
        if (text.IsEmpty)
            return false;

        // Sliding window over all positions
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
    public int FindAll(ReadOnlySpan<char> text, Span<MatchResult> results)
    {
        if (text.IsEmpty || results.IsEmpty)
            return 0;

        int matchCount = 0;
        int startIndex = 0;

        while (startIndex < text.Length && matchCount < results.Length)
        {
            if (MatchFromPosition(text, startIndex, out int matchLength))
            {
                results[matchCount++] = new MatchResult(startIndex, matchLength);
                startIndex += matchLength; // Skip past match
            }
            else
            {
                startIndex++;
            }
        }
        return matchCount;
    }

    /// <summary>
    /// Try to match from a position using Trie traversal + CRC32 verification.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool MatchFromPosition(ReadOnlySpan<char> text, int startIndex, out int matchLength)
    {
        matchLength = 0;
        int currentNodeIndex = _trie.RootIndex;
        int longestVerifiedMatch = 0;

        for (int i = startIndex; i < text.Length; i++)
        {
            char c = text[i];

            // Binary search through edges
            int childIndex = FindChildNode(currentNodeIndex, c);

            if (childIndex < 0)
                break; // No transition for this character

            currentNodeIndex = childIndex;
            var node = _trie.GetNode(currentNodeIndex);

            // Terminal node reached - verify with CRC32
            if (node.IsTerminal)
            {
                int candidateLength = i - startIndex + 1;
                var candidate = text.Slice(startIndex, candidateLength);

                // CRC32 verification
                uint hash = Crc32.Compute(candidate);
                if (_hashSet.Contains(hash))
                {
                    longestVerifiedMatch = candidateLength;
                    // Continue to find potentially longer match
                }
                // If hash doesn't match, it's a false positive from Trie
                // We continue traversing to find other potential matches
            }
        }

        matchLength = longestVerifiedMatch;
        return longestVerifiedMatch > 0;
    }

    /// <summary>
    /// Binary search for child node by character.
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

        return -1;
    }
}
