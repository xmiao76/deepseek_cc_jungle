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
    private readonly TTEntry[] _table;
    private readonly int _size;

    // Zobrist keys
    private static readonly ulong[,] PieceKeys;
    private static readonly ulong TurnKey;
    private static readonly Random _rng;
    private const int PositionCount = 63; // 7×9

    static TranspositionTable()
    {
        _rng = new Random(42); // Fixed seed for reproducibility
        PieceKeys = new ulong[PositionCount, 16]; // 8 animals × 2 owners

        for (int p = 0; p < PositionCount; p++)
            for (int i = 0; i < 16; i++)
                PieceKeys[p, i] = NextULong();

        TurnKey = NextULong();
    }

    private static ulong NextULong()
    {
        byte[] buf = new byte[8];
        _rng.NextBytes(buf);
        return BitConverter.ToUInt64(buf, 0);
    }

    public TranspositionTable(int size = 1 << 20) // 1M entries default
    {
        _size = size;
        _table = new TTEntry[_size];
    }

    private static int PieceIndex(Piece piece) => ((int)piece.Animal - 1) * 2 + (int)piece.Owner;
    private static int PositionIndex(Position pos) => pos.Row * 7 + pos.Col;

    public static ulong ComputeHash(GameState state)
    {
        ulong hash = 0;

        foreach (var kv in state.Pieces)
        {
            int posIdx = PositionIndex(kv.Key);
            int pieceIdx = PieceIndex(kv.Value);
            hash ^= PieceKeys[posIdx, pieceIdx];
        }

        hash ^= TurnKey; // Include side to move

        return hash;
    }

    public void Store(ulong hash, int depth, int score, Move bestMove, BoundType bound)
    {
        int idx = (int)(hash % (ulong)_size);
        _table[idx] = new TTEntry(hash, depth, score, bestMove, bound);
    }

    public bool TryProbe(ulong hash, int depth, int alpha, int beta, out int score, out Move bestMove)
    {
        int idx = (int)(hash % (ulong)_size);
        var entry = _table[idx];

        score = 0;
        bestMove = default;

        if (entry.Hash != hash)
            return false;

        bestMove = entry.BestMove;

        if (entry.Depth >= depth)
        {
            switch (entry.Bound)
            {
                case BoundType.Exact:
                    score = entry.Score;
                    return true;
                case BoundType.LowerBound:
                    if (entry.Score >= beta)
                    {
                        score = entry.Score;
                        return true;
                    }
                    break;
                case BoundType.UpperBound:
                    if (entry.Score <= alpha)
                    {
                        score = entry.Score;
                        return true;
                    }
                    break;
            }
        }

        return false;
    }
}
