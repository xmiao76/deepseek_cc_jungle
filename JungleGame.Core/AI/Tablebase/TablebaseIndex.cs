namespace JungleGame.Core.AI;

/// <summary>
/// Indexing for the endgame tables. Positions never contain a piece on a den
/// (den entry ends the game), so pieces occupy 61 usable squares. The 180°
/// board rotation + color swap is an exact symmetry: every position is
/// canonicalized into its 2-vs-1 orientation (Blue holds the pair), which
/// halves the stored combos — a 1-vs-2 probe rotates into the 2-vs-1 form.
/// The layout is parameterized by piece count (2-piece tables rank ordered
/// pairs; the 4-piece seam adds a 4-square ranker later).
/// </summary>
internal static class TablebaseIndex
{
    internal const int UsableSquares = 61;
    internal const int SquareCount = 63;
    internal const int PieceTypeCount = 16; // (animal-1)*2 + owner

    // Piece-type index: (animal-1)*2 + owner (matches SearchBoard/Zobrist).
    internal static int TypeOf(byte pieceId) => (pieceId - 1) % PieceTypeCount;

    /// <summary>Board square (row*7+col) → usable index (0..60), or -1 for a den.</summary>
    internal static readonly sbyte[] UsableOf = new sbyte[SquareCount];
    internal static readonly byte[] SquareOfUsable = new byte[UsableSquares];

    /// <summary>2-piece combo orbits: 36 = (8 self + 56/2) rotation orbits of (blue, red) type pairs.</summary>
    internal const int Combo2Count = 36;
    internal const int Placements2 = UsableSquares * (UsableSquares - 1); // 3660 ordered
    internal const int EntriesPerCombo2 = Placements2 * 2;                // × side to move

    /// <summary>
    /// 3-piece combos in the 2v1 orientation: 36 blue pairs × 8 red types =
    /// 288. (The 180° rotation is already folded in by forcing the 2v1
    /// orientation: a 1v2 position probes its rotated 2v1 form — same combo.)
    /// </summary>
    internal const int Combo3Count = 288;
    internal const int Placements3 = UsableSquares * (UsableSquares - 1) * (UsableSquares - 2); // 215,940
    internal const int EntriesPerCombo3 = Placements3 * 2;                                        // × side to move

    /// <summary>combo2[blueType, redType]: orbit id of the (blue, red) type pair, or -1.</summary>
    internal static readonly sbyte[,] Combo2Of = new sbyte[PieceTypeCount, PieceTypeCount];

    /// <summary>Inverse: combo id → the orbit representative (blueType, redType).</summary>
    internal static readonly (byte Blue, byte Red)[] Combo2Types = new (byte, byte)[Combo2Count];

    /// <summary>
    /// combo3[a][b][c]: 2v1 combo id for blue pair (a ≤ b) and red type c, or
    /// -1. (short — 288 ids do not fit in an sbyte.)
    /// </summary>
    internal static readonly short[,,] Combo3Of = new short[PieceTypeCount, PieceTypeCount, PieceTypeCount];

    /// <summary>Inverse: combo id → (a, b, c) type triple for the 2v1 orientation.</summary>
    internal static readonly (byte A, byte B, byte C)[] Combo3Types = new (byte, byte, byte)[Combo3Count];

    static TablebaseIndex()
    {
        // Usable squares: all except the two dens (3,0) and (3,8).
        Array.Fill(UsableOf, (sbyte)-1);
        int usable = 0;
        for (int sq = 0; sq < SquareCount; sq++)
        {
            if (SearchBoard.TerrainOf[sq] == 4 || SearchBoard.TerrainOf[sq] == 5) // DenBlue/DenRed
                continue;
            UsableOf[sq] = (sbyte)usable;
            SquareOfUsable[usable] = (byte)sq;
            usable++;
        }

        // 2-piece combo orbits: (blue, red) type pairs under rotation. The
        // orbit folds (x, y) with its rotated pair (y^1, x^1); the
        // representative is the lexicographically smaller member. Blue types
        // are even (owner bit 0), red types odd.
        for (int a = 0; a < PieceTypeCount; a++)
            for (int b = 0; b < PieceTypeCount; b++)
                Combo2Of[a, b] = -1;
        int combo2 = 0;
        for (int x = 0; x < PieceTypeCount; x += 2)
        {
            for (int y = 1; y < PieceTypeCount; y += 2)
            {
                if (Combo2Of[x, y] >= 0)
                    continue;
                // Representative: min((x, y), (y^1, x^1))
                bool rotatedSmaller = (y ^ 1) < x || ((y ^ 1) == x && (x ^ 1) < y);
                byte repX = rotatedSmaller ? (byte)(y ^ 1) : (byte)x;
                byte repY = rotatedSmaller ? (byte)(x ^ 1) : (byte)y;
                Combo2Of[x, y] = (sbyte)combo2;
                Combo2Of[y ^ 1, x ^ 1] = (sbyte)combo2;
                Combo2Types[combo2] = (repX, repY);
                combo2++;
            }
        }

        // 3-piece combos: the 2v1 orientation only (blue pair a ≤ b even, red
        // type c odd). 1v2 positions rotate into this form at probe time.
        for (int a = 0; a < PieceTypeCount; a++)
            for (int b = 0; b < PieceTypeCount; b++)
                for (int c = 0; c < PieceTypeCount; c++)
                    Combo3Of[a, b, c] = -1;
        int combo3 = 0;
        for (int a = 0; a < PieceTypeCount; a += 2)
        {
            for (int b = a; b < PieceTypeCount; b += 2)
            {
                for (int c = 1; c < PieceTypeCount; c += 2)
                {
                    Combo3Of[a, b, c] = (short)combo3;
                    Combo3Types[combo3] = ((byte)a, (byte)b, (byte)c);
                    combo3++;
                }
            }
        }
    }

    /// <summary>Rotates a board square by 180° (usable squares map to usable squares).</summary>
    internal static int RotateSquare(int sq) =>
        (8 - sq / 7) * 7 + (6 - sq % 7);

    /// <summary>
    /// Entry index of a 2-piece position (typeA = blue type, at sqA; typeB =
    /// red type, at sqB). The orbit representative is the lexicographically
    /// smaller of (typeA, typeB) and its rotated pair (typeB^1, typeA^1); when
    /// the rotated pair is the representative, the position is stored in the
    /// rotated orientation (squares rotated, side to move flipped). Self-orbits
    /// (typeA == typeB^1, e.g. cat-vs-cat) tie on types and canonicalize on the
    /// square pair instead — the color swap leaves the types invariant there,
    /// so the entry's types are the same in either orientation.
    /// </summary>
    internal static int Key2(int typeA, int sqA, int typeB, int sqB, int stm)
    {
        bool selfOrbit = (typeB ^ 1) == typeA;
        if (selfOrbit)
        {
            int uA = UsableOf[sqA];
            int uB = UsableOf[sqB];
            int rB = UsableOf[RotateSquare(sqB)];
            int rA = UsableOf[RotateSquare(sqA)];
            bool useRotated = rB < uA || (rB == uA && rA < uB);
            if (useRotated)
                return Rank2(rB, rA) * 2 + (stm ^ 1);
            return Rank2(uA, uB) * 2 + stm;
        }

        bool rotatedSmaller = (typeB ^ 1) < typeA || ((typeB ^ 1) == typeA && (typeA ^ 1) < typeB);
        if (!rotatedSmaller)
            return Rank2(UsableOf[sqA], UsableOf[sqB]) * 2 + stm;

        // Rotated orientation: the original red piece becomes blue at
        // rotate(sqB); the original blue piece becomes red at rotate(sqA).
        int blue = UsableOf[RotateSquare(sqB)];
        int red = UsableOf[RotateSquare(sqA)];
        return Rank2(blue, red) * 2 + (stm ^ 1);
    }

    /// <summary>Rank of an ordered pair of distinct usable squares: 0..Placements2-1.</summary>
    internal static int Rank2(int a, int b) => a * (UsableSquares - 1) + (b < a ? b : b - 1);

    /// <summary>Rank of an ordered triple of distinct usable squares: 0..Placements3-1.</summary>
    internal static int Rank3(int a, int b, int c)
    {
        int r = a * (UsableSquares - 1) * (UsableSquares - 2);
        // Rank (b,c) among ordered pairs from the remaining 60 squares
        // (a 60×59 pair space — not the 61×60 space of Rank2).
        int bb = b < a ? b : b - 1;
        int cc = c < a ? c : c - 1;
        r += bb * (UsableSquares - 2) + (cc < bb ? cc : cc - 1);
        return r;
    }

    /// <summary>
    /// Entry index of a 3-piece position in the 2v1 orientation: combo orbit id
    /// × EntriesPerCombo3 + rank3(sqA, sqB, sqC) × 2 + stm, where sqA/sqB are
    /// the blue pair's squares (types a ≤ b) and sqC the red type's square.
    /// </summary>
    internal static int Key3(int combo, int sqA, int sqB, int sqC, int stm)
    {
        int uA = UsableOf[sqA];
        int uB = UsableOf[sqB];
        int uC = UsableOf[sqC];
        return combo * EntriesPerCombo3 + Rank3(uA, uB, uC) * 2 + stm;
    }
}
