using System.Diagnostics;
using JungleGame.Core.AI;
using JungleGame.Core.Engine;
using JungleGame.Core.Model;

// JungleGame.Bench — engine performance and self-play harness.
//
//   --bench [--time <ms>] [--depth <n>]         single search from the start
//                                               position: nodes, nodes/s, depth
//   --selfplay [--games <n>] [--timeA <ms>]
//              [--timeB <ms>] [--seed <n>]      tournament of the current engine
//                                               against itself at two time budgets
//                                               (alternating colors; sanity: more
//                                               time should win clearly)

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
    default:
        PrintUsage();
        return 1;
}

static int RunBench(string[] args)
{
    int timeMs = ReadIntArg(args, "--time", 2000);
    int? maxDepth = TryReadIntArg(args, "--depth");

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

static int RunSelfPlay(string[] args)
{
    int games = ReadIntArg(args, "--games", 40);
    int timeA = ReadIntArg(args, "--timeA", 2000);
    int timeB = ReadIntArg(args, "--timeB", 500);
    bool legacyB = args.Contains("--legacyB"); // B plays with the legacy (pre-P3) eval

    var engineA = new MinimaxEngine(TimeSpan.FromMilliseconds(timeA));
    var engineB = new MinimaxEngine(TimeSpan.FromMilliseconds(timeB), legacyEval: legacyB);

    int winsA = 0;
    int winsB = 0;
    int draws = 0;
    const int maxMoves = 400;

    for (int g = 0; g < games; g++)
    {
        bool aIsBlue = g % 2 == 0; // alternate colors to cancel the first-move advantage
        var state = GameState.CreateInitial();
        int moves = 0;

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

    Console.WriteLine();
    Console.WriteLine($"A ({timeA} ms/move, v2 eval): {winsA} wins, {draws} draws | B ({timeB} ms/move, {(legacyB ? "legacy" : "v2")} eval): {winsB} wins");
    return 0;
}

static void PrintUsage()
{
    Console.WriteLine("Usage:");
    Console.WriteLine("  JungleGame.Bench --bench [--time <ms>] [--depth <n>]");
    Console.WriteLine("  JungleGame.Bench --selfplay [--games <n>] [--timeA <ms>] [--timeB <ms>] [--legacyB]");
}

static int ReadIntArg(string[] args, string name, int fallback)
{
    for (int i = 0; i < args.Length - 1; i++)
    {
        if (args[i] == name && int.TryParse(args[i + 1], out int value))
            return value;
    }
    return fallback;
}

static int? TryReadIntArg(string[] args, string name)
{
    for (int i = 0; i < args.Length - 1; i++)
    {
        if (args[i] == name && int.TryParse(args[i + 1], out int value))
            return value;
    }
    return null;
}
