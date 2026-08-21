using JungleGame.Core.AI;
using JungleGame.Core.Engine;
using JungleGame.Core.Model;
using JungleGame.Tests.Helpers;
using Xunit;

namespace JungleGame.Tests.AI;

/// <summary>
/// Pondering determinism tests. The engine-side contract: after FindBestMove,
/// LastPredictedReply is the opponent move the engine expects next (the TT best
/// move of the child position, verified legal); Ponder(position) searches the
/// position after that predicted reply so the result can be replayed instantly.
/// </summary>
public class PonderingTests
{
    private static GameState Midgame() => new TestBoardBuilder()
        .WithPiece(Animal.Lion, Player.Blue, 3, 4)
        .WithPiece(Animal.Rat, Player.Red, 3, 7)
        .WithPiece(Animal.Tiger, Player.Blue, 1, 2)
        .WithPiece(Animal.Cat, Player.Red, 1, 6)
        .WithPiece(Animal.Wolf, Player.Blue, 0, 6)
        .WithPiece(Animal.Dog, Player.Red, 1, 7)
        .WithTurn(Player.Red)
        .Build();

    [Fact]
    public void LastPredictedReply_IsLegal_AfterFindBestMove()
    {
        var state = Midgame();
        var engine = new MinimaxEngine(TimeSpan.FromSeconds(5), maxDepth: 6, maxNodes: 200_000);

        var move = engine.FindBestMove(state);
        Assert.NotNull(move);
        var predicted = engine.LastPredictedReply;
        Assert.NotNull(predicted); // a full search with depth >= 2 stores the child's best move

        var afterOurMove = GameController.ApplyMove(state, move!.Value);
        var legalOpponent = MoveGenerator.GenerateLegalMoves(afterOurMove, afterOurMove.CurrentTurn);
        Assert.Contains(legalOpponent,
            m => m.From == predicted!.Value.From && m.To == predicted.Value.To);
    }

    [Fact]
    public void Ponder_ReturnsLegalReply_ForPredictedPosition()
    {
        var state = Midgame();
        var engine = new MinimaxEngine(TimeSpan.FromSeconds(5), maxDepth: 6, maxNodes: 200_000);

        var move = engine.FindBestMove(state);
        var predicted = engine.LastPredictedReply;
        Assert.NotNull(predicted);

        // The pondering search runs on the position after the engine's move and
        // the predicted human reply — exactly what the UI replays when the
        // prediction holds.
        var afterOurMove = GameController.ApplyMove(state, move!.Value);
        var ponderPosition = GameController.ApplyMove(afterOurMove, predicted!.Value);
        var pondered = engine.Ponder(ponderPosition);
        Assert.NotNull(pondered);

        var legal = MoveGenerator.GenerateLegalMoves(ponderPosition, ponderPosition.CurrentTurn);
        Assert.Contains(legal, m => m.From == pondered!.Value.From && m.To == pondered.Value.To);
    }

    [Fact]
    public async Task Ponder_Cancelled_LeavesEngineUsable()
    {
        var state = Midgame();
        var engine = new MinimaxEngine(TimeSpan.FromSeconds(10), maxDepth: 10, maxNodes: 300_000);

        // An already-cancelled pondering search must return promptly (a legal
        // fallback move) and leave the engine fully usable.
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var ponder = Task.Run(() => engine.Ponder(state, cts.Token));
        await ponder.WaitAsync(TimeSpan.FromSeconds(2)); // TimeoutException fails the test

        var move = engine.FindBestMove(state);
        Assert.NotNull(move);
        var legal = MoveGenerator.GenerateLegalMoves(state, state.CurrentTurn);
        Assert.Contains(legal, m => m.From == move!.Value.From && m.To == move.Value.To);
    }
}
