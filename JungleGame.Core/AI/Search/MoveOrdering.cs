using JungleGame.Core.Engine;
using JungleGame.Core.Model;

namespace JungleGame.Core.AI;

/// <summary>
/// Move-ordering state and scoring: killer moves (two slots per ply), a
/// from/to-square history table with per-iteration aging, and the insertion
/// sorts used by the main search and quiescence. Move scores are precomputed
/// into a scratch buffer before sorting — the previous implementation
/// re-scored both elements of every comparison (repeated TT and history
/// lookups inside the inner loop).
/// </summary>
internal sealed class MoveOrdering
{
    private const int KillerBonus1 = 900;
    private const int KillerBonus2 = 800;
    private const int HistoryScoreCap = 500;
    private const int SeeSignBonus = 50;

    private readonly bool _useSee; // disabled by legacySearch (A/B strength tests)
    private readonly int[,] _killerMoves = new int[PVSearcher.MaxPly, 2];
    private readonly int[,] _historyTable = new int[Zobrist.PositionCount, Zobrist.PositionCount];
    private readonly int[] _scoreScratch = new int[SearchBoard.MaxMovesPerPly];

    internal MoveOrdering(bool useSee)
    {
        _useSee = useSee;
    }

    internal int MoveScore(SearchBoard board, in SearchMove move, Move ttMove, int ply)
    {
        if (ttMove.From.Row * 7 + ttMove.From.Col == move.From &&
            ttMove.To.Row * 7 + ttMove.To.Col == move.To)
            return 1_000_000;

        if (move.IsCapture)
        {
            // MVV-LVA ordering; the SEE sign splits winning from losing
            // exchanges (a capture that loses material searches last).
            int victimRank = SearchBoard.RankOf[move.CapturedId];
            int attackerRank = SearchBoard.RankOf[board.Occupant(move.From)];
            int captureScore = victimRank * 100 - attackerRank;
            if (_useSee)
                captureScore += SeeCalculator.See(board, move) >= 0 ? SeeSignBonus : -SeeSignBonus;
            return captureScore;
        }

        int score = 0;

        // Killer move heuristic (full from|to move)
        if (ply < PVSearcher.MaxPly)
        {
            int packed = move.From | (move.To << 6);
            if (packed == _killerMoves[ply, 0])
                score = KillerBonus1;
            else if (packed == _killerMoves[ply, 1])
                score = KillerBonus2;
        }

        // History heuristic bonus
        score += Math.Min(_historyTable[move.From, move.To], HistoryScoreCap);
        return score;
    }

    /// <summary>Insertion sort by precomputed move score (n is small and nearly sorted after iteration 1).</summary>
    internal void OrderMoves(SearchBoard board, SearchMove[] moves, int count, Move ttMove, int ply)
    {
        for (int i = 0; i < count; i++)
            _scoreScratch[i] = MoveScore(board, moves[i], ttMove, ply);

        for (int i = 1; i < count; i++)
        {
            var move = moves[i];
            int score = _scoreScratch[i];
            int j = i - 1;
            while (j >= 0 && _scoreScratch[j] < score)
            {
                moves[j + 1] = moves[j];
                _scoreScratch[j + 1] = _scoreScratch[j];
                j--;
            }
            moves[j + 1] = move;
            _scoreScratch[j + 1] = score;
        }
    }

    internal void SortCaptures(SearchBoard board, SearchMove[] moves, int count)
    {
        for (int i = 0; i < count; i++)
            _scoreScratch[i] = CaptureScore(board, moves[i]);

        for (int i = 1; i < count; i++)
        {
            var move = moves[i];
            int score = _scoreScratch[i];
            int j = i - 1;
            while (j >= 0 && _scoreScratch[j] < score)
            {
                moves[j + 1] = moves[j];
                _scoreScratch[j + 1] = _scoreScratch[j];
                j--;
            }
            moves[j + 1] = move;
            _scoreScratch[j + 1] = score;
        }
    }

    private int CaptureScore(SearchBoard board, in SearchMove move)
    {
        int victimRank = move.IsCapture ? SearchBoard.RankOf[move.CapturedId] : 9; // den entries rank first
        int attackerRank = SearchBoard.RankOf[board.Occupant(move.From)];
        int score = victimRank * 10 - attackerRank;
        if (move.IsCapture && _useSee)
            score += SeeCalculator.See(board, move) >= 0 ? SeeSignBonus : -SeeSignBonus;
        return score;
    }

    /// <summary>Records a beta-cutoff killer/history update (quiet moves only).</summary>
    internal void OnBetaCutoff(in SearchMove move, int depth, int ply)
    {
        if (move.IsCapture || ply >= PVSearcher.MaxPly)
            return;
        int packed = move.From | (move.To << 6);
        _killerMoves[ply, 1] = _killerMoves[ply, 0];
        _killerMoves[ply, 0] = packed;
        _historyTable[move.From, move.To] += depth * depth;
    }

    /// <summary>Halve all history scores so recent refutations dominate stale ones.</summary>
    internal void AgeHistoryTable()
    {
        for (int f = 0; f < Zobrist.PositionCount; f++)
            for (int t = 0; t < Zobrist.PositionCount; t++)
                _historyTable[f, t] /= 2;
    }
}
