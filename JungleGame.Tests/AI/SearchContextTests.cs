using JungleGame.Core.AI;

namespace JungleGame.Tests.AI;

public class SearchContextTests
{
    [Fact]
    public void IsRepetition_ThirdOccurrence_ReturnsTrue()
    {
        var context = new SearchContext();
        ulong hash = 0x123456789ABCDEF;

        Assert.False(context.IsRepetition(hash));
        context.Push(hash); // 1st occurrence
        Assert.False(context.IsRepetition(hash));
        context.Push(hash); // 2nd occurrence
        Assert.Equal(2, context.PathLength);
        Assert.False(context.IsRepetition(0xDEADBEEF));

        // Two prior occurrences = the position is the 3rd occurrence: a draw.
        Assert.True(context.IsRepetition(hash));

        // A second hash interleaved does not disturb the first's count
        context.Push(0xDEADBEEF);
        Assert.True(context.IsRepetition(hash));
        Assert.False(context.IsRepetition(0xDEADBEEF));
    }

    [Fact]
    public void Pop_RestoresCounts_AndAllowsReuse()
    {
        var context = new SearchContext();
        ulong a = 1, b = 2;

        context.Push(a);
        context.Push(b);
        context.Push(a); // count(a) = 2 → repetition
        Assert.True(context.IsRepetition(a));
        context.Pop(a);
        Assert.False(context.IsRepetition(a));
        Assert.Equal(1, context.CountOf(a));
        context.Pop(a);
        context.Pop(b);
        Assert.Equal(0, context.PathLength);

        // After full removal the hash is fresh again
        Assert.False(context.IsRepetition(a));
        context.Push(a);
        Assert.False(context.IsRepetition(a));
    }

    [Fact]
    public void ManyDistinctPushPopCycles_ProbesTerminate()
    {
        // Regression: a search pushes millions of distinct positions; count-0
        // slots must be reclaimed (backward-shift deletion) or probes spin
        // forever once the table fills with stale hashes.
        var context = new SearchContext();
        var rng = new Random(42);

        for (int round = 0; round < 100_000; round++)
        {
            ulong hash = ((ulong)(uint)rng.Next() << 32) | (uint)rng.Next();
            context.Push(hash);
            Assert.False(context.IsRepetition(hash));
            Assert.Equal(1, context.CountOf(hash));
            context.Pop(hash);
            Assert.Equal(0, context.PathLength);
        }
    }

    [Fact]
    public void CollidingHashes_ProbeCorrectly()
    {
        // Hashes with the same table slot exercise the linear-probe chains.
        var context = new SearchContext();
        // TableSlots = 512: construct hashes identical in the low 9 bits.
        ulong h1 = 0x1000, h2 = 0x2000, h3 = 0x3000; // all map to slot 0

        context.Push(h1);
        context.Push(h2);
        context.Push(h3);
        Assert.Equal(1, context.CountOf(h1));
        Assert.Equal(1, context.CountOf(h2));
        Assert.Equal(1, context.CountOf(h3));

        // Delete the middle entry: the cluster must shift back so h3 stays findable.
        context.Pop(h2);
        Assert.Equal(0, context.CountOf(h2));
        Assert.Equal(1, context.CountOf(h1));
        Assert.Equal(1, context.CountOf(h3));

        // Re-insert a probe-chain member and confirm counts stay distinct.
        context.Push(h2);
        Assert.Equal(1, context.CountOf(h2));
        context.Pop(h1);
        context.Pop(h2);
        context.Pop(h3);
        Assert.Equal(0, context.PathLength);
    }
}
