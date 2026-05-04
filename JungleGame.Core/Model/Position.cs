namespace JungleGame.Core.Model;

public readonly struct Position : IEquatable<Position>
{
    public int Col { get; } // 0-6 (a-g)
    public int Row { get; } // 0-8 (1-9 from bottom)

    public Position(int col, int row)
    {
        Col = col;
        Row = row;
    }

    public bool IsValid => Col >= 0 && Col <= 6 && Row >= 0 && Row <= 8;

    public Position Move(int dCol, int dRow) => new(Col + dCol, Row + dRow);

    public static IEnumerable<Position> All
    {
        get
        {
            for (int r = 0; r < 9; r++)
                for (int c = 0; c < 7; c++)
                    yield return new Position(c, r);
        }
    }

    public bool Equals(Position other) => Col == other.Col && Row == other.Row;
    public override bool Equals(object? obj) => obj is Position other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Col, Row);
    public static bool operator ==(Position a, Position b) => a.Equals(b);
    public static bool operator !=(Position a, Position b) => !a.Equals(b);
    public override string ToString() => $"({Col},{Row})";
}
