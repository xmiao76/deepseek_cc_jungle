using JungleGame.Bench;
using JungleGame.Core.Model;

namespace JungleGame.Tests.Bench;

public class ArenaProtocolTests
{
    [Fact]
    public void WilsonCi_BalancedSample_ContainsHalfAndIsTight()
    {
        var (lo, hi) = ArenaStats.WilsonCi(9, 18);

        Assert.True(lo < 0.5 && 0.5 < hi);
        Assert.True(hi - lo < 0.5); // n=18: interval is wide but not degenerate
    }

    [Fact]
    public void WilsonCi_ExtremeSample_SkewsToExtreme()
    {
        var (lo, hi) = ArenaStats.WilsonCi(40, 40);

        Assert.True(lo > 0.8);
        Assert.Equal(1, hi);
    }

    [Fact]
    public void WilsonCi_NoDecisiveGames_ReturnsFullRange()
    {
        Assert.Equal((0, 1), ArenaStats.WilsonCi(0, 0));
    }

    [Fact]
    public void BinomialPValue_PerfectSplit_IsOne()
    {
        Assert.Equal(1, ArenaStats.BinomialPValue(9, 18));
    }

    [Fact]
    public void BinomialPValue_LopsidedSample_IsSmall()
    {
        Assert.True(ArenaStats.BinomialPValue(14, 14) < 0.001);
        Assert.True(ArenaStats.BinomialPValue(0, 14) < 0.001);
    }

    [Theory]
    [InlineData(14, 24, false, 0)]  // 58% of 24 decisive → pass
    [InlineData(13, 24, false, 1)]  // 54% → fail
    [InlineData(10, 18, false, 2)]  // below the decisive floor → inconclusive
    [InlineData(10, 18, true, 0)]   // smoke turns inconclusive into pass
    [InlineData(0, 0, false, 2)]
    public void ExitCode_FollowsGateMatrix(int winsA, int decisive, bool smoke, int expected)
    {
        Assert.Equal(expected, ArenaStats.ExitCode(winsA, decisive, smoke));
    }

    [Fact]
    public void Openings_ParsePlies_AppliesLegalPlies()
    {
        var state = Openings.ParsePlies("0,2-0,3 0,6-0,5");

        Assert.Equal(GameStatus.InProgress, state.Status);
        Assert.Equal(Player.Blue, state.CurrentTurn); // two plies played: Blue, Red → Blue again
        // Both plies applied: Blue Elephant on (0,3), Red Rat on (0,5).
        Assert.Equal(Animal.Elephant, state.GetPieceAt(new Position(0, 3))!.Value.Animal);
        Assert.Equal(Animal.Rat, state.GetPieceAt(new Position(0, 5))!.Value.Animal);
    }

    [Theory]
    [InlineData("0,2-0,4")]   // illegal step (Elephant cannot jump)
    [InlineData("9,9-1,1")]   // bad square
    [InlineData("0,2")]       // bad ply format
    public void Openings_ParsePlies_IllegalPly_Throws(string plies)
    {
        Assert.Throws<FormatException>(() => Openings.ParsePlies(plies));
    }

    [Fact]
    public void Openings_LoadImbalanced_ProducesDistinctLivePositions()
    {
        var openings = Openings.LoadImbalanced(8, seed: 42);

        Assert.Equal(8, openings.Count);
        foreach (var opening in openings)
        {
            Assert.Equal(GameStatus.InProgress, opening.Status);
            Assert.Equal(14, opening.Pieces.Count); // 8+8 minus one per side
        }
        // Distinct piece sets.
        Assert.Equal(8, openings.Select(o => string.Join(',',
            o.Pieces.Values.OrderBy(p => p.Owner).ThenBy(p => p.Animal).Select(p => $"{p.Owner}{p.Animal}"))).Distinct().Count());
    }

    [Fact]
    public void Openings_LoadImbalanced_TooMany_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => Openings.LoadImbalanced(65, seed: 42));
    }
}
