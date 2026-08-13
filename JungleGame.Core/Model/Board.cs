using System.Collections.Immutable;
using JungleGame.Core.Model;

namespace JungleGame.Core.Model;

public class Board
{
    private static readonly Terrain[,] TerrainGrid = new Terrain[7, 9];

    public static Board Initial { get; } = new();

    static Board()
    {
        // Initialize all to Land
        for (int c = 0; c < 7; c++)
            for (int r = 0; r < 9; r++)
                TerrainGrid[c, r] = Terrain.Land;

        // Blue traps and den (bottom, rows 0-1)
        TerrainGrid[2, 0] = Terrain.TrapBlue;
        TerrainGrid[3, 0] = Terrain.DenBlue;
        TerrainGrid[4, 0] = Terrain.TrapBlue;
        TerrainGrid[3, 1] = Terrain.TrapBlue;

        // Red traps and den (top, rows 7-8)
        TerrainGrid[2, 8] = Terrain.TrapRed;
        TerrainGrid[3, 8] = Terrain.DenRed;
        TerrainGrid[4, 8] = Terrain.TrapRed;
        TerrainGrid[3, 7] = Terrain.TrapRed;

        // Rivers: cols 1-2 and 4-5, rows 3-5
        for (int r = 3; r <= 5; r++)
        {
            TerrainGrid[1, r] = Terrain.River;
            TerrainGrid[2, r] = Terrain.River;
            TerrainGrid[4, r] = Terrain.River;
            TerrainGrid[5, r] = Terrain.River;
        }
    }

    public Terrain GetTerrain(Position pos)
    {
        if (!pos.IsValid)
            throw new ArgumentOutOfRangeException(nameof(pos), pos, "Position is outside the 7×9 board.");
        return TerrainGrid[pos.Col, pos.Row];
    }

    public bool IsRiver(Position pos) => GetTerrain(pos) == Terrain.River;
    public bool IsTrap(Position pos, Player player) =>
        (GetTerrain(pos) == Terrain.TrapBlue && player == Player.Red) ||
        (GetTerrain(pos) == Terrain.TrapRed && player == Player.Blue);
    public bool IsDen(Position pos, Player player) =>
        (GetTerrain(pos) == Terrain.DenBlue && player == Player.Blue) ||
        (GetTerrain(pos) == Terrain.DenRed && player == Player.Red);
    public bool IsOpponentDen(Position pos, Player player) =>
        (GetTerrain(pos) == Terrain.DenBlue && player == Player.Red) ||
        (GetTerrain(pos) == Terrain.DenRed && player == Player.Blue);
    public bool IsWater(Position pos) => GetTerrain(pos) == Terrain.River;

    public bool IsValidPosition(Position pos) => pos.IsValid;
}
