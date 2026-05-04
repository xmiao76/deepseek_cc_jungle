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

        bool isHorizontal = dCol != 0;
        bool isVertical = dRow != 0;

        // Tiger cannot jump horizontally
        if (isHorizontal && piece.Animal == Animal.Tiger)
            return "Tiger cannot jump horizontally across the river.";

        // Determine the water squares that would be crossed
        var waterSquares = GetJumpWaterSquares(from, to, dCol, dRow);

        if (waterSquares == null || waterSquares.Count == 0)
            return "Invalid jump: no river squares crossed.";

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
    /// Returns the list of water squares crossed by a jump, or null if the jump is invalid.
    /// Jumps go from one bank, across all water squares, landing on the opposite bank.
    ///
    /// On the standard 7×9 board:
    /// Left river: cols 1-2, rows 3-5 (2 wide × 3 tall)
    /// Right river: cols 4-5, rows 3-5
    ///
    /// Horizontal jump: crosses the river width (2 water squares), from col 0→3 or 3→0 or 3→6 or 6→3
    /// Vertical jump: crosses the river height (3 water squares), from row 2→6 or 6→2
    /// </summary>
    private static List<Position>? GetJumpWaterSquares(Position from, Position to, int dCol, int dRow)
    {
        var squares = new List<Position>();

        if (dCol != 0) // Horizontal jump
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
        else if (dRow != 0) // Vertical jump
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

    /// <summary>
    /// Quick check if a move is a valid jump (useful for move generation).
    /// </summary>
    public static bool IsJumpMove(Position from, Position to)
    {
        int dCol = Math.Abs(to.Col - from.Col);
        int dRow = Math.Abs(to.Row - from.Row);
        return (dCol > 1 && dRow == 0) || (dRow > 1 && dCol == 0);
    }

    /// <summary>
    /// For move generation: checks if a land square borders a river such that a jump could originate from it.
    /// </summary>
    public static bool IsRiverBank(Board board, Position pos)
    {
        if (board.IsWater(pos)) return false;

        // Check if any adjacent square is water
        foreach (var (dc, dr) in new[] { (0, 1), (0, -1), (1, 0), (-1, 0) })
        {
            var adj = new Position(pos.Col + dc, pos.Row + dr);
            if (adj.IsValid && board.IsWater(adj))
                return true;
        }
        return false;
    }
}
