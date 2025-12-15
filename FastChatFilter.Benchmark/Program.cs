using BenchmarkDotNet.Running;
using BenchmarkDotNet.Configs;
using FastChatFilter.Benchmark.Benchmarks;
using NReco.Text;

namespace FastChatFilter.Benchmark;

class Program
{
    static void Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--quick")
        {
            // Quick test mode
            RunQuickTest();
            return;
        }

        if (args.Length > 0 && args[0] == "--init")
        {
            // Initialization timing test only
            InitializationTimingTest.Run();
            return;
        }

        if (args.Length > 0 && args[0] == "--all")
        {
            // Run all benchmarks
            var config = DefaultConfig.Instance
                .WithOptions(ConfigOptions.DisableOptimizationsValidator);

            Console.WriteLine("=== Running All Benchmarks ===\n");

            BenchmarkRunner.Run<ScenarioABenchmark>(config);
            BenchmarkRunner.Run<ScenarioBBenchmark>(config);
            BenchmarkRunner.Run<ScenarioCBenchmark>(config);
            BenchmarkRunner.Run<ScenarioDBenchmark>(config);

            return;
        }

        // Interactive mode
        Console.WriteLine("FastChatFilter vs AhoCorasick (NReco) Benchmark");
        Console.WriteLine("===============================================\n");
        Console.WriteLine("Select benchmark scenario:");
        Console.WriteLine("  1. Scenario A - Normal chat (50 chars, 10K patterns)");
        Console.WriteLine("  2. Scenario B - Long text (500 chars, 10K patterns)");
        Console.WriteLine("  3. Scenario C - Worst case (repeating chars)");
        Console.WriteLine("  4. Scenario D - Initialization (100K patterns)");
        Console.WriteLine("  5. Quick functional test");
        Console.WriteLine("  6. Initialization timing test");
        Console.WriteLine("  7. Run all benchmarks");
        Console.WriteLine();
        Console.Write("Enter choice (1-7): ");

        var choice = Console.ReadLine();

        var config2 = DefaultConfig.Instance
            .WithOptions(ConfigOptions.DisableOptimizationsValidator);

        switch (choice)
        {
            case "1":
                BenchmarkRunner.Run<ScenarioABenchmark>(config2);
                break;
            case "2":
                BenchmarkRunner.Run<ScenarioBBenchmark>(config2);
                break;
            case "3":
                BenchmarkRunner.Run<ScenarioCBenchmark>(config2);
                break;
            case "4":
                BenchmarkRunner.Run<ScenarioDBenchmark>(config2);
                break;
            case "5":
                RunQuickTest();
                break;
            case "6":
                InitializationTimingTest.Run();
                break;
            case "7":
                BenchmarkRunner.Run<ScenarioABenchmark>(config2);
                BenchmarkRunner.Run<ScenarioBBenchmark>(config2);
                BenchmarkRunner.Run<ScenarioCBenchmark>(config2);
                BenchmarkRunner.Run<ScenarioDBenchmark>(config2);
                break;
            default:
                Console.WriteLine("Invalid choice");
                break;
        }
    }

    static void RunQuickTest()
    {
        Console.WriteLine("\n=== Quick Functional Test ===\n");

        var words = DataGenerator.GenerateProfanityWords(1000);
        Console.WriteLine($"Generated {words.Count:N0} profanity words");
        Console.WriteLine($"Sample words: {string.Join(", ", words.Take(10))}...\n");

        // Build FastChatFilter
        var tempCsvPath = Path.GetTempFileName();
        var tempBinPath = Path.ChangeExtension(tempCsvPath, ".bin");

        try
        {
            DataGenerator.SaveWordsToCsv(words, tempCsvPath);
            FastChatFilter.Compiler.HybridBuilder.Build(tempCsvPath, tempBinPath);

            using var filter = ProfanityFilter.Load(tempBinPath);
            var ac = new AhoCorasickDoubleArrayTrie<string>(
                words.Select(w => new KeyValuePair<string, string>(w, w)));

            // Test cases
            var testCases = new[]
            {
                ("Clean text: hello world", "hello world"),
                ("With profanity: hello badword world", $"hello {words[0]} world"),
                ("Multiple: test bad test", $"test {words[0]} {words[1]} test"),
                ("Empty", ""),
                ("Single word match", words[0]),
            };

            Console.WriteLine("Test Results:");
            Console.WriteLine("-".PadRight(60, '-'));

            foreach (var (name, text) in testCases)
            {
                var fcfResult = filter.Contains(text);
                bool acResult = false;
                ac.ParseText(text, (hit) =>
                {
                    acResult = true;
                    return false;
                });
                var match = fcfResult == acResult ? "OK" : "MISMATCH!";

                Console.WriteLine($"{name}");
                Console.WriteLine($"  FastChatFilter: {fcfResult}");
                Console.WriteLine($"  AhoCorasick:    {acResult}");
                Console.WriteLine($"  Status:         {match}");
                Console.WriteLine();
            }

            // Performance quick test
            Console.WriteLine("\nQuick Performance Test (10,000 iterations):");
            Console.WriteLine("-".PadRight(60, '-'));

            var testText = DataGenerator.GenerateText(50, words, 0.1);
            const int iterations = 10_000;

            var sw = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                filter.Contains(testText.AsSpan());
            }
            sw.Stop();
            Console.WriteLine($"FastChatFilter: {sw.ElapsedMilliseconds} ms ({iterations * 1000.0 / sw.ElapsedMilliseconds:N0} ops/sec)");

            sw.Restart();
            for (int i = 0; i < iterations; i++)
            {
                ac.ParseText(testText, (hit) => false);
            }
            sw.Stop();
            Console.WriteLine($"AhoCorasick (NReco): {sw.ElapsedMilliseconds} ms ({iterations * 1000.0 / sw.ElapsedMilliseconds:N0} ops/sec)");
        }
        finally
        {
            if (File.Exists(tempCsvPath)) File.Delete(tempCsvPath);
            if (File.Exists(tempBinPath)) File.Delete(tempBinPath);
        }

        Console.WriteLine("\nQuick test completed!");
    }
}
