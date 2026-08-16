namespace JungleGame.Core.AI;

/// <summary>
/// Side-relative evaluation features (integer counts/sums from the perspective
/// of the side being scored: "My" fields accumulate that side's pieces, "Opp"
/// fields the opponent's). The evaluation score is the dot product of this
/// vector with <see cref="EvalParameters"/> — the linear structure is what the
/// P3 data-driven tuning harness optimizes.
/// </summary>
internal struct EvalFeatures
{
    internal int MaterialMy; // sum of ranks
    internal int MaterialOpp;
    internal int ForwardMy;  // sum of progress × per-rank aggression
    internal int ForwardOpp;
    internal int DenOffenseMy; // sum of (4 - distToOppDen), dist ≤ 3
    internal int DenOffenseOpp;
    internal int DenGuardMy;   // sum of (3 - distToOwnDen), dist ≤ 2
    internal int DenGuardOpp;
    internal int TrapMy;       // count of pieces standing on the enemy trap
    internal int TrapOpp;
    internal int DoomedRankSumMy; // sum of ranks of trapped pieces (P3 term)
    internal int DoomedRankSumOpp;
    internal int EscortMy;     // Lion/Tiger within 2 of own threatened den (P3 term)
    internal int EscortOpp;
    internal int RiverBankMy;  // Lion/Tiger on a bank square
    internal int RiverBankOpp;
    internal int JumpPathMy;   // Lion/Tiger with a clear jump toward the opp den
    internal int JumpPathOpp;
    internal int RatNearWaterMy;
    internal int RatNearWaterOpp;
    internal int RatInWaterMy;
    internal int RatInWaterOpp;
    internal int ElephantFearMy; // sum of (4 - dist) to enemy land Rats, dist ≤ 3
    internal int ElephantFearOpp;
    internal int StrongerThreatSumMy; // sum of ranks of pieces threatened by a strictly stronger enemy
    internal int StrongerThreatSumOpp;
    internal int EqualThreatSumMy;    // sum of ranks of pieces threatened by an equal enemy
    internal int EqualThreatSumOpp;
    internal int RatElephantMy; // Elephants adjacent to an enemy Rat on land
    internal int RatElephantOpp;
    internal int DenThreatExcess; // max(0, attackers - defenders) near the scored side's den
    internal int EndgameDenThreat; // 1 when endgame with attackers and zero defenders near the den
    internal int EndgameAdvanceMy; // sum of (3 - distToOppDen) for Leopard+ near the den (endgame)
    internal int EndgameAdvanceOpp;
    internal int BackRankMy; // pieces on the home row (development gate passed)
    internal int BackRankOpp;
}
