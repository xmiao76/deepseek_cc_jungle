using System.Diagnostics;
using JungleGame.Core.Engine;
using JungleGame.Core.Model;

namespace JungleGame.Core.AI;

public class MinimaxEngine
{
    public const int MateScore = TranspositionTable.MateScore;
    private const int MateThreshold = TranspositionTable.MateScore - TranspositionTable.MateRange;
    private const int DefaultMaxDepth = 20;
    private const int MaxPly = 64;
    private const int MaxMovesPerPly = 128;
    private const int AspirationDelta = 25;

    private long _timeLimitTicks; // accessed via Interlocked (SetTimeLimit may race a running search)
    private readonly int _maxDepth;
    private readonly bool _legacyEval;
    private readonly TranspositionTable _tt;
    private readonly int[,] _killerMoves;
    private readonly int[,] _historyTable;
    private readonly object _searchGate = new(); // serializes searches: one at a time per engine

    // Per-search scratch state (single-threaded; one search at a time)
    private bool _aborted;
    private readonly Stack<SearchBoard> _boardPool = new();
    private readonly SearchMove[][] _plyMoves;   // PVSearch buffers, one per ply
    private readonly SearchMove[][] _qMoves;     // quiescence buffers, one per ply
    private readonly SearchMove[] _rootMoves = new SearchMove[MaxMovesPerPly];
    private readonly ulong[] _pathHashes = new ulong[256];
    private int _pathLen;

    public MinimaxEngine(TimeSpan? timeLimit = null, int? maxDepth = null, bool legacyEval = false)
    {
        _timeLimitTicks = (timeLimit ?? TimeSpan.FromSeconds(4)).Ticks;
        // PVSearch plus quiescence plies must fit inside the per-ply buffer tables
        _maxDepth = Math.Min(maxDepth ?? DefaultMaxDepth, MaxPly - 32);
        _legacyEval = legacyEval;
        _tt = new TranspositionTable(1 << 20);
        _killerMoves = new int[MaxPly, 2];
        _historyTable = new int[Zobrist.PositionCount, Zobrist.PositionCount];
        _plyMoves = new SearchMove[MaxPly][];
        _qMoves = new SearchMove[MaxPly][];
        for (int i = 0; i < MaxPly; i++)
        {
            _plyMoves[i] = new SearchMove[MaxMovesPerPly];
            _qMoves[i] = new SearchMove[MaxMovesPerPly];
        }
    }

    public long NodesSearched { get; private set; }
    public int LastCompletedDepth { get; private set; }

    /// <summary>
    /// Changes the per-move time budget (difficulty). A running search observes the
    /// new limit at its next check; the transposition table is kept, so strength
    /// carries over between games and difficulty changes.
    /// </summary>
    public void SetTimeLimit(TimeSpan timeLimit) =>
        Interlocked.Exchange(ref _timeLimitTicks, timeLimit.Ticks);

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
            return SearchBestMove(state, token);
        }
    }

    private Move? SearchBestMove(GameState state, CancellationToken token)
    {
        if (state.Status != GameStatus.InProgress)
            return null;

        _aborted = false;
        NodesSearched = 0;
        LastCompletedDepth = 0;
        var sw = Stopwatch.StartNew();

        var root = SearchBoard.FromGameState(state);

        // Seed the repetition path with the real game's history, reserving headroom
        // for the search path itself (long shuffling games can approach the cap)
        _pathLen = Math.Min(state.History.Count, _pathHashes.Length - MaxPly);
        for (int i = 0; i < _pathLen; i++)
            _pathHashes[i] = state.History[i];

        int side = (int)state.CurrentTurn;
        int moveCount = root.GenerateMoves(side, _rootMoves);
        if (moveCount == 0)
            return null;
        if (moveCount == 1)
            return ToPublicMove(_rootMoves[0]);

        SearchMove bestMove = _rootMoves[0];
        Move bestMoveForOrdering = ToPublicMove(bestMove);
        int bestScore = int.MinValue;

        for (int depth = 1; depth <= _maxDepth; depth++)
        {
            if (Aborted(sw, token))
                break;

            int currentBest = int.MinValue;
            SearchMove currentBestMove = bestMove;

            // Aspiration window around the previous iteration's score; a fail
            // low/high re-searches the same depth with a widened window.
            int searchAlpha = bestScore == int.MinValue ? int.MinValue + 1 : bestScore - AspirationDelta;
            int searchBeta = bestScore == int.MinValue ? int.MaxValue : bestScore + AspirationDelta;

            bool retry;
            do
            {
                retry = false;
                currentBest = int.MinValue;

                OrderMoves(root, _rootMoves, moveCount, bestMoveForOrdering, 0);

                for (int i = 0; i < moveCount; i++)
                {
                    if (Aborted(sw, token))
                        break;

                    var move = _rootMoves[i];
                    var child = GetBoard();
                    root.CopyTo(child);
                    child.ApplyMove(move);

                    int score;
                    if (i == 0)
                        score = -PVSearch(child, depth - 1, searchAlpha, searchBeta, 1, sw, token);
                    else
                    {
                        score = -PVSearch(child, depth - 1, -(currentBest + 1), -currentBest, 1, sw, token);
                        if (score > currentBest)
                            score = -PVSearch(child, depth - 1, -searchBeta, -currentBest, 1, sw, token);
                    }

                    ReleaseBoard(child);

                    if (score > currentBest)
                    {
                        currentBest = score;
                        currentBestMove = move;
                    }
                }

                if (!Aborted(sw, token) && (currentBest <= searchAlpha || currentBest >= searchBeta))
                {
                    // Failed low/high: widen the window and try the depth again
                    if (currentBest <= searchAlpha)
                        searchAlpha = int.MinValue + 1;
                    if (currentBest >= searchBeta)
                        searchBeta = int.MaxValue;
                    retry = true;
                }
            }
            while (retry && !Aborted(sw, token));

            // Only accept results from fully completed depths
            if (!Aborted(sw, token))
            {
                bestScore = currentBest;
                bestMove = currentBestMove;
                bestMoveForOrdering = ToPublicMove(currentBestMove);
                LastCompletedDepth = depth;
                AgeHistoryTable();
            }

            // A forced win was found — no deeper search needed
            if (bestScore >= MateThreshold)
                break;
        }

        return ToPublicMove(bestMove);
    }

    private int PVSearch(SearchBoard board, int depth, int alpha, int beta, int ply, Stopwatch sw, CancellationToken token)
    {
        NodesSearched++;
        if (Aborted(sw, token))
        {
            _aborted = true;
            return 0;
        }

        // Game ended by the previously applied move
        if (board.WinnerSide != SearchBoard.NoWinner)
            return TerminalScore(board.WinnerSide, board.Turn, ply);

        // Three-fold repetition → draw (checked before the TT probe: the hash carries
        // no repetition state, so a probed score would mask the draw)
        if (IsPathRepetition(board.Hash))
            return 0;

        // TT probe
        if (_tt.TryProbe(board.Hash, depth, alpha, beta, ply, out int ttScore, out Move ttMove))
            return ttScore;

        if (depth == 0)
            return QuiescenceSearch(board, alpha, beta, ply, sw, token);

        int side = board.Turn;
        var moves = _plyMoves[ply];
        int moveCount = board.GenerateMoves(side, moves);

        // No legal moves: the side to move loses
        if (moveCount == 0)
            return -(MateScore - ply);

        OrderMoves(board, moves, moveCount, ttMove, ply);

        SearchMove bestMove = moves[0];
        int bestScore = int.MinValue;
        BoundType bound = BoundType.UpperBound;

        // Push the position onto the repetition path (skip if the stack is full)
        bool pathPushed = _pathLen < _pathHashes.Length;
        if (pathPushed)
            _pathHashes[_pathLen++] = board.Hash;

        for (int i = 0; i < moveCount; i++)
        {
            if (Aborted(sw, token))
            {
                // A partially searched node must not store anything in the TT
                _aborted = true;
                break;
            }

            var move = moves[i];
            var child = GetBoard();
            board.CopyTo(child);
            child.ApplyMove(move);

            int score;
            if (i == 0)
                score = -PVSearch(child, depth - 1, -beta, -alpha, ply + 1, sw, token);
            else if (i >= 3 && depth >= 3 && !move.IsCapture)
            {
                // Late move reduction: quiet moves late in the order are searched
                // one ply shallower with a null window; re-searched if they score well.
                score = -PVSearch(child, depth - 2, -(alpha + 1), -alpha, ply + 1, sw, token);
                if (score > alpha)
                    score = -PVSearch(child, depth - 1, -(alpha + 1), -alpha, ply + 1, sw, token);
                if (score > alpha && score < beta)
                    score = -PVSearch(child, depth - 1, -beta, -alpha, ply + 1, sw, token);
            }
            else
            {
                score = -PVSearch(child, depth - 1, -(alpha + 1), -alpha, ply + 1, sw, token);
                if (score > alpha && score < beta)
                    score = -PVSearch(child, depth - 1, -beta, -alpha, ply + 1, sw, token);
            }

            ReleaseBoard(child);

            if (score > bestScore)
            {
                bestScore = score;
                bestMove = move;
            }

            if (score >= beta)
            {
                // Beta cutoff — this is a lower bound. Heuristic tables and the TT
                // must not be touched by scores from aborted searches.
                if (!move.IsCapture && ply < MaxPly && !_aborted)
                {
                    int packed = move.From | (move.To << 6);
                    _killerMoves[ply, 1] = _killerMoves[ply, 0];
                    _killerMoves[ply, 0] = packed;
                    _historyTable[move.From, move.To] += depth * depth;
                }

                if (!_aborted)
                    _tt.Store(board.Hash, depth, AdjustMateForStore(score, ply),
                        ToPublicMove(move), BoundType.LowerBound);
                if (pathPushed)
                    _pathLen--;
                return score;
            }

            if (score > alpha)
            {
                alpha = score;
                bound = BoundType.Exact;
            }
        }

        if (pathPushed)
            _pathLen--;

        // A partially searched node must not pollute the table
        if (!_aborted)
            _tt.Store(board.Hash, depth, AdjustMateForStore(bestScore, ply),
                ToPublicMove(bestMove), bound);
        return bestScore;
    }

    private int QuiescenceSearch(SearchBoard board, int alpha, int beta, int ply, Stopwatch sw, CancellationToken token)
    {
        NodesSearched++;
        if (Aborted(sw, token))
        {
            _aborted = true;
            return 0;
        }

        if (board.WinnerSide != SearchBoard.NoWinner)
            return TerminalScore(board.WinnerSide, board.Turn, ply);

        int side = board.Turn;
        int myMobility = board.CountLegalMoves(side);
        int oppMobility = board.CountLegalMoves(side ^ 1);
        int standPat = EvaluationFunction.Evaluate(board, side, myMobility, oppMobility, _legacyEval);

        if (standPat >= beta)
            return beta;

        if (standPat > alpha)
            alpha = standPat;

        if (ply > 32) // Safety limit
            return alpha;

        // Captures plus enemy-den entries (a winning "quiet" move that must be
        // visible at the horizon)
        var moves = _qMoves[ply];
        int moveCount = board.GenerateCaptures(side, moves);

        // Order captures by MVV-LVA; den entries (no victim) rank above every capture
        SortCaptures(board, moves, moveCount);

        for (int i = 0; i < moveCount; i++)
        {
            if (Aborted(sw, token))
            {
                _aborted = true;
                break;
            }

            var move = moves[i];
            var child = GetBoard();
            board.CopyTo(child);
            child.ApplyMove(move);

            int score = -QuiescenceSearch(child, -beta, -alpha, ply + 1, sw, token);
            ReleaseBoard(child);

            if (score >= beta)
                return beta;

            if (score > alpha)
                alpha = score;
        }

        return alpha;
    }

    private bool Aborted(Stopwatch sw, CancellationToken token) =>
        _aborted || sw.Elapsed.Ticks >= Interlocked.Read(ref _timeLimitTicks) || token.IsCancellationRequested;

    /// <summary>Score of a finished game, from the perspective of the side to move.</summary>
    private static int TerminalScore(byte winnerSide, int sideToMove, int ply) =>
        winnerSide == sideToMove ? MateScore - ply : ply - MateScore;

    private bool IsPathRepetition(ulong hash)
    {
        int occurrences = 0;
        for (int i = 0; i < _pathLen; i++)
        {
            if (_pathHashes[i] == hash && ++occurrences >= 2)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Convert a root-relative mate score into a node-relative score before storing.
    /// Clamped to ±MateScore so extreme values can never wrap during adjustment.
    /// </summary>
    private static int AdjustMateForStore(int score, int ply)
    {
        if (score > MateThreshold)
            return Math.Min(score + ply, MateScore);
        if (score < -MateThreshold)
            return Math.Max(score - ply, -MateScore);
        return score;
    }

    /// <summary>Halve all history scores so recent refutations dominate stale ones.</summary>
    private void AgeHistoryTable()
    {
        for (int f = 0; f < Zobrist.PositionCount; f++)
            for (int t = 0; t < Zobrist.PositionCount; t++)
                _historyTable[f, t] /= 2;
    }

    // ---- Move ordering ----

    private int MoveScore(SearchBoard board, in SearchMove move, Move ttMove, int ply)
    {
        if (ttMove.From.Row * 7 + ttMove.From.Col == move.From &&
            ttMove.To.Row * 7 + ttMove.To.Col == move.To)
            return 1_000_000;

        if (move.IsCapture)
        {
            // MVV-LVA ordering
            int victimRank = SearchBoard.RankOf[move.CapturedId];
            int attackerRank = SearchBoard.RankOf[board.Occupant(move.From)];
            return victimRank * 100 - attackerRank;
        }

        int score = 0;

        // Killer move heuristic (full from|to move)
        if (ply < MaxPly)
        {
            int packed = move.From | (move.To << 6);
            if (packed == _killerMoves[ply, 0])
                score = 900;
            else if (packed == _killerMoves[ply, 1])
                score = 800;
        }

        // History heuristic bonus
        score += Math.Min(_historyTable[move.From, move.To], 500);
        return score;
    }

    /// <summary>Insertion sort by move score (n is small and nearly sorted after iteration 1).</summary>
    private void OrderMoves(SearchBoard board, SearchMove[] moves, int count, Move ttMove, int ply)
    {
        for (int i = 1; i < count; i++)
        {
            var move = moves[i];
            int score = MoveScore(board, move, ttMove, ply);
            int j = i - 1;
            while (j >= 0 && MoveScore(board, moves[j], ttMove, ply) < score)
            {
                moves[j + 1] = moves[j];
                j--;
            }
            moves[j + 1] = move;
        }
    }

    private static void SortCaptures(SearchBoard board, SearchMove[] moves, int count)
    {
        for (int i = 1; i < count; i++)
        {
            var move = moves[i];
            int score = CaptureScore(board, move);
            int j = i - 1;
            while (j >= 0 && CaptureScore(board, moves[j]) < score)
            {
                moves[j + 1] = moves[j];
                j--;
            }
            moves[j + 1] = move;
        }
    }

    private static int CaptureScore(SearchBoard board, in SearchMove move)
    {
        int victimRank = move.IsCapture ? SearchBoard.RankOf[move.CapturedId] : 9; // den entries rank first
        int attackerRank = SearchBoard.RankOf[board.Occupant(move.From)];
        return victimRank * 10 - attackerRank;
    }

    // ---- Board pool and conversion ----

    private SearchBoard GetBoard() => _boardPool.Count > 0 ? _boardPool.Pop() : new SearchBoard();

    private void ReleaseBoard(SearchBoard board) => _boardPool.Push(board);

    private static Move ToPublicMove(in SearchMove move)
    {
        var from = new Position(move.From % 7, move.From / 7);
        var to = new Position(move.To % 7, move.To / 7);
        Piece? captured = move.CapturedId == 0 ? null : new Piece(
            (Animal)((((move.CapturedId - 1) % SearchBoard.DistinctPieceKinds) >> 1) + 1),
            (Player)(((move.CapturedId - 1) % SearchBoard.DistinctPieceKinds) & 1),
            to);
        return new Move(from, to, captured);
    }
}
