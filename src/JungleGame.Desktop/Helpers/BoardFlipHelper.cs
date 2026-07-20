using JungleGame.Core.Models;

namespace JungleGame.Desktop.Helpers;

/// <summary>
/// Coordinate transformations for the board flip feature.
/// Flip is a pure visual transform — game state is never affected.
/// </summary>
public static class BoardFlipHelper
{
    /// <summary>
    /// Converts a logical board position to its display position when flipped.
    /// When not flipped, returns the position unchanged.
    /// </summary>
    public static BoardPosition LogicalToDisplay(BoardPosition pos, bool isFlipped)
    {
        if (!isFlipped) return pos;
        return new BoardPosition(8 - pos.Col, 10 - pos.Row);
    }

    /// <summary>
    /// Converts a display (pixel-mapped) position back to logical.
    /// </summary>
    public static BoardPosition DisplayToLogical(BoardPosition pos, bool isFlipped)
    {
        // Same transform — it's self-inverse
        return LogicalToDisplay(pos, isFlipped);
    }
}
