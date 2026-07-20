namespace JungleGame.Core.Models;

/// <summary>
/// Represents a single square on the board with its terrain and optional occupant.
/// </summary>
public class Square
{
    public BoardPosition Position { get; }
    public TerrainType Terrain { get; }
    public Piece? Occupant { get; set; }

    public Square(BoardPosition position, TerrainType terrain, Piece? occupant = null)
    {
        Position = position;
        Terrain = terrain;
        Occupant = occupant;
    }

    public Square Clone()
    {
        return new Square(Position, Terrain, Occupant?.Clone());
    }
}
