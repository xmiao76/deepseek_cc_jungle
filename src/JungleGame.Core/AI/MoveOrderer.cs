using JungleGame.Core.Models;

namespace JungleGame.Core.AI;

/// <summary>
/// Orders moves to maximize alpha-beta pruning effectiveness.
/// MVV-LVA (Most Valuable Victim - Least Valuable Attacker) for captures,
/// then den-threatening moves, river jumps, and forward advances.
/// </summary>
public static class MoveOrderer
{
    /// <summary>
    /// Orders moves from most promising to least promising for alpha-beta search.
    /// </summary>
    public static List<Move> OrderMoves(List<Move> moves)
    {
        // Assign a score to each move, then sort descending
        var scored = moves.Select(m => (Move: m, Score: ScoreMove(m)))
                          .OrderByDescending(x => x.Score)
                          .Select(x => x.Move)
                          .ToList();
        return scored;
    }

    private static int ScoreMove(Move move)
    {
        int score = 0;

        // Highest priority: winning moves (den entry)
        if (move.IsDenEntry)
        {
            score += 10000;
            return score; // Den entry wins — always try first
        }

        // Captures: MVV-LVA
        if (move.CapturedPiece != null)
        {
            // Victim value (higher = better to capture)
            int victimValue = GetPieceValue(move.CapturedPiece.Type);
            // Attacker value (lower = cheaper attacker)
            int attackerValue = GetPieceValue(move.Piece.Type);
            score += 1000 + victimValue * 10 + (1000 - attackerValue);
        }

        // River jumps are tactically important
        if (move.IsRiverJump)
        {
            score += 500;
        }

        // Forward progress: Blue attacks UP toward Red's den (row 1),
        // Red attacks DOWN toward Blue's den (row 9)
        int forwardDelta = 0;
        if (move.Piece.Owner == Player.Blue)
            forwardDelta = move.From.Row - move.To.Row; // Blue up = smaller row
        else
            forwardDelta = move.To.Row - move.From.Row; // Red down = larger row

        score += forwardDelta * 20;

        // Proximity to opponent's den
        // Blue's den at row 9, Red's den at row 1
        var opponent = move.Piece.Owner == Player.Blue ? Player.Red : Player.Blue;
        int denRow = opponent == Player.Blue ? 9 : 1;
        int fromDist = Math.Abs(move.From.Row - denRow);
        int toDist = Math.Abs(move.To.Row - denRow);
        score += (fromDist - toDist) * 20;

        // Central columns slightly preferred
        score += (4 - Math.Abs(move.To.Col - 4)) * 5;

        return score;
    }

    private static int GetPieceValue(PieceType type)
    {
        return type switch
        {
            PieceType.Elephant => 1000,
            PieceType.Lion => 600,
            PieceType.Tiger => 500,
            PieceType.Leopard => 350,
            PieceType.Wolf => 250,
            PieceType.Dog => 150,
            PieceType.Cat => 100,
            PieceType.Rat => 50,
            _ => 0
        };
    }
}
