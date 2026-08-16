using System.Diagnostics;
using JungleGame.Bench;
using JungleGame.Core.AI;
using JungleGame.Core.Engine;
using JungleGame.Core.Model;

// JungleGame.Bench — engine performance and strength-measurement harness.
//
//   --bench [--time <ms>] [--depth <n>]      single search from the start
//               [--positions <file>]          position: nodes, nodes/s, depth
//               [--baseline <file>]           node-count regression bench over a
//               [--write-baseline]            .tsuite file vs a baseline JSON
//   --selfplay [--games <n>] [--timeA <ms>]   legacy tournament protocol
//              [--timeB <ms>] [--legacyB]     (alternating colors, shared TTs);
//              [--legacySearchB] [--seed <n>] kept byte-compatible for the
//              [--openings <n>]               recorded results
//   --arena    [--games <n>] [--timeA <ms>]   gated match: paired openings both
//              [--timeB <ms>] [--legacyB]     colors, fresh engines per game,
//              [--legacySearchA]              Wilson CI + binomial p-value,
//              [--legacySearchB] [--seed <n>] exit codes 0/1/2 (see ArenaRunner)
//              [--openings-file <path>]
//              [--openings-imbalanced <n>]
//              [--smoke]
//   --testsuite [--file <path>]               tactical suite runner (fixed depth)
//
// A/B protocol: same time for both sides, B on the legacy feature set
// (--legacyB for eval, --legacySearchB for search); accept a change when A wins
// >= 55% of decisive games (the arena enforces the gate with a decisive floor
// and a confidence interval). Sanity: more time should win clearly.
// --openings plays n random legal plies from the start position before the
// engines take over (deterministic per game, seeded by --seed) so the fixed
// tournament samples more than one opening.

if (args.Length == 0)
{
    PrintUsage();
    return 1;
}

switch (args[0])
{
    case "--bench":
        return RunBench(args);
    case "--selfplay":
        return RunSelfPlay(args);
    case "--arena":
        return ArenaRunner.Run(args);
    case "--testsuite":
        return RunTestSuite(args);
    default:
        PrintUsage();
        return 1;
}

static int RunBench(string[] args)
{
    bool hasPositions = Args.ReadString(args, "--positions") != null;
    if (hasPositions)
        return BenchPositions.Run(args);

    int timeMs = Args.ReadInt(args, "--time", 2000);
    int? maxDepth = Args.TryReadInt(args, "--depth");

    var engine = new MinimaxEngine(TimeSpan.FromMilliseconds(timeMs), maxDepth);
    var state = GameState.CreateInitial();

    var sw = Stopwatch.StartNew();
    var move = engine.FindBestMove(state);
    sw.Stop();

    double seconds = sw.Elapsed.TotalSeconds;
    Console.WriteLine($"Time budget : {timeMs} ms");
    Console.WriteLine($"Elapsed     : {sw.ElapsedMilliseconds} ms");
    Console.WriteLine($"Nodes       : {engine.NodesSearched}");
    Console.WriteLine($"Nodes/sec   : {engine.NodesSearched / seconds:F0}");
    Console.WriteLine($"Depth       : {engine.LastCompletedDepth}");
    Console.WriteLine($"Best move   : {move}");
    return 0;
}

static int RunTestSuite(string[] args)
{
    string path = Args.ResolveDataPath(Args.ReadString(args, "--file", "tactical-suite.tsuite") ?? "tactical-suite.tsuite");
    if (!File.Exists(path))
    {
        Console.Error.WriteLine($"Suite file not found: {path}");
        return 1;
    }

    var entries = TestSuiteParser.ParseFile(path);
    var results = TacticalSuiteRunner.RunAll(entries);
    int failed = 0;
    foreach (var r in results)
    {
        if (!r.Passed) failed++;
        Console.WriteLine($"{(r.Passed ? "PASS" : "FAIL")}  {r.Entry.Description}  " +
            $"→ {r.ActualMove?.ToString() ?? "null"} ({r.Detail})");
    }

    Console.WriteLine($"{results.Count - failed}/{results.Count} suite entries passed");
    return failed == 0 ? 0 : 1;
}

static int RunSelfPlay(string[] args)
{
    int games = Args.ReadInt(args, "--games", 40);
    int timeA = Args.ReadInt(args, "--timeA", 2000);
    int timeB = Args.ReadInt(args, "--timeB", 500);
    bool legacyB = args.Contains("--legacyB"); // B plays with the legacy (pre-P3) eval
    bool legacySearchB = args.Contains("--legacySearchB"); // B plays with the legacy search
    bool legacySearchA = args.Contains("--legacySearchA"); // A plays with the legacy search (bisection)
    int seed = Args.ReadInt(args, "--seed", 42);
    int openings = Args.ReadInt(args, "--openings", 0); // random plies before the engines take over

    var engineA = new MinimaxEngine(TimeSpan.FromMilliseconds(timeA), legacySearch: legacySearchA);
    var engineB = new MinimaxEngine(TimeSpan.FromMilliseconds(timeB), legacyEval: legacyB, legacySearch: legacySearchB);

    int winsA = 0;
    int winsB = 0;
    int draws = 0;
    const int maxMoves = 400;

    for (int g = 0; g < games; g++)
    {
        bool aIsBlue = g % 2 == 0; // alternate colors to cancel the first-move advantage
        var state = GameState.CreateInitial();
        int moves = 0;

        // Deterministic per-game opening variety (default 0 keeps the classic
        // start-position protocol byte-identical)
        var rng = new Random(seed * 397 + g);
        for (int o = 0; o < openings && state.Status == GameStatus.InProgress; o++)
        {
            var legal = MoveGenerator.GenerateLegalMoves(state, state.CurrentTurn);
            if (legal.Count == 0)
                break;
            state = GameController.ApplyMove(state, legal[rng.Next(legal.Count)]);
        }

        while (state.Status == GameStatus.InProgress && moves < maxMoves)
        {
            var engine = (state.CurrentTurn == Player.Blue) == aIsBlue ? engineA : engineB;
            var move = engine.FindBestMove(state);
            if (move == null)
                break;
            state = GameController.ApplyMove(state, move.Value);
            moves++;
        }

        switch (state.Status)
        {
            case GameStatus.BlueWins:
                if (aIsBlue) winsA++; else winsB++;
                break;
            case GameStatus.RedWins:
                if (aIsBlue) winsB++; else winsA++;
                break;
            default:
                draws++;
                break;
        }

        Console.WriteLine($"Game {g + 1}: {state.Status} after {moves} moves");
    }

    int decisive = winsA + winsB;
    string rateA = decisive > 0 ? $"{100.0 * winsA / decisive:F0}% of decisive" : "n/a";
    Console.WriteLine();
    Console.WriteLine($"A ({timeA} ms/move, v2): {winsA} wins | B ({timeB} ms/move," +
        $" {(legacyB ? "legacy eval" : "v2 eval")}{(legacySearchB ? ", legacy search" : "")}): {winsB} wins | {draws} draws");
    Console.WriteLine($"A win rate: {rateA}");
    return 0;
}

static void PrintUsage()
{
    Console.WriteLine("Usage:");
    Console.WriteLine("  JungleGame.Bench --bench [--time <ms>] [--depth <n>]");
    Console.WriteLine("  JungleGame.Bench --bench --positions <file.tsuite> [--baseline <file>] [--write-baseline]");
    Console.WriteLine("  JungleGame.Bench --selfplay [--games <n>] [--timeA <ms>] [--timeB <ms>]");
    Console.WriteLine("                     [--legacyB] [--legacySearchB] [--seed <n>] [--openings <n>]");
    Console.WriteLine("  JungleGame.Bench --arena [--games <n>] [--timeA <ms>] [--timeB <ms>] [--legacyB]");
    Console.WriteLine("                     [--legacySearchA] [--legacySearchB] [--seed <n>] [--smoke]");
    Console.WriteLine("                     [--openings-file <path>] [--openings-imbalanced <n>]");
    Console.WriteLine("  JungleGame.Bench --testsuite [--file <path.tsuite>]");
}
