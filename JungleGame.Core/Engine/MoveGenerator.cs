using JungleGame.Core.Model;
using JungleGame.Core.Rules;

namespace JungleGame.Core.Engine;

public static class MoveGenerator
{
    /// <summary>
    /// Generates all legal moves for the given player in the given game state.
    /// For pieces that can jump, also generates jump destinations.
    /// </summary>
    public static List<Move> GenerateLegalMoves(GameState state, Player player)
    {
        var moves = new List<Move>();

        foreach (var piece in state.GetPlayerPieces(player))
        {
            GeneratePieceMoves(state, piece, moves);
        }

        return moves;
    }

    private static void GeneratePieceMoves(GameState state, Piece piece, List<Move> moves)
    {
        int c = piece.Position.Col;
        int r = piece.Position.Row;

        // Standard orthogonal moves (1 square)
        foreach (var (dc, dr) in new[] { (0, 1), (0, -1), (1, 0), (-1, 0) })
        {
            var target = new Position(c + dc, r + dr);
            if (!target.IsValid) continue;

            var error = MoveValidator.Validate(state, piece.Position, target);
            if (error == null)
            {
                var defender = state.GetPieceAt(target);
                moves.Add(new Move(piece.Position, target, defender));
            }
        }

        // Jump moves for Lion and Tiger
        if (piece.Animal == Animal.Lion || piece.Animal == Animal.Tiger)
        {
            // Lion can jump both horizontally and vertically
            // Tiger can only jump vertically

            // Vertical jumps (both lion and tiger): try jumping across the river
            foreach (var (dc, dr) in new[] { (0, 4), (0, -4) }) // ±4 rows crosses river
            {
                var target = new Position(c + dc, r + dr);
                if (!target.IsValid) continue;

                // Only consider if this is a jump move (distance > 1 and orthogonal)
                var error = MoveValidator.Validate(state, piece.Position, target);
                if (error == null)
                {
                    var defender = state.GetPieceAt(target);
                    moves.Add(new Move(piece.Position, target, defender));
                }
            }

            // Horizontal jumps (lion only): try jumping across the river
            if (piece.Animal == Animal.Lion)
            {
                foreach (var (dc, dr) in new[] { (3, 0), (-3, 0) }) // ±3 cols crosses river
                {
                    var target = new Position(c + dc, r + dr);
                    if (!target.IsValid) continue;

                    var error = MoveValidator.Validate(state, piece.Position, target);
                    if (error == null)
                    {
                        var defender = state.GetPieceAt(target);
                        moves.Add(new Move(piece.Position, target, defender));
                    }
                }
            }
        }
    }

    /// <summary>
    /// Counts legal moves for a player (useful for AI performance estimation).
    /// </summary>
    public static int CountLegalMoves(GameState state, Player player)
    {
        int count = 0;
        foreach (var piece in state.GetPlayerPieces(player))
        {
            int c = piece.Position.Col;
            int r = piece.Position.Row;

            foreach (var (dc, dr) in new[] { (0, 1), (0, -1), (1, 0), (-1, 0) })
            {
                var target = new Position(c + dc, r + dr);
                if (target.IsValid && MoveValidator.Validate(state, piece.Position, target) == null)
                    count++;
            }

            // Jump moves
            if (piece.Animal == Animal.Lion || piece.Animal == Animal.Tiger)
            {
                foreach (var (dc, dr) in new[] { (0, 4), (0, -4) })
                {
                    var target = new Position(c + dc, r + dr);
                    if (target.IsValid && MoveValidator.Validate(state, piece.Position, target) == null)
                        count++;
                }
                if (piece.Animal == Animal.Lion)
                {
                    foreach (var (dc, dr) in new[] { (3, 0), (-3, 0) })
                    {
                        var target = new Position(c + dc, r + dr);
                        if (target.IsValid && MoveValidator.Validate(state, piece.Position, target) == null)
                            count++;
                    }
                }
            }
        }
        return count;
    }
}
