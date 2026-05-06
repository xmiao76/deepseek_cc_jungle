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
        // Search all orthogonal distances ≥ 2, let MoveValidator determine which are valid jumps
        if (piece.Animal == Animal.Lion || piece.Animal == Animal.Tiger)
        {
            foreach (var (dc, dr) in new[] { (0, 1), (0, -1), (1, 0), (-1, 0) })
            {
                // Tiger cannot jump horizontally (along rows)
                if (dr != 0 && piece.Animal == Animal.Tiger)
                    continue;

                for (int dist = 2; dist <= 8; dist++)
                {
                    var target = new Position(c + dc * dist, r + dr * dist);
                    if (!target.IsValid) break;

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
                foreach (var (dc, dr) in new[] { (0, 1), (0, -1), (1, 0), (-1, 0) })
                {
                    if (dr != 0 && piece.Animal == Animal.Tiger)
                        continue;

                    for (int dist = 2; dist <= 8; dist++)
                    {
                        var target = new Position(c + dc * dist, r + dr * dist);
                        if (!target.IsValid) break;
                        if (MoveValidator.Validate(state, piece.Position, target) == null)
                            count++;
                    }
                }
            }
        }
        return count;
    }
}
