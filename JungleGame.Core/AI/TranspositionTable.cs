using JungleGame.Core.Engine;
using JungleGame.Core.Model;

namespace JungleGame.Core.AI;

public enum BoundType : byte
{
    Exact,
    LowerBound,
    UpperBound
}

public readonly struct TTEntry
{
    public ulong Hash { get; }
    public int Depth { get; }
    public int Score { get; }
    public Move BestMove { get; }
    public BoundType Bound { get; }

    public TTEntry(ulong hash, int depth, int score, Move bestMove, BoundType bound)
    {
        Hash = hash;
        Depth = depth;
        Score = score;
        BestMove = bestMove;
        Bound = bound;
    }
}

public class TranspositionTable
{
    /// <summary>
    /// Mate-score convention: scores at or beyond ±(MateScore - MateRange) are mate
    /// scores. They are stored node-relative ("mate in k plies from this position")
    /// and converted to root-relative on probe, using the probe node's ply.
    /// </summary>
    public const int MateScore = 1_000_000;
    public const int MateRange = 500; // search ply (64 + qsearch 32) is far below this

    private readonly TTEntry[] _table;
    private readonly int _size;

    public TranspositionTable(int size = 1 << 20) // 1M entries default
    {
        _size = size;
        _table = new TTEntry[_size];
    }

    public static ulong ComputeHash(GameState state) => Zobrist.ComputeHash(state);

    public void Store(ulong hash, int depth, int score, Move bestMove, BoundType bound)
    {
        int idx = (int)(hash % (ulong)_size);
        _table[idx] = new TTEntry(hash, depth, score, bestMove, bound);
    }

    public bool TryProbe(ulong hash, int depth, int alpha, int beta, int ply, out int score, out Move bestMove)
    {
        int idx = (int)(hash % (ulong)_size);
        var entry = _table[idx];

        score = 0;
        bestMove = default;

        if (entry.Hash != hash)
            return false;

        bestMove = entry.BestMove;

        if (entry.Depth < depth)
            return false;

        // Convert stored node-relative mate scores to root-relative before the
        // bound comparisons, so cutoffs decide on the true value.
        int s = entry.Score;
        if (s > MateScore - MateRange)
            s -= ply;
        else if (s < -(MateScore - MateRange))
            s += ply;

        switch (entry.Bound)
        {
            case BoundType.Exact:
                score = s;
                return true;
            case BoundType.LowerBound:
                if (s >= beta)
                {
                    score = s;
                    return true;
                }
                break;
            case BoundType.UpperBound:
                if (s <= alpha)
                {
                    score = s;
                    return true;
                }
                break;
        }

        return false;
    }
}
