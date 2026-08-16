using JungleGame.Core.Engine;
using JungleGame.Core.Model;

namespace JungleGame.Core.AI;

/// <summary>
/// Public facade over the search modules: owns the long-lived transposition
/// table, time manager, move-ordering state, and the per-search context;
/// <see cref="PVSearcher"/> contains the search itself.
/// </summary>
public class MinimaxEngine
{
    public const int MateScore = TranspositionTable.MateScore;

    private readonly TranspositionTable _tt;
    private readonly TimeManager _time;
    private readonly PVSearcher _searcher;
    private readonly object _searchGate = new(); // serializes searches: one at a time per engine

    /// <param name="legacyEval">Disables the P3 evaluation terms (A/B strength tests).</param>
    /// <param name="legacySearch">Disables the post-P3 search features (A/B strength tests).</param>
    /// <param name="useTablebase">Probes the endgame tables (default on; the tactical
    /// suite pins pure-search behavior and disables it).</param>
    /// <param name="contempt">Draw-avoidance bias in centipawns (default 30; 0 = off).</param>
    /// <param name="maxNodes">Optional fixed-node budget for deterministic tests.</param>
    public MinimaxEngine(
        TimeSpan? timeLimit = null,
        int? maxDepth = null,
        bool legacyEval = false,
        bool legacySearch = false,
        bool useTablebase = true,
        int contempt = 30,
        long? maxNodes = null)
    {
        _tt = new TranspositionTable(1 << 20);
        _time = new TimeManager(timeLimit ?? TimeSpan.FromSeconds(4));
        if (maxNodes.HasValue)
            _time.SetNodeBudget(maxNodes.Value);
        _searcher = new PVSearcher(
            _tt,
            _time,
            new MoveOrdering(useSee: !legacySearch),
            new SearchContext(),
            new SearchOptions(legacyEval, legacySearch, useTablebase, contempt),
            maxDepth);
        TablebaseProbe.Initialize(); // idempotent: loads the table once if present
    }

    public long NodesSearched => _searcher.Nodes;
    public int LastCompletedDepth => _searcher.LastCompletedDepth;

    /// <summary>Search statistics snapshot for the UI (updated per completed depth).</summary>
    public EngineStats Stats => _searcher.Stats;

    /// <summary>
    /// Changes the per-move time budget (difficulty). A running search observes the
    /// new limit at its next check; the transposition table is kept, so strength
    /// carries over between games and difficulty changes.
    /// </summary>
    public void SetTimeLimit(TimeSpan timeLimit) => _time.SetTimeLimit(timeLimit);

    /// <summary>
    /// Searches for the best move. Returns null if the game is already over or the
    /// side to move has no legal moves. Cancellation stops the search promptly and
    /// returns the best completed-depth move. Searches are serialized per engine
    /// instance (the search scratch state is not thread-safe), so overlapping calls
    /// simply queue.
    /// </summary>
    public Move? FindBestMove(GameState state, CancellationToken token = default)
    {
        lock (_searchGate)
        {
            _tt.NewGeneration(); // Entries older than the previous search are ignored
            return _searcher.SearchBestMove(state, token);
        }
    }
}
