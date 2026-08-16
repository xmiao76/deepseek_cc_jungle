namespace JungleGame.Bench;

/// <summary>
/// Statistical reporting and gate logic for the arena: Wilson 95% confidence
/// interval on the decisive win rate, an exact two-tailed binomial p-value
/// (H0: win rate = 50%), and the pass/fail/inconclusive exit-code matrix.
/// </summary>
internal static class ArenaStats
{
    internal const double PassRate = 0.55;
    internal const int MinDecisive = 24;

    internal static (double Low, double High) WilsonCi(int wins, int decisive, double z = 1.959964)
    {
        if (decisive == 0)
            return (0, 1);
        double n = decisive;
        double p = (double)wins / n;
        double denom = 1 + (z * z) / n;
        double center = (p + (z * z) / (2 * n)) / denom;
        double half = z * Math.Sqrt(p * (1 - p) / n + (z * z) / (4 * n * n)) / denom;
        return (Math.Max(0, center - half), Math.Min(1, center + half));
    }

    /// <summary>
    /// Two-tailed exact binomial p-value for P(X ≤ wins) under Bin(decisive, 0.5),
    /// computed by forward recurrence (stable for the arena's game counts).
    /// </summary>
    internal static double BinomialPValue(int wins, int decisive)
    {
        if (decisive == 0)
            return 1;
        double pmf = Math.Pow(0.5, decisive);
        double cdf = pmf;
        for (int k = 0; k < wins; k++)
        {
            pmf *= (double)(decisive - k) / (k + 1);
            cdf += pmf;
        }
        double upperTail = 1 - (cdf - pmf); // P(X >= wins)
        return Math.Min(1.0, 2 * Math.Min(cdf, upperTail));
    }

    /// <summary>
    /// Gate: 0 = pass (>= 55% of decisive with enough decisive games),
    /// 1 = fail, 2 = inconclusive (too few decisive games; 0 with --smoke).
    /// </summary>
    internal static int ExitCode(int winsA, int decisive, bool smoke)
    {
        if (decisive < MinDecisive)
            return smoke ? 0 : 2;
        return (double)winsA / decisive >= PassRate ? 0 : 1;
    }
}
