using JungleGame.Core.Model;

namespace JungleGame.Tests.Model;

public class GameStateFactoryTests
{
    [Fact]
    public void CreateFromPieces_BuildsState_WithSeededHistory()
    {
        var state = GameState.CreateFromPieces(new[]
        {
            new Piece(Animal.Lion, Player.Blue, new Position(3, 1)),
            new Piece(Animal.Cat, Player.Blue, new Position(0, 0)),
            new Piece(Animal.Wolf, Player.Red, new Position(2, 2)),
            new Piece(Animal.Rat, Player.Red, new Position(6, 8)),
        }, Player.Blue);

        Assert.Equal(GameStatus.InProgress, state.Status);
        Assert.Equal(Player.Blue, state.CurrentTurn);
        Assert.Equal(4, state.Pieces.Count);
        Assert.Single(state.History); // the position hash seeds three-fold detection
        Assert.NotEqual(0UL, state.History[0]);
    }

    [Theory]
    [InlineData(3, 0)] // Blue den
    [InlineData(3, 8)] // Red den
    public void CreateFromPieces_PieceOnDen_Throws(int col, int row)
    {
        var ex = Assert.Throws<ArgumentException>(() => GameState.CreateFromPieces(new[]
        {
            new Piece(Animal.Lion, Player.Blue, new Position(col, row)),
            new Piece(Animal.Rat, Player.Red, new Position(0, 1)),
        }, Player.Blue));
        Assert.Contains("den", ex.Message);
    }

    [Fact]
    public void CreateFromPieces_DuplicateRankPerSide_IsAllowed()
    {
        // Constructed test positions may hold duplicate ranks (SearchBoard
        // supports them); the 8-per-side cap is the real invariant.
        var state = GameState.CreateFromPieces(new[]
        {
            new Piece(Animal.Rat, Player.Red, new Position(3, 6)),
            new Piece(Animal.Rat, Player.Red, new Position(6, 8)),
            new Piece(Animal.Wolf, Player.Blue, new Position(2, 7)),
        }, Player.Blue);
        Assert.Equal(3, state.Pieces.Count);
    }

    [Fact]
    public void CreateFromPieces_MoreThanEightPerSide_Throws()
    {
        var pieces = new List<Piece>();
        for (int i = 0; i < 7; i++)
            pieces.Add(new Piece((Animal)((int)Animal.Rat + i), Player.Blue, new Position(i, 1)));
        pieces.Add(new Piece(Animal.Elephant, Player.Blue, new Position(0, 2)));
        pieces.Add(new Piece(Animal.Lion, Player.Blue, new Position(1, 2))); // 9th Blue piece
        pieces.Add(new Piece(Animal.Rat, Player.Red, new Position(0, 8)));

        var ex = Assert.Throws<ArgumentException>(() => GameState.CreateFromPieces(pieces, Player.Blue));
        Assert.Contains("More than 8", ex.Message);
    }

    [Fact]
    public void CreateFromPieces_OneSideOnly_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => GameState.CreateFromPieces(new[]
        {
            new Piece(Animal.Lion, Player.Blue, new Position(0, 1)),
        }, Player.Blue));
        Assert.Contains("both sides", ex.Message);
    }

    [Fact]
    public void CreateFromPieces_OutOfBounds_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => GameState.CreateFromPieces(new[]
        {
            new Piece(Animal.Lion, Player.Blue, new Position(7, 1)),
            new Piece(Animal.Rat, Player.Red, new Position(6, 8)),
        }, Player.Blue));
        Assert.Contains("outside", ex.Message);
    }

}
