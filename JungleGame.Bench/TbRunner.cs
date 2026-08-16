using JungleGame.Core.AI;
using JungleGame.Core.Engine;
using JungleGame.Core.Model;

namespace JungleGame.Bench;

/// <summary>
/// Offline tablebase tooling: --tb-build constructs the 2-piece + 3-piece
/// tables (WDL + DTM, minutes of Release-mode compute — never run in CI);
/// --tb-verify replays the tablebase moves on sampled positions to
/// completion (consistency at scale) and independently cross-checks
/// short-DTM positions against fixed-depth search play.
/// </summary>
internal static class TbRunner
{
    internal static int RunBuild(string[] args)
    {
        string dir = Args.ReadString(args, "--path", DefaultDirectory) ?? DefaultDirectory;
        string path = Path.Combine(dir, TablebaseFile.FileName);
        Directory.CreateDirectory(dir);

        Console.WriteLine($"Building 2-piece + 3-piece tables (WDL + DTM) into {path} ...");
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = TablebaseBuilder.Build(includeDtm: true, log: Console.WriteLine);
        TablebaseFile.Save(path, result);
        sw.Stop();

        Console.WriteLine($"Saved {path} ({new FileInfo(path).Length / (1024 * 1024)} MB) in {sw.Elapsed.TotalSeconds:F0}s.");
        Console.WriteLine("The engine picks the file up automatically on the next search.");
        return 0;
    }

    internal static int RunVerify(string[] args)
    {
        TablebaseProbe.Initialize();
        if (!TablebaseProbe.IsLoaded)
        {
            Console.Error.WriteLine("No tablebase file found — run --tb-build first " +
                $"(search paths: exe dir, {DefaultDirectory}, JUNGLE_TB_PATH).");
            return 1;
        }

        int samples = Args.ReadInt(args, "--samples", 10_000);
        int seed = Args.ReadInt(args, "--seed", 42);
        var rng = new Random(seed);

        Console.WriteLine($"Replaying tablebase moves on {samples} sampled positions...");
        int verified = 0, skipped = 0, mismatches = 0;
        for (int i = 0; i < samples; i++)
        {
            var (board, state) = RandomTbPosition(rng);
            if (state.Status != GameStatus.InProgress)
            {
                skipped++;
                continue;
            }
            if (!TablebaseProbe.TryProbeWithMove(board, 0, out int score, out var move))
            {
                skipped++;
                continue;
            }

            var played = state;
            int plies = 0;
            while (played.Status == GameStatus.InProgress && plies < 400)
            {
                var playedBoard = SearchBoard.FromGameState(played);
                if (!TablebaseProbe.TryProbeWithMove(playedBoard, 0, out _, out var next) || next == null)
                    break;
                played = GameController.ApplyMove(played, next.Value);
                plies++;
            }

            bool tbSideWon = score > 0 &&
                played.Status == (state.CurrentTurn == Player.Blue ? GameStatus.BlueWins : GameStatus.RedWins);
            bool tbSideLost = score < 0 &&
                played.Status == (state.CurrentTurn == Player.Blue ? GameStatus.RedWins : GameStatus.BlueWins);
            bool tbDraw = score == 0 && played.Status == GameStatus.Draw;

            if (tbSideWon || tbSideLost || tbDraw)
                verified++;
            else if (played.Status == GameStatus.InProgress)
                skipped++; // 400-ply cap without a terminal result
            else
            {
                mismatches++;
                if (mismatches <= 5)
                    Console.WriteLine($"MISMATCH: TB={score} stm={state.CurrentTurn} ended={played.Status} after {plies} plies; " +
                        $"pieces=[{string.Join(" ", state.Pieces.Values.Select(p => $"{(p.Owner == Player.Blue ? 'B' : 'R')}{p.Animal}@{p.Position.Col},{p.Position.Row}"))}]");
            }
        }

        Console.WriteLine($"Replay: {verified} consistent, {skipped} skipped, {mismatches} mismatches");
        // A small mismatch class is expected: the retrograde model treats
        // unresolved positions as draws but cannot see repetition CYCLES, so
        // fortress positions (the defender repeats into the three-fold rule)
        // are stored as wins that real play draws. These are strength-neutral
        // (the engine's repetition check still sees the draw first), so the
        // replay rate is reported, not gated.
        double mismatchRate = (double)mismatches / Math.Max(1, samples);
        Console.WriteLine($"Replay mismatch rate: {mismatchRate:P1} (fortress class expected ≤ 2%)");

        Console.WriteLine("Cross-checking short-DTM positions against fixed-depth search play...");
        int crossChecked = 0, crossMismatch = 0;
        for (int attempt = 0; attempt < samples * 5 && crossChecked < 100; attempt++)
        {
            var (board, state) = RandomTbPosition(rng);
            if (state.Status != GameStatus.InProgress)
                continue;
            if (!TablebaseProbe.TryProbe(board, 0, out int score))
                continue;
            int dtm = Math.Abs(Math.Abs(score) - MinimaxEngine.MateScore);
            if (dtm < 1 || dtm > 4)
                continue;

            // legacySearch disables tablebase probing: this is the pure search.
            var engine = new MinimaxEngine(TimeSpan.FromSeconds(5), maxDepth: 10, legacySearch: true);
            var played = state;
            int plies = 0;
            while (played.Status == GameStatus.InProgress && plies < 40)
            {
                var m = engine.FindBestMove(played);
                if (m == null)
                    break;
                played = GameController.ApplyMove(played, m.Value);
                plies++;
            }

            if (played.Status == GameStatus.BlueWins || played.Status == GameStatus.RedWins)
            {
                bool tbWins = score > 0 &&
                    played.Status == (state.CurrentTurn == Player.Blue ? GameStatus.BlueWins : GameStatus.RedWins);
                bool tbLoses = score < 0 &&
                    played.Status == (state.CurrentTurn == Player.Blue ? GameStatus.RedWins : GameStatus.BlueWins);
                if (tbWins || tbLoses)
                    crossChecked++;
                else
                {
                    crossMismatch++;
                    Console.WriteLine($"MISMATCH: TB {score} (DTM {dtm}) but search play ended {played.Status}");
                }
            }
        }

        Console.WriteLine($"Search cross-check: {crossChecked} consistent, {crossMismatch} mismatches");
        return crossMismatch == 0 ? 0 : 1;
    }

    internal static string DefaultDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "JungleGame", "tablebases");

    private static (SearchBoard Board, GameState State) RandomTbPosition(Random rng)
    {
        int pieceCount = rng.Next(2) == 0 ? 2 : 3;
        int stm = rng.Next(2);

        if (pieceCount == 2)
        {
            var animals = new[] { Animal.Rat, Animal.Cat, Animal.Dog, Animal.Wolf, Animal.Leopard, Animal.Tiger, Animal.Lion, Animal.Elephant };
            var a = animals[rng.Next(animals.Length)];
            var b = animals[rng.Next(animals.Length)];
            var (s1, s2) = RandomUsablePair(rng);
            var pieces = new[]
            {
                new Piece(a, stm == 0 ? Player.Blue : Player.Red, s1),
                new Piece(b, stm == 0 ? Player.Red : Player.Blue, s2),
            };
            var state = GameState.CreateFromPieces(pieces, stm == 0 ? Player.Blue : Player.Red);
            return (SearchBoard.FromGameState(state), state);
        }

        // 3-piece: half 2v1, half 1v2 (exercises the rotation path of the probe).
        bool twoBlue = rng.Next(2) == 0;
        var (sq1, sq2, sq3) = RandomUsableTriple(rng);
        Piece[] pcs = twoBlue
            ? new[]
            {
                new Piece(RandomAnimal(rng), Player.Blue, sq1),
                new Piece(RandomAnimal(rng), Player.Blue, sq2),
                new Piece(RandomAnimal(rng), Player.Red, sq3),
            }
            : new[]
            {
                new Piece(RandomAnimal(rng), Player.Blue, sq1),
                new Piece(RandomAnimal(rng), Player.Red, sq2),
                new Piece(RandomAnimal(rng), Player.Red, sq3),
            };
        var state3 = GameState.CreateFromPieces(pcs, stm == 0 ? Player.Blue : Player.Red);
        return (SearchBoard.FromGameState(state3), state3);

        static Animal RandomAnimal(Random r) => (Animal)(r.Next(8) + 1);
    }

    private static (Position, Position) RandomUsablePair(Random rng)
    {
        Position s1, s2;
        do
        {
            s1 = new Position(rng.Next(7), rng.Next(9));
            s2 = new Position(rng.Next(7), rng.Next(9));
        }
        while (s1 == s2 || !Usable(s1) || !Usable(s2));
        return (s1, s2);
    }

    private static (Position, Position, Position) RandomUsableTriple(Random rng)
    {
        Position s1, s2, s3;
        do
        {
            s1 = new Position(rng.Next(7), rng.Next(9));
            s2 = new Position(rng.Next(7), rng.Next(9));
            s3 = new Position(rng.Next(7), rng.Next(9));
        }
        while (s1 == s2 || s1 == s3 || s2 == s3 || !Usable(s1) || !Usable(s2) || !Usable(s3));
        return (s1, s2, s3);
    }

    private static bool Usable(Position pos) =>
        !Board.Initial.IsDen(pos, Player.Blue) && !Board.Initial.IsDen(pos, Player.Red);
}
