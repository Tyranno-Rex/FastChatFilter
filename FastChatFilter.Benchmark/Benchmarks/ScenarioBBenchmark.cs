using BenchmarkDotNet.Attributes;
using NReco.Text;

namespace FastChatFilter.Benchmark.Benchmarks;

/// <summary>
/// Scenario B: Long text environment (posts, reviews)
/// - Text: 500 characters
/// - Patterns: 10,000 words
/// - Focus: Performance degradation with text length increase
/// </summary>
[MemoryDiagnoser]
[RankColumn]
public class ScenarioBBenchmark
{
    private ProfanityFilter _fastChatFilter = null!;
    private AhoCorasickDoubleArrayTrie<string> _ahoCorasick = null!;

    private List<string> _profanityWords = null!;
    private string[] _testTexts = null!;

    private const int PatternCount = 10_000;
    private const int TextLength = 500;
    private const int TestTextCount = 50;

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

        // Generate test texts
        _testTexts = new string[TestTextCount];
        for (int i = 0; i < TestTextCount; i++)
        {
            double profanityRate = i % 3 == 0 ? 0.05 : 0.0;
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
                return false;
            });
            if (hasMatch) found++;
        }
        return found;
    }
}
