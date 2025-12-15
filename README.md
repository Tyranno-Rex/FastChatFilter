# FastChatFilter

High-performance, zero-allocation profanity filter for .NET. Uses binary trie data structure with Span-based API for maximum performance in game servers, chat systems, and real-time applications.

## Features

- **Zero-allocation hot path**: `Contains()` method allocates zero bytes on the heap
- **Binary Trie**: Precompiled dictionary for O(n) text scanning
- **Span-based API**: Works with `ReadOnlySpan<char>` for maximum flexibility
- **Case-insensitive matching**: Built-in lowercase normalization
- **Multi-target**: Supports .NET 8.0 and .NET Standard 2.1

## Installation

```bash
# Install the runtime library
dotnet add package FastChatFilter

# Install the compiler tool (optional)
dotnet tool install -g FastChatFilter.Compiler
```

## Quick Start

### 1. Create a word list (CSV)

```csv
# badwords.csv
badword
offensive
spam
inappropriate
```

### 2. Compile to binary format

```bash
fcfc -i badwords.csv -o badwords.bin --normalize lower
```

### 3. Use in your application

```csharp
using FastChatFilter;

// Load the compiled dictionary
using var filter = ProfanityFilter.Load("badwords.bin");

// Check for profanity (zero allocation)
bool hasProfanity = filter.Contains(message.AsSpan());

// Mask profanity
string censored = filter.Mask(message, '*');
// "this is badword" -> "this is *******"
```

## API Reference

### ProfanityFilter

```csharp
// Load from file
var filter = ProfanityFilter.Load("words.bin");
var filter = ProfanityFilter.Load("words.bin", new LoadOptions { EnableNormalization = false });

// Load from stream (embedded resources)
var filter = ProfanityFilter.Load(stream);

// Load from byte array
var filter = ProfanityFilter.LoadFromBytes(data);

// Check for profanity
bool contains = filter.Contains("text to check");
bool contains = filter.Contains(text.AsSpan()); // Zero allocation

// Mask profanity
string masked = filter.Mask("bad text", '*');
string masked = filter.Mask("bad text", '#', new MaskOptions { FixedMask = "***" });

// Find all matches
Span<MatchResult> results = stackalloc MatchResult[64];
int count = filter.FindMatches(text.AsSpan(), results);
```

### LoadOptions

```csharp
new LoadOptions
{
    EnableNormalization = true  // Enable case-insensitive matching (default: true)
}
```

### MaskOptions

```csharp
new MaskOptions
{
    PreserveLength = true,  // Replace each character (default: true)
    FixedMask = "***"       // Use fixed replacement string (optional)
}
```

## Compiler CLI

The `fcfc` (FastChatFilter Compiler) tool converts CSV word lists to optimized binary format.

```bash
# Basic usage
fcfc -i words.csv -o words.bin

# With options
fcfc --input words.csv --output words.bin --normalize lower

# Help
fcfc --help
```

### CSV Format

- One word per line
- Comma-separated values supported
- Lines starting with `#` are comments
- Quotes around words are automatically removed

```csv
# Sample word list
badword
offensive, inappropriate, spam
"quoted word"
```

## Performance

| Metric | Target |
|--------|--------|
| Contains allocation | 0 bytes |
| Contains throughput (50 chars) | >1M ops/sec |
| Load time (10K words) | <50ms |

## Integration Examples

### ASP.NET Core Middleware

```csharp
public class ProfanityMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ProfanityFilter _filter;

    public ProfanityMiddleware(RequestDelegate next, ProfanityFilter filter)
    {
        _next = next;
        _filter = filter;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Check request body for profanity...
        await _next(context);
    }
}

// In Program.cs
builder.Services.AddSingleton(_ => ProfanityFilter.Load("words.bin"));
```

### Game Server

```csharp
public class ChatHandler
{
    private readonly ProfanityFilter _filter;

    public ChatMessage ProcessMessage(string text)
    {
        if (_filter.Contains(text.AsSpan()))
        {
            return new ChatMessage { Text = _filter.Mask(text), IsCensored = true };
        }
        return new ChatMessage { Text = text };
    }
}
```

## License

MIT License

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.
