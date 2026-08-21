using JungleGame.Core.AI;
using JungleGame.Core.Engine;
using JungleGame.Core.Model;

namespace JungleGame.Tests.AI;

/// <summary>
/// Tablebase unit tests. They share one in-memory 2-piece build and load it
/// into TablebaseProbe via the test hook, so they run serialized (the loaded
/// table is static state) and reset it afterwards. The 3-piece table is never
/// built in CI (minutes of compute); --tb-verify covers it offline.
/// </summary>
[Collection("Tablebase")]
public class TablebaseTests : IDisposable
{
    private static readonly Lazy<(byte[] Wdl2, byte[] Wdl3, byte[] Dtm2, byte[] Dtm3)> Built = new(() =>
    {
        // 2-piece only: the 3-piece build takes minutes of compute and is
        // covered offline by --tb-build / --tb-verify.
        var result = TablebaseBuilder.Build2Piece(includeDtm: true);
        return (
            Pack(result.Wdl2),
            new byte[TablebaseFile.Wdl3ByteCount],
            result.Dtm2!,
            new byte[TablebaseIndex.Combo3Count * TablebaseIndex.EntriesPerCombo3]);
    });

    private static byte[] Pack(byte[] wdl)
    {
        var packed = new byte[(wdl.Length * 2 + 7) / 8];
        for (int i = 0; i < wdl.Length; i++)
        {
            int bit = i * 2;
            packed[bit >> 3] |= (byte)((wdl[i] & 3) << (bit & 7));
        }
        return packed;
    }

    public TablebaseTests()
    {
        var (wdl2, wdl3, dtm2, dtm3) = Built.Value;
        TablebaseProbe.LoadForTesting(wdl2, wdl3, dtm2, dtm3);
    }

    public void Dispose()
    {
        TablebaseProbe.ResetForTesting();
        GC.SuppressFinalize(this);
    }

    private static SearchBoard MakeBoard(params Piece[] pieces) =>
        SearchBoard.FromGameState(GameState.CreateFromPieces(pieces, Player.Blue));

    private static SearchBoard MakeBoardTurn(Player turn, params Piece[] pieces) =>
        SearchBoard.FromGameState(GameState.CreateFromPieces(pieces, turn));

    // ---- Index invariants ----

    [Fact]
    public void Rank2_Unrank2_RoundTrips()
    {
        var rng = new Random(42);
        for (int i = 0; i < 500; i++)
        {
            int a = rng.Next(TablebaseIndex.UsableSquares);
            int b = rng.Next(TablebaseIndex.UsableSquares - 1);
            if (b >= a) b++;
            int rank = TablebaseIndex.Rank2(a, b);
            Assert.InRange(rank, 0, TablebaseIndex.Placements2 - 1);
            var (ua, ub) = TablebaseBuilder.Unrank2(rank);
            Assert.Equal(a, TablebaseIndex.UsableOf[ua]);
            Assert.Equal(b, TablebaseIndex.UsableOf[ub]);
        }
    }

    [Fact]
    public void Rank3_Unrank3_RoundTrips()
    {
        var rng = new Random(42);
        for (int i = 0; i < 500; i++)
        {
            int a = rng.Next(TablebaseIndex.UsableSquares);
            int b = rng.Next(TablebaseIndex.UsableSquares - 1);
            if (b >= a) b++;
            int c = rng.Next(TablebaseIndex.UsableSquares - 2);
            while (c == a || c == b)
                c = (c + 1) % TablebaseIndex.UsableSquares;
            if (c >= a && c >= b) c = (c + 2) % TablebaseIndex.UsableSquares;
            while (c == a || c == b)
                c = (c + 1) % TablebaseIndex.UsableSquares;

            int rank = TablebaseIndex.Rank3(a, b, c);
            Assert.InRange(rank, 0, TablebaseIndex.Placements3 - 1);
            var (ua, ub, uc) = TablebaseBuilder.Unrank3(rank);
            Assert.Equal(a, ua);
            Assert.Equal(b, ub);
            Assert.Equal(c, uc);
        }
    }

    [Fact]
    public void Key2_RotatedPosition_MapsToSameOrbit()
    {
        var rng = new Random(42);
        int checked_ = 0;
        for (int i = 0; checked_ < 200; i++)
        {
            int s1 = rng.Next(TablebaseIndex.SquareCount);
            int s2 = rng.Next(TablebaseIndex.SquareCount);
            if (TablebaseIndex.UsableOf[s1] < 0 || TablebaseIndex.UsableOf[s2] < 0 || s1 == s2)
                continue;
            int blue = rng.Next(8) * 2; // even = blue
            int red = rng.Next(8) * 2 + 1; // odd = red
            int stm = rng.Next(2);

            // The rotated position: the original red piece becomes blue at
            // rotate(s2), the original blue piece becomes red at rotate(s1).
            int r1 = TablebaseIndex.RotateSquare(s1);
            int r2 = TablebaseIndex.RotateSquare(s2);

            int keyA = TablebaseIndex.Key2(blue, s1, red, s2, stm);
            int keyB = TablebaseIndex.Key2(red ^ 1, r2, blue ^ 1, r1, stm ^ 1);
            Assert.Equal(keyA, keyB); // the rotated position maps to the same entry
            checked_++;
        }
    }

    [Fact]
    public void RotationSymmetry_WdlMatches()
    {
        var rng = new Random(123);
        for (int i = 0; i < 200; i++)
        {
            var animals = new[] { Animal.Rat, Animal.Cat, Animal.Dog, Animal.Wolf, Animal.Leopard, Animal.Tiger, Animal.Lion, Animal.Elephant };
            var a = animals[rng.Next(animals.Length)];
            var b = animals[rng.Next(animals.Length)];
            var s1 = new Position(rng.Next(7), rng.Next(9));
            var s2 = new Position(rng.Next(7), rng.Next(9));
            if (s1 == s2 || !Usable(s1) || !Usable(s2))
                continue;

            var board = MakeBoardTurn(Player.Blue,
                new Piece(a, Player.Blue, s1),
                new Piece(b, Player.Red, s2));
            var rotated = MakeBoardTurn(Player.Red,
                new Piece(a, Player.Red, Rotate(s1)),
                new Piece(b, Player.Blue, Rotate(s2)));

            bool ok1 = TablebaseProbe.TryProbe(board, 0, out int score1);
            bool ok2 = TablebaseProbe.TryProbe(rotated, 0, out int score2);
            Assert.Equal(ok1, ok2);
            if (ok1)
                Assert.Equal(score1, score2); // symmetry: same WDL from the mover's side
        }
    }

    // ---- Known 2-piece values ----

    [Fact]
    public void RatVsRat_NoDraws()
    {
        // Literature result: two equal pieces never draw (den races and the
        // unconditional rat capture decide every position).
        var rng = new Random(7);
        for (int i = 0; i < 100; i++)
        {
            var (board, _) = Random2PieceBoard(rng, Animal.Rat, Animal.Rat);
            Assert.True(TablebaseProbe.TryProbe(board, 0, out int score));
            Assert.NotEqual(0, score);
        }
    }

    [Fact]
    public void LionToMove_AdjacentToRat_WinsByCapture()
    {
        var board = MakeBoard(
            new Piece(Animal.Lion, Player.Blue, new Position(3, 2)),
            new Piece(Animal.Rat, Player.Red, new Position(3, 3)));

        Assert.True(TablebaseProbe.TryProbe(board, 0, out int score));
        Assert.Equal(MinimaxEngine.MateScore - 1, score); // win in 1 ply (DTM 1)
    }

    [Fact]
    public void RatToMove_AdjacentToElephant_WinsByCapture()
    {
        var board = MakeBoardTurn(Player.Red,
            new Piece(Animal.Elephant, Player.Blue, new Position(3, 3)),
            new Piece(Animal.Rat, Player.Red, new Position(3, 2)));

        Assert.True(TablebaseProbe.TryProbe(board, 0, out int score));
        Assert.Equal(MinimaxEngine.MateScore - 1, score); // rat captures in 1 ply
    }

    [Fact]
    public void PieceAdjacentToEnemyDen_ToMove_Wins()
    {
        var board = MakeBoard(
            new Piece(Animal.Leopard, Player.Blue, new Position(3, 7)),
            new Piece(Animal.Rat, Player.Red, new Position(0, 0)));

        Assert.True(TablebaseProbe.TryProbe(board, 0, out int score));
        Assert.Equal(MinimaxEngine.MateScore - 1, score); // den entry in 1 ply
    }

    [Fact]
    public void EqualPieces_HaveNoDraws()
    {
        // Literature result: two equal pieces never draw. Sample Lion-vs-Lion.
        var rng = new Random(21);
        for (int i = 0; i < 100; i++)
        {
            var (board, _) = Random2PieceBoard(rng, Animal.Lion, Animal.Lion);
            Assert.True(TablebaseProbe.TryProbe(board, 0, out int score));
            Assert.NotEqual(0, score);
        }
    }

    // ---- Probe-vs-search differential (2-piece, short DTM) ----

    [Fact]
    public void TablebaseWdl_MatchesFixedDepthSearchPlay()
    {
        var rng = new Random(99);
        var animals = new[] { Animal.Rat, Animal.Cat, Animal.Dog, Animal.Wolf, Animal.Leopard, Animal.Tiger, Animal.Lion, Animal.Elephant };
        int verified = 0;

        for (int attempt = 0; attempt < 300 && verified < 10; attempt++)
        {
            var a = animals[rng.Next(animals.Length)];
            var b = animals[rng.Next(animals.Length)];
            var (board, state) = Random2PieceBoard(rng, a, b);
            if (state.Status != GameStatus.InProgress)
                continue;
            if (!TablebaseProbe.TryProbe(board, 0, out int tbScore))
                continue;

            // Only test decisive positions the search can see quickly (DTM ≤ 4).
            int dtm = Math.Abs(Math.Abs(tbScore) - MinimaxEngine.MateScore);
            if (dtm < 1 || dtm > 4)
                continue;

            var engine = new MinimaxEngine(TimeSpan.FromSeconds(5), maxDepth: 8, legacySearch: true);
            var played = state;
            int moves = 0;
            while (played.Status == GameStatus.InProgress && moves < 40)
            {
                var move = engine.FindBestMove(played);
                if (move == null)
                    break;
                played = GameController.ApplyMove(played, move.Value);
                moves++;
            }

            if (played.Status == GameStatus.BlueWins || played.Status == GameStatus.RedWins)
            {
                bool tbSideWon = (tbScore > 0 && played.Status ==
                    (state.CurrentTurn == Player.Blue ? GameStatus.BlueWins : GameStatus.RedWins)) ||
                    (tbScore < 0 && played.Status ==
                    (state.CurrentTurn == Player.Blue ? GameStatus.RedWins : GameStatus.BlueWins));
                Assert.True(tbSideWon,
                    $"TB says {tbScore} for {state.CurrentTurn} (DTM {dtm}) but the game ended {played.Status}");
                verified++;
            }
        }

        Assert.True(verified >= 3, $"only {verified} decisive short-DTM positions found");
    }

    [Fact]
    public void Probe_PieceCountOutsideRange_ReturnsFalse()
    {
        Span<byte> types = stackalloc byte[1] { 0 };
        Span<byte> squares = stackalloc byte[1] { 10 };
        var board = SearchBoard.FromPackedPieces(types, squares, 0);
        Assert.False(TablebaseProbe.TryProbe(board, 0, out _)); // 1 piece
    }

    [Fact]
    public void RootProbe_ReturnsWinningMove()
    {
        var board = MakeBoard(
            new Piece(Animal.Lion, Player.Blue, new Position(3, 7)),
            new Piece(Animal.Rat, Player.Red, new Position(0, 0)));

        Assert.True(TablebaseProbe.TryProbeWithMove(board, 0, out int score, out var move));
        Assert.True(score > 0);
        Assert.NotNull(move);
        Assert.Equal(new Position(3, 7), move!.Value.From);
        Assert.Equal(new Position(3, 8), move.Value.To);
    }

    [Fact]
    public void Engine_UseTablebaseFalse_ProbesNothing()
    {
        // The engine ctor's TablebaseProbe.Initialize() may load the disk file
        // over the test tables when one is present; re-apply the in-memory
        // tables after construction so the probes run against the same data.
        var (wdl2, wdl3, dtm2, dtm3) = Built.Value;
        var on = new MinimaxEngine(TimeSpan.FromSeconds(1), maxDepth: 4, useTablebase: true);
        var off = new MinimaxEngine(TimeSpan.FromSeconds(1), maxDepth: 4, useTablebase: false);
        TablebaseProbe.LoadForTesting(wdl2, wdl3, dtm2, dtm3);

        var state = GameState.CreateFromPieces(new[]
        {
            new Piece(Animal.Lion, Player.Blue, new Position(3, 7)),
            new Piece(Animal.Rat, Player.Red, new Position(0, 0)),
        }, Player.Blue);

        long before = TablebaseInfo.TotalHits;
        on.FindBestMove(state);
        long afterOn = TablebaseInfo.TotalHits;
        off.FindBestMove(state);
        long afterOff = TablebaseInfo.TotalHits;

        Assert.True(afterOn > before, "tablebase-enabled search performed no probes");
        Assert.Equal(afterOn, afterOff);
    }

    private static (SearchBoard Board, GameState State) Random2PieceBoard(Random rng, Animal a, Animal b)
    {
        int stm = rng.Next(2);
        Position s1, s2;
        int tries = 0;
        do
        {
            s1 = new Position(rng.Next(7), rng.Next(9));
            s2 = new Position(rng.Next(7), rng.Next(9));
        }
        while (tries++ < 50 && (s1 == s2 || !Usable(s1) || !Usable(s2)));

        var pieces = new[]
        {
            new Piece(a, stm == 0 ? Player.Blue : Player.Red, s1),
            new Piece(b, stm == 0 ? Player.Red : Player.Blue, s2),
        };
        var state = GameState.CreateFromPieces(pieces, stm == 0 ? Player.Blue : Player.Red);
        return (SearchBoard.FromGameState(state), state);
    }

    private static bool Usable(Position pos) =>
        !Board.Initial.IsDen(pos, Player.Blue) && !Board.Initial.IsDen(pos, Player.Red);

    private static Position Rotate(Position pos) => new(6 - pos.Col, 8 - pos.Row);
}

[CollectionDefinition("Tablebase", DisableParallelization = true)]
public class TablebaseCollection;
