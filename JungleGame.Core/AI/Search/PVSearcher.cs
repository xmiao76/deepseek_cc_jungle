using System.Diagnostics;
using JungleGame.Core.Engine;
using JungleGame.Core.Model;

namespace JungleGame.Core.AI;

/// <summary>
/// The search core: iterative-deepening root loop, PVS with LMR, null-move
/// pruning with verification, depth-1 futility pruning, quiescence search with
/// delta pruning and lazy mobility. Moved verbatim from the pre-refactor
/// MinimaxEngine (see the A/B protocol notes in CLAUDE.md — several deliberate
/// quirks here were empirically validated and must not be "fixed"):
/// the un-negated root window at i==0, the aspiration fail wide-to-full-width,
/// and the LMR-exempt branch still searching at depth-2.
/// </summary>
internal sealed class PVSearcher
{
    internal const int MaxPly = 64;
    private const int DefaultMaxDepth = 20;
    private const int AspirationDelta = 25;
    private const int FutilityMargin = 300;
    private const int DeltaMargin = 200;
    private const int MateThreshold = TranspositionTable.MateScore - TranspositionTable.MateRange;

    private readonly TranspositionTable _tt;
    private readonly TimeManager _time;
    private readonly MoveOrdering _ordering;
    private readonly SearchContext _context;
    private readonly SearchOptions _options;
    private readonly int _maxDepth;

    internal PVSearcher(
        TranspositionTable tt,
        TimeManager time,
        MoveOrdering ordering,
        SearchContext context,
        SearchOptions options,
        int? maxDepth)
    {
        _tt = tt;
        _time = time;
        _ordering = ordering;
        _context = context;
        _options = options;
        // PVSearch plus quiescence plies must fit inside the per-ply buffer tables
        _maxDepth = Math.Min(maxDepth ?? DefaultMaxDepth, MaxPly - 32);
    }

    internal long Nodes { get; private set; }
    internal int LastCompletedDepth { get; private set; }

    internal Move? SearchBestMove(GameState state, CancellationToken token)
    {
        if (state.Status != GameStatus.InProgress)
            return null;

        _context.Reset();
        _time.Reset();
        Nodes = 0;
        LastCompletedDepth = 0;
        var sw = Stopwatch.StartNew();

        var root = SearchBoard.FromGameState(state);

        // Seed the repetition path with the real game's history, reserving headroom
        // for the search path itself (long shuffling games can approach the cap)
        _context.SeedPath(state);

        int side = (int)state.CurrentTurn;
        int moveCount = root.GenerateMoves(side, _context.RootMoves);
        if (moveCount == 0)
            return null;
        if (moveCount == 1)
            return ToPublicMove(_context.RootMoves[0]);

        var moves = _context.RootMoves;
        SearchMove bestMove = moves[0];
        Move bestMoveForOrdering = ToPublicMove(bestMove);
        int bestScore = int.MinValue;

        for (int depth = 1; depth <= _maxDepth; depth++)
        {
            if (_time.Check(sw, token))
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

                _ordering.OrderMoves(root, moves, moveCount, bestMoveForOrdering, 0);

                for (int i = 0; i < moveCount; i++)
                {
                    if (_time.Check(sw, token))
                        break;

                    var move = moves[i];
                    var child = _context.GetBoard();
                    root.CopyTo(child);
                    child.ApplyMove(move);

                    int score;
                    if (i == 0)
                        // NOTE: the root PV child is searched with the UN-negated
                        // window, matching the original engine. The "correct"
                        // negated form (-searchBeta, -searchAlpha) was tried and
                        // produced draw-seeking play (see CLAUDE.md recorded
                        // results): with proper cutoffs, the narrow aspiration
                        // window stores bound entries that steer later searches
                        // toward repetition lines in equal positions.
                        score = -PVSearch(child, depth - 1, searchAlpha, searchBeta, 1, sw, token);
                    else
                    {
                        score = -PVSearch(child, depth - 1, -(currentBest + 1), -currentBest, 1, sw, token);
                        if (score > currentBest)
                            score = -PVSearch(child, depth - 1, -searchBeta, -currentBest, 1, sw, token);
                    }

                    _context.ReleaseBoard(child);

                    if (score > currentBest)
                    {
                        currentBest = score;
                        currentBestMove = move;
                    }
                }

                if (!_time.Check(sw, token) && (currentBest <= searchAlpha || currentBest >= searchBeta))
                {
                    // Failed low/high: widen the window to full width and try the
                    // depth again (incremental widening was tried and reverted —
                    // see the root-window note above)
                    if (currentBest <= searchAlpha)
                        searchAlpha = int.MinValue + 1;
                    if (currentBest >= searchBeta)
                        searchBeta = int.MaxValue;
                    retry = true;
                }
            }
            while (retry && !_time.Check(sw, token));

            // Only accept results from fully completed depths
            if (!_time.Check(sw, token))
            {
                bestScore = currentBest;
                bestMove = currentBestMove;
                bestMoveForOrdering = ToPublicMove(currentBestMove);
                LastCompletedDepth = depth;
                _ordering.AgeHistoryTable();
            }

            // A forced win was found — no deeper search needed
            if (bestScore >= MateThreshold)
                break;
        }

        return ToPublicMove(bestMove);
    }

    private int PVSearch(SearchBoard board, int depth, int alpha, int beta, int ply, Stopwatch sw, CancellationToken token)
    {
        Nodes++;
        if (_time.Check(sw, token))
            return 0;

        // Game ended by the previously applied move
        if (board.WinnerSide != SearchBoard.NoWinner)
            return TerminalScore(board.WinnerSide, board.Turn, ply);

        // Three-fold repetition → draw (checked before the TT probe: the hash carries
        // no repetition state, so a probed score would mask the draw)
        if (_context.IsRepetition(board.Hash))
            return 0;

        // TT probe
        if (_tt.TryProbe(board.Hash, depth, alpha, beta, ply, out int ttScore, out Move ttMove))
            return ttScore;

        if (depth == 0)
            return QuiescenceSearch(board, alpha, beta, ply, sw, token);

        int side = board.Turn;

        // Null-move pruning: if the side to move can pass and still refute the
        // opponent's best try at reduced depth, this node is a beta cutoff. The
        // pass move makes the position strictly worse for the passer, so a cutoff
        // is sound — except in zugzwang, which Dou Shou Qi positions with few
        // pieces frequently are. Guards: disabled with <= 6 total pieces or <= 2
        // pieces for the side to move; a verification re-search at reduced depth
        // must confirm the cutoff. (Never applied at the root.)
        if (!_options.LegacySearch && depth >= 3 && beta < MateThreshold
            && board.PieceCount(0) + board.PieceCount(1) > 6
            && board.PieceCount(side) > 2)
        {
            const int nullReduction = 2;
            board.MakeNullMove();
            int nullScore = -PVSearch(board, depth - 1 - nullReduction, -beta, -(beta - 1), ply + 1, sw, token);
            board.UnmakeNullMove();
            if (_time.Check(sw, token))
                return 0;

            if (nullScore >= beta)
            {
                // Verification: a reduced-depth real search of this node must
                // confirm the cutoff (catches zugzwang the guards missed)
                int verifyScore = PVSearch(board, depth - 1 - nullReduction, alpha, beta, ply, sw, token);
                if (_time.Check(sw, token))
                    return 0;
                if (verifyScore >= beta)
                    return beta;
            }
        }

        var moves = _context.PlyMoves[ply];
        int moveCount = board.GenerateMoves(side, moves);

        // No legal moves: the side to move loses
        if (moveCount == 0)
            return -(TranspositionTable.MateScore - ply);

        _ordering.OrderMoves(board, moves, moveCount, ttMove, ply);

        SearchMove bestMove = moves[0];
        int bestScore = int.MinValue;
        BoundType bound = BoundType.UpperBound;
        bool anySearched = false;

        // Futility pruning at the frontier: a quiet move that cannot lift the
        // static score near alpha is skipped. Trap squares are exempt — their
        // tactical value is not captured by the static evaluation. Never applies
        // in mate-or-be-mated windows, where the only defense may be quiet moves.
        bool futilityApplicable = !_options.LegacySearch && depth == 1
            && alpha > -MateThreshold && alpha < MateThreshold
            && board.PieceCount(0) + board.PieceCount(1) > 8;
        int staticEval = 0;
        if (futilityApplicable)
            staticEval = EvaluationFunction.EvaluateStatic(board, side, _options.LegacyEval);

        // Push the position onto the repetition path (skip if the stack is full)
        bool pathPushed = _context.CanPush;
        if (pathPushed)
            _context.Push(board.Hash);

        for (int i = 0; i < moveCount; i++)
        {
            if (_time.Check(sw, token))
            {
                // A partially searched node must not store anything in the TT
                break;
            }

            var move = moves[i];

            if (futilityApplicable && !move.IsCapture && !move.EntersDen
                && !SearchBoard.IsEnemyTrapSquare(move.To, side)
                && SearchBoard.EffectiveRankOf(board.Occupant(move.From), move.From) != 0
                && staticEval + FutilityMargin <= alpha)
                continue;

            anySearched = true;

            var child = _context.GetBoard();
            board.CopyTo(child);
            child.ApplyMove(move);

            int score;
            if (i == 0)
                score = -PVSearch(child, depth - 1, -beta, -alpha, ply + 1, sw, token);
            else if (i >= 3 && depth >= 3 && !move.IsCapture)
            {
                // Late move reduction: quiet moves late in the order are searched
                // shallower with a null window; re-searched if they score well.
                // The reduction scales with move index and depth; moves that enter
                // a den, and the last couple of moves (small move sets), are exempt.
                bool reduce = !_options.LegacySearch && !move.EntersDen && i < moveCount - 2;
                int reducedDepth = reduce
                    ? Math.Max(1, depth - 1 - (1 + (i >= 6 ? 1 : 0) + (depth >= 8 ? 1 : 0)))
                    : depth - 2;

                score = -PVSearch(child, reducedDepth, -(alpha + 1), -alpha, ply + 1, sw, token);
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

            _context.ReleaseBoard(child);

            if (score > bestScore)
            {
                bestScore = score;
                bestMove = move;
            }

            if (score >= beta)
            {
                // Beta cutoff — this is a lower bound. Heuristic tables and the TT
                // must not be touched by scores from aborted searches.
                if (!_time.Aborted)
                    _ordering.OnBetaCutoff(move, depth, ply);

                if (!_time.Aborted)
                    _tt.Store(board.Hash, depth, AdjustMateForStore(score, ply),
                        ToPublicMove(move), BoundType.LowerBound);
                if (pathPushed)
                    _context.Pop(board.Hash);
                return score;
            }

            if (score > alpha)
            {
                alpha = score;
                bound = BoundType.Exact;
            }
        }

        if (pathPushed)
            _context.Pop(board.Hash);

        // Every move was pruned by futility: return the static evaluation (a
        // fail-low value). Returning int.MinValue here would overflow when
        // negated by the parent and turn the line into a phantom blunder.
        if (!anySearched && !_time.Aborted)
            return staticEval;

        // An abort between the top-of-function check and the first completed
        // move leaves bestScore at int.MinValue, which would wrap on negation
        // in the parent; return a neutral value instead (the abort flag already
        // prevents any TT store and root acceptance)
        if (_time.Aborted)
            return 0;

        // A partially searched node must not pollute the table
        _tt.Store(board.Hash, depth, AdjustMateForStore(bestScore, ply),
            ToPublicMove(bestMove), bound);
        return bestScore;
    }

    private int QuiescenceSearch(SearchBoard board, int alpha, int beta, int ply, Stopwatch sw, CancellationToken token)
    {
        Nodes++;
        if (_time.Check(sw, token))
            return 0;

        if (board.WinnerSide != SearchBoard.NoWinner)
            return TerminalScore(board.WinnerSide, board.Turn, ply);

        int side = board.Turn;

        // Lazy mobility in the stand-pat: the full evaluation costs two move
        // generations per qnode. Most qnodes are null-window cut nodes, so the
        // mobility-free value is checked first; the full evaluation only runs
        // when that value falls inside the window.
        int standPat = _options.LegacySearch
            ? EvaluationFunction.Evaluate(board, side, board.CountLegalMoves(side), board.CountLegalMoves(side ^ 1), _options.LegacyEval)
            : EvaluationFunction.EvaluateStatic(board, side, _options.LegacyEval);

        if (standPat >= beta)
            return beta;

        if (!_options.LegacySearch && standPat > alpha)
        {
            standPat = EvaluationFunction.Evaluate(board, side, board.CountLegalMoves(side), board.CountLegalMoves(side ^ 1), _options.LegacyEval);
            if (standPat >= beta)
                return beta;
        }

        if (standPat > alpha)
            alpha = standPat;

        if (ply > 32) // Safety limit
            return alpha;

        // Captures plus enemy-den entries (a winning "quiet" move that must be
        // visible at the horizon)
        var moves = _context.QMoves[ply];
        int moveCount = board.GenerateCaptures(side, moves);

        // Order captures by MVV-LVA; den entries (no victim) rank above every capture
        _ordering.SortCaptures(board, moves, moveCount);

        for (int i = 0; i < moveCount; i++)
        {
            if (_time.Check(sw, token))
                break;

            var move = moves[i];

            // Delta pruning: skip a capture that cannot raise the score near
            // alpha. Den entries are never pruned (winning move at the horizon).
            if (!_options.LegacySearch && move.IsCapture)
            {
                int victimGain = SearchBoard.EffectiveRankOf(move.CapturedId, move.To) * 100;
                if (standPat + victimGain + DeltaMargin <= alpha)
                    continue;

                // SEE pruning: a capture that loses the exchange never improves
                // a quiet position (the opponent recaptures favorably), so it is
                // not worth searching at the horizon.
                if (SeeCalculator.See(board, move) < 0)
                    continue;
            }

            var child = _context.GetBoard();
            board.CopyTo(child);
            child.ApplyMove(move);

            int score = -QuiescenceSearch(child, -beta, -alpha, ply + 1, sw, token);
            _context.ReleaseBoard(child);

            if (score >= beta)
                return beta;

            if (score > alpha)
                alpha = score;
        }

        return alpha;
    }

    /// <summary>Score of a finished game, from the perspective of the side to move.</summary>
    private static int TerminalScore(byte winnerSide, int sideToMove, int ply) =>
        winnerSide == sideToMove ? TranspositionTable.MateScore - ply : ply - TranspositionTable.MateScore;

    /// <summary>
    /// Convert a root-relative mate score into a node-relative score before storing.
    /// Clamped to ±MateScore so extreme values can never wrap during adjustment.
    /// </summary>
    private static int AdjustMateForStore(int score, int ply)
    {
        if (score > MateThreshold)
            return Math.Min(score + ply, TranspositionTable.MateScore);
        if (score < -MateThreshold)
            return Math.Max(score - ply, -TranspositionTable.MateScore);
        return score;
    }

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
