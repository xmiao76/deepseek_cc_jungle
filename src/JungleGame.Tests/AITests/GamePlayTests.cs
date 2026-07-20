using JungleGame.Core.AI;
using JungleGame.Core.Logic;
using JungleGame.Core.Models;

namespace JungleGame.Tests.AITests;

public class GamePlayTests
{
    [Fact]
    public void AIVsAI_CompletesGameWithoutCrash()
    {
        var gameController = new GameController();
        var aiBlue = new AiController { Difficulty = DifficultyLevel.Medium };
        var aiRed = new AiController { Difficulty = DifficultyLevel.Medium };

        gameController.NewGame(Player.Blue);

        int moveLimit = 1000;
        int moveCount = 0;

        while (gameController.CurrentState?.Phase == GamePhase.Playing && moveCount < moveLimit)
        {
            var currentState = gameController.CurrentState;
            var ai = currentState!.CurrentPlayer == Player.Blue ? aiBlue : aiRed;
            var move = ai.FindBestMoveSync(currentState);

            if (move == null)
            {
                Assert.Fail("AI had no legal moves but game is still playing");
                break;
            }

            gameController.ApplyMove(move);
            moveCount++;
        }

        Assert.True(gameController.CurrentState!.Phase == GamePhase.GameOver,
            $"Game did not finish within {moveLimit} moves (ended at {moveCount} with phase {gameController.CurrentState!.Phase})");
        Assert.NotNull(gameController.CurrentState.Winner);
    }

    [Fact]
    public void AI_WinsByDenEntry_WhenUnblocked()
    {
        // Red den at (4,1); Blue piece at (4,2) — immediate win
        var board = new Board();
        var blueLion = new Piece(PieceType.Lion, Player.Blue, new BoardPosition(4, 2));
        board.PlacePiece(blueLion);
        board.PlacePiece(new Piece(PieceType.Rat, Player.Red, new BoardPosition(1, 1)));
        board.PlacePiece(new Piece(PieceType.Cat, Player.Red, new BoardPosition(2, 1)));

        var state = new GameState(board.BuildPieceDictionary(), Player.Blue,
            GamePhase.Playing, null, null, 0, null, new List<Move>(), Player.Blue);

        var ai = new AiController { Difficulty = DifficultyLevel.Hard };
        var move = ai.FindBestMoveSync(state);

        Assert.NotNull(move);
        Assert.Equal(new BoardPosition(4, 1), move!.To);
        Assert.True(move.IsDenEntry);
    }

    [Fact]
    public void Pieces_RemainOnBoard_AfterMultipleMoves()
    {
        // Verify no pieces disappear after several AI moves
        var engine = new GameEngine();
        var state = engine.CreateInitialState();
        var ai = new AiController { Difficulty = DifficultyLevel.Medium };

        for (int turn = 0; turn < 6; turn++)
        {
            if (state.Phase != GamePhase.Playing) break;

            var move = ai.FindBestMoveSync(state);
            Assert.NotNull(move);

            // Count pieces before
            int piecesBefore = state.Pieces.Count;

            state = engine.ApplyMove(state, move!);

            // All pieces should be accounted for (16 minus any captures)
            int expectedPieces = piecesBefore - (move!.CapturedPiece != null ? 1 : 0);
            Assert.Equal(expectedPieces, state.Pieces.Count);

            // Every piece in the state should have a position matching its dictionary key
            foreach (var kvp in state.Pieces)
            {
                Assert.Equal(kvp.Key, kvp.Value.Position);
            }
        }
    }

    [Fact]
    public void AI_NeverReturnsNull_WhenLegalMovesExist()
    {
        var engine = new GameEngine();
        var state = engine.CreateInitialState();
        var ai = new AiController { Difficulty = DifficultyLevel.Medium };

        for (int i = 0; i < 10; i++)
        {
            var move = ai.FindBestMoveSync(state);
            Assert.NotNull(move);
            state = engine.ApplyMove(state, move!);
            if (state.Phase == GamePhase.GameOver) break;
        }
    }
}
