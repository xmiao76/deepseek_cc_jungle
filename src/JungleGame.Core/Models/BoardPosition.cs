namespace JungleGame.Core.Models;

/// <summary>
/// Immutable value type representing a board coordinate.
/// Columns 1-7 (left to right), Rows 1-9 (top to bottom).
/// (1,1) is the top-left corner for Blue's side.
/// </summary>
public readonly struct BoardPosition : IEquatable<BoardPosition>
{
    public int Col { get; }
    public int Row { get; }

    public BoardPosition(int col, int row)
    {
        Col = col;
        Row = row;
    }

    public static bool IsValid(int col, int row)
    {
        return col >= 1 && col <= 7 && row >= 1 && row <= 9;
    }

    public bool IsValid()
    {
        return IsValid(Col, Row);
    }

    /// <summary>
    /// Returns the four orthogonal neighbor positions (up, down, left, right)
    /// that are within the board boundaries.
    /// </summary>
    public IEnumerable<BoardPosition> Neighbors()
    {
        if (IsValid(Col - 1, Row)) yield return new BoardPosition(Col - 1, Row);
        if (IsValid(Col + 1, Row)) yield return new BoardPosition(Col + 1, Row);
        if (IsValid(Col, Row - 1)) yield return new BoardPosition(Col, Row - 1);
        if (IsValid(Col, Row + 1)) yield return new BoardPosition(Col, Row + 1);
    }

    public override bool Equals(object? obj)
    {
        return obj is BoardPosition other && Equals(other);
    }

    public bool Equals(BoardPosition other)
    {
        return Col == other.Col && Row == other.Row;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Col, Row);
    }

    public static bool operator ==(BoardPosition left, BoardPosition right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(BoardPosition left, BoardPosition right)
    {
        return !left.Equals(right);
    }

    public override string ToString()
    {
        return $"({Col},{Row})";
    }
}
