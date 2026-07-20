using JungleGame.Core.Logic;
using JungleGame.Core.Models;

namespace JungleGame.Tests.CoreTests;

public class MoveValidatorTests
{
    [Fact]
    public void GetLegalMoves_BasicMove_FromStartingPosition()
    {
        var board = Board.CreateInitial();
        // Blue Elephant at (1,7) can move to (1,6) and (2,7)
        var elephant = board.GetPiece(new BoardPosition(1, 7));

        var moves = MoveValidator.GetLegalMovesForPiece(board, elephant!);

        Assert.NotEmpty(moves);
        Assert.Contains(moves, m => m.To.Col == 1 && m.To.Row == 6); // Can move up
        Assert.Contains(moves, m => m.To.Col == 2 && m.To.Row == 7); // Can move right
    }

    [Fact]
    public void CannotEnterOwnDen()
    {
        var board = new Board();
        // Blue den at (4,9); Blue piece at (4,8) cannot move into own den
        var piece = new Piece(PieceType.Lion, Player.Blue, new BoardPosition(4, 8));
        board.PlacePiece(piece);

        var moves = MoveValidator.GetLegalMovesForPiece(board, piece);

        // (4,9) is Blue's own den — must not be in legal moves
        Assert.DoesNotContain(moves, m => m.To.Col == 4 && m.To.Row == 9);
        // But (4,7), (3,8), (5,8) should be legal
        Assert.Contains(moves, m => m.To.Col == 4 && m.To.Row == 7);
    }

    [Fact]
    public void OnlyRatCanEnterWater()
    {
        var board = new Board();
        var cat = new Piece(PieceType.Cat, Player.Blue, new BoardPosition(1, 4));
        board.PlacePiece(cat);

        var moves = MoveValidator.GetLegalMovesForPiece(board, cat);

        Assert.DoesNotContain(moves, m => m.To.Col == 2 && m.To.Row == 4);
    }

    [Fact]
    public void RatCanEnterWater()
    {
        var board = new Board();
        var rat = new Piece(PieceType.Rat, Player.Blue, new BoardPosition(1, 4));
        board.PlacePiece(rat);

        var moves = MoveValidator.GetLegalMovesForPiece(board, rat);

        Assert.Contains(moves, m => m.To.Col == 2 && m.To.Row == 4); // Into water
    }

    [Fact]
    public void RatInWater_IsVulnerableToRatInWater()
    {
        var board = new Board();
        var blueRat = new Piece(PieceType.Rat, Player.Blue, new BoardPosition(2, 4)); // In water
        var redRat = new Piece(PieceType.Rat, Player.Red, new BoardPosition(2, 5)); // In water, adjacent
        board.PlacePiece(blueRat);
        board.PlacePiece(redRat);

        var moves = MoveValidator.GetLegalMovesForPiece(board, redRat);

        Assert.Contains(moves, m => m.To.Col == 2 && m.To.Row == 4 && m.CapturedPiece != null);
    }

    [Fact]
    public void RatInWater_ImmuneToLandPieces()
    {
        var board = new Board();
        var redRat = new Piece(PieceType.Rat, Player.Red, new BoardPosition(2, 4)); // In water
        var blueWolf = new Piece(PieceType.Wolf, Player.Blue, new BoardPosition(2, 3)); // Land
        board.PlacePiece(redRat);
        board.PlacePiece(blueWolf);

        var moves = MoveValidator.GetLegalMovesForPiece(board, blueWolf);

        Assert.DoesNotContain(moves, m => m.To.Col == 2 && m.To.Row == 4);
    }

    [Fact]
    public void RatCapturesElephant_FromLand()
    {
        var board = new Board();
        // Use row 3 (all land) — (4,3) and (4,4) are both land (col 4 is always land)
        var rat = new Piece(PieceType.Rat, Player.Blue, new BoardPosition(4, 3));
        var elephant = new Piece(PieceType.Elephant, Player.Red, new BoardPosition(4, 4));
        board.PlacePiece(rat);
        board.PlacePiece(elephant);

        var moves = MoveValidator.GetLegalMovesForPiece(board, rat);

        Assert.Contains(moves, m => m.To.Col == 4 && m.To.Row == 4 && m.CapturedPiece != null);
    }

    [Fact]
    public void RatCannotCaptureElephant_FromWater()
    {
        var board = new Board();
        var rat = new Piece(PieceType.Rat, Player.Blue, new BoardPosition(2, 4)); // In water
        var elephant = new Piece(PieceType.Elephant, Player.Red, new BoardPosition(1, 4)); // Land adjacent (col 1 is land)
        board.PlacePiece(rat);
        board.PlacePiece(elephant);

        var moves = MoveValidator.GetLegalMovesForPiece(board, rat);

        // Rat in water cannot capture Elephant on land — move to (1,4) should not exist
        Assert.DoesNotContain(moves, m => m.To.Col == 1 && m.To.Row == 4);
    }

    [Fact]
    public void ElephantCannotCaptureRat()
    {
        var board = new Board();
        // Use column 4 (always land) — (4,3) and (4,4) are land
        var elephant = new Piece(PieceType.Elephant, Player.Blue, new BoardPosition(4, 3));
        var rat = new Piece(PieceType.Rat, Player.Red, new BoardPosition(4, 4));
        board.PlacePiece(elephant);
        board.PlacePiece(rat);

        var moves = MoveValidator.GetLegalMovesForPiece(board, elephant);

        // Elephant should not be able to capture the rat
        Assert.DoesNotContain(moves, m => m.To.Col == 4 && m.To.Row == 4);
    }

    [Fact]
    public void StandardCapture_HigherRankCapturesLowerRank()
    {
        var board = new Board();
        // Column 4 is all land — (4,3) and (4,4) are land
        var lion = new Piece(PieceType.Lion, Player.Blue, new BoardPosition(4, 3));
        var cat = new Piece(PieceType.Cat, Player.Red, new BoardPosition(4, 4));
        board.PlacePiece(lion);
        board.PlacePiece(cat);

        var moves = MoveValidator.GetLegalMovesForPiece(board, lion);

        Assert.Contains(moves, m => m.To.Col == 4 && m.To.Row == 4 && m.CapturedPiece != null);
    }

    [Fact]
    public void StandardCapture_LowerRankCannotCaptureHigherRank()
    {
        var board = new Board();
        var cat = new Piece(PieceType.Cat, Player.Blue, new BoardPosition(4, 3));
        var lion = new Piece(PieceType.Lion, Player.Red, new BoardPosition(4, 4));
        board.PlacePiece(cat);
        board.PlacePiece(lion);

        var moves = MoveValidator.GetLegalMovesForPiece(board, cat);

        Assert.DoesNotContain(moves, m => m.To.Col == 4 && m.To.Row == 4);
    }

    [Fact]
    public void EqualRankCanCapture()
    {
        var board = new Board();
        var tiger1 = new Piece(PieceType.Tiger, Player.Blue, new BoardPosition(4, 3));
        var tiger2 = new Piece(PieceType.Tiger, Player.Red, new BoardPosition(4, 4));
        board.PlacePiece(tiger1);
        board.PlacePiece(tiger2);

        var moves = MoveValidator.GetLegalMovesForPiece(board, tiger1);

        Assert.Contains(moves, m => m.To.Col == 4 && m.To.Row == 4 && m.CapturedPiece != null);
    }
}
