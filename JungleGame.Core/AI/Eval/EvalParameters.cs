namespace JungleGame.Core.AI;

/// <summary>
/// Evaluation term weights — the tunable vector for the P3 data-driven tuning
/// harness. Features are the side-relative integer counts extracted by
/// <see cref="EvalFeatureExtractor"/>; the score is the dot product. Today's
/// values are the hand-tuned constants the evaluation has always carried;
/// the legacyEval variant matches the pre-P3 eval (the P3 doomed/escort
/// features are suppressed by the extractor, so the same weights apply).
/// </summary>
internal static class EvalParameters
{
    internal const int MaterialWeight = 100;                 // per rank
    internal const int ForwardWeight = 1;                    // per progress × aggression point
    internal const int DenOffenseWeight = 12;                // per (4 - distToOppDen), dist ≤ 3
    internal const int DenGuardWeight = 8;                   // per (3 - distToOwnDen), dist ≤ 2
    internal const int TrapPenalty = 80;                     // per piece on an enemy trap
    internal const int DoomedPieceWeightPerRank = 5;         // per trapped piece rank (40 × rank/8)
    internal const int DenEscortBonus = 30;                  // per Lion/Tiger guarding a threatened den
    internal const int RiverBankBonus = 15;                  // per Lion/Tiger on a bank square
    internal const int JumpPathBonus = 10;                   // per Lion/Tiger with a jump toward the opp den
    internal const int RatNearWaterBonus = 8;                // per Rat adjacent to water
    internal const int RatInWaterBonus = 12;                 // per Rat in the water
    internal const int ElephantRatFearWeight = 15;           // per (4 - dist) to enemy Rat, dist ≤ 3
    internal const int ThreatStrongerWeight = 15;            // per rank threatened by a strictly stronger piece
    internal const int ThreatEqualWeight = 8;                // per rank threatened by an equal piece
    internal const int RatThreatensElephantPenalty = 25;     // per Elephant adjacent to enemy Rat on land
    internal const int MobilityWeight = 3;                   // per extra legal move over the opponent
    internal const int DenThreatWeight = 40;                 // per excess attacker near own den
    internal const int EndgameDenThreatPenalty = 200;        // endgame: attackers with zero defenders
    internal const int EndgameAdvanceWeight = 25;            // per (3 - dist) for Leopard+, dist ≤ 2
    internal const int BackRankPenalty = 5;                  // per undeveloped piece on the home row

    internal static int Dot(in EvalFeatures f)
    {
        int score =
            (f.MaterialMy - f.MaterialOpp) * MaterialWeight +
            (f.ForwardMy - f.ForwardOpp) * ForwardWeight +
            (f.DenOffenseMy - f.DenOffenseOpp) * DenOffenseWeight +
            (f.DenGuardMy - f.DenGuardOpp) * DenGuardWeight +
            (-f.TrapMy + f.TrapOpp) * TrapPenalty +
            (-f.DoomedRankSumMy + f.DoomedRankSumOpp) * DoomedPieceWeightPerRank +
            (f.EscortMy - f.EscortOpp) * DenEscortBonus +
            (f.RiverBankMy - f.RiverBankOpp) * RiverBankBonus +
            (f.JumpPathMy - f.JumpPathOpp) * JumpPathBonus +
            (f.RatNearWaterMy - f.RatNearWaterOpp) * RatNearWaterBonus +
            (f.RatInWaterMy - f.RatInWaterOpp) * RatInWaterBonus +
            (-f.ElephantFearMy + f.ElephantFearOpp) * ElephantRatFearWeight +
            (-f.StrongerThreatSumMy + f.StrongerThreatSumOpp) * ThreatStrongerWeight +
            (-f.EqualThreatSumMy + f.EqualThreatSumOpp) * ThreatEqualWeight +
            (-f.RatElephantMy + f.RatElephantOpp) * RatThreatensElephantPenalty +
            -f.DenThreatExcess * DenThreatWeight -
            f.EndgameDenThreat * EndgameDenThreatPenalty +
            (f.EndgameAdvanceMy - f.EndgameAdvanceOpp) * EndgameAdvanceWeight +
            (-f.BackRankMy + f.BackRankOpp) * BackRankPenalty;
        return score;
    }
}
