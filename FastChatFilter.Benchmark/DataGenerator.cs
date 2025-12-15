using System.Text;

namespace FastChatFilter.Benchmark;

/// <summary>
/// Generates test data for benchmarks.
/// </summary>
public static class DataGenerator
{
    private static readonly Random Rng = new(42); // Fixed seed for reproducibility

    private static readonly string[] CommonWords =
    {
        "the", "be", "to", "of", "and", "a", "in", "that", "have", "I",
        "it", "for", "not", "on", "with", "he", "as", "you", "do", "at",
        "this", "but", "his", "by", "from", "they", "we", "say", "her", "she",
        "or", "an", "will", "my", "one", "all", "would", "there", "their", "what",
        "so", "up", "out", "if", "about", "who", "get", "which", "go", "me",
        "when", "make", "can", "like", "time", "no", "just", "him", "know", "take",
        "people", "into", "year", "your", "good", "some", "could", "them", "see", "other",
        "than", "then", "now", "look", "only", "come", "its", "over", "think", "also",
        "back", "after", "use", "two", "how", "our", "work", "first", "well", "way",
        "even", "new", "want", "because", "any", "these", "give", "day", "most", "us"
    };

    /// <summary>
    /// Generate a list of profanity words for testing.
    /// Creates words with varying lengths (3-15 characters).
    /// </summary>
    public static List<string> GenerateProfanityWords(int count)
    {
        var words = new HashSet<string>();
        var prefixes = new[] { "bad", "off", "hate", "spam", "ugly", "evil", "dumb", "sick", "fool", "jerk" };
        var suffixes = new[] { "word", "talk", "text", "msg", "chat", "post", "user", "name", "guy", "man" };
        var middles = new[] { "", "er", "ing", "ly", "ness", "ful", "less", "ment", "tion", "able" };

        // Add base words
        foreach (var prefix in prefixes)
        {
            words.Add(prefix);
            foreach (var suffix in suffixes)
            {
                words.Add(prefix + suffix);
                foreach (var middle in middles)
                {
                    if (words.Count >= count) break;
                    words.Add(prefix + middle + suffix);
                }
            }
        }

        // Generate more random words if needed
        while (words.Count < count)
        {
            int length = Rng.Next(3, 16);
            var word = GenerateRandomWord(length);
            words.Add(word);
        }

        return words.Take(count).ToList();
    }

    /// <summary>
    /// Generate random text with optional profanity injection.
    /// </summary>
    public static string GenerateText(int targetLength, List<string>? profanityWords = null, double profanityRate = 0.0)
    {
        var sb = new StringBuilder();

        while (sb.Length < targetLength)
        {
            // Decide whether to inject profanity
            if (profanityWords != null && profanityRate > 0 && Rng.NextDouble() < profanityRate)
            {
                var profanity = profanityWords[Rng.Next(profanityWords.Count)];
                sb.Append(profanity);
            }
            else
            {
                var word = CommonWords[Rng.Next(CommonWords.Length)];
                sb.Append(word);
            }

            sb.Append(' ');
        }

        return sb.ToString(0, Math.Min(sb.Length, targetLength));
    }

    /// <summary>
    /// Generate worst-case text (repeating character + profanity at end).
    /// </summary>
    public static string GenerateWorstCaseText(int length, string profanityWord)
    {
        var padding = new string('a', length - profanityWord.Length - 1);
        return padding + " " + profanityWord;
    }

    /// <summary>
    /// Generate text with similar-but-not-matching words (for false positive testing).
    /// </summary>
    public static string GenerateSimilarText(List<string> profanityWords, int count)
    {
        var sb = new StringBuilder();

        for (int i = 0; i < count; i++)
        {
            var word = profanityWords[Rng.Next(profanityWords.Count)];
            // Modify the word slightly to test false positives
            var modified = ModifyWord(word);
            sb.Append(modified);
            sb.Append(' ');
        }

        return sb.ToString().TrimEnd();
    }

    private static string GenerateRandomWord(int length)
    {
        const string chars = "abcdefghijklmnopqrstuvwxyz";
        var word = new char[length];
        for (int i = 0; i < length; i++)
        {
            word[i] = chars[Rng.Next(chars.Length)];
        }
        return new string(word);
    }

    private static string ModifyWord(string word)
    {
        if (word.Length < 2) return word + "x";

        int modification = Rng.Next(4);
        return modification switch
        {
            0 => word + "s",           // Add suffix
            1 => word + "ing",         // Add suffix
            2 => "pre" + word,         // Add prefix
            3 => word.Insert(word.Length / 2, "x"), // Insert character
            _ => word
        };
    }

    /// <summary>
    /// Save word list to CSV file.
    /// </summary>
    public static void SaveWordsToCsv(List<string> words, string path)
    {
        File.WriteAllLines(path, words);
    }
}
