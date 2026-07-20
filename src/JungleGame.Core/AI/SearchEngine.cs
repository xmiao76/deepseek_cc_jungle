using System.Diagnostics;
using JungleGame.Core.Logic;
using JungleGame.Core.Models;

namespace JungleGame.Core.AI;

/// <summary>
/// Iterative deepening PVS (Principal Variation Search) with alpha-beta,
/// transposition table, null-move pruning, killer moves, history heuristic,
/// check extensions, and quiescence search.
/// </summary>
public class SearchEngine
{
    private readonly EvaluationFunction _evaluator;
    private readonly TranspositionTable _tt;
    private readonly ZobristHasher _hasher;
    private readonly GameEngine _gameEngine;

    private int _maxDepth;
    private int _timeLimitMs;
    private int _randomNoise;
    private bool _useAdvancedFeatures;

    private CancellationToken _ct;
    private Stopwatch _timer;
    private bool _timeUp;

    private int _nodesSearched;
    private int _ttHits;

    // Killer moves: [depth, slot] — 2 killers per depth
    private readonly Move?[,] _killers;
    // History heuristic: [pieceType 0..7, toCol 0..6, toRow 0..8]
    private readonly int[,,] _history;
    private const int MaxDepth = 64;

    public int NodesSearched => _nodesSearched;
    public int TTHits => _ttHits;

    public SearchEngine()
    {
        _evaluator = new EvaluationFunction();
        _tt = new TranspositionTable(1 << 20);
        _hasher = new ZobristHasher();
        _gameEngine = new GameEngine();
        _timer = new Stopwatch();
        _killers = new Move?[MaxDepth, 2];
        _history = new int[8, 7, 9];
    }

    public void SetDifficulty(DifficultyLevel level)
    {
        switch (level)
        {
            case DifficultyLevel.Easy:
                _maxDepth = 3; _timeLimitMs = 500; _randomNoise = 200; _useAdvancedFeatures = false; break;
            case DifficultyLevel.Medium:
                _maxDepth = 8; _timeLimitMs = 1500; _randomNoise = 20; _useAdvancedFeatures = true; break;
            case DifficultyLevel.Hard:
                _maxDepth = 14; _timeLimitMs = 3000; _randomNoise = 0; _useAdvancedFeatures = true; break;
            case DifficultyLevel.Expert:
                _maxDepth = 20; _timeLimitMs = 6000; _randomNoise = 0; _useAdvancedFeatures = true; break;
        }
    }

    public void SetCancellationToken(CancellationToken ct) => _ct = ct;

    public Move? FindBestMove(GameState state)
    {
        _nodesSearched = 0; _ttHits = 0;
        _timer.Restart(); _timeUp = false;
        _tt.Clear();
        Array.Clear(_killers, 0, _killers.Length);
        Array.Clear(_history, 0, _history.Length);

        var legalMoves = _gameEngine.GetLegalMoves(state);
        if (legalMoves.Count == 0) return null;
        if (legalMoves.Count == 1) return legalMoves[0];

        // Opening principles for very early game
        if (state.MoveHistory.Count < 3 && legalMoves.Count > 1)
        {
            var bookMove = GetOpeningMove(state, legalMoves);
            if (bookMove != null) return bookMove;
        }

        Move? bestMove = legalMoves[0];
        int currentDepth = 1;
        int alpha = int.MinValue + 1, beta = int.MaxValue - 1;

        while (currentDepth <= _maxDepth && !_timeUp && !_ct.IsCancellationRequested)
        {
            int bestScore = int.MinValue;
            Move? depthBestMove = null;

            var orderedMoves = OrderRootMoves(state, legalMoves);

            for (int i = 0; i < orderedMoves.Count; i++)
            {
                if (_timeUp || _ct.IsCancellationRequested) break;

                var move = orderedMoves[i];
                var newState = _gameEngine.ApplyMove(state, move);
                int score;

                if (i == 0)
                {
                    // First move: full window search
                    score = -AlphaBeta(newState, currentDepth - 1, -beta, -alpha, true);
                }
                else
                {
                    // PVS: zero-window search first
                    score = -AlphaBeta(newState, currentDepth - 1, -alpha - 1, -alpha, true);
                    if (score > alpha && score < beta)
                    {
                        // Re-search with full window
                        score = -AlphaBeta(newState, currentDepth - 1, -beta, -alpha, true);
                    }
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    depthBestMove = move;
                    if (score > alpha) alpha = score;
                }
            }

            if (depthBestMove != null && !_timeUp)
            {
                bestMove = depthBestMove;
                // Narrow aspiration window for next iteration
                alpha = Math.Max(int.MinValue + 1, bestScore - 250);
                beta = Math.Min(int.MaxValue - 1, bestScore + 250);
            }

            currentDepth++;
            if (_timer.ElapsedMilliseconds > _timeLimitMs * 0.65) break;
        }

        // Easy difficulty random moves
        if (_randomNoise >= 200 && legalMoves.Count > 1)
        {
            var rng = new Random();
            if (rng.Next(100) < 25)
                return MoveOrderer.OrderMoves(legalMoves)[rng.Next(Math.Min(3, legalMoves.Count))];
        }

        return bestMove;
    }

    private int AlphaBeta(GameState state, int depth, int alpha, int beta, bool allowNull)
    {
        if (_timeUp || _ct.IsCancellationRequested) return 0;
        _nodesSearched++;

        // Terminal check
        if (state.Phase == GamePhase.GameOver)
        {
            if (state.Winner == state.CurrentPlayer) return -1_000_000;
            return 1_000_000;
        }

        // Leaf node
        if (depth <= 0)
            return QuiescenceSearch(state, alpha, beta, 4);

        // TT lookup
        ulong hash = _hasher.ComputeHash(state.Pieces, state.CurrentPlayer);
        if (_tt.TryLookup(hash, depth, alpha, beta, out int ttScore, out Move? ttMove))
        {
            _ttHits++;
            return ttScore;
        }

        // Null-move pruning (use R=3 for jungle)
        if (allowNull && depth >= 3)
        {
            var nullState = state.With(currentPlayer: Board.Opponent(state.CurrentPlayer));
            int nullScore = -AlphaBeta(nullState, depth - 1 - 3, -beta, -beta + 1, false);
            if (nullScore >= beta) return beta;
        }

        var moves = _gameEngine.GetLegalMoves(state);
        if (moves.Count == 0)
            return -1_000_000 + (10 - depth) * 1000;

        // Check extension: extend search if den is threatened
        bool inCheck = IsDenThreatened(state);
        if (inCheck) depth++;

        // Order moves with all heuristics
        moves = OrderMovesInternal(moves, ttMove, depth);

        int bestValue = int.MinValue + 1;
        Move? bestMove = null;
        TranspositionFlag flag = TranspositionFlag.UpperBound;
        int movesSearched = 0;

        foreach (var move in moves)
        {
            var newState = _gameEngine.ApplyMove(state, move);
            int value;

            // Late Move Reduction: reduce quiet moves after first few
            if (movesSearched >= 4 && depth >= 3 && !move.IsDenEntry && move.CapturedPiece == null && !move.IsRiverJump)
            {
                int r = depth >= 6 ? 2 : 1;
                value = -AlphaBeta(newState, depth - 1 - r, -alpha - 1, -alpha, false);
                if (value <= alpha)
                {
                    movesSearched++;
                    continue; // Failed low, skip full search
                }
                // Re-search at full depth
                value = -AlphaBeta(newState, depth - 1, -beta, -alpha, allowNull);
            }
            else
            {
                value = -AlphaBeta(newState, depth - 1, -beta, -alpha, allowNull);
            }

            movesSearched++;

            if (value > bestValue)
            {
                bestValue = value;
                bestMove = move;
            }
            if (value > alpha)
            {
                alpha = value;
                flag = TranspositionFlag.Exact;
            }
            if (alpha >= beta)
            {
                flag = TranspositionFlag.LowerBound;
                // Store killer move (quiet moves only)
                if (move.CapturedPiece == null && !move.IsDenEntry)
                    StoreKiller(move, depth);
                // Update history heuristic
                _history[((int)move.Piece.Type - 1), move.To.Col - 1, move.To.Row - 1] += depth * depth;
                break;
            }

            if (_timer.ElapsedMilliseconds > _timeLimitMs) { _timeUp = true; break; }
        }

        if (!_timeUp)
            _tt.Store(hash, depth, bestValue, flag, bestMove);

        return bestValue;
    }

    private int QuiescenceSearch(GameState state, int alpha, int beta, int maxDepth)
    {
        if (maxDepth <= 0) return _evaluator.Evaluate(state, state.CurrentPlayer);

        int standPat = _evaluator.Evaluate(state, state.CurrentPlayer);
        if (standPat >= beta) return beta;
        if (standPat > alpha) alpha = standPat;

        // Search captures, den entries, and trap-step moves (tactical)
        var moves = _gameEngine.GetLegalMoves(state)
            .Where(m => m.CapturedPiece != null || m.IsDenEntry || StepsIntoTrap(state, m))
            .ToList();

        // SEE-like pruning: skip bad captures
        moves = moves.Where(m => !IsBadCapture(m)).ToList();

        moves = MoveOrderer.OrderMoves(moves);

        foreach (var move in moves)
        {
            var newState = _gameEngine.ApplyMove(state, move);
            int score = -QuiescenceSearch(newState, -beta, -alpha, maxDepth - 1);
            if (score >= beta) return beta;
            if (score > alpha) alpha = score;
        }
        return alpha;
    }

    // ==================== Move ordering ====================

    private List<Move> OrderRootMoves(GameState state, List<Move> moves)
    {
        ulong hash = _hasher.ComputeHash(state.Pieces, state.CurrentPlayer);
        Move? ttMove = null;
        _tt.TryLookup(hash, 1, int.MinValue, int.MaxValue, out _, out ttMove);

        // Put TT best move first
        if (ttMove != null)
        {
            var match = moves.FirstOrDefault(m => m.From == ttMove.From && m.To == ttMove.To);
            if (match != null) { moves.Remove(match); moves.Insert(0, match); return moves; }
        }
        return MoveOrderer.OrderMoves(moves);
    }

    private List<Move> OrderMovesInternal(List<Move> moves, Move? ttMove, int depth)
    {
        // TT move first
        if (ttMove != null)
        {
            var match = moves.FirstOrDefault(m => m.From == ttMove.From && m.To == ttMove.To);
            if (match != null) { moves.Remove(match); moves.Insert(0, match); return moves; }
        }

        // Score and sort
        var scored = moves.Select(m => (Move: m, Score: ScoreMoveInternal(m, depth))).ToList();
        scored.Sort((a, b) => b.Score.CompareTo(a.Score));
        return scored.Select(x => x.Move).ToList();
    }

    private int ScoreMoveInternal(Move move, int depth)
    {
        // TT best is handled before calling this

        // Killer move 1
        if (_killers[depth, 0] != null &&
            _killers[depth, 0]!.From == move.From &&
            _killers[depth, 0]!.To == move.To)
            return 9000;
        // Killer move 2
        if (_killers[depth, 1] != null &&
            _killers[depth, 1]!.From == move.From &&
            _killers[depth, 1]!.To == move.To)
            return 8000;

        int score = 0;

        if (move.IsDenEntry) return 20000;
        if (move.CapturedPiece != null)
            score += 10000 + (int)move.CapturedPiece.Type * 100 - (int)move.Piece.Type * 10;
        if (move.IsRiverJump) score += 3000;

        // History heuristic bonus
        score += _history[((int)move.Piece.Type - 1), move.To.Col - 1, move.To.Row - 1] / 10;

        // Forward progress
        int forward = move.Piece.Owner == Player.Blue
            ? move.From.Row - move.To.Row   // Blue goes UP toward Red's den (row 1)
            : move.To.Row - move.From.Row;  // Red goes DOWN toward Blue's den (row 9)
        score += forward * 20;

        return score;
    }

    private void StoreKiller(Move move, int depth)
    {
        if (depth >= MaxDepth || depth < 0) return;
        if (_killers[depth, 0] == null || !SameMove(_killers[depth, 0]!, move))
        {
            _killers[depth, 1] = _killers[depth, 0];
            _killers[depth, 0] = move;
        }
    }

    private static bool SameMove(Move a, Move b) => a.From == b.From && a.To == b.To;

    // ==================== Helpers ====================

    private static bool IsDenThreatened(GameState state)
    {
        int denRow = state.CurrentPlayer == Player.Blue ? 9 : 1;
        foreach (var kvp in state.Pieces)
        {
            if (kvp.Value.Owner == state.CurrentPlayer) continue;
            if (Math.Abs(kvp.Value.Position.Row - denRow) <= 1 && Math.Abs(kvp.Value.Position.Col - 4) <= 1)
                return true;
        }
        return false;
    }

    private static bool StepsIntoTrap(GameState state, Move move)
    {
        return Board.IsOpponentTrap(move.To, move.Piece.Owner);
    }

    private static bool IsBadCapture(Move move)
    {
        if (move.CapturedPiece == null) return false;
        // Don't prune if attacker is lower value than victim (good trade)
        int attackerVal = (int)move.Piece.Type;
        int victimVal = (int)move.CapturedPiece.Type;
        // Rat captures Elephant is always good
        if (move.Piece.Type == PieceType.Rat && move.CapturedPiece.Type == PieceType.Elephant) return false;
        // Prune if losing a higher-valued piece for a lower-valued one
        return attackerVal > victimVal;
    }

    private Move? GetOpeningMove(GameState state, List<Move> legalMoves)
    {
        // Prefer advancing central pieces in opening
        var preferred = legalMoves
            .Where(m => m.Piece.Type is PieceType.Leopard or PieceType.Wolf or PieceType.Tiger or PieceType.Lion)
            .Where(m => !m.IsRiverJump) // Don't jump rivers too early
            .Where(m => IsForwardMove(m))
            .OrderByDescending(m => (int)m.Piece.Type)
            .ToList();

        if (preferred.Count > 0)
        {
            var best = preferred.First();
            // Move toward center
            var towardCenter = preferred
                .OrderBy(m => Math.Abs(m.To.Col - 4))
                .First();
            return towardCenter;
        }
        return null;
    }

    private static bool IsForwardMove(Move move) => move.Piece.Owner == Player.Blue
        ? move.To.Row < move.From.Row   // Blue goes up toward Red's den
        : move.To.Row > move.From.Row;  // Red goes down toward Blue's den
}
