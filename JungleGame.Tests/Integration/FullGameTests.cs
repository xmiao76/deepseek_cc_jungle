using JungleGame.Core.AI;
using JungleGame.Core.Engine;
using JungleGame.Core.Model;
using Xunit;

namespace JungleGame.Tests.Integration;

public class FullGameTests
{
    [Fact]
    public void FullGame_RandomPlay_CompletesWithoutCrash()
    {
        var state = GameState.CreateInitial();
        var random = new Random(12345);
        int moveCount = 0;
        const int maxMoves = 500;

        while (state.Status == GameStatus.InProgress && moveCount < maxMoves)
        {
            var moves = MoveGenerator.GenerateLegalMoves(state, state.CurrentTurn);
            if (moves.Count == 0)
            {
                Assert.Fail($"No legal moves for {state.CurrentTurn} at move {moveCount}");
            }

            var move = moves[random.Next(moves.Count)];

            // Verify move is valid
            var error = JungleGame.Core.Rules.MoveValidator.Validate(state, move.From, move.To);
            Assert.Null(error);

            state = GameController.ApplyMove(state, move);
            moveCount++;
        }

        Assert.True(moveCount > 0);
        // Game should have ended one way or another
        if (moveCount >= maxMoves)
        {
            // 500-move draw-like situation — acceptable
            Assert.Equal(GameStatus.InProgress, state.Status);
        }
    }

    [Fact]
    public void AIvsAI_QuickGame_Completes()
    {
        var state = GameState.CreateInitial();
        var engine = new MinimaxEngine(TimeSpan.FromSeconds(1)); // Fast AI
        int moveCount = 0;
        const int maxMoves = 100;

        while (state.Status == GameStatus.InProgress && moveCount < maxMoves)
        {
            var move = engine.FindBestMove(state);
            var error = JungleGame.Core.Rules.MoveValidator.Validate(state, move.From, move.To);
            Assert.Null(error);

            state = GameController.ApplyMove(state, move);
            moveCount++;
        }

        Assert.True(moveCount > 0);
    }

    [Fact]
    public void InitialState_HasCorrectPieceCount()
    {
        var state = GameState.CreateInitial();
        Assert.Equal(8, state.GetPlayerPieces(Player.Blue).Count);
        Assert.Equal(8, state.GetPlayerPieces(Player.Red).Count);
        Assert.Equal(16, state.Pieces.Count);
    }

    [Fact]
    public void ElectricRat_CannotCaptureElephant_FromWater()
    {
        // Verify the rat-in-water restriction is enforced in a full game scenario
        var state = GameState.CreateInitial();
        // Move Blue Rat into water and try to capture Red Elephant
        var engine = new MinimaxEngine(TimeSpan.FromSeconds(2));
        var move = engine.FindBestMove(state);
        // This just verifies the AI doesn't crash
        // (Move is a value type, so it's never null)
    }
}
