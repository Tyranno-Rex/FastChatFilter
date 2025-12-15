using System;
using System.CommandLine;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace FastChatFilter.Compiler;

class Program
{
    static async Task<int> Main(string[] args)
    {
        var inputOption = new Option<FileInfo>(
            aliases: new[] { "-i", "--input" },
            description: "Input CSV or text file containing words (one per line or comma-separated)")
        {
            IsRequired = true
        };

        var outputOption = new Option<FileInfo>(
            aliases: new[] { "-o", "--output" },
            description: "Output binary file path (.bin)")
        {
            IsRequired = true
        };

        var normalizeOption = new Option<string[]>(
            aliases: new[] { "-n", "--normalize" },
            description: "Normalization options: 'lower' for lowercase conversion",
            getDefaultValue: () => new[] { "lower" });

        var rootCommand = new RootCommand("FastChatFilter CSV to Binary Compiler")
        {
            inputOption,
            outputOption,
            normalizeOption
        };

        rootCommand.SetHandler(CompileAsync, inputOption, outputOption, normalizeOption);

        return await rootCommand.InvokeAsync(args);
    }

    static async Task CompileAsync(FileInfo input, FileInfo output, string[] normalize)
    {
        Console.WriteLine($"FastChatFilter Compiler v1.0.0");
        Console.WriteLine($"Input:  {input.FullName}");
        Console.WriteLine($"Output: {output.FullName}");
        Console.WriteLine();

        if (!input.Exists)
        {
            Console.Error.WriteLine($"Error: Input file not found: {input.FullName}");
            Environment.ExitCode = 1;
            return;
        }

        try
        {
            // Read words from CSV
            Console.WriteLine("Reading input file...");
            var words = await CsvReader.ReadWordsAsync(input.FullName);
            Console.WriteLine($"  Loaded {words.Count} words");

            // Apply normalization
            bool doLowercase = normalize.Contains("lower", StringComparer.OrdinalIgnoreCase);

            if (doLowercase)
            {
                Console.WriteLine("Applying lowercase normalization...");
                words = words
                    .Select(w => w.ToLowerInvariant())
                    .Distinct()
                    .ToList();
                Console.WriteLine($"  {words.Count} unique words after normalization");
            }
            else
            {
                words = words.Distinct().ToList();
                Console.WriteLine($"  {words.Count} unique words");
            }

            // Build hybrid structure (Trie + CRC32)
            Console.WriteLine("Building hybrid structure (Trie + CRC32)...");
            var builder = new HybridBuilder();
            builder.AddRange(words);
            Console.WriteLine($"  Trie nodes:   {builder.NodeCount}");
            Console.WriteLine($"  Trie edges:   {builder.EdgeCount}");
            Console.WriteLine($"  CRC32 hashes: {builder.HashCount}");
            Console.WriteLine($"  Length range: {builder.MinWordLength}-{builder.MaxWordLength}");

            // Ensure output directory exists
            if (output.Directory != null && !output.Directory.Exists)
            {
                output.Directory.Create();
            }

            // Write binary
            Console.WriteLine("Writing binary file (Hybrid format)...");
            await HybridBinaryWriter.WriteAsync(output.FullName, builder);

            // Report results
            var outputInfo = new FileInfo(output.FullName);
            Console.WriteLine();
            Console.WriteLine($"Success! Output: {outputInfo.Length:N0} bytes");
            Console.WriteLine($"  Words:        {words.Count:N0}");
            Console.WriteLine($"  Trie nodes:   {builder.NodeCount:N0}");
            Console.WriteLine($"  Trie edges:   {builder.EdgeCount:N0}");
            Console.WriteLine($"  CRC32 hashes: {builder.HashCount:N0}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            Environment.ExitCode = 1;
        }
    }
}
