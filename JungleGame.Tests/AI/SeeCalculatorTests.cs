using JungleGame.Core.AI;
using JungleGame.Core.Model;

namespace JungleGame.Tests.AI;

public class SeeCalculatorTests
{
    // Square index convention: row * 7 + col (matches SearchBoard).
    private static int Sq(int col, int row) => row * 7 + col;

    private static int SeeOf(SearchBoard board, Position from, Position to) =>
        SeeOf(board, Sq(from.Col, from.Row), Sq(to.Col, to.Row));

    private static int SeeOf(SearchBoard board, int from, int to)
    {
        var moves = new SearchMove[SearchBoard.MaxMovesPerPly];
        int count = board.GenerateMoves(board.Turn, moves);
        var move = moves.Take(count).First(m => m.From == from && m.To == to && m.IsCapture);
        return SeeCalculator.See(board, move);
    }

    private static SearchBoard Board(params Piece[] pieces) =>
        SearchBoard.FromGameState(GameState.CreateFromPieces(pieces, Player.Blue));

    [Fact]
    public void See_UnprotectedCapture_ReturnsVictimValue()
    {
        var board = Board(
            new Piece(Animal.Wolf, Player.Blue, new Position(3, 2)),
            new Piece(Animal.Dog, Player.Red, new Position(3, 3)),
            new Piece(Animal.Rat, Player.Red, new Position(6, 8)));

        // Wolf x Dog with no defenders: +300
        Assert.Equal(300, SeeOf(board, Sq(3, 2), Sq(3, 3)));
    }

    [Fact]
    public void See_ProtectedCapture_LosingExchange_IsNegative()
    {
        var board = Board(
            new Piece(Animal.Wolf, Player.Blue, new Position(3, 2)),
            new Piece(Animal.Cat, Player.Red, new Position(3, 3)),
            new Piece(Animal.Leopard, Player.Red, new Position(3, 4)),
            new Piece(Animal.Rat, Player.Red, new Position(6, 8)));

        // Wolf x Cat, Leopard recaptures the Wolf: 200 - 400 = -200
        Assert.Equal(-200, SeeOf(board, Sq(3, 2), Sq(3, 3)));
    }

    [Fact]
    public void See_TrappedVictim_StillWorthFullMaterial()
    {
        var board = Board(
            new Piece(Animal.Wolf, Player.Blue, new Position(3, 2)),
            new Piece(Animal.Cat, Player.Red, new Position(3, 1)), // on Blue's trap: effective rank 0
            new Piece(Animal.Rat, Player.Red, new Position(6, 8)));

        // The trapped Cat is doomed (capturable by anything) but removing it is
        // worth its full material: no defenders, so +200.
        Assert.Equal(200, SeeOf(board, Sq(3, 2), Sq(3, 1)));
    }

    [Fact]
    public void See_RatTakesElephant_WithCatRecapture_WinsElephant()
    {
        var board = Board(
            new Piece(Animal.Rat, Player.Blue, new Position(3, 2)),
            new Piece(Animal.Elephant, Player.Red, new Position(3, 3)),
            new Piece(Animal.Cat, Player.Red, new Position(3, 4)),
            new Piece(Animal.Lion, Player.Blue, new Position(0, 0)));

        // Rat x Elephant, Cat recaptures the Rat: the Cat recapture would expose
        // the Cat to the Lion, so Red declines: +800 - 100 = +700.
        Assert.Equal(700, SeeOf(board, Sq(3, 2), Sq(3, 3)));
    }

    [Fact]
    public void See_RatTakesElephant_WithUnprotectedCatRecapture_LosesRatOnly()
    {
        var board = Board(
            new Piece(Animal.Rat, Player.Blue, new Position(3, 2)),
            new Piece(Animal.Elephant, Player.Red, new Position(3, 3)),
            new Piece(Animal.Cat, Player.Red, new Position(3, 4)));

        // No Blue piece left to recapture the Cat: Red takes the Rat back.
        // Blue: +800 - 100 = +700, Red: +100 — the exchange nets +700 for Blue
        // (the Cat is not lost, so it is not deducted).
        Assert.Equal(700, SeeOf(board, Sq(3, 2), Sq(3, 3)));
    }

    [Fact]
    public void See_AttackerLandsOnEnemyTrap_RecapturedForFree()
    {
        var board = Board(
            new Piece(Animal.Wolf, Player.Blue, new Position(3, 6)),
            new Piece(Animal.Dog, Player.Red, new Position(3, 7)), // on Red's own trap
            new Piece(Animal.Rat, Player.Red, new Position(2, 7)));

        // Wolf x Dog, but the Wolf lands on Red's trap square (3,7) — its
        // effective rank is 0, so the Rat recaptures it: 300 - 400 = -100.
        Assert.Equal(-100, SeeOf(board, Sq(3, 6), Sq(3, 7)));
    }

    [Fact]
    public void See_RatVsRat_InWater_IsEvenTrade()
    {
        var board = Board(
            new Piece(Animal.Rat, Player.Blue, new Position(2, 4)), // water
            new Piece(Animal.Rat, Player.Red, new Position(1, 4)),  // water
            new Piece(Animal.Rat, Player.Red, new Position(1, 3))); // water, recaptures

        // Rat x Rat in water, Rat recaptures: 100 - 100 = 0
        Assert.Equal(0, SeeOf(board, Sq(2, 4), Sq(1, 4)));
    }
}
