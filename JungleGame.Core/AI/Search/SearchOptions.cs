namespace JungleGame.Core.AI;

/// <summary>
/// Immutable engine configuration flags (the A/B strength-test toggles).
/// </summary>
internal readonly struct SearchOptions
{
    internal readonly bool LegacyEval;     // disables the P3 evaluation terms
    internal readonly bool LegacySearch;   // disables the post-P3 search features
    internal readonly bool EnableTablebase; // probes the endgame tables (default on)

    internal SearchOptions(bool legacyEval, bool legacySearch, bool enableTablebase = true)
    {
        LegacyEval = legacyEval;
        LegacySearch = legacySearch;
        EnableTablebase = enableTablebase;
    }
}
