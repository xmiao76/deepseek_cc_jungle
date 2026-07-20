using JungleGame.Core.Logic;
using JungleGame.Core.Models;

namespace JungleGame.Tests.CoreTests;

public class TrapTests
{
    [Fact]
    public void Red_Traps_AreAtTop()
    {
        // Red den at (4,1); traps at (3,1), (5,1), (4,2)
        Assert.True(Board.IsTrap(new BoardPosition(3, 1), Player.Red));
        Assert.True(Board.IsTrap(new BoardPosition(5, 1), Player.Red));
        Assert.True(Board.IsTrap(new BoardPosition(4, 2), Player.Red));
    }

    [Fact]
    public void Blue_Traps_AreAtBottom()
    {
        Assert.True(Board.IsTrap(new BoardPosition(3, 9), Player.Blue));
        Assert.True(Board.IsTrap(new BoardPosition(5, 9), Player.Blue));
        Assert.True(Board.IsTrap(new BoardPosition(4, 8), Player.Blue));
    }

    [Fact]
    public void OpponentTrap_ReducesRankToZero()
    {
        var board = new Board();
        // Blue piece on Red's trap at (3,1)
        var piece = new Piece(PieceType.Elephant, Player.Blue, new BoardPosition(3, 1));
        board.PlacePiece(piece);
        Assert.Equal(0, MoveValidator.EffectiveRank(piece));
    }

    [Fact]
    public void AnyPieceCanCaptureTrappedPiece()
    {
        var board = new Board();
        // Blue Elephant on Red's trap (3,1); Red Rat can capture it
        var blueElephant = new Piece(PieceType.Elephant, Player.Blue, new BoardPosition(3, 1)); // On Red trap
        var redRat = new Piece(PieceType.Rat, Player.Red, new BoardPosition(3, 2));
        board.PlacePiece(blueElephant);
        board.PlacePiece(redRat);

        var moves = MoveValidator.GetLegalMovesForPiece(board, redRat);
        Assert.Contains(moves, m => m.To == new BoardPosition(3, 1) && m.CapturedPiece != null);
    }

    [Fact]
    public void ExitTrap_RestoresRank()
    {
        var board = new Board();
        var piece = new Piece(PieceType.Elephant, Player.Blue, new BoardPosition(3, 1)); // On Red trap
        board.PlacePiece(piece);
        Assert.Equal(0, MoveValidator.EffectiveRank(piece));

        board.MovePiece(piece, new BoardPosition(3, 1), new BoardPosition(3, 2));
        Assert.Equal(8, MoveValidator.EffectiveRank(piece));
    }

    [Fact]
    public void OwnTrap_NoEffect()
    {
        var board = new Board();
        // Red piece on Red's own trap
        var piece = new Piece(PieceType.Lion, Player.Red, new BoardPosition(4, 2));
        board.PlacePiece(piece);
        Assert.Equal(7, MoveValidator.EffectiveRank(piece));
    }
}
