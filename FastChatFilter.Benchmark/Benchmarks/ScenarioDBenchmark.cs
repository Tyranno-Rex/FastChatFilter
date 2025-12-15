using BenchmarkDotNet.Attributes;
using NReco.Text;
using System.Diagnostics;

namespace FastChatFilter.Benchmark.Benchmarks;

/// <summary>
/// Scenario D: Initialization and memory usage
/// - Patterns: 100,000 words
/// - Focus: Load time, memory footprint
/// </summary>
[MemoryDiagnoser]
[RankColumn]
public class ScenarioDBenchmark
{
    private List<string> _profanityWords = null!;
    private string _tempCsvPath = null!;
    private string _tempBinPath = null!;
    private byte[] _binaryData = null!;

    private const int PatternCount = 100_000;

    [GlobalSetup]
    public void Setup()
    {
        // Generate large word list
        _profanityWords = DataGenerator.GenerateProfanityWords(PatternCount);

        // Prepare files
        _tempCsvPath = Path.GetTempFileName();
        _tempBinPath = Path.ChangeExtension(_tempCsvPath, ".bin");

        DataGenerator.SaveWordsToCsv(_profanityWords, _tempCsvPath);
        FastChatFilter.Compiler.HybridBuilder.Build(_tempCsvPath, _tempBinPath);

        // Load binary data for LoadFromBytes benchmark
        _binaryData = File.ReadAllBytes(_tempBinPath);

        Console.WriteLine($"Word count: {_profanityWords.Count:N0}");
        Console.WriteLine($"Binary file size: {new FileInfo(_tempBinPath).Length:N0} bytes");
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        if (File.Exists(_tempCsvPath)) File.Delete(_tempCsvPath);
        if (File.Exists(_tempBinPath)) File.Delete(_tempBinPath);
    }

    [Benchmark(Baseline = true, Description = "FastChatFilter.Load (from file)")]
    public ProfanityFilter FastChatFilter_LoadFromFile()
    {
        var filter = ProfanityFilter.Load(_tempBinPath);
        filter.Dispose();
        return filter;
    }

    [Benchmark(Description = "FastChatFilter.LoadFromBytes")]
    public ProfanityFilter FastChatFilter_LoadFromBytes()
    {
        var filter = ProfanityFilter.LoadFromBytes(_binaryData);
        filter.Dispose();
        return filter;
    }

    [Benchmark(Description = "AhoCorasick (NReco) build")]
    public AhoCorasickDoubleArrayTrie<string> AhoCorasick_Build()
    {
        return new AhoCorasickDoubleArrayTrie<string>(
            _profanityWords.Select(w => new KeyValuePair<string, string>(w, w)));
    }
}

/// <summary>
/// Separate benchmark for initialization timing (not affected by BenchmarkDotNet overhead)
/// </summary>
public static class InitializationTimingTest
{
    public static void Run()
    {
        Console.WriteLine("\n=== Initialization Timing Test ===\n");

        var words = DataGenerator.GenerateProfanityWords(100_000);
        var tempCsvPath = Path.GetTempFileName();
        var tempBinPath = Path.ChangeExtension(tempCsvPath, ".bin");

        try
        {
            DataGenerator.SaveWordsToCsv(words, tempCsvPath);

            // Measure compilation time
            var sw = Stopwatch.StartNew();
            FastChatFilter.Compiler.HybridBuilder.Build(tempCsvPath, tempBinPath);
            sw.Stop();
            Console.WriteLine($"FastChatFilter compilation: {sw.ElapsedMilliseconds:N0} ms");

            var fileInfo = new FileInfo(tempBinPath);
            Console.WriteLine($"Binary file size: {fileInfo.Length:N0} bytes ({fileInfo.Length / 1024.0 / 1024.0:F2} MB)");

            // Measure FastChatFilter load time
            sw.Restart();
            using (var filter = ProfanityFilter.Load(tempBinPath))
            {
                sw.Stop();
                Console.WriteLine($"FastChatFilter load time: {sw.ElapsedMilliseconds:N0} ms");
            }

            // Measure AhoCorasick build time
            sw.Restart();
            var ac = new AhoCorasickDoubleArrayTrie<string>(
                words.Select(w => new KeyValuePair<string, string>(w, w)));
            sw.Stop();
            Console.WriteLine($"AhoCorasick (NReco) build time: {sw.ElapsedMilliseconds:N0} ms");

            // Memory estimation
            var beforeGC = GC.GetTotalMemory(true);
            var filter2 = ProfanityFilter.Load(tempBinPath);
            var afterFCF = GC.GetTotalMemory(false);
            Console.WriteLine($"\nFastChatFilter memory (approx): {(afterFCF - beforeGC) / 1024.0 / 1024.0:F2} MB");
            filter2.Dispose();

            beforeGC = GC.GetTotalMemory(true);
            var ac2 = new AhoCorasickDoubleArrayTrie<string>(
                words.Select(w => new KeyValuePair<string, string>(w, w)));
            var afterAC = GC.GetTotalMemory(false);
            Console.WriteLine($"AhoCorasick (NReco) memory (approx): {(afterAC - beforeGC) / 1024.0 / 1024.0:F2} MB");
        }
        finally
        {
            if (File.Exists(tempCsvPath)) File.Delete(tempCsvPath);
            if (File.Exists(tempBinPath)) File.Delete(tempBinPath);
        }
    }
}
