using JungleGame.Core.AI;

namespace JungleGame.Tests.AITests;

public class TranspositionTableTests
{
    [Fact]
    public void StoreAndLookup_ExactScore()
    {
        var tt = new TranspositionTable(1024);
        tt.Store(12345UL, 5, 100, TranspositionFlag.Exact, null);

        bool found = tt.TryLookup(12345UL, 5, -10000, 10000, out int score, out _);

        Assert.True(found);
        Assert.Equal(100, score);
    }

    [Fact]
    public void Lookup_DepthTooShallow_ReturnsFalse()
    {
        var tt = new TranspositionTable(1024);
        tt.Store(12345UL, 3, 100, TranspositionFlag.Exact, null);

        bool found = tt.TryLookup(12345UL, 5, -10000, 10000, out _, out _);

        Assert.False(found);
    }

    [Fact]
    public void Lookup_DepthEqualOrGreater_ReturnsTrue()
    {
        var tt = new TranspositionTable(1024);
        tt.Store(12345UL, 5, 200, TranspositionFlag.Exact, null);

        Assert.True(tt.TryLookup(12345UL, 5, -10000, 10000, out int s1, out _));
        Assert.Equal(200, s1);
        Assert.True(tt.TryLookup(12345UL, 3, -10000, 10000, out int s2, out _));
        Assert.Equal(200, s2);
    }

    [Fact]
    public void DifferentKeys_DontCollide()
    {
        var tt = new TranspositionTable(1024);
        tt.Store(11111UL, 5, 100, TranspositionFlag.Exact, null);

        bool found = tt.TryLookup(22222UL, 5, -10000, 10000, out _, out _);
        Assert.False(found);
    }

    [Fact]
    public void LowerBound_BetaCutoff()
    {
        var tt = new TranspositionTable(1024);
        tt.Store(42UL, 5, 500, TranspositionFlag.LowerBound, null);

        // Alpha=400, Beta=450: entry score 500 >= beta 450 => cutoff
        bool found = tt.TryLookup(42UL, 5, 400, 450, out int score, out _);
        Assert.True(found);
        Assert.Equal(500, score);
    }

    [Fact]
    public void UpperBound_AlphaCutoff()
    {
        var tt = new TranspositionTable(1024);
        tt.Store(42UL, 5, -300, TranspositionFlag.UpperBound, null);

        // Alpha=-300, Beta=-250: entry score -300 <= alpha -300 => cutoff
        bool found = tt.TryLookup(42UL, 5, -300, -250, out int score, out _);
        Assert.True(found);
        Assert.Equal(-300, score);
    }

    [Fact]
    public void Clear_RemovesAllEntries()
    {
        var tt = new TranspositionTable(1024);
        tt.Store(1UL, 1, 100, TranspositionFlag.Exact, null);
        tt.Clear();

        Assert.False(tt.TryLookup(1UL, 1, -10000, 10000, out _, out _));
    }
}
