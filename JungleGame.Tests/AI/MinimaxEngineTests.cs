using JungleGame.Core.AI;
using JungleGame.Core.Engine;
using JungleGame.Core.Model;
using Xunit;

namespace JungleGame.Tests.AI;

public class MinimaxEngineTests
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
    public void AI_ProducesLegalMove()
    {
        var state = GameState.CreateInitial();
        var engine = new MinimaxEngine(TimeSpan.FromSeconds(2));
        var move = engine.FindBestMove(state);

        // The move must be legal
        Assert.NotNull(move);
        var error = JungleGame.Core.Rules.MoveValidator.Validate(state, move!.Value.From, move.Value.To);
        Assert.Null(error);
    }

    [Fact]
    public void AI_FindsMateInOne_DenCapture()
    {
        // Setup: Blue Lion adjacent to Red's den, undefended
        var state = CreateState(new Dictionary<Position, Piece>
        {
            [new Position(3, 7)] = new Piece(Animal.Lion, Player.Blue, new Position(3, 7)),
            [new Position(0, 0)] = new Piece(Animal.Rat, Player.Red, new Position(0, 0))
        }, Player.Blue);

        var engine = new MinimaxEngine(TimeSpan.FromSeconds(5));
        var move = engine.FindBestMove(state);

        // Should move Lion into Red's den at (3,8)
        Assert.NotNull(move);
        Assert.Equal(new Position(3, 7), move!.Value.From);
        Assert.Equal(new Position(3, 8), move.Value.To);
    }

    [Fact]
    public void AI_AvoidsMovingHighPieceIntoTrap()
    {
        // Setup: high-value Blue piece adjacent to Red trap with safe alternatives.
        // The evaluation function must score the same position with the Elephant ON
        // the enemy trap lower than with it off the trap.
        var onTrap = CreateState(new Dictionary<Position, Piece>
        {
            [new Position(2, 8)] = new Piece(Animal.Elephant, Player.Blue, new Position(2, 8)),
            [new Position(4, 7)] = new Piece(Animal.Wolf, Player.Blue, new Position(4, 7)),
            [new Position(0, 0)] = new Piece(Animal.Rat, Player.Blue, new Position(0, 0)),
            [new Position(1, 8)] = new Piece(Animal.Cat, Player.Red, new Position(1, 8)),
            [new Position(0, 5)] = new Piece(Animal.Dog, Player.Red, new Position(0, 5))
        }, Player.Blue);
        var offTrap = CreateState(new Dictionary<Position, Piece>
        {
            [new Position(2, 7)] = new Piece(Animal.Elephant, Player.Blue, new Position(2, 7)),
            [new Position(4, 7)] = new Piece(Animal.Wolf, Player.Blue, new Position(4, 7)),
            [new Position(0, 0)] = new Piece(Animal.Rat, Player.Blue, new Position(0, 0)),
            [new Position(1, 8)] = new Piece(Animal.Cat, Player.Red, new Position(1, 8)),
            [new Position(0, 5)] = new Piece(Animal.Dog, Player.Red, new Position(0, 5))
        }, Player.Blue);

        int onTrapEval = JungleGame.Core.AI.EvaluationFunction.Evaluate(onTrap, Player.Blue);
        int offTrapEval = JungleGame.Core.AI.EvaluationFunction.Evaluate(offTrap, Player.Blue);
        Assert.True(onTrapEval < offTrapEval,
            $"Elephant on enemy trap ({onTrapEval}) should score below off-trap ({offTrapEval})");

        // The engine must produce a legal move and not voluntarily walk onto the trap
        var engine = new MinimaxEngine(TimeSpan.FromSeconds(2));
        var move = engine.FindBestMove(offTrap);

        Assert.NotNull(move);
        var error = JungleGame.Core.Rules.MoveValidator.Validate(offTrap, move!.Value.From, move.Value.To);
        Assert.Null(error);
        Assert.NotEqual(new Position(2, 8), move.Value.To);
    }

    [Fact]
    public void AI_From_Midgame_Position_ProducesLegalMove()
    {
        // Play several moves to get a midgame position, then test AI
        var state = GameState.CreateInitial();
        var random = new Random(42);

        for (int i = 0; i < 8 && state.Status == GameStatus.InProgress; i++)
        {
            var moves = MoveGenerator.GenerateLegalMoves(state, state.CurrentTurn);
            var move = moves[random.Next(moves.Count)];
            state = GameController.ApplyMove(state, move);
        }

        if (state.Status == GameStatus.InProgress)
        {
            var engine = new MinimaxEngine(TimeSpan.FromSeconds(2));
            var move = engine.FindBestMove(state);
            Assert.NotNull(move);
            var error = JungleGame.Core.Rules.MoveValidator.Validate(state, move!.Value.From, move.Value.To);
            Assert.Null(error);
        }
    }

    [Fact]
    public void AI_Handles_SingleLegalMove()
    {
        // Setup: exactly one legal move — the Wolf is boxed in by uncapturable
        // Elephants on two sides and water on the other two, with one land square
        // left open. Exercises the single-move fast path in FindBestMove.
        var state = CreateState(new Dictionary<Position, Piece>
        {
            [new Position(3, 3)] = new Piece(Animal.Wolf, Player.Blue, new Position(3, 3)),
            [new Position(3, 2)] = new Piece(Animal.Elephant, Player.Red, new Position(3, 2)),
            [new Position(0, 8)] = new Piece(Animal.Rat, Player.Red, new Position(0, 8))
        }, Player.Blue);

        var engine = new MinimaxEngine(TimeSpan.FromSeconds(5));
        var move = engine.FindBestMove(state);

        Assert.NotNull(move);
        Assert.Equal(new Position(3, 3), move!.Value.From);
        Assert.Equal(new Position(3, 4), move.Value.To);
    }

    [Fact]
    public void Qsearch_FindsStrongTakesWeak_Capture()
    {
        // The quiescence search must see strong-takes-weak captures at the horizon.
        // Blue Wolf prepares Wolf-takes-Dog; the Dog is boxed in (water on both
        // sides, its only escape square occupied by a friendly Rat that cannot
        // recapture). Old quiescence filter (defenderVal >= attackerVal) excluded
        // this capture.
        var state = CreateState(new Dictionary<Position, Piece>
        {
            [new Position(3, 2)] = new Piece(Animal.Wolf, Player.Blue, new Position(3, 2)),
            [new Position(0, 0)] = new Piece(Animal.Lion, Player.Blue, new Position(0, 0)),
            [new Position(3, 4)] = new Piece(Animal.Dog, Player.Red, new Position(3, 4)),
            [new Position(3, 5)] = new Piece(Animal.Rat, Player.Red, new Position(3, 5)),
            [new Position(0, 1)] = new Piece(Animal.Cat, Player.Red, new Position(0, 1)),
            [new Position(6, 8)] = new Piece(Animal.Rat, Player.Red, new Position(6, 8))
        }, Player.Blue);

        var engine = new MinimaxEngine(TimeSpan.FromSeconds(5), 2, useTablebase: false);
        var move = engine.FindBestMove(state);

        Assert.NotNull(move);
        Assert.Equal(new Position(3, 2), move!.Value.From);
        Assert.Equal(new Position(3, 3), move.Value.To);
    }

    [Fact]
    public void Engine_FindsMateInTwo_ViaHorizonDenEntry()
    {
        // Mate in 2: Wolf steps onto the trap square, then enters the den. The den entry
        // happens at the search horizon (depth 0), so only a quiescence search that
        // includes enemy-den entries can see the mate. The material alternative
        // (Wolf takes Dog) must not be preferred over the mate.
        var state = CreateState(new Dictionary<Position, Piece>
        {
            [new Position(3, 6)] = new Piece(Animal.Wolf, Player.Blue, new Position(3, 6)),
            [new Position(0, 0)] = new Piece(Animal.Cat, Player.Blue, new Position(0, 0)),
            [new Position(2, 6)] = new Piece(Animal.Dog, Player.Red, new Position(2, 6)),
            [new Position(0, 8)] = new Piece(Animal.Rat, Player.Red, new Position(0, 8))
        }, Player.Blue);

        var engine = new MinimaxEngine(TimeSpan.FromSeconds(5), 2, useTablebase: false);
        var move = engine.FindBestMove(state);

        Assert.NotNull(move);
        Assert.Equal(new Position(3, 6), move!.Value.From);
        Assert.Equal(new Position(3, 7), move.Value.To);
    }

    [Fact]
    public void FindBestMove_ReturnsNull_WhenNoMoves()
    {
        // Blue Wolf is boxed in: Elephants (uncapturable) on both land neighbors,
        // water on the other two. Blue has zero legal moves.
        var state = CreateState(new Dictionary<Position, Piece>
        {
            [new Position(3, 3)] = new Piece(Animal.Wolf, Player.Blue, new Position(3, 3)),
            [new Position(3, 2)] = new Piece(Animal.Elephant, Player.Red, new Position(3, 2)),
            [new Position(3, 4)] = new Piece(Animal.Elephant, Player.Red, new Position(3, 4)),
            [new Position(0, 8)] = new Piece(Animal.Rat, Player.Red, new Position(0, 8))
        }, Player.Blue);

        var engine = new MinimaxEngine(TimeSpan.FromSeconds(5));
        Assert.Null(engine.FindBestMove(state));
    }
}
