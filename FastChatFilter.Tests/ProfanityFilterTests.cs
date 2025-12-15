using System;
using System.IO;
using System.Threading.Tasks;
using FastChatFilter.Compiler;
using Xunit;

namespace FastChatFilter.Tests;

public class ProfanityFilterTests : IAsyncLifetime
{
    private string _testBinaryPath = null!;
    private ProfanityFilter _filter = null!;

    public async Task InitializeAsync()
    {
        // Create a test binary file
        _testBinaryPath = Path.Combine(Path.GetTempPath(), $"test-{Guid.NewGuid()}.bin");

        var builder = new HybridBuilder();
        builder.Add("badword");
        builder.Add("offensive");
        builder.Add("spam");
        builder.Add("test");

        await HybridBinaryWriter.WriteAsync(_testBinaryPath, builder);

        _filter = ProfanityFilter.Load(_testBinaryPath);
    }

    public Task DisposeAsync()
    {
        _filter?.Dispose();
        if (File.Exists(_testBinaryPath))
        {
            File.Delete(_testBinaryPath);
        }
        return Task.CompletedTask;
    }

    [Fact]
    public void Contains_WithProfanity_ReturnsTrue()
    {
        Assert.True(_filter.Contains("this has badword in it"));
        Assert.True(_filter.Contains("badword"));
        Assert.True(_filter.Contains("offensive content"));
        Assert.True(_filter.Contains("this is spam"));
    }

    [Fact]
    public void Contains_WithProfanity_CaseInsensitive()
    {
        Assert.True(_filter.Contains("BADWORD"));
        Assert.True(_filter.Contains("BadWord"));
        Assert.True(_filter.Contains("OFFENSIVE"));
    }

    [Fact]
    public void Contains_WithCleanText_ReturnsFalse()
    {
        Assert.False(_filter.Contains("this is clean text"));
        Assert.False(_filter.Contains("hello world"));
        Assert.False(_filter.Contains("normal message"));
    }

    [Fact]
    public void Contains_WithPartialMatch_ReturnsFalse()
    {
        // "bad" is not in the list, only "badword"
        Assert.False(_filter.Contains("bad"));
        // "off" is not in the list
        Assert.False(_filter.Contains("off"));
    }

    [Fact]
    public void Contains_EmptyAndNull_ReturnsFalse()
    {
        Assert.False(_filter.Contains(""));
        Assert.False(_filter.Contains(string.Empty));
        Assert.False(_filter.Contains((string)null!));
    }

    [Fact]
    public void Contains_Span_ZeroAllocation()
    {
        ReadOnlySpan<char> text = "this has badword in it".AsSpan();
        Assert.True(_filter.Contains(text));

        text = "clean text".AsSpan();
        Assert.False(_filter.Contains(text));
    }

    [Fact]
    public void Mask_ReplacesProfanity()
    {
        var result = _filter.Mask("this has badword in it");
        Assert.Equal("this has ******* in it", result);
    }

    [Fact]
    public void Mask_MultipleMatches()
    {
        var result = _filter.Mask("badword and spam here");
        Assert.Equal("******* and **** here", result);
    }

    [Fact]
    public void Mask_CustomMaskChar()
    {
        var result = _filter.Mask("this has badword", '#');
        Assert.Equal("this has #######", result);
    }

    [Fact]
    public void Mask_NoMatches_ReturnsOriginal()
    {
        var result = _filter.Mask("clean text");
        Assert.Equal("clean text", result);
    }

    [Fact]
    public void Mask_EmptyString_ReturnsEmpty()
    {
        var result = _filter.Mask("");
        Assert.Equal("", result);
    }

    [Fact]
    public void Mask_WithFixedMask()
    {
        var options = new MaskOptions { FixedMask = "***" };
        var result = _filter.Mask("this has badword in it", '*', options);
        Assert.Equal("this has *** in it", result);
    }

    [Theory]
    [InlineData("prefix_badword_suffix", true)]
    [InlineData("badwordatstart", true)]
    [InlineData("endswitbadword", true)]
    [InlineData("nobadwords", true)] // Contains "badword" as substring
    [InlineData("cleantext", false)]
    public void Contains_SubstringMatching(string text, bool expected)
    {
        // Note: Current implementation matches substrings, not word boundaries
        // "nobadwords" contains "badword" as a substring, so it matches
        Assert.Equal(expected, _filter.Contains(text));
    }

    [Fact]
    public void FindMatches_ReturnsCorrectPositions()
    {
        Span<Matching.MatchResult> results = stackalloc Matching.MatchResult[10];
        int count = _filter.FindMatches("badword and spam", results);

        Assert.Equal(2, count);
        Assert.Equal(0, results[0].StartIndex);
        Assert.Equal(7, results[0].Length);
        Assert.Equal(12, results[1].StartIndex);
        Assert.Equal(4, results[1].Length);
    }
}
