using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace FastChatFilter.Compiler;

/// <summary>
/// Reads word lists from CSV files.
/// Supports simple one-word-per-line format and comma-separated format.
/// </summary>
internal static class CsvReader
{
    /// <summary>
    /// Read words from a CSV file.
    /// </summary>
    /// <param name="path">Path to the CSV file.</param>
    /// <returns>List of words.</returns>
    public static async Task<List<string>> ReadWordsAsync(string path)
    {
        var words = new List<string>();
        var lines = await File.ReadAllLinesAsync(path);

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            // Skip comment lines
            if (line.TrimStart().StartsWith('#'))
                continue;

            // Handle comma-separated values
            if (line.Contains(','))
            {
                var parts = line.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                foreach (var part in parts)
                {
                    var word = CleanWord(part);
                    if (!string.IsNullOrEmpty(word))
                    {
                        words.Add(word);
                    }
                }
            }
            else
            {
                var word = CleanWord(line);
                if (!string.IsNullOrEmpty(word))
                {
                    words.Add(word);
                }
            }
        }

        return words;
    }

    /// <summary>
    /// Read words from a text file (one word per line).
    /// </summary>
    public static async Task<List<string>> ReadWordsFromTextAsync(string path)
    {
        var words = new List<string>();
        var lines = await File.ReadAllLinesAsync(path);

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            if (line.TrimStart().StartsWith('#'))
                continue;

            var word = CleanWord(line);
            if (!string.IsNullOrEmpty(word))
            {
                words.Add(word);
            }
        }

        return words;
    }

    private static string CleanWord(string word)
    {
        if (string.IsNullOrWhiteSpace(word))
            return string.Empty;

        // Remove surrounding quotes
        word = word.Trim();
        if (word.Length >= 2)
        {
            if ((word.StartsWith('"') && word.EndsWith('"')) ||
                (word.StartsWith('\'') && word.EndsWith('\'')))
            {
                word = word[1..^1];
            }
        }

        return word.Trim();
    }
}
