using JungleGame.Core.Models;

namespace JungleGame.Core.Logic;

/// <summary>
/// Contains all move legality validation logic in static methods for easy unit testing.
/// </summary>
public static class MoveValidator
{
    /// <summary>
    /// Returns the effective rank of a piece at a given position, considering trap effects.
    /// A piece on the opponent's trap has effective rank 0.
    /// </summary>
    public static int EffectiveRank(Piece piece)
    {
        if (Board.IsOpponentTrap(piece.Position, piece.Owner))
            return 0;
        return piece.BaseRank;
    }

    /// <summary>
    /// Gets all legal moves for a specific piece on the board.
    /// </summary>
    public static List<Move> GetLegalMovesForPiece(Board board, Piece piece)
    {
        var moves = new List<Move>();

        // 1. Normal orthogonal moves
        foreach (var neighbor in piece.Position.Neighbors())
        {
            var destTerrain = board.GetTerrain(neighbor);
            var occupant = board.GetPiece(neighbor);

            // Cannot enter own den
            if (Board.IsOwnDen(neighbor, piece.Owner))
                continue;

            // Water restriction: only Rat can enter water
            if (destTerrain == TerrainType.Water && piece.Type != PieceType.Rat)
                continue;

            // Check if destination has an occupant (capture attempt)
            if (occupant != null)
            {
                if (occupant.Owner == piece.Owner)
                    continue; // Cannot capture own piece

                if (!CanCapture(piece, occupant, neighbor))
                    continue;
            }

            var move = new Move(piece, piece.Position, neighbor)
            {
                CapturedPiece = occupant,
                IsRiverJump = false,
                IsDenEntry = Board.IsOpponentDen(neighbor, piece.Owner)
            };
            moves.Add(move);
        }

        // 2. River jumps for Lion and Tiger
        if (piece.Type == PieceType.Lion || piece.Type == PieceType.Tiger)
        {
            var jumps = GetRiverJumps(board, piece);
            moves.AddRange(jumps);
        }

        return moves;
    }

    /// <summary>
    /// Validates whether a piece can capture another piece at the given destination.
    /// </summary>
    public static bool CanCapture(Piece attacker, Piece defender, BoardPosition destPos)
    {
        // Elephant can never capture Rat
        if (attacker.Type == PieceType.Elephant && defender.Type == PieceType.Rat)
            return false;

        // Rat in water is invulnerable to land pieces
        if (Board.IsWater(defender.Position) && !Board.IsWater(attacker.Position))
            return false;

        // Rat can only capture Elephant from land, not from water
        if (attacker.Type == PieceType.Rat && defender.Type == PieceType.Elephant && Board.IsWater(attacker.Position))
            return false;

        int attackerRank = EffectiveRank(attacker);
        int defenderRank = EffectiveRank(defender);

        // Rat captures Elephant (special case) — only from land (already checked above)
        if (attacker.Type == PieceType.Rat && defender.Type == PieceType.Elephant)
            return true;

        // Standard capture: attacker rank >= defender rank
        return attackerRank >= defenderRank;
    }

    /// <summary>
    /// Gets all legal river jump moves for a Lion or Tiger piece.
    /// </summary>
    private static List<Move> GetRiverJumps(Board board, Piece piece)
    {
        var jumps = new List<Move>();

        // === VERTICAL JUMPS (across river, 3 columns, same row) ===
        // Available to BOTH Lion and Tiger, but only from land to land within river zone rows (4-6).
        if (piece.Position.Row >= 4 && piece.Position.Row <= 6)
        {
            // From column 1 to 4 (over left river cols 2-3)
            if (piece.Position.Col == 1)
                TryAddJump(board, piece, new BoardPosition(4, piece.Position.Row), jumps);
            // From column 4 to 1 (over left river cols 2-3)
            else if (piece.Position.Col == 4)
            {
                TryAddJump(board, piece, new BoardPosition(1, piece.Position.Row), jumps);
                TryAddJump(board, piece, new BoardPosition(7, piece.Position.Row), jumps);
            }
            // From column 7 to 4 (over right river cols 5-6)
            else if (piece.Position.Col == 7)
                TryAddJump(board, piece, new BoardPosition(4, piece.Position.Row), jumps);
        }

        // === HORIZONTAL JUMPS (along river, 4 rows, same column) ===
        // Available to LION ONLY in columns 2, 3, 5, 6
        if (piece.Type == PieceType.Lion)
        {
            // Column 2 — jump from row 3 to row 7 or vice versa
            if (piece.Position.Col == 2)
            {
                if (piece.Position.Row == 3)
                    TryAddJump(board, piece, new BoardPosition(2, 7), jumps);
                else if (piece.Position.Row == 7)
                    TryAddJump(board, piece, new BoardPosition(2, 3), jumps);
            }
            // Column 3
            else if (piece.Position.Col == 3)
            {
                if (piece.Position.Row == 3)
                    TryAddJump(board, piece, new BoardPosition(3, 7), jumps);
                else if (piece.Position.Row == 7)
                    TryAddJump(board, piece, new BoardPosition(3, 3), jumps);
            }
            // Column 5
            else if (piece.Position.Col == 5)
            {
                if (piece.Position.Row == 3)
                    TryAddJump(board, piece, new BoardPosition(5, 7), jumps);
                else if (piece.Position.Row == 7)
                    TryAddJump(board, piece, new BoardPosition(5, 3), jumps);
            }
            // Column 6
            else if (piece.Position.Col == 6)
            {
                if (piece.Position.Row == 3)
                    TryAddJump(board, piece, new BoardPosition(6, 7), jumps);
                else if (piece.Position.Row == 7)
                    TryAddJump(board, piece, new BoardPosition(6, 3), jumps);
            }
        }

        return jumps;
    }

    private static void TryAddJump(Board board, Piece piece, BoardPosition to, List<Move> jumps)
    {
        // Destination must be valid
        if (!to.IsValid()) return;

        // Destination must be land (cannot jump onto water or own den)
        if (Board.IsWater(to)) return;
        if (Board.IsOwnDen(to, piece.Owner)) return;

        // Check for rat blocking in the water squares between
        var waterSquares = Board.GetRiverJumpWaterSquares(piece.Position, to);
        foreach (var ws in waterSquares)
        {
            var blocker = board.GetPiece(ws);
            if (blocker != null && blocker.Type == PieceType.Rat)
                return; // Jump blocked by a Rat in the water
        }

        // Check capture at destination
        var occupant = board.GetPiece(to);
        if (occupant != null)
        {
            if (occupant.Owner == piece.Owner) return; // Can't capture own piece
            if (!CanCapture(piece, occupant, to)) return;
        }

        jumps.Add(new Move(piece, piece.Position, to)
        {
            CapturedPiece = occupant,
            IsRiverJump = true,
            IsDenEntry = Board.IsOpponentDen(to, piece.Owner)
        });
    }

    /// <summary>
    /// Validates a proposed move is legal on the given board.
    /// Returns true with the populated move (capture info, etc.), or false.
    /// </summary>
    public static bool IsMoveLegal(Board board, Piece piece, BoardPosition to, out Move? legalMove)
    {
        legalMove = null;

        var moves = GetLegalMovesForPiece(board, piece);
        var match = moves.FirstOrDefault(m => m.To == to);

        if (match != null)
        {
            legalMove = match;
            return true;
        }

        return false;
    }
}
