using JungleGame.Core.Model;
using JungleGame.Core.Rules;
using Xunit;

namespace JungleGame.Tests.Rules;

public class CaptureResolverTests
{
    private Board Board => Board.Initial;

    [Fact]
    public void HigherRank_Captures_LowerRank()
    {
        var lion = new Piece(Animal.Lion, Player.Blue, new Position(3, 2));
        var cat = new Piece(Animal.Cat, Player.Red, new Position(3, 3));
        Assert.True(CaptureResolver.CanCapture(lion, cat, Board));
    }

    [Fact]
    public void EqualRank_CanCapture()
    {
        var leopard1 = new Piece(Animal.Leopard, Player.Blue, new Position(3, 2));
        var leopard2 = new Piece(Animal.Leopard, Player.Red, new Position(3, 3));
        Assert.True(CaptureResolver.CanCapture(leopard1, leopard2, Board));
    }

    [Fact]
    public void LowerRank_CannotCapture_HigherRank()
    {
        var cat = new Piece(Animal.Cat, Player.Blue, new Position(3, 2));
        var lion = new Piece(Animal.Lion, Player.Red, new Position(3, 3));
        Assert.False(CaptureResolver.CanCapture(cat, lion, Board));
    }

    [Fact]
    public void CannotCapture_OwnPiece()
    {
        var lion1 = new Piece(Animal.Lion, Player.Blue, new Position(3, 2));
        var lion2 = new Piece(Animal.Lion, Player.Blue, new Position(3, 3));
        Assert.False(CaptureResolver.CanCapture(lion1, lion2, Board));
    }

    [Fact]
    public void Trap_ReducesRank_ToZero()
    {
        // Blue piece on Red's trap: effective rank 0
        var elephant = new Piece(Animal.Elephant, Player.Blue, new Position(2, 8)); // On Red's trap
        var rat = new Piece(Animal.Rat, Player.Red, new Position(2, 7));

        int effectiveRank = CaptureResolver.GetEffectiveRank(elephant, Board);
        Assert.Equal(0, effectiveRank);

        // Rat (rank 1) can capture Elephant (rank 8) if Elephant is on Red's trap
        Assert.True(CaptureResolver.CanCapture(rat, elephant, Board));
    }

    [Fact]
    public void OwnTrap_DoesNotReduceRank()
    {
        // Red piece on Red's trap
        var elephant = new Piece(Animal.Elephant, Player.Red, new Position(2, 8)); // On Red's trap
        int effectiveRank = CaptureResolver.GetEffectiveRank(elephant, Board);
        Assert.Equal(8, effectiveRank);
    }

    [Fact]
    public void RatCapturesElephant_FromLand()
    {
        var rat = new Piece(Animal.Rat, Player.Blue, new Position(3, 2));
        var elephant = new Piece(Animal.Elephant, Player.Red, new Position(3, 3));
        Assert.True(CaptureResolver.CanCapture(rat, elephant, Board));
    }

    [Fact]
    public void RatCannotCaptureElephant_FromWater()
    {
        // Rat in water at (1,3) trying to capture Elephant on land at (1,2)
        var rat = new Piece(Animal.Rat, Player.Blue, new Position(1, 3)); // Water
        var elephant = new Piece(Animal.Elephant, Player.Red, new Position(1, 2)); // Land
        Assert.True(Board.IsWater(rat.Position));
        Assert.False(CaptureResolver.CanCapture(rat, elephant, Board));
    }

    [Fact]
    public void ElephantCannotCaptureRat()
    {
        var elephant = new Piece(Animal.Elephant, Player.Blue, new Position(3, 2));
        var rat = new Piece(Animal.Rat, Player.Red, new Position(3, 3));
        Assert.False(CaptureResolver.CanCapture(elephant, rat, Board));
    }

    [Fact]
    public void RatInWater_InvulnerableToLandPiece()
    {
        var rat = new Piece(Animal.Rat, Player.Red, new Position(1, 3)); // In water
        var lion = new Piece(Animal.Lion, Player.Blue, new Position(1, 2)); // On land
        Assert.True(Board.IsWater(rat.Position));
        Assert.False(Board.IsWater(lion.Position));
        Assert.False(CaptureResolver.CanCapture(lion, rat, Board));
    }

    [Fact]
    public void RatCanCaptureRat_InWater()
    {
        var rat1 = new Piece(Animal.Rat, Player.Blue, new Position(1, 3)); // In water
        var rat2 = new Piece(Animal.Rat, Player.Red, new Position(1, 4)); // In water
        Assert.True(Board.IsWater(rat1.Position));
        Assert.True(Board.IsWater(rat2.Position));
        Assert.True(CaptureResolver.CanCapture(rat1, rat2, Board));
    }

    [Fact]
    public void RatOnLand_CanCapture_RatInWater()
    {
        var rat1 = new Piece(Animal.Rat, Player.Blue, new Position(1, 2)); // On land
        var rat2 = new Piece(Animal.Rat, Player.Red, new Position(1, 3));  // In water
        Assert.False(Board.IsWater(rat1.Position));
        Assert.True(Board.IsWater(rat2.Position));
        Assert.True(CaptureResolver.CanCapture(rat1, rat2, Board));
    }

    [Fact]
    public void NotOnTrap_KeepsNormalRank()
    {
        var lion = new Piece(Animal.Lion, Player.Blue, new Position(3, 2)); // Land
        Assert.Equal(7, CaptureResolver.GetEffectiveRank(lion, Board));
    }
}
