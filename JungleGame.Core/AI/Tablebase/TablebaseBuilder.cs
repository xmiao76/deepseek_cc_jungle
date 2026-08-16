namespace JungleGame.Core.AI;

/// <summary>
/// Retrograde tablebase construction (forward resolution, after van Rijn &
/// Vis): build the 2-piece tables, then the 3-piece tables layer by layer —
/// moves never increase the piece count, so each combo resolves against
/// already-solved smaller tables plus its own quiet-move siblings. WDL
/// values (1 = loss, 2 = draw, 3 = win; 0 = unresolved) transition
/// monotonically, so the parallel fixpoint sweeps are idempotent and the
/// result is bit-identical regardless of thread count. Positions left
/// unresolved after the fixpoint are draws (the three-fold rule: neither
/// side can force progress). Optional DTM in plies, clamped to 254.
/// </summary>
internal static class TablebaseBuilder
{
    internal sealed class BuildResult
    {
        internal required byte[] Wdl2;
        internal required byte[] Wdl3;
        internal byte[]? Dtm2;
        internal byte[]? Dtm3;
        internal int Sweeps2;
        internal int Sweeps3;
    }

    private const byte Unknown = 0;
    private const byte Loss = 1;
    private const byte Win = 3;

    /// <summary>
    /// Builds only the 2-piece tables (fast — seconds). The unit tests use this;
    /// the full build (2-piece + 3-piece, minutes of compute) runs offline via
    /// Bench --tb-build in Release.
    /// </summary>
    internal static BuildResult Build2Piece(bool includeDtm)
    {
        var result = new BuildResult
        {
            Wdl2 = new byte[TablebaseIndex.Combo2Count * TablebaseIndex.EntriesPerCombo2],
            Wdl3 = Array.Empty<byte>(),
        };
        result.Sweeps2 = Sweep2Piece(result.Wdl2);
        if (includeDtm)
            result.Dtm2 = BuildDtm2(result.Wdl2);
        return result;
    }

    internal static BuildResult Build(bool includeDtm, Action<string>? log = null)
    {
        var result = new BuildResult
        {
            Wdl2 = new byte[TablebaseIndex.Combo2Count * TablebaseIndex.EntriesPerCombo2],
            Wdl3 = new byte[TablebaseIndex.Combo3Count * TablebaseIndex.EntriesPerCombo3],
        };

        result.Sweeps2 = Sweep2Piece(result.Wdl2);
        log?.Invoke($"2-piece fixed point after {result.Sweeps2} sweeps.");
        result.Sweeps3 = Sweep3Piece(result.Wdl3, result.Wdl2);
        log?.Invoke($"3-piece fixed point after {result.Sweeps3} sweeps.");

        if (includeDtm)
        {
            result.Dtm2 = BuildDtm2(result.Wdl2);
            result.Dtm3 = BuildDtm3(result.Wdl3, result.Wdl2, result.Dtm2);
            log?.Invoke("DTM computed.");
        }

        return result;
    }

    // ---- Shared position helpers ----

    /// <summary>
    /// Reconstructs the stored 2-piece position of an entry: the combo's orbit
    /// representative types on the ranked square pair. The entry always holds
    /// the representative orientation — Key2 canonicalizes positions into it.
    /// </summary>
    private static (byte TypeA, byte SqA, byte TypeB, byte SqB) Canonical2(int combo, int placement)
    {
        var (typeA, typeB) = TablebaseIndex.Combo2Types[combo];
        var (sqA, sqB) = Unrank2(placement);
        return (typeA, sqA, typeB, sqB);
    }

    private static (byte TypeA, byte SqA, byte TypeB, byte SqB, byte TypeC, byte SqC)
        Position3(int combo, int placement)
    {
        var (typeA, typeB, typeC) = TablebaseIndex.Combo3Types[combo];
        var (uA, uB, uC) = Unrank3(placement);
        return (typeA, TablebaseIndex.SquareOfUsable[uA],
                typeB, TablebaseIndex.SquareOfUsable[uB],
                typeC, TablebaseIndex.SquareOfUsable[uC]);
    }

    /// <summary>
    /// The 2-piece child position after a capture: the mover's piece (at
    /// move.To) and the remaining piece — the one on neither move.From nor
    /// move.To (which may be the mover's own side when the lone opponent
    /// piece was captured). Ordered blue-first: Key2 and Combo2Of expect the
    /// blue type first.
    /// </summary>
    private static (byte BlueType, byte BlueSq, byte RedType, byte RedSq) Child2AfterCapture(
        byte typeA, byte sqA, byte typeB, byte sqB, byte typeC, byte sqC, in SearchMove move)
    {
        byte moverType = move.From == sqA ? typeA : move.From == sqB ? typeB : typeC;
        byte survivorType, survivorSq;
        if (sqA != move.From && sqA != move.To)
        {
            survivorType = typeA;
            survivorSq = sqA;
        }
        else if (sqB != move.From && sqB != move.To)
        {
            survivorType = typeB;
            survivorSq = sqB;
        }
        else
        {
            survivorType = typeC;
            survivorSq = sqC;
        }

        return (moverType & 1) == 0
            ? (moverType, move.To, survivorType, survivorSq)
            : (survivorType, survivorSq, moverType, move.To);
    }

    // ---- 2-piece ----

    private static int Sweep2Piece(byte[] wdl)
    {
        int sweeps = 0;
        while (true)
        {
            int changes = 0;
            Parallel.For(0, TablebaseIndex.Combo2Count, combo =>
            {
                int local = 0;
                for (int placement = 0; placement < TablebaseIndex.Placements2; placement++)
                {
                    for (int stm = 0; stm < 2; stm++)
                    {
                        int idx = combo * TablebaseIndex.EntriesPerCombo2 + placement * 2 + stm;
                        if (wdl[idx] != Unknown)
                            continue;
                        if (Resolve2Piece(combo, placement, stm, wdl) is byte v)
                        {
                            wdl[idx] = v;
                            local++;
                        }
                    }
                }
                Interlocked.Add(ref changes, local);
            });
            sweeps++;
            if (changes == 0)
                return sweeps;
        }
    }

    private static byte? Resolve2Piece(int combo, int placement, int stm, byte[] wdl)
    {
        var (typeA, sqA, typeB, sqB) = Canonical2(combo, placement);
        Span<SearchMove> moves = stackalloc SearchMove[SearchBoard.MaxMovesPerPly];
        int count = SearchBoard.GenerateMovesCompact(typeA, sqA, typeB, sqB, 255, 0, stm, moves);

        bool anyWinChild = false;
        bool allWinChildren = count > 0;
        for (int i = 0; i < count; i++)
        {
            var move = moves[i];
            if (move.EntersDen || move.IsCapture)
                return Win; // den entry, or capture = elimination

            // Quiet move: same combo, side to move flipped
            int blueSq = move.From == sqA ? move.To : sqA;
            int redSq = move.From == sqB ? move.To : sqB;
            int childIdx = combo * TablebaseIndex.EntriesPerCombo2 +
                TablebaseIndex.Key2(typeA, blueSq, typeB, redSq, stm ^ 1) %
                TablebaseIndex.EntriesPerCombo2;
            byte child = wdl[childIdx];
            if (child == Loss)
                return Win;
            if (child == Win)
                anyWinChild = true;
            else
                allWinChildren = false;
        }

        if (count == 0)
            return Loss; // no legal moves
        if (allWinChildren && anyWinChild)
            return Loss;
        return null; // unresolved → draw once the fixpoint completes
    }

    // ---- 3-piece ----

    private static int Sweep3Piece(byte[] wdl, byte[] wdl2)
    {
        int sweeps = 0;
        while (true)
        {
            int changes = 0;
            Parallel.For(0, TablebaseIndex.Combo3Count, combo =>
            {
                int local = 0;
                for (int placement = 0; placement < TablebaseIndex.Placements3; placement++)
                {
                    for (int stm = 0; stm < 2; stm++)
                    {
                        int idx = combo * TablebaseIndex.EntriesPerCombo3 + placement * 2 + stm;
                        if (wdl[idx] != Unknown)
                            continue;
                        if (Resolve3Piece(combo, placement, stm, wdl, wdl2) is byte v)
                        {
                            wdl[idx] = v;
                            local++;
                        }
                    }
                }
                Interlocked.Add(ref changes, local);
            });
            sweeps++;
            if (changes == 0)
                return sweeps;
        }
    }

    private static byte? Resolve3Piece(int combo, int placement, int stm, byte[] wdl, byte[] wdl2)
    {
        var (typeA, sqA, typeB, sqB, typeC, sqC) = Position3(combo, placement);
        Span<SearchMove> moves = stackalloc SearchMove[SearchBoard.MaxMovesPerPly];
        int count = SearchBoard.GenerateMovesCompact(typeA, sqA, typeB, sqB, typeC, sqC, stm, moves);

        bool anyWinChild = false;
        bool allWinChildren = count > 0;
        for (int i = 0; i < count; i++)
        {
            var move = moves[i];
            if (move.EntersDen)
                return Win;

            if (move.IsCapture)
            {
                var (t1, s1, t2, s2) = Child2AfterCapture(typeA, sqA, typeB, sqB, typeC, sqC, move);
                if ((t1 & 1) == (t2 & 1))
                    return Win; // captured the lone opponent piece: elimination
                int childIdx = TablebaseIndex.Combo2Of[t1, t2] * TablebaseIndex.EntriesPerCombo2 +
                    TablebaseIndex.Key2(t1, s1, t2, s2, stm ^ 1) % TablebaseIndex.EntriesPerCombo2;
                byte child = wdl2[childIdx];
                if (child == Loss)
                    return Win;
                if (child == Win)
                    anyWinChild = true;
                else
                    allWinChildren = false;
            }
            else
            {
                // Quiet move: same combo, side to move flipped
                byte newSqA = move.From == sqA ? move.To : sqA;
                byte newSqB = move.From == sqB ? move.To : sqB;
                byte newSqC = move.From == sqC ? move.To : sqC;
                int childIdx = TablebaseIndex.Key3(combo, newSqA, newSqB, newSqC, stm ^ 1);
                byte child = wdl[childIdx];
                if (child == Loss)
                    return Win;
                if (child == Win)
                    anyWinChild = true;
                else
                    allWinChildren = false;
            }
        }

        if (count == 0)
            return Loss;
        if (allWinChildren && anyWinChild)
            return Loss;
        return null;
    }

    // ---- DTM (after the WDL fixpoint; double-buffered monotone sweeps) ----

    private static byte[] BuildDtm2(byte[] wdl)
    {
        var dtm = new byte[wdl.Length];
        var next = new byte[wdl.Length];
        bool changed = true;
        while (changed)
        {
            changed = false;
            Array.Copy(dtm, next, dtm.Length);
            Parallel.For(0, TablebaseIndex.Combo2Count, combo =>
            {
                for (int placement = 0; placement < TablebaseIndex.Placements2; placement++)
                {
                    for (int stm = 0; stm < 2; stm++)
                    {
                        int idx = combo * TablebaseIndex.EntriesPerCombo2 + placement * 2 + stm;
                        byte v = wdl[idx];
                        if (v != Win && v != Loss)
                            continue;
                        int d = Dtm2Of(combo, placement, stm, v, next);
                        if (d != next[idx])
                        {
                            next[idx] = (byte)d;
                            Volatile.Write(ref changed, true);
                        }
                    }
                }
            });
            (dtm, next) = (next, dtm);
        }
        return dtm;
    }

    private static int Dtm2Of(int combo, int placement, int stm, byte value, byte[] dtm)
    {
        var (typeA, sqA, typeB, sqB) = Canonical2(combo, placement);
        Span<SearchMove> moves = stackalloc SearchMove[SearchBoard.MaxMovesPerPly];
        int count = SearchBoard.GenerateMovesCompact(typeA, sqA, typeB, sqB, 255, 0, stm, moves);

        int best = value == Win ? int.MaxValue : 0;
        for (int i = 0; i < count; i++)
        {
            var move = moves[i];
            if (move.EntersDen || move.IsCapture)
            {
                if (value == Win)
                    return 1;
                continue;
            }
            int blueSq = move.From == sqA ? move.To : sqA;
            int redSq = move.From == sqB ? move.To : sqB;
            int childIdx = combo * TablebaseIndex.EntriesPerCombo2 +
                TablebaseIndex.Key2(typeA, blueSq, typeB, redSq, stm ^ 1) %
                TablebaseIndex.EntriesPerCombo2;
            int d = dtm[childIdx];
            if (d == 0)
                continue; // draw child (or not yet refined)
            if (value == Win)
            {
                if (d + 1 < best)
                    best = d + 1;
            }
            else if (d + 1 > best)
            {
                best = d + 1;
            }
        }

        return best == int.MaxValue ? 254 : Math.Min(best, 254);
    }

    private static byte[] BuildDtm3(byte[] wdl, byte[] wdl2, byte[] dtm2)
    {
        var dtm = new byte[wdl.Length];
        var next = new byte[wdl.Length];
        bool changed = true;
        while (changed)
        {
            changed = false;
            Array.Copy(dtm, next, dtm.Length);
            Parallel.For(0, TablebaseIndex.Combo3Count, combo =>
            {
                for (int placement = 0; placement < TablebaseIndex.Placements3; placement++)
                {
                    for (int stm = 0; stm < 2; stm++)
                    {
                        int idx = combo * TablebaseIndex.EntriesPerCombo3 + placement * 2 + stm;
                        byte v = wdl[idx];
                        if (v != Win && v != Loss)
                            continue;
                        int d = Dtm3Of(combo, placement, stm, v, next, wdl2, dtm2);
                        if (d != next[idx])
                        {
                            next[idx] = (byte)d;
                            Volatile.Write(ref changed, true);
                        }
                    }
                }
            });
            (dtm, next) = (next, dtm);
        }
        return dtm;
    }

    private static int Dtm3Of(int combo, int placement, int stm, byte value, byte[] dtm, byte[] wdl2, byte[] dtm2)
    {
        var (typeA, sqA, typeB, sqB, typeC, sqC) = Position3(combo, placement);
        Span<SearchMove> moves = stackalloc SearchMove[SearchBoard.MaxMovesPerPly];
        int count = SearchBoard.GenerateMovesCompact(typeA, sqA, typeB, sqB, typeC, sqC, stm, moves);

        int best = value == Win ? int.MaxValue : 0;
        for (int i = 0; i < count; i++)
        {
            var move = moves[i];
            if (move.EntersDen)
            {
                if (value == Win)
                    return 1;
                continue;
            }

            int childIdx;
            int d;
            if (move.IsCapture)
            {
                var (t1, s1, t2, s2) = Child2AfterCapture(typeA, sqA, typeB, sqB, typeC, sqC, move);
                if ((t1 & 1) == (t2 & 1))
                {
                    if (value == Win)
                        return 1; // elimination in one ply
                    continue;
                }
                childIdx = TablebaseIndex.Combo2Of[t1, t2] * TablebaseIndex.EntriesPerCombo2 +
                    TablebaseIndex.Key2(t1, s1, t2, s2, stm ^ 1) % TablebaseIndex.EntriesPerCombo2;
                d = dtm2[childIdx];
            }
            else
            {
                byte newSqA = move.From == sqA ? move.To : sqA;
                byte newSqB = move.From == sqB ? move.To : sqB;
                byte newSqC = move.From == sqC ? move.To : sqC;
                childIdx = TablebaseIndex.Key3(combo, newSqA, newSqB, newSqC, stm ^ 1);
                d = dtm[childIdx];
            }

            if (d == 0)
                continue; // draw child (or not yet refined)
            if (value == Win)
            {
                if (d + 1 < best)
                    best = d + 1;
            }
            else if (d + 1 > best)
            {
                best = d + 1;
            }
        }

        return best == int.MaxValue ? 254 : Math.Min(best, 254);
    }

    // ---- Index helpers ----

    internal static (byte SqA, byte SqB) Unrank2(int placement)
    {
        int a = placement / (TablebaseIndex.UsableSquares - 1);
        int rem = placement % (TablebaseIndex.UsableSquares - 1);
        int b = rem < a ? rem : rem + 1;
        return (TablebaseIndex.SquareOfUsable[a], TablebaseIndex.SquareOfUsable[b]);
    }

    internal static (int A, int B, int C) Unrank3(int placement)
    {
        int stride = (TablebaseIndex.UsableSquares - 1) * (TablebaseIndex.UsableSquares - 2);
        int a = placement / stride;
        int rem = placement % stride;
        // Unrank the ordered pair over the 60 remaining values, then shift both
        // past `a` (each value ≥ a sits one higher in the full 61-square set).
        int bb = rem / (TablebaseIndex.UsableSquares - 2);
        int cc = rem % (TablebaseIndex.UsableSquares - 2);
        if (cc >= bb) cc++;
        int b = bb + (bb >= a ? 1 : 0);
        int c = cc + (cc >= a ? 1 : 0);
        return (a, b, c);
    }
}
