using JungleGame.Core.Logic;
using JungleGame.Core.Models;

namespace JungleGame.Tests.CoreTests;

public class BoardTests
{
    [Fact]
    public void CreateInitial_HasCorrectPieceCount()
    {
        var board = Board.CreateInitial();
        Assert.Equal(8, board.GetPieces(Player.Blue).Count);
        Assert.Equal(8, board.GetPieces(Player.Red).Count);
    }

    // Red = North = top (rows 1-3), Blue = South = bottom (rows 7-9)

    [Fact]
    public void Red_Den_AtTop()
    {
        Assert.True(Board.IsDen(new BoardPosition(4, 1), Player.Red));
    }

    [Fact]
    public void Blue_Den_AtBottom()
    {
        Assert.True(Board.IsDen(new BoardPosition(4, 9), Player.Blue));
    }

    [Fact]
    public void Dens_AreEmpty()
    {
        var board = Board.CreateInitial();
        Assert.Null(board.GetPiece(new BoardPosition(4, 1))); // Red den
        Assert.Null(board.GetPiece(new BoardPosition(4, 9))); // Blue den
    }

    [Fact]
    public void CreateInitial_PiecesAtCorrectPositions_Red()
    {
        var b = Board.CreateInitial();
        // Row 1: Lion(1,1), Tiger(7,1)
        AssertPiece(b, 1, 1, PieceType.Lion, Player.Red);
        AssertPiece(b, 7, 1, PieceType.Tiger, Player.Red);
        // Row 2: Dog(2,2), Cat(6,2)
        AssertPiece(b, 2, 2, PieceType.Dog, Player.Red);
        AssertPiece(b, 6, 2, PieceType.Cat, Player.Red);
        // Row 3: Rat(1,3), Leopard(3,3), Wolf(5,3), Elephant(7,3)
        AssertPiece(b, 1, 3, PieceType.Rat, Player.Red);
        AssertPiece(b, 3, 3, PieceType.Leopard, Player.Red);
        AssertPiece(b, 5, 3, PieceType.Wolf, Player.Red);
        AssertPiece(b, 7, 3, PieceType.Elephant, Player.Red);
    }

    [Fact]
    public void CreateInitial_PiecesAtCorrectPositions_Blue()
    {
        var b = Board.CreateInitial();
        // Row 7: Elephant(1,7), Wolf(3,7), Leopard(5,7), Rat(7,7)
        AssertPiece(b, 1, 7, PieceType.Elephant, Player.Blue);
        AssertPiece(b, 3, 7, PieceType.Wolf, Player.Blue);
        AssertPiece(b, 5, 7, PieceType.Leopard, Player.Blue);
        AssertPiece(b, 7, 7, PieceType.Rat, Player.Blue);
        // Row 8: Cat(2,8), Dog(6,8)
        AssertPiece(b, 2, 8, PieceType.Cat, Player.Blue);
        AssertPiece(b, 6, 8, PieceType.Dog, Player.Blue);
        // Row 9: Tiger(1,9), Lion(7,9)
        AssertPiece(b, 1, 9, PieceType.Tiger, Player.Blue);
        AssertPiece(b, 7, 9, PieceType.Lion, Player.Blue);
    }

    [Theory]
    [InlineData(2, 4)] [InlineData(3, 4)] [InlineData(2, 5)]
    [InlineData(3, 5)] [InlineData(2, 6)] [InlineData(3, 6)]
    [InlineData(5, 4)] [InlineData(6, 4)] [InlineData(5, 5)]
    [InlineData(6, 5)] [InlineData(5, 6)] [InlineData(6, 6)]
    public void IsWater_ReturnsTrueForRiverSquares(int col, int row)
    {
        Assert.True(Board.IsWater(new BoardPosition(col, row)));
    }

    [Theory]
    [InlineData(1, 4)] [InlineData(4, 4)] [InlineData(7, 4)]
    [InlineData(4, 1)] [InlineData(1, 1)] [InlineData(7, 9)]
    public void IsWater_ReturnsFalseForLandSquares(int col, int row)
    {
        Assert.False(Board.IsWater(new BoardPosition(col, row)));
    }

    [Fact]
    public void Clone_CreatesDeepCopy()
    {
        var board = Board.CreateInitial();
        var clone = board.Clone();
        var pos = new BoardPosition(1, 1);
        clone.RemovePiece(pos);
        Assert.NotNull(board.GetPiece(pos));
        Assert.Null(clone.GetPiece(pos));
    }

    [Fact]
    public void MovePiece_UpdatesPosition()
    {
        var board = Board.CreateInitial();
        var piece = board.GetPiece(new BoardPosition(1, 7)); // Blue Elephant
        board.MovePiece(piece!, new BoardPosition(1, 7), new BoardPosition(1, 6));
        Assert.Null(board.GetPiece(new BoardPosition(1, 7)));
        Assert.NotNull(board.GetPiece(new BoardPosition(1, 6)));
    }

    private static void AssertPiece(Board b, int col, int row, PieceType type, Player owner)
    {
        var p = b.GetPiece(new BoardPosition(col, row));
        Assert.NotNull(p);
        Assert.Equal(type, p!.Type);
        Assert.Equal(owner, p.Owner);
    }
}
