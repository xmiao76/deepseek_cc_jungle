using JungleGame.Core.AI;
using JungleGame.Core.Engine;
using JungleGame.Core.Model;
using Xunit;

namespace JungleGame.Tests.AI;

public class TranspositionTableTests
{
    [Fact]
    public void StoreAndProbe_ExactMatch()
    {
        var state = GameState.CreateInitial();
        var tt = new TranspositionTable(1024);

        ulong hash = TranspositionTable.ComputeHash(state);
        var bestMove = MoveGenerator.GenerateLegalMoves(state, Player.Blue)[0];

        tt.Store(hash, 5, 42, bestMove, BoundType.Exact);

        Assert.True(tt.TryProbe(hash, 5, -10000, 10000, out int score, out Move probedMove));
        Assert.Equal(42, score);
        Assert.Equal(bestMove, probedMove);
    }

    [Fact]
    public void Probe_DepthInsufficient_ReturnsFalse()
    {
        var state = GameState.CreateInitial();
        var tt = new TranspositionTable(1024);

        ulong hash = TranspositionTable.ComputeHash(state);
        var bestMove = MoveGenerator.GenerateLegalMoves(state, Player.Blue)[0];

        tt.Store(hash, 3, 42, bestMove, BoundType.Exact);

        Assert.False(tt.TryProbe(hash, 5, -10000, 10000, out _, out _));
    }

    [Fact]
    public void Hash_SameState_SameHash()
    {
        var state1 = GameState.CreateInitial();
        var state2 = GameState.CreateInitial();

        ulong hash1 = TranspositionTable.ComputeHash(state1);
        ulong hash2 = TranspositionTable.ComputeHash(state2);

        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void Hash_DifferentState_DifferentHash()
    {
        var state1 = GameState.CreateInitial();
        var moves = MoveGenerator.GenerateLegalMoves(state1, Player.Blue);
        var state2 = GameController.ApplyMove(state1, moves[0]);

        ulong hash1 = TranspositionTable.ComputeHash(state1);
        ulong hash2 = TranspositionTable.ComputeHash(state2);

        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void LowerBound_Cutoff_WhenAboveBeta()
    {
        var state = GameState.CreateInitial();
        var tt = new TranspositionTable(1024);

        ulong hash = TranspositionTable.ComputeHash(state);
        var bestMove = MoveGenerator.GenerateLegalMoves(state, Player.Blue)[0];

        tt.Store(hash, 5, 500, bestMove, BoundType.LowerBound);

        // Probe with beta <= 500 should return the lower-bound score
        Assert.True(tt.TryProbe(hash, 5, -10000, 400, out int score, out _));
        Assert.Equal(500, score);
    }

    [Fact]
    public void UpperBound_Cutoff_WhenBelowAlpha()
    {
        var state = GameState.CreateInitial();
        var tt = new TranspositionTable(1024);

        ulong hash = TranspositionTable.ComputeHash(state);
        var bestMove = MoveGenerator.GenerateLegalMoves(state, Player.Blue)[0];

        tt.Store(hash, 5, -100, bestMove, BoundType.UpperBound);

        // Probe with alpha >= -100 should return the upper-bound score
        Assert.True(tt.TryProbe(hash, 5, 0, 10000, out int score, out _));
        Assert.Equal(-100, score);
    }
}
