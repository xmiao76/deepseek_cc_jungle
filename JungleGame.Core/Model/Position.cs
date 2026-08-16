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

    public bool Equals(Position other) => Col == other.Col && Row == other.Row;
    public override bool Equals(object? obj) => obj is Position other && Equals(other);
    // Stable hash (no HashCode.Combine): randomized hashing would make
    // ImmutableDictionary iteration order — and therefore move generation
    // order, root move ordering, and seeded random-play positions — vary
    // between processes. Bijective over the 7×9 board.
    public override int GetHashCode() => Col * 9 + Row;
    public static bool operator ==(Position a, Position b) => a.Equals(b);
    public static bool operator !=(Position a, Position b) => !a.Equals(b);
    public override string ToString() => $"({Col},{Row})";
}
