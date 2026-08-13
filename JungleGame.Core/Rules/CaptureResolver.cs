using JungleGame.Core.Model;

namespace JungleGame.Core.Rules;

public static class CaptureResolver
{
    /// <summary>
    /// Determines if the attacker can capture the defender.
    /// Returns true if capture is allowed by the rules.
    /// </summary>
    public static bool CanCapture(Piece attacker, Piece defender, Board board)
    {
        if (attacker.Owner == defender.Owner)
            return false; // Cannot capture own pieces

        int attackerEffectiveRank = GetEffectiveRank(attacker, board);

        // Defending piece's effective rank (if defender is on attacker's trap)
        int defenderEffectiveRank = GetEffectiveRank(defender, board);

        // Special case: Rat captures Elephant (from land only)
        if (attacker.Animal == Animal.Rat && defender.Animal == Animal.Elephant)
        {
            // Rat can only capture elephant from land, not from water
            return !board.IsWater(attacker.Position);
        }

        // Special case: Elephant cannot capture Rat
        if (attacker.Animal == Animal.Elephant && defender.Animal == Animal.Rat)
            return false;

        // Special case: Rat vs Rat is unconditional — a rat on land or in water
        // may capture a rat on land or in water
        if (attacker.Animal == Animal.Rat && defender.Animal == Animal.Rat)
            return true;

        // Special case: Rat in water cannot be captured by any other land piece
        if (defender.Animal == Animal.Rat && board.IsWater(defender.Position) &&
            !board.IsWater(attacker.Position))
            return false;

        // Standard rank-based capture
        return attackerEffectiveRank >= defenderEffectiveRank;
    }

    /// <summary>
    /// Gets the effective rank considering trap status.
    /// A piece on an opponent's trap has effective rank 0.
    /// </summary>
    public static int GetEffectiveRank(Piece piece, Board board)
    {
        if (board.IsTrap(piece.Position, piece.Owner))
            return 0;
        return piece.Rank;
    }
}
