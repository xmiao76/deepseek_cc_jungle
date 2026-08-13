using JungleGame.Core.Model;
using JungleGame.Core.Rules;

namespace JungleGame.Tests.Helpers;

/// <summary>
/// Independent reference implementation of the movement rules, written directly
/// from the rules spec (sharing only CaptureResolver, the rule module itself —
/// no code from the engine's MoveGenerator). Used for perft-style differential
/// move counting against MoveGenerator and SearchBoard.
/// </summary>
public static class NaiveMoveGenerator
{
    public static List<(Position From, Position To)> Generate(GameState state)
    {
        var moves = new List<(Position, Position)>();
        if (state.Status != GameStatus.InProgress)
            return moves;

        foreach (var piece in state.GetPlayerPieces(state.CurrentTurn))
        {
            // Single-step orthogonal moves
            foreach (var (dc, dr) in new[] { (0, 1), (0, -1), (1, 0), (-1, 0) })
            {
                var target = new Position(piece.Position.Col + dc, piece.Position.Row + dr);
                if (target.IsValid && IsLegalStep(state, piece, target))
                    moves.Add((piece.Position, target));
            }

            // Jump moves for Lion (all directions) and Tiger (column change only)
            if (piece.Animal == Animal.Lion || piece.Animal == Animal.Tiger)
            {
                foreach (var (dc, dr) in new[] { (0, 1), (0, -1), (1, 0), (-1, 0) })
                {
                    if (dr != 0 && piece.Animal == Animal.Tiger)
                        continue;

                    for (int dist = 2; dist <= 8; dist++)
                    {
                        var target = new Position(piece.Position.Col + dc * dist, piece.Position.Row + dr * dist);
                        if (!target.IsValid)
                            break;
                        if (IsLegalJump(state, piece, target, dc, dr))
                            moves.Add((piece.Position, target));
                    }
                }
            }
        }
        return moves;
    }

    private static bool IsLegalStep(GameState state, Piece piece, Position to)
    {
        if (state.Board.IsDen(to, piece.Owner))
            return false; // Cannot move into own den
        if (state.Board.IsWater(to) && piece.Animal != Animal.Rat)
            return false; // Only the Rat enters water

        var defender = state.GetPieceAt(to);
        return defender == null || CaptureResolver.CanCapture(piece, defender.Value, state.Board);
    }

    private static bool IsLegalJump(GameState state, Piece piece, Position to, int dc, int dr)
    {
        if (state.Board.IsDen(to, piece.Owner))
            return false;

        // The landing square must be land
        if (state.Board.IsWater(to))
            return false;

        // Every square between must be water, with no rat on the path
        int col = piece.Position.Col + Math.Sign(dc);
        int row = piece.Position.Row + Math.Sign(dr);
        while (col != to.Col || row != to.Row)
        {
            var sq = new Position(col, row);
            if (!state.Board.IsWater(sq))
                return false;
            var occupant = state.GetPieceAt(sq);
            if (occupant != null && occupant.Value.Animal == Animal.Rat)
                return false;
            col += Math.Sign(dc);
            row += Math.Sign(dr);
        }

        var defender = state.GetPieceAt(to);
        return defender == null || CaptureResolver.CanCapture(piece, defender.Value, state.Board);
    }
}
