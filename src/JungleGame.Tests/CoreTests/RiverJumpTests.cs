using JungleGame.Core.Logic;
using JungleGame.Core.Models;

namespace JungleGame.Tests.CoreTests;

public class RiverJumpTests
{
    [Fact]
    public void Lion_VerticalJump_LeftRiver()
    {
        var board = new Board();
        var lion = new Piece(PieceType.Lion, Player.Blue, new BoardPosition(1, 4));
        board.PlacePiece(lion);

        var moves = MoveValidator.GetLegalMovesForPiece(board, lion);

        Assert.Contains(moves, m => m.To == new BoardPosition(4, 4) && m.IsRiverJump);
    }

    [Fact]
    public void Lion_VerticalJump_RightRiver_FromCenter()
    {
        var board = new Board();
        var lion = new Piece(PieceType.Lion, Player.Blue, new BoardPosition(4, 5));
        board.PlacePiece(lion);

        var moves = MoveValidator.GetLegalMovesForPiece(board, lion);

        Assert.Contains(moves, m => m.To == new BoardPosition(7, 5) && m.IsRiverJump);
    }

    [Fact]
    public void Tiger_VerticalJump_LeftRiver()
    {
        var board = new Board();
        var tiger = new Piece(PieceType.Tiger, Player.Blue, new BoardPosition(1, 4));
        board.PlacePiece(tiger);

        var moves = MoveValidator.GetLegalMovesForPiece(board, tiger);

        Assert.Contains(moves, m => m.To == new BoardPosition(4, 4) && m.IsRiverJump);
    }

    [Fact]
    public void Tiger_VerticalJump_RightRiver_FromBank()
    {
        var board = new Board();
        var tiger = new Piece(PieceType.Tiger, Player.Blue, new BoardPosition(7, 5));
        board.PlacePiece(tiger);

        var moves = MoveValidator.GetLegalMovesForPiece(board, tiger);

        Assert.Contains(moves, m => m.To == new BoardPosition(4, 5) && m.IsRiverJump);
    }

    [Fact]
    public void Lion_HorizontalJump_Down()
    {
        var board = new Board();
        var lion = new Piece(PieceType.Lion, Player.Blue, new BoardPosition(2, 3));
        board.PlacePiece(lion);

        var moves = MoveValidator.GetLegalMovesForPiece(board, lion);

        Assert.Contains(moves, m => m.To == new BoardPosition(2, 7) && m.IsRiverJump);
    }

    [Fact]
    public void Lion_HorizontalJump_Up()
    {
        var board = new Board();
        var lion = new Piece(PieceType.Lion, Player.Red, new BoardPosition(2, 7));
        board.PlacePiece(lion);

        var moves = MoveValidator.GetLegalMovesForPiece(board, lion);

        Assert.Contains(moves, m => m.To == new BoardPosition(2, 3) && m.IsRiverJump);
    }

    [Fact]
    public void Tiger_CannotHorizontalJump()
    {
        var board = new Board();
        var tiger = new Piece(PieceType.Tiger, Player.Blue, new BoardPosition(2, 3));
        board.PlacePiece(tiger);

        var moves = MoveValidator.GetLegalMovesForPiece(board, tiger);

        Assert.DoesNotContain(moves, m => m.To == new BoardPosition(2, 7));
    }

    [Fact]
    public void RatBlocksVerticalJump()
    {
        var board = new Board();
        var lion = new Piece(PieceType.Lion, Player.Blue, new BoardPosition(1, 4));
        var rat = new Piece(PieceType.Rat, Player.Red, new BoardPosition(2, 4)); // In water between
        board.PlacePiece(lion);
        board.PlacePiece(rat);

        var moves = MoveValidator.GetLegalMovesForPiece(board, lion);

        Assert.DoesNotContain(moves, m => m.To == new BoardPosition(4, 4));
    }

    [Fact]
    public void RatBlocksHorizontalLionJump()
    {
        var board = new Board();
        var lion = new Piece(PieceType.Lion, Player.Blue, new BoardPosition(2, 3));
        var rat = new Piece(PieceType.Rat, Player.Red, new BoardPosition(2, 5)); // In water in path
        board.PlacePiece(lion);
        board.PlacePiece(rat);

        var moves = MoveValidator.GetLegalMovesForPiece(board, lion);

        Assert.DoesNotContain(moves, m => m.To == new BoardPosition(2, 7));
    }

    [Fact]
    public void OwnRatBlocksLionJump()
    {
        var board = new Board();
        var lion = new Piece(PieceType.Lion, Player.Blue, new BoardPosition(1, 4));
        var ownRat = new Piece(PieceType.Rat, Player.Blue, new BoardPosition(2, 4)); // Own rat in water
        board.PlacePiece(lion);
        board.PlacePiece(ownRat);

        var moves = MoveValidator.GetLegalMovesForPiece(board, lion);

        Assert.DoesNotContain(moves, m => m.To == new BoardPosition(4, 4));
    }

    [Fact]
    public void LionVerticalJump_CapturesEnemyAtDestination()
    {
        var board = new Board();
        var lion = new Piece(PieceType.Lion, Player.Blue, new BoardPosition(1, 4));
        var enemyCat = new Piece(PieceType.Cat, Player.Red, new BoardPosition(4, 4));
        board.PlacePiece(lion);
        board.PlacePiece(enemyCat);

        var moves = MoveValidator.GetLegalMovesForPiece(board, lion);

        var jump = moves.FirstOrDefault(m => m.To == new BoardPosition(4, 4));
        Assert.NotNull(jump);
        Assert.True(jump!.IsRiverJump);
        Assert.NotNull(jump.CapturedPiece);
        Assert.Equal(PieceType.Cat, jump.CapturedPiece!.Type);
    }

    [Fact]
    public void LionVerticalJump_CannotCaptureStrongerEnemy()
    {
        var board = new Board();
        var lion = new Piece(PieceType.Lion, Player.Blue, new BoardPosition(1, 4));
        var enemyElephant = new Piece(PieceType.Elephant, Player.Red, new BoardPosition(4, 4));
        board.PlacePiece(lion);
        board.PlacePiece(enemyElephant);

        var moves = MoveValidator.GetLegalMovesForPiece(board, lion);

        Assert.DoesNotContain(moves, m => m.To == new BoardPosition(4, 4));
    }

    [Fact]
    public void RiverJump_DenEntry_IsCorrectlyFlagged()
    {
        var board = new Board();
        // Blue piece at (4,2) can enter Red's den at (4,1) for the win
        var lion = new Piece(PieceType.Lion, Player.Blue, new BoardPosition(4, 2));
        board.PlacePiece(lion);

        var moves = MoveValidator.GetLegalMovesForPiece(board, lion);

        var denEntry = moves.FirstOrDefault(m => m.To == new BoardPosition(4, 1));
        Assert.NotNull(denEntry);
        Assert.True(denEntry!.IsDenEntry);
    }
}
