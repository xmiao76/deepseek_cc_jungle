namespace JungleGame.Core.AI;

using JungleGame.Core.Models;

/// <summary>
/// Flag indicating the type of score stored in a transposition table entry.
/// </summary>
public enum TranspositionFlag
{
    Exact,     // Exact score from a full search
    LowerBound, // Score is a lower bound (alpha cutoff)
    UpperBound  // Score is an upper bound (beta cutoff)
}

/// <summary>
/// A single entry in the transposition table.
/// </summary>
public struct TranspositionEntry
{
    public ulong Key;
    public int Depth;
    public int Score;
    public TranspositionFlag Flag;
    public Move? BestMove;
}

/// <summary>
/// Fixed-size transposition table using Zobrist hashing for fast position lookups.
/// Uses always-replace strategy (simple and effective for small tables).
/// </summary>
public class TranspositionTable
{
    private readonly TranspositionEntry[] _table;
    private readonly int _size;
    private int _stores;
    private int _hits;

    public int StoreCount => _stores;
    public int HitCount => _hits;
    public double HitRate => _stores > 0 ? (double)_hits / _stores : 0;

    /// <summary>
    /// Creates a transposition table with the specified number of entries (power of 2 recommended).
    /// </summary>
    public TranspositionTable(int size = 1 << 20) // Default: ~1M entries (~16MB)
    {
        _size = size;
        _table = new TranspositionEntry[size];
    }

    /// <summary>
    /// Store a search result in the transposition table.
    /// </summary>
    public void Store(ulong key, int depth, int score, TranspositionFlag flag, Move? bestMove)
    {
        int index = (int)(key & (ulong)(_size - 1));
        _table[index] = new TranspositionEntry
        {
            Key = key,
            Depth = depth,
            Score = score,
            Flag = flag,
            BestMove = bestMove
        };
        _stores++;
    }

    /// <summary>
    /// Try to retrieve a previously stored search result.
    /// Returns true if a usable entry was found.
    /// </summary>
    public bool TryLookup(ulong key, int depth, int alpha, int beta, out int score, out Move? bestMove)
    {
        int index = (int)(key & (ulong)(_size - 1));
        var entry = _table[index];

        if (entry.Key == key && entry.Depth >= depth)
        {
            _hits++;
            bestMove = entry.BestMove;

            switch (entry.Flag)
            {
                case TranspositionFlag.Exact:
                    score = entry.Score;
                    return true;

                case TranspositionFlag.LowerBound:
                    if (entry.Score >= beta)
                    {
                        score = entry.Score;
                        return true;
                    }
                    break;

                case TranspositionFlag.UpperBound:
                    if (entry.Score <= alpha)
                    {
                        score = entry.Score;
                        return true;
                    }
                    break;
            }
        }

        score = 0;
        bestMove = null;
        return false;
    }

    /// <summary>
    /// Clear all entries.
    /// </summary>
    public void Clear()
    {
        Array.Clear(_table, 0, _size);
        _stores = 0;
        _hits = 0;
    }
}
