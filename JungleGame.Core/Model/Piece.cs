namespace JungleGame.Core.Model;

public readonly struct Piece : IEquatable<Piece>
{
    public Animal Animal { get; }
    public Player Owner { get; }
    public Position Position { get; }

    public Piece(Animal animal, Player owner, Position position)
    {
        Animal = animal;
        Owner = owner;
        Position = position;
    }

    public int Rank => (int)Animal;

    public Piece WithPosition(Position newPos) => new(Animal, Owner, newPos);

    public bool Equals(Piece other) => Animal == other.Animal && Owner == other.Owner && Position.Equals(other.Position);
    public override bool Equals(object? obj) => obj is Piece other && Equals(other);
    // Stable hash (see Position.GetHashCode): (animal, owner) index in 2..17,
    // combined with the square index — collision-free for live positions.
    public override int GetHashCode() => ((int)Animal * 2 + (int)Owner) * 63 + Position.Col * 9 + Position.Row;
    public static bool operator ==(Piece a, Piece b) => a.Equals(b);
    public static bool operator !=(Piece a, Piece b) => !a.Equals(b);
    public override string ToString() => $"{Owner}.{Animal}@{Position}";
}
