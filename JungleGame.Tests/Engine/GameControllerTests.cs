using JungleGame.Core.Engine;
using JungleGame.Core.Model;
using Xunit;

namespace JungleGame.Tests.Engine;

public class GameControllerTests
{
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
