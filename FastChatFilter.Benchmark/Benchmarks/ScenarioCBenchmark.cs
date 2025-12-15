using BenchmarkDotNet.Attributes;
using NReco.Text;

namespace FastChatFilter.Benchmark.Benchmarks;

/// <summary>
/// Scenario C: Worst case scenario
/// - Text: Repeating characters ('aaaaaa...') + profanity at end
/// - Tests: Failure link efficiency (AC) vs Sliding Window (FastChatFilter)
/// </summary>
[MemoryDiagnoser]
[RankColumn]
public class ScenarioCBenchmark
{
    private ProfanityFilter _fastChatFilter = null!;
    private AhoCorasickDoubleArrayTrie<string> _ahoCorasick = null!;

    private List<string> _profanityWords = null!;
    private string _worstCaseText100 = null!;
    private string _worstCaseText500 = null!;
    private string _worstCaseText1000 = null!;

    private const int PatternCount = 500;

    [GlobalSetup]
    public void Setup()
    {
        // Generate profanity words with varying lengths
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

        // Generate worst-case texts
        var targetWord = _profanityWords.First(w => w.Length >= 5);
        _worstCaseText100 = DataGenerator.GenerateWorstCaseText(100, targetWord);
        _worstCaseText500 = DataGenerator.GenerateWorstCaseText(500, targetWord);
        _worstCaseText1000 = DataGenerator.GenerateWorstCaseText(1000, targetWord);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _fastChatFilter?.Dispose();
    }

    [Benchmark(Baseline = true, Description = "FastChatFilter (100 chars)")]
    public bool FastChatFilter_100()
    {
        return _fastChatFilter.Contains(_worstCaseText100.AsSpan());
    }

    [Benchmark(Description = "AhoCorasick (100 chars)")]
    public bool AhoCorasick_100()
    {
        bool hasMatch = false;
        _ahoCorasick.ParseText(_worstCaseText100, (hit) =>
        {
            hasMatch = true;
            return false;
        });
        return hasMatch;
    }

    [Benchmark(Description = "FastChatFilter (500 chars)")]
    public bool FastChatFilter_500()
    {
        return _fastChatFilter.Contains(_worstCaseText500.AsSpan());
    }

    [Benchmark(Description = "AhoCorasick (500 chars)")]
    public bool AhoCorasick_500()
    {
        bool hasMatch = false;
        _ahoCorasick.ParseText(_worstCaseText500, (hit) =>
        {
            hasMatch = true;
            return false;
        });
        return hasMatch;
    }

    [Benchmark(Description = "FastChatFilter (1000 chars)")]
    public bool FastChatFilter_1000()
    {
        return _fastChatFilter.Contains(_worstCaseText1000.AsSpan());
    }

    [Benchmark(Description = "AhoCorasick (1000 chars)")]
    public bool AhoCorasick_1000()
    {
        bool hasMatch = false;
        _ahoCorasick.ParseText(_worstCaseText1000, (hit) =>
        {
            hasMatch = true;
            return false;
        });
        return hasMatch;
    }
}
