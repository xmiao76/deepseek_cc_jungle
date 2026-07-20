namespace JungleGame.Core.Models;

/// <summary>
/// A game piece belonging to a player, with a type/rank and position.
/// </summary>
public class Piece
{
    public PieceType Type { get; }
    public Player Owner { get; }
    public BoardPosition Position { get; set; }
    public bool IsAlive { get; set; } = true;

    /// <summary>
    /// Effective rank considering trap effects. When a piece is on an opponent's trap,
    /// its effective rank is 0 (any opposing piece can capture it).
    /// This property is set externally by the Board/GameEngine based on the piece's position.
    /// </summary>
    public int BaseRank => (int)Type;

    public Piece(PieceType type, Player owner, BoardPosition position)
    {
        Type = type;
        Owner = owner;
        Position = position;
    }

    public Piece Clone()
    {
        return new Piece(Type, Owner, Position) { IsAlive = IsAlive };
    }

    public override string ToString()
    {
        return $"{Owner} {Type} at {Position}";
    }
}
