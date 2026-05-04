using System.Diagnostics;
using JungleGame.Core.Engine;
using JungleGame.Core.Model;
using JungleGame.Core.Rules;

namespace JungleGame.Core.AI;

public class MinimaxEngine
{
    private readonly TimeSpan _timeLimit;
    private readonly TranspositionTable _tt;
    private readonly int[,] _killerMoves;
    private readonly int[,] _historyTable;
    private const int MaxKillerPly = 64;
    private const int PositionCount = 63; // 7×9

    public MinimaxEngine(TimeSpan? timeLimit = null)
    {
        _timeLimit = timeLimit ?? TimeSpan.FromSeconds(4);
        _tt = new TranspositionTable(1 << 20);
        _killerMoves = new int[MaxKillerPly, 2];
        _historyTable = new int[PositionCount, PositionCount];
    }

    public Move FindBestMove(GameState state)
    {
        var sw = Stopwatch.StartNew();
        var moves = MoveGenerator.GenerateLegalMoves(state, state.CurrentTurn);

        if (moves.Count == 0)
            throw new InvalidOperationException("No legal moves available.");

        if (moves.Count == 1)
            return moves[0];

        Move bestMove = moves[0];
        int bestScore = int.MinValue;

        for (int depth = 1; depth <= 20; depth++)
        {
            if (sw.Elapsed >= _timeLimit)
                break;

            int currentBest = int.MinValue;
            Move currentBestMove = bestMove;

            // Order root moves
            OrderMoves(moves, state, bestMove, 0);

            for (int i = 0; i < moves.Count; i++)
            {
                if (sw.Elapsed >= _timeLimit)
                    break;

                var move = moves[i];
                var newState = GameController.ApplyMove(state, move);

                int score;
                if (i == 0)
                    score = -PVSearch(newState, depth - 1, int.MinValue + 1, int.MaxValue, 1, sw);
                else
                {
                    score = -PVSearch(newState, depth - 1, -(currentBest + 1), -currentBest, 1, sw);
                    if (score > currentBest)
                        score = -PVSearch(newState, depth - 1, int.MinValue + 1, -currentBest, 1, sw);
                }

                if (score > currentBest)
                {
                    currentBest = score;
                    currentBestMove = move;
                }
            }

            if (sw.Elapsed < _timeLimit)
            {
                bestScore = currentBest;
                bestMove = currentBestMove;
            }

            // Early exit: found winning move
            if (bestScore > 900000)
                break;
        }

        return bestMove;
    }

    private int PVSearch(GameState state, int depth, int alpha, int beta, int ply, Stopwatch sw)
    {
        if (sw.Elapsed >= _timeLimit)
            return 0;

        // TT probe
        ulong hash = TranspositionTable.ComputeHash(state);
        if (_tt.TryProbe(hash, depth, alpha, beta, out int ttScore, out Move ttBestMove))
            return ttScore;

        if (depth == 0)
            return QuiescenceSearch(state, alpha, beta, ply, sw);

        if (state.Status != GameStatus.InProgress)
            return EvaluationFunction.Evaluate(state, state.CurrentTurn);

        var moves = MoveGenerator.GenerateLegalMoves(state, state.CurrentTurn);

        if (moves.Count == 0)
            return EvaluationFunction.Evaluate(state, state.CurrentTurn);

        OrderMoves(moves, state, ttBestMove, ply);

        Move bestMove = moves[0];
        int bestScore = int.MinValue;
        BoundType bound = BoundType.UpperBound;

        for (int i = 0; i < moves.Count; i++)
        {
            if (sw.Elapsed >= _timeLimit)
                break;

            var move = moves[i];
            var newState = GameController.ApplyMove(state, move);

            int score;
            if (i == 0)
                score = -PVSearch(newState, depth - 1, -beta, -alpha, ply + 1, sw);
            else
            {
                score = -PVSearch(newState, depth - 1, -(alpha + 1), -alpha, ply + 1, sw);
                if (score > alpha && score < beta)
                    score = -PVSearch(newState, depth - 1, -beta, -alpha, ply + 1, sw);
            }

            if (score > bestScore)
            {
                bestScore = score;
                bestMove = move;
            }

            if (score >= beta)
            {
                // Beta cutoff — this is a lower bound
                if (!move.IsCapture && ply < MaxKillerPly)
                {
                    // Store killer move
                    _killerMoves[ply, 1] = _killerMoves[ply, 0];
                    _killerMoves[ply, 0] = PosIndex(move.To);

                    // Update history table
                    _historyTable[PosIndex(move.From), PosIndex(move.To)] += depth * depth;
                }

                _tt.Store(hash, depth, score, move, BoundType.LowerBound);
                return score;
            }

            if (score > alpha)
            {
                alpha = score;
                bound = BoundType.Exact;
            }
        }

        _tt.Store(hash, depth, bestScore, bestMove, bound);
        return bestScore;
    }

    private int QuiescenceSearch(GameState state, int alpha, int beta, int ply, Stopwatch sw)
    {
        if (sw.Elapsed >= _timeLimit)
            return 0;

        int standPat = EvaluationFunction.Evaluate(state, state.CurrentTurn);

        if (standPat >= beta)
            return beta;

        if (standPat > alpha)
            alpha = standPat;

        if (ply > 32) // Safety limit
            return alpha;

        // Generate only captures
        var allMoves = MoveGenerator.GenerateLegalMoves(state, state.CurrentTurn);
        var captures = new List<Move>();
        foreach (var m in allMoves)
        {
            if (m.IsCapture)
            {
                // Only search captures that win material or are equal trades
                var attacker = state.GetPieceAt(m.From)!.Value;
                var defender = m.Captured!.Value;
                int attackerVal = attacker.Rank;
                int defenderVal = defender.Rank;

                // Also capture if defender is on attacker's trap (free capture)
                if (state.Board.IsTrap(defender.Position, defender.Owner))
                    defenderVal = 0;

                if (defenderVal >= attackerVal) // Winning or equal capture
                    captures.Add(m);
            }
        }

        // Order captures by MVV-LVA
        captures.Sort((a, b) =>
        {
            int scoreA = (int)a.Captured!.Value.Animal * 10 - (int)(state.GetPieceAt(a.From)?.Rank ?? 0);
            int scoreB = (int)b.Captured!.Value.Animal * 10 - (int)(state.GetPieceAt(b.From)?.Rank ?? 0);
            return scoreB.CompareTo(scoreA);
        });

        foreach (var move in captures)
        {
            var newState = GameController.ApplyMove(state, move);
            int score = -QuiescenceSearch(newState, -beta, -alpha, ply + 1, sw);

            if (score >= beta)
                return beta;

            if (score > alpha)
                alpha = score;
        }

        return alpha;
    }

    private void OrderMoves(List<Move> moves, GameState state, Move ttBestMove, int ply)
    {
        var scores = new (Move move, int score)[moves.Count];

        for (int i = 0; i < moves.Count; i++)
        {
            var move = moves[i];

            // TT best move first
            if (move.From == ttBestMove.From && move.To == ttBestMove.To)
            {
                scores[i] = (move, 1000000);
                continue;
            }

            int score = 0;

            if (move.IsCapture)
            {
                // MVV-LVA ordering
                var attacker = state.GetPieceAt(move.From)!.Value;
                int victimRank = (int)move.Captured!.Value.Animal;
                int attackerRank = (int)attacker.Animal;
                score = victimRank * 100 - attackerRank;
            }
            else
            {
                // Killer move heuristic
                int toIdx = PosIndex(move.To);
                if (ply < MaxKillerPly)
                {
                    if (toIdx == _killerMoves[ply, 0])
                        score = 900;
                    else if (toIdx == _killerMoves[ply, 1])
                        score = 800;
                }

                // History heuristic bonus
                int fromIdx = PosIndex(move.From);
                score += Math.Min(_historyTable[fromIdx, toIdx], 500);
            }

            scores[i] = (move, score);
        }

        moves.Clear();
        foreach (var (move, _) in scores.OrderByDescending(s => s.score))
            moves.Add(move);
    }

    private static int PosIndex(Position pos) => pos.Row * 7 + pos.Col;
}
