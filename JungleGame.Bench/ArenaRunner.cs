using JungleGame.Core.AI;
using JungleGame.Core.Engine;
using JungleGame.Core.Model;

namespace JungleGame.Bench;

/// <summary>
/// Engine-match arena: every opening is played twice with colors swapped, with
/// fresh engine instances per game (uncorrelated transposition tables). Reports
/// the decisive win rate with a Wilson 95% confidence interval and an exact
/// binomial p-value; gates on >= 55% of decisive games with a pre-committed
/// decisive floor. Exit codes: 0 = pass, 1 = fail, 2 = inconclusive (0 with
/// --smoke). Sequential games keep results fully reproducible for a fixed seed.
/// </summary>
internal static class ArenaRunner
{
    private const int MaxMoves = 400;

    internal static int Run(string[] args)
    {
        int timeA = Args.ReadInt(args, "--timeA", 2000);
        int timeB = Args.ReadInt(args, "--timeB", 2000);
        bool legacyB = args.Contains("--legacyB");
        bool legacySearchA = args.Contains("--legacySearchA");
        bool legacySearchB = args.Contains("--legacySearchB");
        bool noTablebaseA = args.Contains("--noTablebaseA");
        bool noTablebaseB = args.Contains("--noTablebaseB");
        int seed = Args.ReadInt(args, "--seed", 42);
        int games = Args.ReadInt(args, "--games", 120);
        string? openingsFile = Args.ReadString(args, "--openings-file");
        int imbalanced = Args.ReadInt(args, "--openings-imbalanced", 0);
        bool smoke = args.Contains("--smoke");
        int contemptA = Args.ReadInt(args, "--contemptA", 30);
        int contemptB = Args.ReadInt(args, "--contemptB", 30);

        var openings = Openings.Load(openingsFile, imbalanced, seed);
        int pairs = openingsFile != null || imbalanced > 0
            ? openings.Count
            : Math.Max(1, games / 2);

        string source = openingsFile ?? (imbalanced > 0 ? $"imbalanced ×{imbalanced}" : "classic start");
        Console.WriteLine($"Arena: {pairs * 2} games = {pairs} openings × 2 colors " +
            $"(A {timeA} ms, B {timeB} ms{(legacyB ? ", B legacy eval" : "")}" +
            $"{(legacySearchA ? ", A legacy search" : "")}{(legacySearchB ? ", B legacy search" : "")}" +
            $"{(noTablebaseA ? ", A tablebase off" : "")}{(noTablebaseB ? ", B tablebase off" : "")}" +
            $", contempt A={contemptA} B={contemptB})");
        Console.WriteLine($"Openings: {source} (seed {seed})");
        Console.WriteLine();

        int winsA = 0, winsB = 0, draws = 0;
        for (int o = 0; o < pairs; o++)
        {
            var opening = openings[o];
            for (int color = 0; color < 2; color++)
            {
                bool aIsBlue = color == 0;
                var (wA, wB, d, moves, status) = PlayGame(
                    opening, aIsBlue, timeA, timeB, legacyB, legacySearchA, legacySearchB,
                    noTablebaseA, noTablebaseB, contemptA, contemptB);

                winsA += wA;
                winsB += wB;
                draws += d;
                Console.WriteLine($"Game {o * 2 + color + 1}/{pairs * 2}: {status} " +
                    $"{(aIsBlue ? "A=Blue" : "A=Red")} after {moves} moves");
            }
        }

        int decisive = winsA + winsB;
        var (lo, hi) = ArenaStats.WilsonCi(winsA, decisive);
        double rate = decisive > 0 ? (double)winsA / decisive : 0;
        double p = ArenaStats.BinomialPValue(winsA, decisive);
        int exit = ArenaStats.ExitCode(winsA, decisive, smoke);

        Console.WriteLine();
        Console.WriteLine($"Summary: A {winsA} wins | B {winsB} wins | {draws} draws");
        Console.WriteLine($"Decisive: {decisive} (floor {ArenaStats.MinDecisive})");
        Console.WriteLine($"A decisive win rate: {rate:P0} " +
            $"(Wilson 95% CI [{lo:P0}, {hi:P0}], p = {p:F3} two-tailed)");
        Console.WriteLine(exit switch
        {
            0 => "GATE: PASS",
            1 => $"GATE: FAIL (below {ArenaStats.PassRate:P0} of decisive)",
            _ => "GATE: INCONCLUSIVE (too few decisive games)",
        });
        return exit;
    }

    private static (int WinsA, int WinsB, int Draws, int Moves, GameStatus Status) PlayGame(
        GameState opening, bool aIsBlue, int timeA, int timeB,
        bool legacyB, bool legacySearchA, bool legacySearchB,
        bool noTablebaseA, bool noTablebaseB, int contemptA, int contemptB)
    {
        // Fresh engines per game: uncorrelated results (the shared-TT protocol of
        // the legacy --selfplay mode biases later games).
        // NOTE: --legacySearchB also disables tablebase probing, so an isolated
        // tablebase A/B needs --noTablebaseA/--noTablebaseB instead.
        var engineA = new MinimaxEngine(
            TimeSpan.FromMilliseconds(timeA), legacySearch: legacySearchA,
            useTablebase: !noTablebaseA, contempt: contemptA);
        var engineB = new MinimaxEngine(
            TimeSpan.FromMilliseconds(timeB), legacyEval: legacyB, legacySearch: legacySearchB,
            useTablebase: !noTablebaseB, contempt: contemptB);

        var state = opening;
        int moves = 0;
        while (state.Status == GameStatus.InProgress && moves < MaxMoves)
        {
            var engine = (state.CurrentTurn == Player.Blue) == aIsBlue ? engineA : engineB;
            var move = engine.FindBestMove(state);
            if (move == null)
                break;
            state = GameController.ApplyMove(state, move.Value);
            moves++;
        }

        int winsA = 0, winsB = 0, draws = 0;
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

        return (winsA, winsB, draws, moves, state.Status);
    }
}
