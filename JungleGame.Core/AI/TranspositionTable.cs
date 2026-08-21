using JungleGame.Core.Engine;
using JungleGame.Core.Model;

namespace JungleGame.Core.AI;

public enum BoundType : byte
{
    Exact,
    LowerBound,
    UpperBound
}

/// <summary>
/// A packed transposition-table entry (24 bytes: 8 hash + 4 move + 4 score +
/// 1 depth + 1 bound + 1 generation + padding). The best move packs the two
/// squares (6 bits each) and the captured piece id (6 bits, 0 = none) into a
/// uint; depth is a byte (searches never exceed 127).
/// </summary>
internal readonly struct TTEntry
{
    internal readonly ulong Hash;
    internal readonly uint Move;
    internal readonly int Score;
    internal readonly byte Depth;
    internal readonly byte Bound;
    internal readonly byte Generation;

    internal TTEntry(ulong hash, byte generation, int depth, int score, Move bestMove, BoundType bound)
    {
        Hash = hash;
        Generation = generation;
        Depth = (byte)Math.Min(depth, 255);
        Score = score;
        Bound = (byte)bound;
        Move = PackMove(bestMove);
    }

    internal Move BestMove => UnpackMove(Move);

    internal static uint PackMove(Move move)
    {
        int from = move.From.Row * 7 + move.From.Col;
        int to = move.To.Row * 7 + move.To.Col;
        int capturedId = move.Captured == null
            ? 0
            : ((int)move.Captured.Value.Animal - 1) * 2 + (int)move.Captured.Value.Owner + 1;
        return (uint)(from | (to << 6) | (capturedId << 12));
    }

    internal static Move UnpackMove(uint packed)
    {
        int from = (int)(packed & 63);
        int to = (int)((packed >> 6) & 63);
        int capturedId = (int)((packed >> 12) & 63);
        var fromPos = new Position(from % 7, from / 7);
        var toPos = new Position(to % 7, to / 7);
        Piece? captured = capturedId == 0
            ? null
            : new Piece(
                (Animal)((((capturedId - 1) % SearchBoard.DistinctPieceKinds) >> 1) + 1),
                (Player)(((capturedId - 1) % SearchBoard.DistinctPieceKinds) & 1),
                toPos);
        return new Move(fromPos, toPos, captured);
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

    /// <summary>
    /// Each hash slot holds BucketCount entries with depth-preferred replacement
    /// (a shallow entry never evicts a deeper one of the same generation).
    /// Entries are generation-tagged: probes only accept the current or the
    /// previous search generation, and stale entries are the first replacement
    /// candidates. This bounds the lifetime of window-dependent bounds — narrow
    /// aspiration windows produce bound entries that must not outlive the search
    /// that produced them (an older entry is a valid bound, but its bound is too
    /// weak to steer a later search; keeping it is strictly worse than a fresh,
    /// wider-bound entry). An entry with Depth == 0 is empty (PVSearch never
    /// stores at depth 0).
    /// </summary>
    private const int BucketCount = 4;
    private const byte MaxGenerationLag = 1; // accept the previous search's entries

    private readonly TTEntry[] _table;
    private readonly int _slotCount;
    private byte _generation;

    public TranspositionTable(int size = 1 << 20) // 1M entries default
    {
        // Round the entry count down to whole buckets (same 1M entries as before:
        // 262,144 slots × 4)
        _slotCount = Math.Max(1, size / BucketCount);
        _table = new TTEntry[_slotCount * BucketCount];
    }

    public static ulong ComputeHash(GameState state) => Zobrist.ComputeHash(state);

    /// <summary>Starts a new search generation (called once per FindBestMove).</summary>
    public void NewGeneration() => _generation++;

    private static bool IsFresh(byte entryGeneration, byte currentGeneration) =>
        currentGeneration - entryGeneration <= MaxGenerationLag;

    public void Store(ulong hash, int depth, int score, Move bestMove, BoundType bound)
    {
        int baseIdx = (int)(hash % (ulong)_slotCount) * BucketCount;

        // Same hash: update in place (keeps the newest information for the position)
        for (int i = 0; i < BucketCount; i++)
        {
            if (_table[baseIdx + i].Hash == hash)
            {
                _table[baseIdx + i] = new TTEntry(hash, _generation, depth, score, bestMove, bound);
                return;
            }
        }

        // Stale entries (older than the accepted probe window) go first
        for (int i = 0; i < BucketCount; i++)
        {
            if (!IsFresh(_table[baseIdx + i].Generation, _generation))
            {
                _table[baseIdx + i] = new TTEntry(hash, _generation, depth, score, bestMove, bound);
                return;
            }
        }

        // Depth-preferred among fresh entries: never overwrite a deeper entry
        // with a shallower one
        int shallowest = 0;
        for (int i = 1; i < BucketCount; i++)
        {
            if (_table[baseIdx + i].Depth < _table[baseIdx + shallowest].Depth)
                shallowest = i;
        }
        if (depth >= _table[baseIdx + shallowest].Depth)
            _table[baseIdx + shallowest] = new TTEntry(hash, _generation, depth, score, bestMove, bound);
    }

    /// <summary>
    /// The stored best move for a fresh (current or previous generation) entry
    /// matching the hash, without bound semantics — used to extract the engine's
    /// predicted reply for pondering. Null when nothing usable is stored.
    /// </summary>
    internal Move? GetBestMove(ulong hash)
    {
        int baseIdx = (int)(hash % (ulong)_slotCount) * BucketCount;
        for (int i = 0; i < BucketCount; i++)
        {
            var entry = _table[baseIdx + i];
            if (entry.Hash == hash && IsFresh(entry.Generation, _generation))
                return entry.BestMove;
        }
        return null;
    }

    public bool TryProbe(ulong hash, int depth, int alpha, int beta, int ply, out int score, out Move bestMove)
    {
        int baseIdx = (int)(hash % (ulong)_slotCount) * BucketCount;

        score = 0;
        bestMove = default;

        for (int i = 0; i < BucketCount; i++)
        {
            var entry = _table[baseIdx + i];
            if (entry.Hash != hash)
                continue;

            bestMove = entry.BestMove;

            // Entries from searches before the previous one are treated as absent:
            // their bounds were computed under unrelated windows and histories
            if (!IsFresh(entry.Generation, _generation) || entry.Depth < depth)
                return false;

            // Convert stored node-relative mate scores to root-relative before the
            // bound comparisons, so cutoffs decide on the true value.
            int s = entry.Score;
            if (s > MateScore - MateRange)
                s -= ply;
            else if (s < -(MateScore - MateRange))
                s += ply;

            switch ((BoundType)entry.Bound)
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

        return false;
    }
}
