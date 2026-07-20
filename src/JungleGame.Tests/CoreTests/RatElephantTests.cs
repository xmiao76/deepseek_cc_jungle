using JungleGame.Core.Logic;
using JungleGame.Core.Models;

namespace JungleGame.Tests.CoreTests;

public class RatElephantTests
{
    [Fact]
    public void RatOnLand_CanCaptureElephant()
    {
        var board = new Board();
        var rat = new Piece(PieceType.Rat, Player.Blue, new BoardPosition(4, 3));
        var elephant = new Piece(PieceType.Elephant, Player.Red, new BoardPosition(4, 4));
        board.PlacePiece(rat);
        board.PlacePiece(elephant);
        Assert.True(MoveValidator.CanCapture(rat, elephant, elephant.Position));
    }

    [Fact]
    public void RatInWater_CannotCaptureElephantOnLand()
    {
        var rat = new Piece(PieceType.Rat, Player.Blue, new BoardPosition(2, 4)); // Water
        var elephant = new Piece(PieceType.Elephant, Player.Red, new BoardPosition(1, 4)); // Land
        Assert.False(MoveValidator.CanCapture(rat, elephant, elephant.Position));
    }

    [Fact]
    public void Elephant_CannotCaptureRat()
    {
        Assert.False(MoveValidator.CanCapture(
            new Piece(PieceType.Elephant, Player.Red, new BoardPosition(4, 3)),
            new Piece(PieceType.Rat, Player.Blue, new BoardPosition(4, 4)),
            new BoardPosition(4, 4)));
    }

    [Fact]
    public void Elephant_CanCaptureOtherPieces()
    {
        Assert.True(MoveValidator.CanCapture(
            new Piece(PieceType.Elephant, Player.Blue, new BoardPosition(4, 3)),
            new Piece(PieceType.Lion, Player.Red, new BoardPosition(4, 4)),
            new BoardPosition(4, 4)));
    }

    [Fact]
    public void RatOnOpponentTrap_CanBeCapturedByAnyPiece()
    {
        var board = new Board();
        // Blue Rat on Red's trap at (3,1); Red Lion can capture it
        var rat = new Piece(PieceType.Rat, Player.Blue, new BoardPosition(3, 1));
        var redLion = new Piece(PieceType.Lion, Player.Red, new BoardPosition(3, 2));
        board.PlacePiece(rat);
        board.PlacePiece(redLion);

        var moves = MoveValidator.GetLegalMovesForPiece(board, redLion);
        Assert.Contains(moves, m => m.To == new BoardPosition(3, 1));
    }

    [Fact]
    public void RatInWater_ImmuneToCapture()
    {
        var board = new Board();
        var rat = new Piece(PieceType.Rat, Player.Blue, new BoardPosition(2, 4));
        var lion = new Piece(PieceType.Lion, Player.Red, new BoardPosition(1, 4));
        board.PlacePiece(rat);
        board.PlacePiece(lion);

        var moves = MoveValidator.GetLegalMovesForPiece(board, lion);
        Assert.DoesNotContain(moves, m => m.To == new BoardPosition(2, 4));
    }
}
