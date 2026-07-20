using JungleGame.Core.Logic;
using JungleGame.Core.Models;

namespace JungleGame.Core.AI;

/// <summary>
/// Public API for AI move generation. Runs minimax search on a background thread,
/// fires events when a move is found, and supports cancellation.
/// </summary>
public class AiController
{
    private readonly SearchEngine _engine;
    private CancellationTokenSource? _cts;
    private Task<Move?>? _currentSearch;

    public bool IsThinking => _currentSearch != null && !_currentSearch.IsCompleted;
    public DifficultyLevel Difficulty { get; set; } = DifficultyLevel.Medium;
    public int? LastSearchDepth { get; private set; }
    public int? LastNodesSearched { get; private set; }
    public TimeSpan? LastSearchTime { get; private set; }

    public event EventHandler<Move>? MoveFound;
    public event EventHandler<string>? StatusUpdate;

    public AiController()
    {
        _engine = new SearchEngine();
    }

    /// <summary>
    /// Request the AI to find a move for the current position.
    /// Runs asynchronously on a background thread. Exceptions are silently caught
    /// to prevent the AI search from crashing the application.
    /// </summary>
    public void RequestMove(GameState state)
    {
        CancelSearch();

        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        _engine.SetDifficulty(Difficulty);
        _engine.SetCancellationToken(ct);

        var startTime = DateTime.UtcNow;

        _currentSearch = Task.Run(() =>
        {
            try
            {
                var move = _engine.FindBestMove(state);

                LastNodesSearched = _engine.NodesSearched;
                LastSearchTime = DateTime.UtcNow - startTime;

                if (move != null && !ct.IsCancellationRequested)
                {
                    MoveFound?.Invoke(this, move);
                }

                return move;
            }
            catch (OperationCanceledException) { return null; }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AI search error: {ex.Message}");
                return null;
            }
        }, ct);
    }

    /// <summary>
    /// Synchronously find the best move (blocks until complete).
    /// Used for AI-vs-AI mode where we don't need async behavior.
    /// </summary>
    public Move? FindBestMoveSync(GameState state)
    {
        _engine.SetDifficulty(Difficulty);
        _engine.SetCancellationToken(CancellationToken.None);

        var startTime = DateTime.UtcNow;
        var move = _engine.FindBestMove(state);

        LastNodesSearched = _engine.NodesSearched;
        LastSearchTime = DateTime.UtcNow - startTime;

        return move;
    }

    /// <summary>
    /// Cancel the currently running search.
    /// </summary>
    public void CancelSearch()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        _currentSearch = null;
    }
}
