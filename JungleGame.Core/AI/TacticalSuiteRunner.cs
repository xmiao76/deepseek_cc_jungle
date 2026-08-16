using JungleGame.Core.Engine;

namespace JungleGame.Core.AI;

/// <summary>
/// Runs tactical test-suite entries at a fixed search depth (deterministic).
/// Shared by the Bench runner (full report + exit code) and the xUnit
/// regression suite (asserts every entry passes).
/// </summary>
public sealed record SuiteRunResult(TestSuiteEntry Entry, Move? ActualMove, bool Passed, string Detail);

public static class TacticalSuiteRunner
{
    /// <summary>
    /// Generous wall-clock budget; fixed-depth searches finish far earlier, so
    /// the time limit never influences the result and runs stay deterministic.
    /// </summary>
    public const int TimeBudgetMs = 10_000;

    public static SuiteRunResult Run(TestSuiteEntry entry)
    {
        // Tablebase probing disabled: the suite pins the pure-search behavior
        // (with the table loaded, a proven-but-slower capture can legitimately
        // outrank a pinned mate move via the forced-win early exit).
        var engine = new MinimaxEngine(TimeSpan.FromMilliseconds(TimeBudgetMs), entry.SearchDepth, useTablebase: false);
        Move? move = engine.FindBestMove(entry.State);

        if (move == null)
            return new SuiteRunResult(entry, null, false, "no legal move found");

        var actual = move.Value;
        if (entry.ExpectedMove != null)
        {
            bool matched = SameSquareMove(actual, entry.ExpectedMove.Value);
            return new SuiteRunResult(entry, actual, matched,
                matched ? "found expected move" : $"played {actual.From}→{actual.To}");
        }

        if (entry.ForbiddenMove != null)
        {
            bool avoided = !SameSquareMove(actual, entry.ForbiddenMove.Value);
            return new SuiteRunResult(entry, actual, avoided,
                avoided ? "avoided forbidden move" : $"played forbidden {actual.From}→{actual.To}");
        }

        // Bench-only entry: any legal move passes.
        return new SuiteRunResult(entry, actual, true, "legal move");
    }

    public static IReadOnlyList<SuiteRunResult> RunAll(IEnumerable<TestSuiteEntry> entries)
    {
        var results = new List<SuiteRunResult>();
        foreach (var entry in entries)
            results.Add(Run(entry));
        return results;
    }

    private static bool SameSquareMove(Move a, Move b) => a.From == b.From && a.To == b.To;
}
