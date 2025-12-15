using System.Runtime.InteropServices;

namespace FastChatFilter.Matching;

/// <summary>
/// Represents a match found in text.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct MatchResult
{
    /// <summary>
    /// The starting index of the match in the text.
    /// </summary>
    public readonly int StartIndex;

    /// <summary>
    /// The length of the matched word.
    /// </summary>
    public readonly int Length;

    /// <summary>
    /// Creates a new match result.
    /// </summary>
    public MatchResult(int startIndex, int length)
    {
        StartIndex = startIndex;
        Length = length;
    }

    /// <summary>
    /// Gets the ending index (exclusive) of the match.
    /// </summary>
    public int EndIndex => StartIndex + Length;
}
