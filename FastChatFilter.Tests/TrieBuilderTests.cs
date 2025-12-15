using System.IO;
using System.Threading.Tasks;
using FastChatFilter.Compiler;
using Xunit;

namespace FastChatFilter.Tests;

public class HybridBuilderTests
{
    [Fact]
    public void Add_SingleWord_CreatesNodesAndHash()
    {
        var builder = new HybridBuilder();
        builder.Add("test");

        // Root + 4 nodes for "test"
        Assert.Equal(5, builder.NodeCount);
        // 4 edges: t->e, e->s, s->t
        Assert.Equal(4, builder.EdgeCount);
        // 1 hash for "test"
        Assert.Equal(1, builder.HashCount);
    }

    [Fact]
    public void Add_CommonPrefix_SharesNodes()
    {
        var builder = new HybridBuilder();
        builder.Add("test");
        builder.Add("testing");

        // Root + "test" (4) + "ing" (3) = 8
        Assert.Equal(8, builder.NodeCount);
        // 2 hashes for "test" and "testing"
        Assert.Equal(2, builder.HashCount);
    }

    [Fact]
    public void Add_DuplicateWord_NoDuplicateNodesOrHashes()
    {
        var builder = new HybridBuilder();
        builder.Add("test");
        int nodeCountAfterFirst = builder.NodeCount;
        int hashCountAfterFirst = builder.HashCount;

        builder.Add("test");
        Assert.Equal(nodeCountAfterFirst, builder.NodeCount);
        Assert.Equal(hashCountAfterFirst, builder.HashCount);
    }

    [Fact]
    public void Add_EmptyString_Ignored()
    {
        var builder = new HybridBuilder();
        builder.Add("");
        builder.Add(null!);

        // Only root node
        Assert.Equal(1, builder.NodeCount);
        Assert.Equal(0, builder.EdgeCount);
        Assert.Equal(0, builder.HashCount);
    }

    [Fact]
    public void Build_SortsEdgesByCharacter()
    {
        var builder = new HybridBuilder();
        builder.Add("c");
        builder.Add("a");
        builder.Add("b");

        var (nodes, edges, hashes) = builder.Build();

        // Root node should have edges sorted: a, b, c
        Assert.Equal('a', edges[0].Character);
        Assert.Equal('b', edges[1].Character);
        Assert.Equal('c', edges[2].Character);
    }

    [Fact]
    public async Task Build_ProducesValidBinary()
    {
        var builder = new HybridBuilder();
        builder.Add("hello");
        builder.Add("world");

        var path = Path.GetTempFileName();
        try
        {
            await HybridBinaryWriter.WriteAsync(path, builder);

            // Verify file was created and has content
            var fileInfo = new FileInfo(path);
            Assert.True(fileInfo.Exists);
            Assert.True(fileInfo.Length > 32); // At least header size
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void AddRange_AddsMultipleWords()
    {
        var builder = new HybridBuilder();
        builder.AddRange(new[] { "one", "two", "three" });

        // All words should be added
        var (nodes, edges, hashes) = builder.Build();

        // Check nodes and hashes exist
        Assert.True(builder.NodeCount > 1);
        Assert.Equal(3, builder.HashCount);
    }

    [Fact]
    public void MinMaxWordLength_TracksCorrectly()
    {
        var builder = new HybridBuilder();
        builder.Add("ab");        // length 2
        builder.Add("abcde");     // length 5
        builder.Add("abc");       // length 3

        Assert.Equal(2, builder.MinWordLength);
        Assert.Equal(5, builder.MaxWordLength);
    }
}
