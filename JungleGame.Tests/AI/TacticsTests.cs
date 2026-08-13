using JungleGame.Core.AI;
using JungleGame.Core.Engine;
using JungleGame.Core.Model;
using Xunit;

namespace JungleGame.Tests.AI;

/// <summary>
/// Tactical strength regression suite: each test asserts a specific best move in a
/// constructed position. These guard against future engine regressions (search,
/// quiescence, or evaluation) that would make the engine miss the tactic.
/// </summary>
public class TacticsTests
{
    private static GameState CreateState(Dictionary<Position, Piece> pieces, Player turn)
        => new(
            Board.Initial,
            System.Collections.Immutable.ImmutableDictionary.CreateRange(pieces),
            turn,
            GameStatus.InProgress,
            System.Collections.Immutable.ImmutableList<Piece>.Empty,
            System.Collections.Immutable.ImmutableList<Piece>.Empty);

    [Fact]
    public void AI_WinsMaterial_InThreePly_Fork()
    {
        // Blue Lion steps forward and forks Red's Wolf and Dog: Red can save only
        // one, and Blue captures the other.
        var state = CreateState(new Dictionary<Position, Piece>
        {
            [new Position(3, 1)] = new Piece(Animal.Lion, Player.Blue, new Position(3, 1)),
            [new Position(0, 0)] = new Piece(Animal.Cat, Player.Blue, new Position(0, 0)),
            [new Position(2, 2)] = new Piece(Animal.Wolf, Player.Red, new Position(2, 2)),
            [new Position(4, 2)] = new Piece(Animal.Dog, Player.Red, new Position(4, 2)),
            [new Position(6, 8)] = new Piece(Animal.Rat, Player.Red, new Position(6, 8))
        }, Player.Blue);

        var engine = new MinimaxEngine(TimeSpan.FromSeconds(5), 3);
        var move = engine.FindBestMove(state);

        Assert.NotNull(move);
        Assert.Equal(new Position(3, 1), move!.Value.From);
        Assert.Equal(new Position(3, 2), move.Value.To);
    }

    [Fact]
    public void AI_DefendsDen_ByCapturingInvader()
    {
        // Red Wolf sits on Blue's trap, one step from Blue's den. Blue must capture
        // it immediately (rank 0 on the trap) or lose the game.
        var state = CreateState(new Dictionary<Position, Piece>
        {
            [new Position(3, 2)] = new Piece(Animal.Cat, Player.Blue, new Position(3, 2)),
            [new Position(0, 0)] = new Piece(Animal.Lion, Player.Blue, new Position(0, 0)),
            [new Position(3, 1)] = new Piece(Animal.Wolf, Player.Red, new Position(3, 1)),
            [new Position(6, 8)] = new Piece(Animal.Rat, Player.Red, new Position(6, 8))
        }, Player.Blue);

        var engine = new MinimaxEngine(TimeSpan.FromSeconds(5), 2);
        var move = engine.FindBestMove(state);

        Assert.NotNull(move);
        Assert.Equal(new Position(3, 2), move!.Value.From);
        Assert.Equal(new Position(3, 1), move.Value.To);
    }

    [Fact]
    public void AI_DoesNotBlunder_ElephantNextToRat()
    {
        // Blue Elephant at (3,4) must not step to (3,3): Red's Rat at (3,2) is
        // adjacent to that square and captures Elephants. Whatever quiet move the
        // engine prefers, walking into the Rat's jaws loses the Elephant.
        var state = CreateState(new Dictionary<Position, Piece>
        {
            [new Position(3, 4)] = new Piece(Animal.Elephant, Player.Blue, new Position(3, 4)),
            [new Position(0, 0)] = new Piece(Animal.Cat, Player.Blue, new Position(0, 0)),
            [new Position(3, 2)] = new Piece(Animal.Rat, Player.Red, new Position(3, 2)),
            [new Position(6, 8)] = new Piece(Animal.Wolf, Player.Red, new Position(6, 8))
        }, Player.Blue);

        var engine = new MinimaxEngine(TimeSpan.FromSeconds(5), 2);
        var move = engine.FindBestMove(state);

        Assert.NotNull(move);
        Assert.NotEqual(new Position(3, 3), move!.Value.To);
    }

    [Fact]
    public void AI_RefusesFreeTrapEntry()
    {
        // Both trap squares the Wolf could reach are guarded: (2,8) by Red's Cat,
        // (3,7) by Red's Rat (each captures the rank-0 Wolf). The engine must
        // choose a safe square instead.
        var state = CreateState(new Dictionary<Position, Piece>
        {
            [new Position(2, 7)] = new Piece(Animal.Wolf, Player.Blue, new Position(2, 7)),
            [new Position(0, 0)] = new Piece(Animal.Lion, Player.Blue, new Position(0, 0)),
            [new Position(1, 8)] = new Piece(Animal.Cat, Player.Red, new Position(1, 8)),
            [new Position(3, 6)] = new Piece(Animal.Rat, Player.Red, new Position(3, 6)),
            [new Position(6, 8)] = new Piece(Animal.Rat, Player.Red, new Position(6, 8))
        }, Player.Blue);

        var engine = new MinimaxEngine(TimeSpan.FromSeconds(5), 2);
        var move = engine.FindBestMove(state);

        Assert.NotNull(move);
        Assert.DoesNotContain(move!.Value.To, new[] { new Position(2, 8), new Position(3, 7) });
    }

    [Fact]
    public void AI_FindsMateInTwo_CaptureThenDenEntry()
    {
        // Wolf captures the Dog on the trap square, then enters the undefended den.
        var state = CreateState(new Dictionary<Position, Piece>
        {
            [new Position(3, 6)] = new Piece(Animal.Wolf, Player.Blue, new Position(3, 6)),
            [new Position(0, 0)] = new Piece(Animal.Cat, Player.Blue, new Position(0, 0)),
            [new Position(3, 7)] = new Piece(Animal.Dog, Player.Red, new Position(3, 7)),
            [new Position(0, 8)] = new Piece(Animal.Rat, Player.Red, new Position(0, 8))
        }, Player.Blue);

        var engine = new MinimaxEngine(TimeSpan.FromSeconds(5), 2);
        var move = engine.FindBestMove(state);

        Assert.NotNull(move);
        Assert.Equal(new Position(3, 6), move!.Value.From);
        Assert.Equal(new Position(3, 7), move.Value.To);
    }
}
