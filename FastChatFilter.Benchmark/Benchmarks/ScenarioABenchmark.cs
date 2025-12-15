using BenchmarkDotNet.Attributes;
using NReco.Text;

namespace FastChatFilter.Benchmark.Benchmarks;

/// <summary>
/// Scenario A: Normal chat environment
/// - Text: 50 characters (random words + occasional profanity)
/// - Patterns: 10,000 words
/// - Focus: Throughput (ops/sec), Memory allocation (B/op)
/// </summary>
[MemoryDiagnoser]
[RankColumn]
public class ScenarioABenchmark
{
    private ProfanityFilter _fastChatFilter = null!;
    private AhoCorasickDoubleArrayTrie<string> _ahoCorasick = null!;

    private List<string> _profanityWords = null!;
    private string[] _testTexts = null!;

    private const int PatternCount = 10_000;
    private const int TextLength = 50;
    private const int TestTextCount = 100;

    [GlobalSetup]
    public void Setup()
    {
        // Generate profanity words
        _profanityWords = DataGenerator.GenerateProfanityWords(PatternCount);

        // Build FastChatFilter
        var tempCsvPath = Path.GetTempFileName();
        var tempBinPath = Path.ChangeExtension(tempCsvPath, ".bin");

        try
        {
            DataGenerator.SaveWordsToCsv(_profanityWords, tempCsvPath);
            FastChatFilter.Compiler.HybridBuilder.Build(tempCsvPath, tempBinPath);
            _fastChatFilter = ProfanityFilter.Load(tempBinPath);
        }
        finally
        {
            if (File.Exists(tempCsvPath)) File.Delete(tempCsvPath);
            if (File.Exists(tempBinPath)) File.Delete(tempBinPath);
        }

        // Build AhoCorasick (NReco.Text)
        _ahoCorasick = new AhoCorasickDoubleArrayTrie<string>(
            _profanityWords.Select(w => new KeyValuePair<string, string>(w, w)));

        // Generate test texts (mix of clean and profane)
        _testTexts = new string[TestTextCount];
        for (int i = 0; i < TestTextCount; i++)
        {
            // 30% chance of containing profanity
            double profanityRate = i % 3 == 0 ? 0.1 : 0.0;
            _testTexts[i] = DataGenerator.GenerateText(TextLength, _profanityWords, profanityRate);
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _fastChatFilter?.Dispose();
    }

    [Benchmark(Baseline = true, Description = "FastChatFilter")]
    public int FastChatFilter_Contains()
    {
        int found = 0;
        foreach (var text in _testTexts)
        {
            if (_fastChatFilter.Contains(text.AsSpan()))
                found++;
        }
        return found;
    }

    [Benchmark(Description = "AhoCorasick (NReco)")]
    public int AhoCorasick_Contains()
    {
        int found = 0;
        foreach (var text in _testTexts)
        {
            bool hasMatch = false;
            _ahoCorasick.ParseText(text, (hit) =>
            {
                hasMatch = true;
                return false; // Stop on first match
            });
            if (hasMatch) found++;
        }
        return found;
    }

    [Benchmark(Description = "FastChatFilter (single)")]
    public bool FastChatFilter_Single()
    {
        return _fastChatFilter.Contains(_testTexts[0].AsSpan());
    }

    [Benchmark(Description = "AhoCorasick (single)")]
    public bool AhoCorasick_Single()
    {
        bool hasMatch = false;
        _ahoCorasick.ParseText(_testTexts[0], (hit) =>
        {
            hasMatch = true;
            return false;
        });
        return hasMatch;
    }
}
