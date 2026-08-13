using JungleGame.Core.Model;
using Xunit;

namespace JungleGame.Tests.Rules;

public class BoardTests
{
    [Fact]
    public void GetTerrain_OutOfBounds_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Board.Initial.GetTerrain(new Position(-1, 0)));
        Assert.Throws<ArgumentOutOfRangeException>(() => Board.Initial.GetTerrain(new Position(7, 0)));
        Assert.Throws<ArgumentOutOfRangeException>(() => Board.Initial.GetTerrain(new Position(0, 9)));
    }

    [Fact]
    public void GetTerrain_ValidSquare_ReturnsTerrain()
    {
        Assert.Equal(Terrain.Land, Board.Initial.GetTerrain(new Position(3, 3)));
        Assert.Equal(Terrain.River, Board.Initial.GetTerrain(new Position(1, 4)));
        Assert.Equal(Terrain.DenBlue, Board.Initial.GetTerrain(new Position(3, 0)));
        Assert.Equal(Terrain.DenRed, Board.Initial.GetTerrain(new Position(3, 8)));
        Assert.Equal(Terrain.TrapBlue, Board.Initial.GetTerrain(new Position(2, 0)));
        Assert.Equal(Terrain.TrapRed, Board.Initial.GetTerrain(new Position(4, 8)));
    }
}
