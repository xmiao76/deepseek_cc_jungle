namespace JungleGame.Core.AI;

/// <summary>
/// Extracts the evaluation feature vector from a SearchBoard, from the
/// perspective of a given side. The extraction logic is a straight
/// restructuring of the pre-refactor EvaluationFunction loops — one pass over
/// both sides' pieces, with the global den-threat terms computed afterwards.
/// The legacyEval flag suppresses the P3 features (doomed piece bonus, den
/// escort) exactly as the old code did, including skipping the den-threatened
/// flag scan.
/// </summary>
internal static class EvalFeatureExtractor
{
    internal const int EndgamePieceCount = 8;         // endgame threshold (total pieces)
    internal const int DevelopmentMaterialGate = 20;  // rank-sum gate for the back-rank penalty

    private const byte WaterTerrain = 1;

    internal static EvalFeatures ExtractStatic(SearchBoard board, int side, bool legacyEval)
    {
        var f = new EvalFeatures();

        int totalPieces = board.PieceCount(0) + board.PieceCount(1);
        bool isEndgame = totalPieces <= EndgamePieceCount;
        int totalMaterial = TotalMaterialOnBoard(board);

        // Den escort triggers, per den: is an enemy piece close enough to invade
        // Blue's den (3,0) or Red's den (3,8)? Each side's escort bonus fires only
        // for pieces guarding a den that is actually threatened.
        bool blueDenThreatened = false; // a Red piece within 3 of (3,0)
        bool redDenThreatened = false;  // a Blue piece within 3 of (3,8)
        if (!legacyEval)
        {
            foreach (byte enemy in board.PieceIds(1)) // Red
            {
                if (ManhattanDistance(board.SquareOf(enemy), 3) <= 3)
                {
                    blueDenThreatened = true;
                    break;
                }
            }
            foreach (byte enemy in board.PieceIds(0)) // Blue
            {
                if (ManhattanDistance(board.SquareOf(enemy), 59) <= 3)
                {
                    redDenThreatened = true;
                    break;
                }
            }
        }

        for (int s = 0; s < 2; s++)
        {
            bool ownDenThreatened = s == 0 ? blueDenThreatened : redDenThreatened;
            bool forMe = s == side;
            foreach (byte id in board.PieceIds(s))
                AccumulatePiece(ref f, board, id, isEndgame, legacyEval, ownDenThreatened, forMe, totalMaterial);
        }

        // Den threat detection: opponent pieces near the scored side's den
        // without defenders nearby
        int ownDenCol = 3, ownDenRow = side == 0 ? 0 : 8;
        int defendersNearDen = 0;
        int attackersNearDen = 0;

        for (int s = 0; s < 2; s++)
        {
            foreach (byte id in board.PieceIds(s))
            {
                int sq = board.SquareOf(id);
                int col = sq % 7;
                int row = sq / 7;
                if (Math.Abs(col - ownDenCol) + Math.Abs(row - ownDenRow) <= 2)
                {
                    if (s == side)
                        defendersNearDen++;
                    else
                        attackersNearDen++;
                }
            }
        }

        f.DenThreatExcess = Math.Max(0, attackersNearDen - defendersNearDen);
        if (isEndgame && attackersNearDen > 0 && defendersNearDen == 0)
            f.EndgameDenThreat = 1;

        return f;
    }

    private static void AccumulatePiece(
        ref EvalFeatures f, SearchBoard board, byte id, bool isEndgame,
        bool legacyEval, bool ownDenThreatened, bool forMe, int totalMaterial)
    {
        int rank = SearchBoard.RankOf[id];
        int owner = ((id - 1) % SearchBoard.DistinctPieceKinds) & 1;
        int sq = board.SquareOf(id);
        int row = sq / 7;
        int col = sq % 7;

        if (forMe) f.MaterialMy += rank; else f.MaterialOpp += rank;

        // Forward progression bonus
        int forward = ForwardBonus(rank, row, owner);
        if (forMe) f.ForwardMy += forward; else f.ForwardOpp += forward;

        // Den proximity
        int ownDenCol = 3, ownDenRow = owner == 0 ? 0 : 8;
        int oppDenCol = 3, oppDenRow = owner == 0 ? 8 : 0;
        int distToOppDen = Math.Abs(col - oppDenCol) + Math.Abs(row - oppDenRow);
        int distToOwnDen = Math.Abs(col - ownDenCol) + Math.Abs(row - ownDenRow);

        if (distToOppDen <= 3)
        {
            int offense = (4 - distToOppDen);
            if (forMe) f.DenOffenseMy += offense; else f.DenOffenseOpp += offense;
        }
        if (distToOwnDen <= 2)
        {
            int guard = (3 - distToOwnDen);
            if (forMe) f.DenGuardMy += guard; else f.DenGuardOpp += guard;
        }

        // Trap penalty: piece on enemy trap, plus a "doomed piece" bonus for the
        // opponent — a trapped piece can be captured by anything.
        if (SearchBoard.EffectiveRankOf(id, sq) == 0)
        {
            if (forMe) f.TrapMy++; else f.TrapOpp++;
            if (!legacyEval)
            {
                if (forMe) f.DoomedRankSumMy += rank; else f.DoomedRankSumOpp += rank;
            }
        }

        // Den escort: a strong piece guarding its own den is worth more while an
        // enemy piece is close enough to invade that den
        if (!legacyEval && rank >= 7 && ownDenThreatened && distToOwnDen <= 2)
        {
            if (forMe) f.EscortMy++; else f.EscortOpp++;
        }

        // Lion/Tiger river bank and jump-path bonuses
        if (rank == 7 || rank == 6)
        {
            if (IsRiverBank(sq))
            {
                if (forMe) f.RiverBankMy++; else f.RiverBankOpp++;
            }

            int oppDenSq = owner == 0 ? 59 : 3; // (3,8) for Blue, (3,0) for Red
            if (HasRiverBetween(sq, oppDenSq))
            {
                if (forMe) f.JumpPathMy++; else f.JumpPathOpp++;
            }
        }

        // Rat near water bonus
        if (rank == 1)
        {
            if (IsAdjacentToWater(sq))
            {
                if (forMe) f.RatNearWaterMy++; else f.RatNearWaterOpp++;
            }
            if (SearchBoard.TerrainOf[sq] == WaterTerrain)
            {
                if (forMe) f.RatInWaterMy++; else f.RatInWaterOpp++;
            }
        }

        // Elephant safety: penalty for being near opponent's Rat
        if (rank == 8)
        {
            int opp = owner ^ 1;
            int fear = 0;
            foreach (byte enemy in board.PieceIds(opp))
            {
                int enemySq = board.SquareOf(enemy);
                if (SearchBoard.RankOf[enemy] == 1 && SearchBoard.TerrainOf[enemySq] != WaterTerrain)
                {
                    int dist = ManhattanDistance(sq, enemySq);
                    if (dist <= 3)
                        fear += 4 - dist;
                }
            }
            if (forMe) f.ElephantFearMy += fear; else f.ElephantFearOpp += fear;
        }

        // Threat analysis: being threatened by equal or stronger adjacent enemy
        foreach (byte neighborSq in SearchBoard.Neighbors[sq])
        {
            byte enemy = board.Occupant(neighborSq);
            if (enemy == 0 || ((enemy - 1) & 1) == owner)
                continue;

            int ourEffRank = SearchBoard.EffectiveRankOf(id, sq);
            int enemyEffRank = SearchBoard.EffectiveRankOf(enemy, neighborSq);
            int enemyRank = SearchBoard.RankOf[enemy];

            // Rat threatens Elephant
            if (rank == 8 && enemyRank == 1)
            {
                if (SearchBoard.TerrainOf[neighborSq] != WaterTerrain)
                {
                    if (forMe) f.RatElephantMy++; else f.RatElephantOpp++;
                }
            }
            else if (enemyEffRank >= ourEffRank)
            {
                if (SearchBoard.CanCapture(enemy, neighborSq, id, sq))
                {
                    if (enemyEffRank > ourEffRank)
                    {
                        if (forMe) f.StrongerThreatSumMy += rank; else f.StrongerThreatSumOpp += rank;
                    }
                    else
                    {
                        if (forMe) f.EqualThreatSumMy += rank; else f.EqualThreatSumOpp += rank;
                    }
                }
            }
        }

        // Endgame: bonus for advancing toward den more aggressively
        if (isEndgame && rank >= 5) // Leopard+
        {
            if (distToOppDen <= 2)
            {
                int advance = 3 - distToOppDen;
                if (forMe) f.EndgameAdvanceMy += advance; else f.EndgameAdvanceOpp += advance;
            }
        }

        // Penalty for being on the back rank too long (encourages development)
        int homeRow = owner == 0 ? 0 : 8;
        if (row == homeRow && totalMaterial > DevelopmentMaterialGate)
        {
            if (forMe) f.BackRankMy++; else f.BackRankOpp++;
        }
    }

    private static int TotalMaterialOnBoard(SearchBoard board)
    {
        int total = 0;
        foreach (byte id in board.PieceIds(0))
            total += SearchBoard.RankOf[id];
        foreach (byte id in board.PieceIds(1))
            total += SearchBoard.RankOf[id];
        return total;
    }

    // Positional bonuses: bonus for forward progression toward opponent's den.
    // Blue advances toward high rows, Red toward low rows.
    private static int ForwardBonus(int rank, int row, int owner)
    {
        int progress = owner == 0 ? row : (8 - row);

        // Aggressive pieces (Lion, Tiger, Leopard) get more forward bonus
        int aggression = rank switch
        {
            7 => 6, // Lion
            6 => 4, // Tiger
            5 => 4, // Leopard
            4 => 3, // Wolf
            3 => 3, // Dog
            2 => 2, // Cat
            1 => 2, // Rat
            8 => 1, // Elephant prefers to stay safe
            _ => 2
        };

        return progress * aggression;
    }

    private static int ManhattanDistance(int sqA, int sqB) =>
        Math.Abs(sqA % 7 - sqB % 7) + Math.Abs(sqA / 7 - sqB / 7);

    private static bool IsRiverBank(int sq)
    {
        if (SearchBoard.TerrainOf[sq] == WaterTerrain) return false;

        foreach (byte adj in SearchBoard.Neighbors[sq])
        {
            if (SearchBoard.TerrainOf[adj] == WaterTerrain)
                return true;
        }
        return false;
    }

    private static bool IsAdjacentToWater(int sq)
    {
        foreach (byte adj in SearchBoard.Neighbors[sq])
        {
            if (SearchBoard.TerrainOf[adj] == WaterTerrain)
                return true;
        }
        return false;
    }

    private static bool HasRiverBetween(int fromSq, int toSq)
    {
        int fc = fromSq % 7, fr = fromSq / 7;
        int tc = toSq % 7, tr = toSq / 7;

        int stepCol = Math.Sign(tc - fc);
        int stepRow = Math.Sign(tr - fr);

        int col = fc + stepCol;
        int row = fr + stepRow;

        bool foundWater = false;
        while (col != tc || row != tr)
        {
            if (col >= 0 && col <= 6 && row >= 0 && row <= 8 &&
                SearchBoard.TerrainOf[row * 7 + col] == WaterTerrain)
                foundWater = true;

            col += stepCol;
            row += stepRow;

            if (Math.Abs(col - fc) > Math.Abs(tc - fc) && Math.Abs(row - fr) > Math.Abs(tr - fr))
                break;
        }

        return foundWater;
    }
}
