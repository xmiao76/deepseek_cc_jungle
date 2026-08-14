using JungleGame.Core.AI;
using JungleGame.Core.Engine;
using JungleGame.Core.Model;
using Xunit;

namespace JungleGame.Tests.AI;

/// <summary>
/// Lifecycle behavior of MinimaxEngine that the tactical tests never exercise:
/// time-limit changes mid-search, cancellation, and transposition-table integrity
/// after an aborted search. All tests bound their wall-clock so the suite stays fast.
/// </summary>
public class EngineLifecycleTests
{
    private static void AssertLegal(GameState state, Move move)
    {
        var error = JungleGame.Core.Rules.MoveValidator.Validate(state, move.From, move.To);
        Assert.Null(error);
    }

    [Fact]
    public async Task SetTimeLimit_MidSearch_AppliesImmediately()
    {
        var state = GameState.CreateInitial();
        var engine = new MinimaxEngine(TimeSpan.FromSeconds(10));

        var task = Task.Run(() => engine.FindBestMove(state));
        await Task.Delay(150);
        engine.SetTimeLimit(TimeSpan.FromMilliseconds(50)); // Difficulty drop mid-think

        var completed = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.Same(task, completed); // Must not hang at the old 10s budget

        var move = await task;
        Assert.NotNull(move);
        AssertLegal(state, move!.Value);
    }

    [Fact]
    public async Task Cancellation_MidSearch_ReturnsPromptly()
    {
        var state = GameState.CreateInitial();
        var engine = new MinimaxEngine(TimeSpan.FromSeconds(10));
        using var cts = new CancellationTokenSource();

        var task = Task.Run(() => engine.FindBestMove(state, cts.Token));
        await Task.Delay(100);
        cts.Cancel();

        var completed = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.Same(task, completed);

        var move = await task;
        Assert.NotNull(move);
        AssertLegal(state, move!.Value);
    }

    [Fact]
    public async Task AbortedSearch_DoesNotPolluteTT()
    {
        var state = GameState.CreateInitial();
        var engine = new MinimaxEngine(TimeSpan.FromSeconds(10));
        using var cts = new CancellationTokenSource();

        // Abort a search partway through; partially searched nodes must not be
        // stored in the TT (PVSearch guards store calls with _aborted)
        var aborted = Task.Run(() => engine.FindBestMove(state, cts.Token));
        await Task.Delay(100);
        cts.Cancel();
        await aborted;

        // The same engine (TT kept, as in real play across games) must still
        // reach a full depth on a fresh search and return a legal move. The
        // generous budget and modest floor keep this robust on slow CI runners
        // (a Debug build reaches depth 7+ in 2s on this machine).
        engine.SetTimeLimit(TimeSpan.FromSeconds(5));
        var move = engine.FindBestMove(state);

        Assert.True(engine.LastCompletedDepth >= 5,
            $"After an aborted search the engine reached depth {engine.LastCompletedDepth}, expected >= 5");
        Assert.NotNull(move);
        AssertLegal(state, move!.Value);
    }
}
