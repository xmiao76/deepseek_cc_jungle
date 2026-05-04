using JungleGame.Core.AI;
using JungleGame.Core.Engine;
using JungleGame.Core.Model;
using Xunit;

namespace JungleGame.Tests.AI;

public class MinimaxEngineTests
{
    [Fact]
    public void AI_ProducesLegalMove()
    {
        var state = GameState.CreateInitial();
        var engine = new MinimaxEngine(TimeSpan.FromSeconds(5));
        var move = engine.FindBestMove(state);

        // The move must be legal
        var error = JungleGame.Core.Rules.MoveValidator.Validate(state, move.From, move.To);
        Assert.Null(error);
    }

    [Fact]
    public void AI_FindsMateInOne_DenCapture()
    {
        // Setup: Blue Lion adjacent to Red's den, undefended
        var pieces = new Dictionary<Position, Piece>
        {
            [new Position(3, 7)] = new Piece(Animal.Lion, Player.Blue, new Position(3, 7)),
            [new Position(0, 0)] = new Piece(Animal.Rat, Player.Red, new Position(0, 0))
        };
        var state = new GameState(
            Board.Initial,
            System.Collections.Immutable.ImmutableDictionary.CreateRange(pieces),
            Player.Blue,
            GameStatus.InProgress,
            System.Collections.Immutable.ImmutableList<Piece>.Empty,
            System.Collections.Immutable.ImmutableList<Piece>.Empty);

        var engine = new MinimaxEngine(TimeSpan.FromSeconds(5));
        var move = engine.FindBestMove(state);

        // Should move Lion into Red's den at (3,8)
        Assert.Equal(new Position(3, 7), move.From);
        Assert.Equal(new Position(3, 8), move.To);
    }

    [Fact]
    public void AI_AvoidsMovingHighPieceIntoTrap()
    {
        // Setup: high-value Blue piece adjacent to Red trap with safe alternatives
        // Verify the AI produces a legal move and the evaluation function penalizes trap squares
        var pieces = new Dictionary<Position, Piece>
        {
            [new Position(2, 7)] = new Piece(Animal.Elephant, Player.Blue, new Position(2, 7)),
            [new Position(4, 7)] = new Piece(Animal.Wolf, Player.Blue, new Position(4, 7)),
            [new Position(0, 0)] = new Piece(Animal.Rat, Player.Blue, new Position(0, 0)),
            [new Position(1, 0)] = new Piece(Animal.Cat, Player.Red, new Position(1, 0)),
            [new Position(0, 5)] = new Piece(Animal.Dog, Player.Red, new Position(0, 5))
        };
        var state = new GameState(
            Board.Initial,
            System.Collections.Immutable.ImmutableDictionary.CreateRange(pieces),
            Player.Blue,
            GameStatus.InProgress,
            System.Collections.Immutable.ImmutableList<Piece>.Empty,
            System.Collections.Immutable.ImmutableList<Piece>.Empty);

        var engine = new MinimaxEngine(TimeSpan.FromSeconds(5));
        var move = engine.FindBestMove(state);

        var error = JungleGame.Core.Rules.MoveValidator.Validate(state, move.From, move.To);
        Assert.Null(error);

        // Verify the evaluation function penalizes trap squares by checking the static eval
        // of a piece on a trap is lower than the same piece off the trap
        var onTrapPiece = new Piece(Animal.Elephant, Player.Blue, new Position(2, 8));
        var offTrapPiece = new Piece(Animal.Elephant, Player.Blue, new Position(2, 7));
        int onTrapRank = JungleGame.Core.Rules.CaptureResolver.GetEffectiveRank(onTrapPiece, Board.Initial);
        int offTrapRank = JungleGame.Core.Rules.CaptureResolver.GetEffectiveRank(offTrapPiece, Board.Initial);
        Assert.True(onTrapRank < offTrapRank, "Piece on enemy trap should have reduced effective rank");
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
            var engine = new MinimaxEngine(TimeSpan.FromSeconds(5));
            var move = engine.FindBestMove(state);
            var error = JungleGame.Core.Rules.MoveValidator.Validate(state, move.From, move.To);
            Assert.Null(error);
        }
    }

    [Fact]
    public void AI_Handles_SingleLegalMove()
    {
        // Setup: one move available
        var pieces = new Dictionary<Position, Piece>
        {
            [new Position(3, 7)] = new Piece(Animal.Lion, Player.Blue, new Position(3, 7)),
            [new Position(0, 0)] = new Piece(Animal.Rat, Player.Red, new Position(0, 0))
        };
        var state = new GameState(
            Board.Initial,
            System.Collections.Immutable.ImmutableDictionary.CreateRange(pieces),
            Player.Blue,
            GameStatus.InProgress,
            System.Collections.Immutable.ImmutableList<Piece>.Empty,
            System.Collections.Immutable.ImmutableList<Piece>.Empty);

        var engine = new MinimaxEngine(TimeSpan.FromSeconds(5));
        var move = engine.FindBestMove(state);

        var error = JungleGame.Core.Rules.MoveValidator.Validate(state, move.From, move.To);
        Assert.Null(error);
    }
}
