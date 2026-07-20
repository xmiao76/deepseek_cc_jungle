using JungleGame.Core.Logic;
using JungleGame.Core.Models;

namespace JungleGame.Tests.CoreTests;

public class WinConditionTests
{
    private readonly GameEngine _engine = new();

    [Fact]
    public void Blue_DenEntry_AtRedDen_Wins()
    {
        // Red den at (4,1); Blue piece enters it
        var board = new Board();
        var bluePiece = new Piece(PieceType.Lion, Player.Blue, new BoardPosition(4, 2));
        board.PlacePiece(bluePiece);

        var state = new GameState(board.BuildPieceDictionary(), Player.Blue,
            GamePhase.Playing, null, null, 0, null, new List<Move>(), Player.Blue);

        var move = new Move(bluePiece, new BoardPosition(4, 2), new BoardPosition(4, 1)) { IsDenEntry = true };
        var result = _engine.ApplyMove(state, move);

        Assert.Equal(GamePhase.GameOver, result.Phase);
        Assert.Equal(Player.Blue, result.Winner);
        Assert.Equal(WinCondition.DenEntry, result.WinReason);
    }

    [Fact]
    public void Red_DenEntry_AtBlueDen_Wins()
    {
        // Blue den at (4,9); Red piece enters it
        var board = new Board();
        var redPiece = new Piece(PieceType.Lion, Player.Red, new BoardPosition(4, 8));
        board.PlacePiece(redPiece);

        var state = new GameState(board.BuildPieceDictionary(), Player.Red,
            GamePhase.Playing, null, null, 0, null, new List<Move>(), Player.Red);

        var move = new Move(redPiece, new BoardPosition(4, 8), new BoardPosition(4, 9)) { IsDenEntry = true };
        var result = _engine.ApplyMove(state, move);

        Assert.Equal(GamePhase.GameOver, result.Phase);
        Assert.Equal(Player.Red, result.Winner);
    }

    [Fact]
    public void CaptureAllPieces_Wins()
    {
        var board = new Board();
        var blueLion = new Piece(PieceType.Lion, Player.Blue, new BoardPosition(4, 3));
        var redRat = new Piece(PieceType.Rat, Player.Red, new BoardPosition(4, 4));
        board.PlacePiece(blueLion);
        board.PlacePiece(redRat);

        var state = new GameState(board.BuildPieceDictionary(), Player.Blue,
            GamePhase.Playing, null, null, 0, null, new List<Move>(), Player.Blue);

        var move = new Move(blueLion, new BoardPosition(4, 3), new BoardPosition(4, 4)) { CapturedPiece = redRat };
        var result = _engine.ApplyMove(state, move);

        Assert.Equal(GamePhase.GameOver, result.Phase);
        Assert.Equal(Player.Blue, result.Winner);
        Assert.Equal(WinCondition.AllPiecesCaptured, result.WinReason);
    }

    [Fact]
    public void Blue_CannotEnterOwnDen()
    {
        var board = new Board();
        // Blue den at (4,9); Blue piece at (4,8) cannot move into own den
        var bluePiece = new Piece(PieceType.Lion, Player.Blue, new BoardPosition(4, 8));
        board.PlacePiece(bluePiece);

        var moves = MoveValidator.GetLegalMovesForPiece(board, bluePiece);
        Assert.DoesNotContain(moves, m => m.To == new BoardPosition(4, 9));
    }

    [Fact]
    public void Red_CanEnterOwnDen_AtOpponentDen()
    {
        var board = new Board();
        // Red piece can enter Red den... wait, own den prohibition.
        // Red den at (4,1); Red piece CAN enter Blue's den at (4,9)
        var redPiece = new Piece(PieceType.Rat, Player.Red, new BoardPosition(4, 8));
        board.PlacePiece(redPiece);

        var moves = MoveValidator.GetLegalMovesForPiece(board, redPiece);
        Assert.Contains(moves, m => m.To == new BoardPosition(4, 9) && m.IsDenEntry);
    }
}
