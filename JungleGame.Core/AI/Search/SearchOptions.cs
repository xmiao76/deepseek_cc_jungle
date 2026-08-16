namespace JungleGame.Core.AI;

/// <summary>
/// Immutable engine configuration flags (the A/B strength-test toggles).
/// </summary>
internal readonly struct SearchOptions
{
    internal readonly bool LegacyEval;   // disables the P3 evaluation terms
    internal readonly bool LegacySearch; // disables the post-P3 search features

    internal SearchOptions(bool legacyEval, bool legacySearch)
    {
        LegacyEval = legacyEval;
        LegacySearch = legacySearch;
    }
}
