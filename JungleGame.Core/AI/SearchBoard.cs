using System.Collections.Immutable;
using System.Diagnostics;
using JungleGame.Core.Model;

namespace JungleGame.Core.AI;

/// <summary>A packed search move. Squares are indices 0..62 (row*7+col).</summary>
internal readonly struct SearchMove
{
    public readonly byte From;
    public readonly byte To;
    public readonly byte CapturedId; // 0 = no capture
    public readonly bool EntersDen;

    public SearchMove(byte from, byte to, byte capturedId = 0, bool entersDen = false)
    {
        From = from;
        To = to;
        CapturedId = capturedId;
        EntersDen = entersDen;
    }

    public bool IsCapture => CapturedId != 0;
}

/// <summary>
/// Mutable internal board used only by the search engine. Piece ids 1..32 encode
/// (animal, owner) pairs: ids 1..16 are the first copy of each pair, ids 17..32 the
/// second copy (positions constructed by tests can contain duplicate animals per
/// side). The Zobrist piece index is (id-1) % 16, so both copies hash identically.
/// Nodes are produced by copying this ~120-byte state (CopyTo/Clone) and applying
/// one move; all per-node arrays are reused from a pool owned by the engine.
/// </summary>
internal sealed class SearchBoard
{
    internal const int SquareCount = 63;
    internal const byte NoWinner = 255;
    internal const int DistinctPieceKinds = 16; // 8 animals × 2 owners
    internal const int MaxPieceIds = 32;        // up to 2 copies of each kind
    internal const int MaxMovesPerPly = 128;    // 16 pieces × (4 steps + 2 jumps) = 96 max

    // Terrain codes
    private const byte Land = 0;
    private const byte Water = 1;
    private const byte TrapBlue = 2;
    private const byte TrapRed = 3;
    private const byte DenBlue = 4;
    private const byte DenRed = 5;

    /// <summary>Jump spec: target square plus the water squares crossed (max 3 on this board).</summary>
    private readonly struct Jump
    {
        public readonly byte Target;
        public readonly byte Mid0;
        public readonly byte Mid1;
        public readonly byte Mid2;
        public readonly byte MidCount;

        public Jump(byte target, byte m0, byte m1, byte m2, byte midCount)
        {
            Target = target;
            Mid0 = m0;
            Mid1 = m1;
            Mid2 = m2;
            MidCount = midCount;
        }

        public bool IsBlockedByRat(SearchBoard board)
        {
            if (MidCount >= 1 && IsRat(board._squareIds[Mid0])) return true;
            if (MidCount >= 2 && IsRat(board._squareIds[Mid1])) return true;
            if (MidCount >= 3 && IsRat(board._squareIds[Mid2])) return true;
            return false;
        }
    }

    /// <summary>Jump spec in reverse: a square that jumps onto this square, with the mids crossed.</summary>
    internal readonly struct JumpAttack
    {
        internal readonly byte From;
        internal readonly byte Mid0;
        internal readonly byte Mid1;
        internal readonly byte Mid2;
        internal readonly byte MidCount;

        internal JumpAttack(byte from, byte m0, byte m1, byte m2, byte midCount)
        {
            From = from;
            Mid0 = m0;
            Mid1 = m1;
            Mid2 = m2;
            MidCount = midCount;
        }

        internal bool IsBlockedByRat(SearchBoard board) =>
            (MidCount >= 1 && IsRat(board._squareIds[Mid0])) ||
            (MidCount >= 2 && IsRat(board._squareIds[Mid1])) ||
            (MidCount >= 3 && IsRat(board._squareIds[Mid2]));
    }

    // ---- Precomputed static tables (built once from Board.Initial) ----
    internal static readonly byte[] TerrainOf = new byte[SquareCount];
    internal static readonly byte[] RankOf = new byte[MaxPieceIds + 1]; // piece id → rank 1..8
    internal static readonly byte[][] Neighbors = new byte[SquareCount][];
    /// <summary>All jump landings onto each square (used by the SEE attacker scan).</summary>
    internal static readonly JumpAttack[][] JumpAttackersTo = new JumpAttack[SquareCount][];
    private static readonly Jump[][] LionJumpsOf = new Jump[SquareCount][];
    private static readonly Jump[][] TigerJumpsOf = new Jump[SquareCount][];

    static SearchBoard()
    {
        for (int r = 0; r < 9; r++)
        {
            for (int c = 0; c < 7; c++)
            {
                int sq = r * 7 + c;
                TerrainOf[sq] = Board.Initial.GetTerrain(new Position(c, r)) switch
                {
                    Terrain.River => Water,
                    Terrain.TrapBlue => TrapBlue,
                    Terrain.TrapRed => TrapRed,
                    Terrain.DenBlue => DenBlue,
                    Terrain.DenRed => DenRed,
                    _ => Land
                };
            }
        }

        for (int copy = 0; copy < 2; copy++)
        {
            for (int i = 1; i <= 8; i++)
            {
                RankOf[copy * DistinctPieceKinds + ((i - 1) * 2) + 1] = (byte)i; // Blue piece of each animal
                RankOf[copy * DistinctPieceKinds + ((i - 1) * 2) + 2] = (byte)i; // Red piece of each animal
            }
        }

        for (int sq = 0; sq < SquareCount; sq++)
        {
            int r = sq / 7;
            int c = sq % 7;

            var neighbors = new List<byte>(4);
            AddIfValid(neighbors, c, r - 1);
            AddIfValid(neighbors, c, r + 1);
            AddIfValid(neighbors, c - 1, r);
            AddIfValid(neighbors, c + 1, r);
            Neighbors[sq] = neighbors.ToArray();

            LionJumpsOf[sq] = BuildJumps(c, r, includeRowChange: true);
            TigerJumpsOf[sq] = BuildJumps(c, r, includeRowChange: false);
        }

        // Reverse jump table: for every square, the jumpers that can land on it
        // (Lion covers both axes, so it alone enumerates every jump landing).
        var attackersTo = new List<JumpAttack>[SquareCount];
        for (int sq = 0; sq < SquareCount; sq++)
            attackersTo[sq] = new List<JumpAttack>(4);
        for (int sq = 0; sq < SquareCount; sq++)
        {
            foreach (var jump in LionJumpsOf[sq])
                attackersTo[jump.Target].Add(new JumpAttack((byte)sq, jump.Mid0, jump.Mid1, jump.Mid2, jump.MidCount));
        }
        for (int sq = 0; sq < SquareCount; sq++)
            JumpAttackersTo[sq] = attackersTo[sq].ToArray();

        static void AddIfValid(List<byte> list, int c, int r)
        {
            if (c >= 0 && c <= 6 && r >= 0 && r <= 8)
                list.Add((byte)(r * 7 + c));
        }
    }

    /// <summary>
    /// All jump targets from (c,r) whose entire path is water and whose landing is
    /// land, per the MoveValidator rules. Row-changing jumps cross the 3-tall river
    /// (3 mids); column-changing jumps cross the 2-wide river (2 mids). Any other
    /// path would include land, so at most 3 mids are ever needed.
    /// Distances are scanned from 8 down to 2 and the first valid crossing wins;
    /// on this terrain exactly one distance per direction can be valid (the water
    /// runs are 2 wide / 3 tall), so this equals MoveValidator.GetJumpWaterSquares.
    /// </summary>
    private static Jump[] BuildJumps(int c, int r, bool includeRowChange)
    {
        var jumps = new List<Jump>(4);
        TryJump(c, r, 0, -1, includeRowChange); // up (row change)
        TryJump(c, r, 0, 1, includeRowChange);  // down (row change)
        TryJump(c, r, -1, 0, true);             // left (col change)
        TryJump(c, r, 1, 0, true);              // right (col change)
        return jumps.ToArray();

        void TryJump(int col, int row, int dc, int dr, bool allowed)
        {
            if (!allowed)
                return;

            for (int dist = 8; dist >= 2; dist--)
            {
                int tc = col + dc * dist;
                int tr = row + dr * dist;
                if (tc < 0 || tc > 6 || tr < 0 || tr > 8)
                    continue;

                var mids = new List<byte>(3);
                bool allWater = true;
                for (int step = 1; step < dist; step++)
                {
                    int mc = col + dc * step;
                    int mr = row + dr * step;
                    int msq = mr * 7 + mc;
                    if (TerrainOf[msq] != Water)
                    {
                        allWater = false;
                        break;
                    }
                    mids.Add((byte)msq);
                }

                int target = tr * 7 + tc;
                if (!allWater || TerrainOf[target] == Water)
                    continue;

                Debug.Assert(mids.Count <= 3, "Jump paths on this board cross at most 3 water squares");
                if (mids.Count > 3)
                    continue;

                jumps.Add(new Jump(
                    (byte)target,
                    mids.Count >= 1 ? mids[0] : (byte)0,
                    mids.Count >= 2 ? mids[1] : (byte)0,
                    mids.Count >= 3 ? mids[2] : (byte)0,
                    (byte)mids.Count));
                return; // Exactly one valid crossing exists per direction on this terrain
            }
        }
    }

    // ---- Instance state ----
    private readonly byte[] _squareIds = new byte[SquareCount]; // 0 = empty, else piece id
    private readonly byte[] _posOf = new byte[MaxPieceIds + 1]; // piece id → square (255 = captured)
    private readonly byte[][] _ids = { new byte[16], new byte[16] };
    private readonly byte[] _count = new byte[2];
    private byte _turn;                 // 0 = Blue, 1 = Red
    private byte _winnerSide = NoWinner; // set by the move that ends the game

    public ulong Hash { get; private set; }
    public int Turn => _turn;
    public int PieceCount(int side) => _count[side];
    public byte Occupant(int sq) => _squareIds[sq];
    public byte SquareOf(byte id) => _posOf[id];
    public byte WinnerSide => _winnerSide;
    public ReadOnlySpan<byte> PieceIds(int side) => _ids[side].AsSpan(0, _count[side]);

    /// <summary>Zobrist piece index for a piece id (0..15).</summary>
    private static int ZobristIndex(byte id) => (id - 1) % DistinctPieceKinds;

    public static bool IsRat(byte pieceId) => pieceId != 0 && RankOf[pieceId] == 1;
    private static bool IsWater(int sq) => TerrainOf[sq] == Water;
    private static bool IsOwnDen(int sq, int side) =>
        side == 0 ? TerrainOf[sq] == DenBlue : TerrainOf[sq] == DenRed;
    private static bool IsOppDen(int sq, int side) =>
        side == 0 ? TerrainOf[sq] == DenRed : TerrainOf[sq] == DenBlue;

    /// <summary>Effective rank: 0 when standing on the opponent's trap.</summary>
    internal static int EffectiveRankOf(byte id, int sq)
    {
        int side = ZobristIndex(id) & 1;
        bool onEnemyTrap = side == 0 ? TerrainOf[sq] == TrapRed : TerrainOf[sq] == TrapBlue;
        return onEnemyTrap ? 0 : RankOf[id];
    }

    /// <summary>True when the square is a trap of the given side's opponent.</summary>
    internal static bool IsEnemyTrapSquare(int sq, int side) =>
        side == 0 ? TerrainOf[sq] == TrapRed : TerrainOf[sq] == TrapBlue;

    /// <summary>Mirror of CaptureResolver.CanCapture (kept in sync by differential tests).</summary>
    public static bool CanCapture(byte attackerId, int attackerSq, byte defenderId, int defenderSq)
    {
        int aRank = RankOf[attackerId];
        int dRank = RankOf[defenderId];

        // Rat captures Elephant from land only
        if (aRank == 1 && dRank == 8)
            return !IsWater(attackerSq);

        // Elephant cannot capture Rat
        if (aRank == 8 && dRank == 1)
            return false;

        // Rat vs Rat is unconditional (land or water, even from a trap square)
        if (aRank == 1 && dRank == 1)
            return true;

        // Rat in water cannot be captured by any other piece on land
        if (dRank == 1 && IsWater(defenderSq) && !IsWater(attackerSq))
            return false;

        return EffectiveRankOf(attackerId, attackerSq) >= EffectiveRankOf(defenderId, defenderSq);
    }

    public static SearchBoard FromGameState(GameState state)
    {
        var board = new SearchBoard();
        var copies = new byte[DistinctPieceKinds];
        foreach (var kv in state.Pieces)
        {
            int sq = kv.Key.Row * 7 + kv.Key.Col;
            int pieceIdx = ((int)kv.Value.Animal - 1) * 2 + (int)kv.Value.Owner;
            // Duplicate (animal, owner) pairs (possible in constructed positions)
            // get a second id range so every piece remains uniquely addressable.
            if (copies[pieceIdx] >= 2)
                throw new ArgumentException(
                    $"More than two {kv.Value.Owner} {kv.Value.Animal}s are not supported by the search engine.");
            byte id = (byte)(pieceIdx + 1 + (copies[pieceIdx]++ > 0 ? DistinctPieceKinds : 0));
            int side = (int)kv.Value.Owner;
            board._squareIds[sq] = id;
            board._posOf[id] = (byte)sq;
            board._ids[side][board._count[side]++] = id;
        }
        board._turn = (byte)(int)state.CurrentTurn;
        board.Hash = Zobrist.ComputeHash(state);
        return board;
    }

    /// <summary>Rebuilds the public immutable state (used by differential tests).</summary>
    public GameState ToGameState()
    {
        var builder = ImmutableDictionary.CreateBuilder<Position, Piece>();
        for (int side = 0; side < 2; side++)
        {
            foreach (byte id in PieceIds(side))
            {
                byte sq = _posOf[id];
                var pos = new Position(sq % 7, sq / 7);
                var piece = new Piece((Animal)((ZobristIndex(id) >> 1) + 1), (Player)side, pos);
                builder.Add(pos, piece);
            }
        }
        return new GameState(
            Board.Initial,
            builder.ToImmutable(),
            (Player)_turn,
            GameStatus.InProgress,
            ImmutableList<Piece>.Empty,
            ImmutableList<Piece>.Empty);
    }

    public void CopyTo(SearchBoard target)
    {
        Array.Copy(_squareIds, target._squareIds, SquareCount);
        Array.Copy(_posOf, target._posOf, MaxPieceIds + 1);
        Array.Copy(_ids[0], target._ids[0], 16);
        Array.Copy(_ids[1], target._ids[1], 16);
        target._count[0] = _count[0];
        target._count[1] = _count[1];
        target._turn = _turn;
        target._winnerSide = _winnerSide;
        target.Hash = Hash;
    }

    public SearchBoard Clone()
    {
        var board = new SearchBoard();
        CopyTo(board);
        return board;
    }

    /// <summary>Flips the side to move without a piece move (null-move pruning).</summary>
    public void MakeNullMove()
    {
        _turn = (byte)(_turn ^ 1);
        Hash ^= Zobrist.TurnKey;
    }

    /// <summary>Undoes <see cref="MakeNullMove"/>.</summary>
    public void UnmakeNullMove() => MakeNullMove();

    public void ApplyMove(in SearchMove m)
    {
        int side = _turn;
        byte id = _squareIds[m.From];

        Hash ^= Zobrist.PieceKeys[m.From, ZobristIndex(id)];
        _squareIds[m.From] = 0;

        if (m.CapturedId != 0)
        {
            int opp = side ^ 1;
            int c = _count[opp];
            for (int i = 0; i < c; i++)
            {
                if (_ids[opp][i] == m.CapturedId)
                {
                    _ids[opp][i] = _ids[opp][c - 1];
                    break;
                }
            }
            _count[opp] = (byte)(c - 1);
            _posOf[m.CapturedId] = 255;
            Hash ^= Zobrist.PieceKeys[m.To, ZobristIndex(m.CapturedId)];

            if (_count[opp] == 0)
                _winnerSide = (byte)side; // Total elimination
        }

        _squareIds[m.To] = id;
        _posOf[id] = m.To;
        Hash ^= Zobrist.PieceKeys[m.To, ZobristIndex(id)];

        if (m.EntersDen)
            _winnerSide = (byte)side;

        Hash ^= Zobrist.TurnKey;
        _turn = (byte)(side ^ 1);
    }

    /// <summary>
    /// Generates all legal moves for the given side into buf; returns the count.
    /// A side has at most 16 pieces × (4 steps + 2 jumps) = 96 moves, so buffers
    /// of 128 entries are safe.
    /// </summary>
    public int GenerateMoves(int side, SearchMove[] buf)
    {
        int n = 0;
        foreach (byte id in PieceIds(side))
        {
            byte from = _posOf[id];

            foreach (byte to in Neighbors[from])
            {
                if (TryMakeMove(side, id, from, to, out var m))
                {
                    Debug.Assert(n < buf.Length, "Move buffer overflow");
                    buf[n++] = m;
                }
            }

            Jump[]? jumps = RankOf[id] == 7 ? LionJumpsOf[from] : RankOf[id] == 6 ? TigerJumpsOf[from] : null;
            if (jumps != null)
            {
                foreach (var jump in jumps)
                {
                    if (jump.IsBlockedByRat(this))
                        continue;
                    if (TryMakeMove(side, id, from, jump.Target, out var m))
                    {
                        Debug.Assert(n < buf.Length, "Move buffer overflow");
                        buf[n++] = m;
                    }
                }
            }
        }
        return n;
    }

    /// <summary>Captures plus enemy-den entries (the moves searched by quiescence).</summary>
    public int GenerateCaptures(int side, SearchMove[] buf)
    {
        int n = 0;
        foreach (byte id in PieceIds(side))
        {
            byte from = _posOf[id];

            foreach (byte to in Neighbors[from])
            {
                if (TryMakeMove(side, id, from, to, out var m) && (m.IsCapture || m.EntersDen))
                    buf[n++] = m;
            }

            Jump[]? jumps = RankOf[id] == 7 ? LionJumpsOf[from] : RankOf[id] == 6 ? TigerJumpsOf[from] : null;
            if (jumps != null)
            {
                foreach (var jump in jumps)
                {
                    if (jump.IsBlockedByRat(this))
                        continue;
                    if (TryMakeMove(side, id, from, jump.Target, out var m) && (m.IsCapture || m.EntersDen))
                        buf[n++] = m;
                }
            }
        }
        return n;
    }

    public int CountLegalMoves(int side)
    {
        // Reuse the instance scratch buffer; callers never nest CountLegalMoves
        return GenerateMoves(side, _scratch);
    }
    private readonly SearchMove[] _scratch = new SearchMove[MaxMovesPerPly];

    private bool TryMakeMove(int side, byte id, byte from, byte to, out SearchMove m)
    {
        m = default;

        // Cannot move into own den (checked first, mirroring MoveValidator)
        if (IsOwnDen(to, side))
            return false;

        // Only the Rat may enter water
        if (IsWater(to) && RankOf[id] != 1)
            return false;

        byte occ = _squareIds[to];
        if (occ != 0)
        {
            if ((ZobristIndex(occ) & 1) == side)
                return false; // Own piece
            if (!CanCapture(id, from, occ, to))
                return false;
            m = new SearchMove(from, to, occ, false);
            return true;
        }

        m = new SearchMove(from, to, 0, IsOppDen(to, side));
        return true;
    }
}
