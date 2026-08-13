using JungleGame.Core.Engine;
using JungleGame.Core.Model;
using Xunit;

namespace JungleGame.Tests.Engine;

public class GameControllerTests
{
    private static GameState CreateState(Dictionary<Position, Piece> pieces, Player turn, GameStatus status)
        => new(
            Board.Initial,
            System.Collections.Immutable.ImmutableDictionary.CreateRange(pieces),
            turn,
            status,
            System.Collections.Immutable.ImmutableList<Piece>.Empty,
            System.Collections.Immutable.ImmutableList<Piece>.Empty);

    private static Move FindMove(GameState state, Position from, Position to)
        => MoveGenerator.GenerateLegalMoves(state, state.CurrentTurn).First(m => m.From == from && m.To == to);

    [Fact]
    public void ApplyMove_Throws_WhenGameOver()
    {
        var terminal = CreateState(new Dictionary<Position, Piece>
        {
            [new Position(0, 0)] = new Piece(Animal.Lion, Player.Blue, new Position(0, 0)),
            [new Position(0, 8)] = new Piece(Animal.Rat, Player.Red, new Position(0, 8))
        }, Player.Blue, GameStatus.BlueWins);

        var move = new Move(new Position(0, 0), new Position(1, 0));
        Assert.Throws<InvalidOperationException>(() => GameController.ApplyMove(terminal, move));
    }

    [Fact]
    public void ApplyMove_DetectsThreeFoldRepetition_AsDraw()
    {
        // Wolf and Rat shuffle back and forth. Each full cycle returns to the
        // starting position; the third occurrence is a draw.
        var state = CreateState(new Dictionary<Position, Piece>
        {
            [new Position(3, 3)] = new Piece(Animal.Wolf, Player.Blue, new Position(3, 3)),
            [new Position(0, 3)] = new Piece(Animal.Rat, Player.Red, new Position(0, 3))
        }, Player.Blue, GameStatus.InProgress);

        void ShuffleCycle()
        {
            state = GameController.ApplyMove(state, FindMove(state, new Position(3, 3), new Position(3, 2)));
            state = GameController.ApplyMove(state, FindMove(state, new Position(0, 3), new Position(0, 2)));
            state = GameController.ApplyMove(state, FindMove(state, new Position(3, 2), new Position(3, 3)));
            state = GameController.ApplyMove(state, FindMove(state, new Position(0, 2), new Position(0, 3)));
        }

        ShuffleCycle();
        ShuffleCycle();
        Assert.Equal(GameStatus.InProgress, state.Status);

        // Every position in the cycle occurs once per cycle, so the first move
        // of the third cycle reaches its third occurrence and is a draw.
        state = GameController.ApplyMove(state, FindMove(state, new Position(3, 3), new Position(3, 2)));
        Assert.Equal(GameStatus.Draw, state.Status);

        // And no further moves may be applied to a finished game
        Assert.Throws<InvalidOperationException>(
            () => GameController.ApplyMove(state, new Move(new Position(0, 3), new Position(0, 2))));
    }

    [Fact]
    public void Position_WithNoLegalMoves_IsLossForSideToMove()
    {
        // Blue Wolf is boxed in by two Red Elephants (uncapturable) and water.
        // After Red's move, Blue has no legal moves and loses.
        var state = CreateState(new Dictionary<Position, Piece>
        {
            [new Position(3, 3)] = new Piece(Animal.Wolf, Player.Blue, new Position(3, 3)),
            [new Position(3, 2)] = new Piece(Animal.Elephant, Player.Red, new Position(3, 2)),
            [new Position(3, 4)] = new Piece(Animal.Elephant, Player.Red, new Position(3, 4)),
            [new Position(0, 8)] = new Piece(Animal.Rat, Player.Red, new Position(0, 8))
        }, Player.Red, GameStatus.InProgress);

        state = GameController.ApplyMove(state, new Move(new Position(0, 8), new Position(0, 7)));

        Assert.Equal(GameStatus.RedWins, state.Status);
    }

    [Fact]
    public void ApplyMove_ChangesTurn()
    {
        var state = GameState.CreateInitial();
        Assert.Equal(Player.Blue, state.CurrentTurn);

        var moves = MoveGenerator.GenerateLegalMoves(state, Player.Blue);
        var move = moves[0];
        state = GameController.ApplyMove(state, move);
        Assert.Equal(Player.Red, state.CurrentTurn);
    }

    [Fact]
    public void ApplyMove_Capture_RemovesDefender()
    {
        // Setup: Blue Lion next to Red Cat
        var pieces = new Dictionary<Position, Piece>
        {
            [new Position(3, 3)] = new Piece(Animal.Lion, Player.Blue, new Position(3, 3)),
            [new Position(3, 4)] = new Piece(Animal.Cat, Player.Red, new Position(3, 4))
        };
        var state = new GameState(
            Board.Initial,
            System.Collections.Immutable.ImmutableDictionary.CreateRange(pieces),
            Player.Blue,
            GameStatus.InProgress,
            System.Collections.Immutable.ImmutableList<Piece>.Empty,
            System.Collections.Immutable.ImmutableList<Piece>.Empty);

        var move = new Move(new Position(3, 3), new Position(3, 4),
            state.GetPieceAt(new Position(3, 4)));
        state = GameController.ApplyMove(state, move);

        // Lion moved FROM (3,3) TO (3,4), capturing Cat. (3,3) should be empty, (3,4) has Lion.
        Assert.Null(state.GetPieceAt(new Position(3, 3)));
        Assert.NotNull(state.GetPieceAt(new Position(3, 4)));
        Assert.Single(state.CapturedRed);
        Assert.Equal(Animal.Cat, state.CapturedRed[0].Animal);
    }

    [Fact]
    public void TotalElimination_BlueWins()
    {
        // Capture all Red pieces
        var pieces = new Dictionary<Position, Piece>
        {
            [new Position(3, 7)] = new Piece(Animal.Lion, Player.Blue, new Position(3, 7)),
            [new Position(3, 8)] = new Piece(Animal.Rat, Player.Red, new Position(3, 8))
        };
        var state = new GameState(
            Board.Initial,
            System.Collections.Immutable.ImmutableDictionary.CreateRange(pieces),
            Player.Blue,
            GameStatus.InProgress,
            System.Collections.Immutable.ImmutableList<Piece>.Empty,
            System.Collections.Immutable.ImmutableList<Piece>.Empty);

        // Blue Lion captures Red Rat (last Red piece)
        state = GameController.ApplyMove(state,
            new Move(new Position(3, 7), new Position(3, 8),
                state.GetPieceAt(new Position(3, 8))));

        Assert.Equal(GameStatus.BlueWins, state.Status);
    }

    [Fact]
    public void GameOver_PreventsFurtherMoves()
    {
        var pieces = new Dictionary<Position, Piece>
        {
            [new Position(3, 7)] = new Piece(Animal.Lion, Player.Blue, new Position(3, 7)),
            [new Position(3, 8)] = new Piece(Animal.Rat, Player.Red, new Position(3, 8))
        };
        var state = new GameState(
            Board.Initial,
            System.Collections.Immutable.ImmutableDictionary.CreateRange(pieces),
            Player.Blue,
            GameStatus.InProgress,
            System.Collections.Immutable.ImmutableList<Piece>.Empty,
            System.Collections.Immutable.ImmutableList<Piece>.Empty);

        state = GameController.ApplyMove(state,
            new Move(new Position(3, 7), new Position(3, 8),
                state.GetPieceAt(new Position(3, 8))));

        Assert.Equal(GameStatus.BlueWins, state.Status);
        var moves = MoveGenerator.GenerateLegalMoves(state, state.CurrentTurn);
        Assert.Empty(moves); // No moves after game over (actually, the function should work, but no valid moves exist)
    }

    [Fact]
    public void ApplyMove_UpdatesPiecePosition()
    {
        var state = GameState.CreateInitial();
        var moves = MoveGenerator.GenerateLegalMoves(state, Player.Blue);
        // Find a simple non-capture move
        var move = moves.First(m => !m.IsCapture);
        var originalPos = move.From;
        var newPos = move.To;
        var piece = state.GetPieceAt(originalPos)!.Value;

        state = GameController.ApplyMove(state, move);

        Assert.Null(state.GetPieceAt(originalPos));
        var movedPiece = state.GetPieceAt(newPos);
        Assert.NotNull(movedPiece);
        Assert.Equal(piece.Animal, movedPiece!.Value.Animal);
        Assert.Equal(piece.Owner, movedPiece.Value.Owner);
    }
}
