namespace JungleGame.Core.AI;

/// <summary>
/// Immutable engine configuration flags (the A/B strength-test toggles).
/// </summary>
internal readonly struct SearchOptions
{
    internal readonly bool LegacyEval;     // disables the P3 evaluation terms
    internal readonly bool LegacySearch;   // disables the post-P3 search features
    internal readonly bool EnableTablebase; // probes the endgame tables (default on)
    internal readonly int Contempt;        // draw-avoidance bias in centipawns (0 = off)
    internal readonly EvalParameters? EvalWeights; // explicit weight vector (A/B gate); null = process-wide

    internal SearchOptions(
        bool legacyEval, bool legacySearch, bool enableTablebase = true, int contempt = 30,
        EvalParameters? evalWeights = null)
    {
        LegacyEval = legacyEval;
        LegacySearch = legacySearch;
        EnableTablebase = enableTablebase;
        Contempt = contempt;
        EvalWeights = evalWeights;
    }
}
