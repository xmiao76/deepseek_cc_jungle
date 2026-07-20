using JungleGame.Core.AI;
using JungleGame.Core.Logic;
using JungleGame.Core.Models;

namespace JungleGame.Tests.AITests;

public class SearchEngineTests
{
    private readonly GameEngine _gameEngine = new();

    [Fact]
    public void FindBestMove_ReturnsLegalMove()
    {
        var state = _gameEngine.CreateInitialState();
        var engine = new SearchEngine();
        engine.SetDifficulty(DifficultyLevel.Easy);

        var move = engine.FindBestMove(state);
        Assert.NotNull(move);
        Assert.Equal(Player.Blue, move!.Piece.Owner);
    }

    [Fact]
    public void FindBestMove_FindsWinningDenEntry()
    {
        // Blue piece at (4,2) — Red's den at (4,1), immediate win available
        var board = new Board();
        var blueLion = new Piece(PieceType.Lion, Player.Blue, new BoardPosition(4, 2));
        board.PlacePiece(blueLion);

        var state = new GameState(board.BuildPieceDictionary(), Player.Blue,
            GamePhase.Playing, null, null, 0, null, new List<Move>(), Player.Blue);

        var engine = new SearchEngine();
        engine.SetDifficulty(DifficultyLevel.Hard);

        var move = engine.FindBestMove(state);
        Assert.NotNull(move);
        Assert.Equal(new BoardPosition(4, 1), move!.To); // Into Red's den
        Assert.True(move.IsDenEntry);
    }

    [Fact]
    public void AllDifficultyLevels_ReturnLegalMoves()
    {
        var state = _gameEngine.CreateInitialState();
        var engine = new SearchEngine();

        foreach (var level in new[] { DifficultyLevel.Easy, DifficultyLevel.Medium, DifficultyLevel.Hard, DifficultyLevel.Expert })
        {
            engine.SetDifficulty(level);
            var move = engine.FindBestMove(state);
            Assert.NotNull(move);
        }
    }

    [Fact]
    public void FindBestMove_WhenOnlyDenEntryWins()
    {
        var board = new Board();
        var blueLion = new Piece(PieceType.Lion, Player.Blue, new BoardPosition(4, 2));
        board.PlacePiece(blueLion);

        var state = new GameState(board.BuildPieceDictionary(), Player.Blue,
            GamePhase.Playing, null, null, 0, null, new List<Move>(), Player.Blue);

        var engine = new SearchEngine();
        engine.SetDifficulty(DifficultyLevel.Hard);

        var move = engine.FindBestMove(state);
        Assert.Equal(new BoardPosition(4, 1), move!.To);
    }
}
