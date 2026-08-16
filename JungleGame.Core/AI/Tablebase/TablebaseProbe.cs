using JungleGame.Core.Engine;
using JungleGame.Core.Model;

namespace JungleGame.Core.AI;

/// <summary>
/// Tablebase access for the search: lazy one-time load from the exe directory,
/// %LOCALAPPDATA%\JungleGame\tablebases, or JUNGLE_TB_PATH; silent degradation
/// to search-only play when the file is absent or corrupt (hash-checked).
/// Probes resolve ≤ 3-piece positions exactly: a win/loss maps to a mate-range
/// score with DTM distance preference, a draw to 0. The root probe also
/// returns the best tablebase move (used for root move ordering — the search
/// still runs, since the opponent may err). The 4-piece seam: piece-count
/// checks here are the only place a larger table would plug in.
/// </summary>
internal static class TablebaseProbe
{
    internal enum Status
    {
        NotPresent,
        Loaded,
        Corrupt,
    }

    private static readonly object Gate = new();
    private static bool _initialized;
    private static Status _status = Status.NotPresent;
    private static byte[]? _wdl2;
    private static byte[]? _wdl3;
    private static byte[]? _dtm2;
    private static byte[]? _dtm3;
    private static long _hits;

    internal static Status CurrentStatus => _status;
    internal static bool IsLoaded => _status == Status.Loaded;
    internal static long Hits => Interlocked.Read(ref _hits);

    /// <summary>
    /// Test hook (InternalsVisibleTo): loads packed tables from memory without
    /// touching the disk. Tests reset afterwards — see ResetForTesting.
    /// </summary>
    internal static void LoadForTesting(byte[] wdl2Packed, byte[] wdl3Packed, byte[]? dtm2 = null, byte[]? dtm3 = null)
    {
        lock (Gate)
        {
            _wdl2 = wdl2Packed;
            _wdl3 = wdl3Packed;
            _dtm2 = dtm2;
            _dtm3 = dtm3;
            _status = Status.Loaded;
        }
    }

    internal static void ResetForTesting()
    {
        lock (Gate)
        {
            _initialized = false;
            _status = Status.NotPresent;
            _wdl2 = _wdl3 = _dtm2 = _dtm3 = null;
            _hits = 0;
        }
    }

    /// <summary>Idempotent: scans the search paths once and loads the file if found.</summary>
    internal static void Initialize()
    {
        lock (Gate)
        {
            if (_initialized)
                return;
            _initialized = true;

            string? path = FindTablebaseFile();
            if (path == null)
                return; // NotPresent

            var loaded = TablebaseFile.Load(path);
            if (loaded == null)
            {
                _status = Status.Corrupt;
                return;
            }

            _wdl2 = loaded.Wdl2;
            _wdl3 = loaded.Wdl3;
            _dtm2 = loaded.Dtm2;
            _dtm3 = loaded.Dtm3;
            _status = Status.Loaded;
        }
    }

    internal static string? FindTablebaseFile()
    {
        string? env = Environment.GetEnvironmentVariable("JUNGLE_TB_PATH");
        if (!string.IsNullOrEmpty(env))
        {
            string envFile = Path.Combine(env, TablebaseFile.FileName);
            if (File.Exists(envFile))
                return envFile;
        }

        string exeDir = Path.Combine(AppContext.BaseDirectory, TablebaseFile.FileName);
        if (File.Exists(exeDir))
            return exeDir;

        string appData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "JungleGame", "tablebases", TablebaseFile.FileName);
        return File.Exists(appData) ? appData : null;
    }

    /// <summary>
    /// Exact score of a ≤ 3-piece position for the side to move (root-relative,
    /// in the search's mate convention). Callers check the piece count first.
    /// </summary>
    internal static bool TryProbe(SearchBoard board, int ply, out int score)
    {
        score = 0;
        if (_status != Status.Loaded)
            return false;

        int count = board.PieceCount(0) + board.PieceCount(1);
        if (count < 2 || count > 3 || board.WinnerSide != SearchBoard.NoWinner)
            return false;

        Interlocked.Increment(ref _hits);

        if (count == 2)
        {
            if (TryKey2(board, out int entry))
            {
                if ((uint)entry * 2 >= (uint)_wdl2!.Length * 8)
                    throw new InvalidOperationException($"2-piece entry out of range: {entry} pieces=[{Describe(board)}]");
                byte wdl = TablebaseFile.GetWdl(_wdl2!, entry);
                score = MapScore(wdl, Dtm2(entry), ply);
                return wdl != 0;
            }
            return false;
        }

        if (TryKey3(board, out int entry3))
        {
            if ((uint)entry3 * 2 >= (uint)_wdl3!.Length * 8)
            {
                var (tA, sA, tB, sB, tC, sC) = Extract3(board);
                throw new InvalidOperationException(
                    $"3-piece entry out of range: {entry3} pieces=[{Describe(board)}] " +
                    $"extracted=({tA}@{sA},{tB}@{sB},{tC}@{sC}) combo={TablebaseIndex.Combo3Of[tA, tB, tC]}");
            }
            byte wdl = TablebaseFile.GetWdl(_wdl3!, entry3);
            score = MapScore(wdl, Dtm3(entry3), ply);
            return wdl != 0;
        }
        return false;
    }

    /// <summary>Root probe: score plus the best tablebase move (null when none exists).</summary>
    internal static bool TryProbeWithMove(SearchBoard board, int ply, out int score, out Move? bestMove)
    {
        bestMove = null;
        score = 0;
        if (_status != Status.Loaded)
            return false;

        int count = board.PieceCount(0) + board.PieceCount(1);
        if (count < 2 || count > 3)
            return false;

        if (!TryProbe(board, ply, out score))
            return false;

        int side = board.Turn;
        Span<SearchMove> moves = stackalloc SearchMove[SearchBoard.MaxMovesPerPly];
        int moveCount = board.GenerateMoves(side, moves);
        if (moveCount == 0)
            return true;

        // Rank the children: win (shortest DTM) > draw > loss (longest DTM).
        int bestRank = int.MinValue;
        for (int i = 0; i < moveCount; i++)
        {
            var move = moves[i];
            int rank;
            if (move.EntersDen)
            {
                rank = 1_000_000 - 1;
            }
            else
            {
                var (wdl, dtm) = ResolveChild(board, count, move);
                rank = wdl switch
                {
                    1 => 1_000_000 - dtm,   // child loss = our win (prefer shorter)
                    3 => -1_000_000 + dtm,  // child win = our loss (prefer longer resistance)
                    _ => 0,                 // draw
                };
            }

            if (rank > bestRank)
            {
                bestRank = rank;
                bestMove = ToPublicMove(move);
            }
        }

        return true;
    }

    /// <summary>Child WDL (from the child's mover perspective) and DTM in plies.</summary>
    internal static (byte Wdl, int Dtm) ResolveChild(SearchBoard board, int parentCount, in SearchMove move)
    {
        int stm = board.Turn;
        if (move.EntersDen)
            return ((byte)1, 1); // the child's mover (the opponent) loses immediately
        if (parentCount == 2)
        {
            // 2-piece: quiet child = same 2-piece table, side flipped
            int typeA = TablebaseIndex.TypeOf(board.Occupant(move.From));
            // The two types: the mover's and the other piece's
            int otherType = 0;
            byte otherSq = 0;
            for (int s = 0; s < 2; s++)
            {
                foreach (byte id in board.PieceIds(s))
                {
                    int sq = board.SquareOf(id);
                    if (sq != move.From && sq != move.To)
                    {
                        otherType = TablebaseIndex.TypeOf(id);
                        otherSq = (byte)sq;
                    }
                }
            }

            if (move.IsCapture)
            {
                // elimination: the child's mover loses immediately
                return ((byte)1, 1);
            }

            // Blue-first ordering: typeA is the mover's type (either side).
            byte blueType = (typeA & 1) == 0 ? (byte)typeA : (byte)otherType;
            byte blueSq = (typeA & 1) == 0 ? move.To : otherSq;
            byte redType = (typeA & 1) == 0 ? (byte)otherType : (byte)typeA;
            byte redSq = (typeA & 1) == 0 ? otherSq : move.To;
            int combo = TablebaseIndex.Combo2Of[blueType, redType];
            int entry = combo * TablebaseIndex.EntriesPerCombo2 +
                TablebaseIndex.Key2(blueType, blueSq, redType, redSq, stm ^ 1) %
                TablebaseIndex.EntriesPerCombo2;
            return (TablebaseFile.GetWdl(_wdl2!, entry), Dtm2(entry));
        }

        // 3-piece: extract the three pieces in the ORIGINAL frame, then map
        // the child into the 2v1 form only for the table lookups.
        Span<byte> types = stackalloc byte[3];
        Span<byte> squares = stackalloc byte[3];
        int n = 0;
        for (int s = 0; s < 2; s++)
        {
            foreach (byte id in board.PieceIds(s))
            {
                types[n] = (byte)TablebaseIndex.TypeOf(id);
                squares[n] = board.SquareOf(id);
                n++;
            }
        }

        if (move.IsCapture)
        {
            int moverIdx = move.From == squares[0] ? 0 : move.From == squares[1] ? 1 : 2;
            // The survivor is the piece on neither move.From nor move.To.
            int survivorIdx = 0;
            for (int i = 0; i < 3; i++)
            {
                if (i != moverIdx && squares[i] != move.To)
                {
                    survivorIdx = i;
                    break;
                }
            }

            byte moverType = types[moverIdx];
            byte survivorType = types[survivorIdx];
            byte survivorSq = squares[survivorIdx];

            // Key2/Combo2Of expect the blue type first (Key2 canonicalizes the
            // type orbit itself; 2-piece children never need the 2v1 rotation).
            byte blueType = (moverType & 1) == 0 ? moverType : survivorType;
            byte blueSq = (moverType & 1) == 0 ? move.To : survivorSq;
            byte redType = (moverType & 1) == 0 ? survivorType : moverType;
            byte redSq = (moverType & 1) == 0 ? survivorSq : move.To;
            if ((blueType & 1) == (redType & 1))
                return ((byte)1, 1); // lone opponent piece captured: elimination
            int combo2 = TablebaseIndex.Combo2Of[blueType, redType];
            int entry2 = combo2 * TablebaseIndex.EntriesPerCombo2 +
                TablebaseIndex.Key2(blueType, blueSq, redType, redSq, stm ^ 1) %
                TablebaseIndex.EntriesPerCombo2;
            return (TablebaseFile.GetWdl(_wdl2!, entry2), Dtm2(entry2));
        }

        // Quiet child: same three types, the mover at move.To, turn flipped.
        int moverIdxQ = move.From == squares[0] ? 0 : move.From == squares[1] ? 1 : 2;
        squares[moverIdxQ] = move.To;
        int childStm = stm ^ 1;
        if (board.PieceCount(0) == 1)
        {
            // Rotate the 1v2 child into the 2v1 form: owner bits flip, squares
            // rotate, and the rotated side to move is the original one.
            for (int i = 0; i < 3; i++)
            {
                types[i] ^= 1;
                squares[i] = (byte)TablebaseIndex.RotateSquare(squares[i]);
            }
            childStm = stm;
        }

        // Blue pair sorted ascending for the combo lookup.
        byte a = 0, b = 0, c = 0;
        byte sa = 0, sb = 0, sc = 0;
        int written = 0;
        for (int i = 0; i < 3; i++)
        {
            if ((types[i] & 1) == 0) // blue
            {
                if (written == 0 || types[i] < a)
                {
                    if (written > 0)
                    {
                        b = a; sb = sa;
                    }
                    a = types[i]; sa = squares[i];
                }
                else
                {
                    b = types[i]; sb = squares[i];
                }
                written++;
            }
            else
            {
                c = types[i]; sc = squares[i];
            }
        }
        int combo3 = TablebaseIndex.Combo3Of[a, b, c];
        int entry3 = TablebaseIndex.Key3(combo3, sa, sb, sc, childStm);
        return (TablebaseFile.GetWdl(_wdl3!, entry3), Dtm3(entry3));
    }

    private static bool TryKey2(SearchBoard board, out int entry)
    {
        // Pieces: one per side (2-piece probe)
        byte[] types = new byte[2];
        byte[] squares = new byte[2];
        int n = 0;
        for (int s = 0; s < 2; s++)
        {
            foreach (byte id in board.PieceIds(s))
            {
                types[n] = (byte)TablebaseIndex.TypeOf(id);
                squares[n] = board.SquareOf(id);
                n++;
            }
        }
        if (n != 2)
        {
            entry = 0;
            return false;
        }

        int combo = TablebaseIndex.Combo2Of[types[0], types[1]];
        entry = combo * TablebaseIndex.EntriesPerCombo2 +
            TablebaseIndex.Key2(types[0], squares[0], types[1], squares[1], board.Turn) %
            TablebaseIndex.EntriesPerCombo2;
        return true;
    }

    /// <summary>Extracts the 3-piece position in the 2v1 orientation.</summary>
    private static (byte TA, byte SA, byte TB, byte SB, byte TC, byte SC) Extract3(SearchBoard board)
    {
        // Blue pair (a ≤ b) and the red type, in the 2v1 orientation: if Blue
        // holds one piece, rotate the position (squares rotated, stm flipped).
        if (board.PieceCount(0) == 2)
        {
            byte a = 0, b = 0, c = 0;
            byte sa = 0, sb = 0, sc = 0;
            bool first = true;
            foreach (byte id in board.PieceIds(0))
            {
                byte t = (byte)TablebaseIndex.TypeOf(id);
                if (first || t < a)
                {
                    b = a; sb = sa;
                    a = t; sa = board.SquareOf(id);
                    first = false;
                }
                else
                {
                    b = t; sb = board.SquareOf(id);
                }
            }
            foreach (byte id in board.PieceIds(1))
            {
                c = (byte)TablebaseIndex.TypeOf(id);
                sc = board.SquareOf(id);
            }
            return (a, sa, b, sb, c, sc);
        }

        // Blue holds one piece: rotate (red pair becomes the blue pair — the
        // owner bit flips on every type).
        byte ra = 0, rb = 0, rc = 0;
        byte rsa = 0, rsb = 0, rsc = 0;
        bool firstR = true;
        foreach (byte id in board.PieceIds(1))
        {
            byte t = (byte)(TablebaseIndex.TypeOf(id) ^ 1);
            byte rotated = (byte)TablebaseIndex.RotateSquare(board.SquareOf(id));
            if (firstR || t < ra)
            {
                rb = ra; rsb = rsa;
                ra = t; rsa = rotated;
                firstR = false;
            }
            else
            {
                rb = t; rsb = rotated;
            }
        }
        foreach (byte id in board.PieceIds(0))
        {
            rc = (byte)(TablebaseIndex.TypeOf(id) ^ 1);
            rsc = (byte)TablebaseIndex.RotateSquare(board.SquareOf(id));
        }
        return (ra, rsa, rb, rsb, rc, rsc);
    }

    private static bool TryKey3(SearchBoard board, out int entry)
    {
        if (board.PieceCount(0) + board.PieceCount(1) != 3)
        {
            entry = 0;
            return false;
        }

        var (tA, sA, tB, sB, tC, sC) = Extract3(board);
        int combo = TablebaseIndex.Combo3Of[tA, tB, tC];
        int stm = board.PieceCount(0) == 2 ? board.Turn : board.Turn ^ 1;
        entry = TablebaseIndex.Key3(combo, sA, sB, sC, stm);
        return true;
    }

    private static int Dtm2(int entry) => _dtm2 != null ? _dtm2[entry] : 0;
    private static int Dtm3(int entry) => _dtm3 != null ? _dtm3[entry] : 0;

    private static string Describe(SearchBoard board)
    {
        var parts = new List<string>();
        for (int s = 0; s < 2; s++)
            foreach (byte id in board.PieceIds(s))
                parts.Add($"{(s == 0 ? 'B' : 'R')}{TablebaseIndex.TypeOf(id)}@{board.SquareOf(id)}");
        return string.Join(" ", parts) + $" stm={board.Turn}";
    }

    private static int MapScore(byte wdl, int dtm, int ply) => wdl switch
    {
        3 => MinimaxEngine.MateScore - ply - dtm,
        1 => ply + dtm - MinimaxEngine.MateScore,
        _ => 0,
    };

    private static Move ToPublicMove(in SearchMove move)
    {
        var from = new Position(move.From % 7, move.From / 7);
        var to = new Position(move.To % 7, move.To / 7);
        Piece? captured = move.CapturedId == 0 ? null : new Piece(
            (Animal)((((move.CapturedId - 1) % SearchBoard.DistinctPieceKinds) >> 1) + 1),
            (Player)(((move.CapturedId - 1) % SearchBoard.DistinctPieceKinds) & 1),
            to);
        return new Move(from, to, captured);
    }
}
