using JungleGame.Core.Model;

namespace JungleGame.Core.Rules;

public static class MoveValidator
{
    /// <summary>
    /// Validates if a move is legal according to all Jungle rules.
    /// Returns null if legal, or a string error message if illegal.
    /// </summary>
    public static string? Validate(GameState state, Position from, Position to)
    {
        if (!from.IsValid || !to.IsValid)
            return "Position out of bounds.";

        if (from == to)
            return "Cannot move to the same square.";

        var piece = state.GetPieceAt(from);
        if (piece == null)
            return "No piece at source position.";

        if (piece.Value.Owner != state.CurrentTurn)
            return "Not your turn.";

        if (state.Status != GameStatus.InProgress)
            return "Game is over.";

        // Cannot move into own den
        if (state.Board.IsDen(to, piece.Value.Owner))
            return "Cannot move into your own den.";

        int dCol = to.Col - from.Col;
        int dRow = to.Row - from.Row;

        // Must move orthogonally (no diagonals)
        if (dCol != 0 && dRow != 0)
            return "Must move orthogonally (no diagonals).";

        int dist = Math.Abs(dCol) + Math.Abs(dRow);

        // Check for river jump (lion and tiger)
        if (dist > 1)
        {
            var jumpError = ValidateJump(state, piece.Value, from, to, dCol, dRow);
            if (jumpError != null)
                return jumpError;
        }
        else
        {
            // One-square move: check terrain restrictions
            var terrainError = ValidateTerrainMove(state, piece.Value, to);
            if (terrainError != null)
                return terrainError;
        }

        // Check capture rules if destination is occupied
        var defender = state.GetPieceAt(to);
        if (defender != null)
        {
            if (!CaptureResolver.CanCapture(piece.Value, defender.Value, state.Board))
                return "Cannot capture this piece.";
        }

        return null; // Move is legal
    }

    private static string? ValidateJump(GameState state, Piece piece, Position from, Position to, int dCol, int dRow)
    {
        // Only Lion and Tiger can jump
        if (piece.Animal != Animal.Lion && piece.Animal != Animal.Tiger)
            return "Only Lion and Tiger can jump across rivers.";

        bool isRowChange = dRow != 0;
        bool isColChange = dCol != 0;

        // Tiger cannot jump along rows (a row-changing jump crosses the 3-tall
        // river body; Tigers only get the 2-wide column-changing jump)
        if (isRowChange && piece.Animal == Animal.Tiger)
            return "Tiger cannot jump along rows across the river.";

        // Determine the water squares that would be crossed (always non-empty:
        // the move is orthogonal with distance > 1, so at least one square lies between)
        var waterSquares = GetJumpWaterSquares(from, to, dCol, dRow);

        // All squares between must be water
        foreach (var ws in waterSquares)
        {
            if (!state.Board.IsWater(ws))
                return "Jump path must cross only water squares.";
        }

        // Destination must be land (not water)
        if (state.Board.IsWater(to))
            return "Jump must land on land.";

        // Check for blocking rats on any water square in the jump path
        foreach (var ws in waterSquares)
        {
            var blocker = state.GetPieceAt(ws);
            if (blocker != null && blocker.Value.Animal == Animal.Rat)
                return "Jump blocked by a rat in the water.";
        }

        return null;
    }

    private static string? ValidateTerrainMove(GameState state, Piece piece, Position to)
    {
        bool destIsWater = state.Board.IsWater(to);

        if (destIsWater && piece.Animal != Animal.Rat)
            return "Only the Rat can enter water squares.";

        return null;
    }

    /// <summary>
    /// Returns the water squares crossed by a jump, or an empty list if none.
    /// Jumps go from one bank, across all water squares, landing on the opposite bank.
    ///
    /// On the standard 7×9 board:
    /// Left river: cols 1-2, rows 3-5 (2 wide × 3 tall)
    /// Right river: cols 4-5, rows 3-5
    ///
    /// Row-changing jump (dRow != 0): crosses the 3-tall river (3 water squares),
    /// from row 2→6 or 6→2 (Lion only).
    /// Column-changing jump (dCol != 0): crosses the 2-wide river (2 water squares),
    /// col 0↔3 or 3↔6 (Lion and Tiger).
    /// </summary>
    private static List<Position> GetJumpWaterSquares(Position from, Position to, int dCol, int dRow)
    {
        var squares = new List<Position>();

        if (dCol != 0) // Column-changing jump
        {
            int step = Math.Sign(dCol);
            int col = from.Col + step;
            int row = from.Row;

            while (col != to.Col)
            {
                squares.Add(new Position(col, row));
                col += step;
            }
        }
        else if (dRow != 0) // Row-changing jump
        {
            int step = Math.Sign(dRow);
            int col = from.Col;
            int row = from.Row + step;

            while (row != to.Row)
            {
                squares.Add(new Position(col, row));
                row += step;
            }
        }

        return squares;
    }
}
