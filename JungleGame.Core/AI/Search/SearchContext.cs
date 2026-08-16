using System.Diagnostics;
using JungleGame.Core.Model;

namespace JungleGame.Core.AI;

/// <summary>
/// Per-search scratch state: the pooled node boards, per-ply move buffers, and
/// the repetition path. The path is an open-addressing hash→count table with
/// linear probing — it replaces the previous O(path length) linear scan that
/// ran at every node. Push/pop mirror the old path-array sites exactly; a
/// position is a three-fold repetition when its count reaches 2 (two prior
/// occurrences). Entries are removed (backward-shift deletion) when their
/// count returns to 0 — a search pushes millions of distinct positions, so
/// stale slots must be reused. At most 256 hashes are live at once in 512
/// slots; the table is cleared once per search.
/// </summary>
internal sealed class SearchContext
{
    internal const int PathCapacity = 256;
    private const int TableSlots = 512;

    private readonly ulong[] _hashes = new ulong[TableSlots];
    private readonly byte[] _counts = new byte[TableSlots];

    internal readonly Stack<SearchBoard> BoardPool = new();
    internal readonly SearchMove[][] PlyMoves;
    internal readonly SearchMove[][] QMoves;
    internal readonly SearchMove[] RootMoves = new SearchMove[SearchBoard.MaxMovesPerPly];

    /// <summary>Total entries on the path (history + search), mirroring the old _pathLen.</summary>
    internal int PathLength { get; private set; }

    internal SearchContext()
    {
        PlyMoves = new SearchMove[PVSearcher.MaxPly][];
        QMoves = new SearchMove[PVSearcher.MaxPly][];
        for (int i = 0; i < PVSearcher.MaxPly; i++)
        {
            PlyMoves[i] = new SearchMove[SearchBoard.MaxMovesPerPly];
            QMoves[i] = new SearchMove[SearchBoard.MaxMovesPerPly];
        }
    }

    internal void Reset()
    {
        Array.Clear(_hashes, 0, _hashes.Length);
        Array.Clear(_counts, 0, _counts.Length);
        PathLength = 0;
    }

    /// <summary>
    /// Seeds the path with the real game's history, reserving headroom for the
    /// search path itself (long shuffling games can approach the cap).
    /// </summary>
    internal void SeedPath(GameState state)
    {
        int seed = Math.Min(state.History.Count, PathCapacity - PVSearcher.MaxPly);
        for (int i = 0; i < seed; i++)
            Push(state.History[i]);
    }

    internal bool CanPush => PathLength < PathCapacity;

    internal void Push(ulong hash)
    {
        Debug.Assert(CanPush, "Path capacity exceeded");
        int idx = Find(hash);
        if (_counts[idx] == 0)
            _hashes[idx] = hash;
        _counts[idx]++;
        PathLength++;
    }

    internal void Pop(ulong hash)
    {
        int idx = Find(hash);
        Debug.Assert(_counts[idx] > 0, "Pop without matching push");
        PathLength--;

        if (--_counts[idx] == 0)
        {
            // Free the slot. A search pushes millions of distinct positions, so
            // count-0 slots must be reusable (retaining their stale hash would
            // eventually fill the table and make probes never terminate).
            // Backward-shift deletion keeps linear-probe chains intact.
            _hashes[idx] = 0;
            int next = (idx + 1) & (TableSlots - 1);
            while (_hashes[next] != 0)
            {
                ulong h = _hashes[next];
                byte c = _counts[next];
                _hashes[next] = 0;
                _counts[next] = 0;
                int pos = (int)(h & (TableSlots - 1));
                while (_hashes[pos] != 0)
                    pos = (pos + 1) & (TableSlots - 1);
                _hashes[pos] = h;
                _counts[pos] = c;
                next = (next + 1) & (TableSlots - 1);
            }
        }
    }

    /// <summary>True when the hash is the third occurrence of a position on the path.</summary>
    internal bool IsRepetition(ulong hash)
    {
        int idx = Find(hash);
        return _counts[idx] >= 2;
    }

    internal int CountOf(ulong hash)
    {
        int idx = Find(hash);
        return _counts[idx];
    }

    private int Find(ulong hash)
    {
        int idx = (int)(hash & (TableSlots - 1));
        while (_hashes[idx] != 0 && _hashes[idx] != hash)
            idx = (idx + 1) & (TableSlots - 1);
        return idx;
    }

    internal SearchBoard GetBoard() => BoardPool.Count > 0 ? BoardPool.Pop() : new SearchBoard();

    internal void ReleaseBoard(SearchBoard board) => BoardPool.Push(board);
}
