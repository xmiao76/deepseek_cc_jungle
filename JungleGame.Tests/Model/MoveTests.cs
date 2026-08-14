using JungleGame.Core.Engine;
using JungleGame.Core.Model;
using Xunit;

namespace JungleGame.Tests.Model;

public class MoveTests
{
    [Fact]
    public void ToString_QuietMove_ShowsFromAndTo()
    {
        var move = new Move(new Position(0, 2), new Position(1, 2));
        Assert.Equal("(0,2)→(1,2)", move.ToString());
    }

    [Fact]
    public void ToString_Capture_ShowsCapturedAnimal()
    {
        var victim = new Piece(Animal.Dog, Player.Red, new Position(1, 2));
        var move = new Move(new Position(0, 2), new Position(1, 2), victim);
        Assert.Equal("(0,2)→(1,2) xDog", move.ToString());
    }

    [Fact]
    public void IsCapture_ReflectsCapturedPiece()
    {
        var quiet = new Move(new Position(0, 2), new Position(1, 2));
        Assert.False(quiet.IsCapture);

        var victim = new Piece(Animal.Dog, Player.Red, new Position(1, 2));
        var capture = new Move(new Position(0, 2), new Position(1, 2), victim);
        Assert.True(capture.IsCapture);
    }

    [Fact]
    public void Equality_ComparesAllFields()
    {
        var victim = new Piece(Animal.Dog, Player.Red, new Position(1, 2));
        var a = new Move(new Position(0, 2), new Position(1, 2), victim);
        var b = new Move(new Position(0, 2), new Position(1, 2), victim);
        var differentTo = new Move(new Position(0, 2), new Position(2, 2), victim);
        var differentVictim = new Move(new Position(0, 2), new Position(1, 2),
            new Piece(Animal.Cat, Player.Red, new Position(1, 2)));

        Assert.True(a.Equals(b));
        Assert.False(a.Equals(differentTo));
        Assert.False(a.Equals(differentVictim));
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }
}
