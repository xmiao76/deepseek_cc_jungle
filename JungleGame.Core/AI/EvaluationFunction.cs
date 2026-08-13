using JungleGame.Core.Engine;
using JungleGame.Core.Model;
using JungleGame.Core.Rules;

namespace JungleGame.Core.AI;

public static class EvaluationFunction
{
    // Material values in centipawns
    private static readonly int[] MaterialValue = { 0, 100, 200, 300, 400, 500, 600, 700, 800 };
    // Indexed by (int)Animal: Rat=1..Elephant=8

    // Positional bonuses: bonus for forward progression toward opponent's den
    // Blue advances toward high rows, Red toward low rows
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

    // Den proximity: bonus for being near opponent's den, penalty for enemy near our den
    private static int DenProximity(int sq, int owner)
    {
        int col = sq % 7;
        int row = sq / 7;
        int ownDenCol = 3, ownDenRow = owner == 0 ? 0 : 8;
        int oppDenCol = 3, oppDenRow = owner == 0 ? 8 : 0;

        int distToOwnDen = Math.Abs(col - ownDenCol) + Math.Abs(row - ownDenRow);
        int distToOppDen = Math.Abs(col - oppDenCol) + Math.Abs(row - oppDenRow);

        int score = 0;

        // Bonus for being near opponent's den (offensive)
        if (distToOppDen <= 3)
            score += (4 - distToOppDen) * 12;

        // Bonus for guarding own den (defensive)
        if (distToOwnDen <= 2)
            score += (3 - distToOwnDen) * 8;

        return score;
    }

    private static int ManhattanDistance(int sqA, int sqB) =>
        Math.Abs(sqA % 7 - sqB % 7) + Math.Abs(sqA / 7 - sqB / 7);

    /// <summary>
    /// Fast-path evaluation on the internal search board. Mobility counts are supplied
    /// by the caller (the side-to-move count comes from move generation it already
    /// performed; the opponent count from one cheap SearchBoard.CountLegalMoves call).
    /// With legacyEval, the P3 terms (doomed-piece bonus, den escort) are disabled —
    /// used only by the self-play harness for A/B strength validation.
    /// </summary>
    internal static int Evaluate(SearchBoard board, int side, int myMobility, int oppMobility, bool legacyEval = false)
    {
        int score = 0;

        int totalPieces = board.PieceCount(0) + board.PieceCount(1);
        bool isEndgame = totalPieces <= 8;

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
            foreach (byte id in board.PieceIds(s))
            {
                int sq = board.SquareOf(id);
                bool ownDenThreatened = s == 0 ? blueDenThreatened : redDenThreatened;
                int pieceScore = EvaluatePiece(board, id, sq, isEndgame, legacyEval, ownDenThreatened);
                score += s == side ? pieceScore : -pieceScore;
            }
        }

        // Mobility: small bonus per legal move
        score += (myMobility - oppMobility) * 3;

        // Den threat detection: opponent piece near our den without defender nearby
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

        if (attackersNearDen > defendersNearDen)
            score -= 40 * (attackersNearDen - defendersNearDen);
        if (isEndgame && attackersNearDen > 0 && defendersNearDen == 0)
            score -= 200; // Severe den-threat penalty in endgame

        return score;
    }

    private static int EvaluatePiece(SearchBoard board, byte id, int sq, bool isEndgame, bool legacyEval, bool ownDenThreatened)
    {
        int rank = SearchBoard.RankOf[id];
        int owner = ((id - 1) % SearchBoard.DistinctPieceKinds) & 1;
        int row = sq / 7;
        int col = sq % 7;

        int score = MaterialValue[rank];

        // Forward progression bonus
        score += ForwardBonus(rank, row, owner);

        // Den proximity
        score += DenProximity(sq, owner);

        // Trap penalty: piece on enemy trap, plus a "doomed piece" bonus for the
        // opponent — a trapped piece can be captured by anything.
        if (SearchBoard.EffectiveRankOf(id, sq) == 0)
        {
            score -= 80;
            if (!legacyEval)
                score -= 40 * rank / 8;
        }

        // Den escort: a strong piece guarding its own den is worth more while an
        // enemy piece is close enough to invade that den
        if (!legacyEval && rank >= 7 && ownDenThreatened)
        {
            int ownDenCol = 3, ownDenRow = owner == 0 ? 0 : 8;
            int distToOwnDen = Math.Abs(col - ownDenCol) + Math.Abs(row - ownDenRow);
            if (distToOwnDen <= 2)
                score += 30;
        }

        // Lion/Tiger river bank bonus
        if (rank == 7 || rank == 6)
        {
            if (IsRiverBank(sq))
                score += 15;

            // Check for clear jump path
            int oppDenSq = owner == 0 ? 59 : 3; // (3,8) for Blue, (3,0) for Red
            if (HasRiverBetween(sq, oppDenSq))
                score += 10; // Can potentially jump toward opponent's den
        }

        // Rat near water bonus
        if (rank == 1)
        {
            if (IsAdjacentToWater(sq))
                score += 8;
            if (SearchBoard.TerrainOf[sq] == WaterTerrain)
                score += 12; // Rat in water disrupts enemy jump paths
        }

        // Elephant safety: penalty for being near opponent's Rat
        if (rank == 8)
        {
            int opp = owner ^ 1;
            foreach (byte enemy in board.PieceIds(opp))
            {
                int enemySq = board.SquareOf(enemy);
                if (SearchBoard.RankOf[enemy] == 1 && SearchBoard.TerrainOf[enemySq] != WaterTerrain)
                {
                    int dist = ManhattanDistance(sq, enemySq);
                    if (dist <= 3)
                        score -= (4 - dist) * 15;
                }
            }
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
                    score -= 25;
            }
            else if (enemyEffRank >= ourEffRank)
            {
                if (SearchBoard.CanCapture(enemy, neighborSq, id, sq))
                {
                    if (enemyEffRank > ourEffRank)
                        score -= rank * 15; // Losing higher piece is worse
                    else
                        score -= rank * 8;  // Equal trade
                }
            }
        }

        // Endgame: bonus for advancing toward den more aggressively
        if (isEndgame && rank >= 5) // Leopard+
        {
            int oppDenCol = 3, oppDenRow = owner == 0 ? 8 : 0;
            int distToOppDen = Math.Abs(col - oppDenCol) + Math.Abs(row - oppDenRow);
            if (distToOppDen <= 2)
                score += (3 - distToOppDen) * 25;
        }

        // Penalty for being on the back rank too long (encourages development)
        int homeRow = owner == 0 ? 0 : 8;
        if (row == homeRow && TotalMaterialOnBoard(board) > 20)
            score -= 5;

        return score;
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

    private const byte WaterTerrain = 1;

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

    /// <summary>
    /// Public evaluation of a game state (delegates to the fast internal path).
    /// </summary>
    public static int Evaluate(GameState state, Player player)
    {
        // Terminal scores match MinimaxEngine's mate convention; the search itself
        // handles terminal nodes before reaching this method.
        if (state.Status == GameStatus.BlueWins)
            return player == Player.Blue ? MinimaxEngine.MateScore : -MinimaxEngine.MateScore;
        if (state.Status == GameStatus.RedWins)
            return player == Player.Red ? MinimaxEngine.MateScore : -MinimaxEngine.MateScore;
        if (state.Status == GameStatus.Draw)
            return 0;

        var board = SearchBoard.FromGameState(state);
        int side = (int)player;
        return Evaluate(board, side, board.CountLegalMoves(side), board.CountLegalMoves(side ^ 1));
    }
}
