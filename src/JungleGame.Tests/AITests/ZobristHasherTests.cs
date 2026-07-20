using JungleGame.Core.AI;
using JungleGame.Core.Logic;
using JungleGame.Core.Models;

namespace JungleGame.Tests.AITests;

public class ZobristHasherTests
{
    [Fact]
    public void SamePosition_ProducesSameHash()
    {
        var hasher = new ZobristHasher();
        var engine = new GameEngine();
        var state1 = engine.CreateInitialState();
        var state2 = engine.CreateInitialState();

        ulong hash1 = hasher.ComputeHash(state1.Pieces, state1.CurrentPlayer);
        ulong hash2 = hasher.ComputeHash(state2.Pieces, state2.CurrentPlayer);

        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void DifferentPositions_ProduceDifferentHashes()
    {
        var hasher = new ZobristHasher();
        var engine = new GameEngine();
        var state1 = engine.CreateInitialState();

        // Apply a move to get a different position
        var moves = engine.GetLegalMoves(state1);
        var state2 = engine.ApplyMove(state1, moves[0]);

        ulong hash1 = hasher.ComputeHash(state1.Pieces, state1.CurrentPlayer);
        ulong hash2 = hasher.ComputeHash(state2.Pieces, state2.CurrentPlayer);

        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void DifferentPlayer_ProducesDifferentHash()
    {
        var hasher = new ZobristHasher();
        var engine = new GameEngine();
        var state = engine.CreateInitialState();

        ulong blueHash = hasher.ComputeHash(state.Pieces, Player.Blue);
        ulong redHash = hasher.ComputeHash(state.Pieces, Player.Red);

        Assert.NotEqual(blueHash, redHash);
    }
}
