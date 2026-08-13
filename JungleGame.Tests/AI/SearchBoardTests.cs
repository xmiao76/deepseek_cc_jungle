using JungleGame.Core.AI;
using JungleGame.Core.Model;
using Xunit;

namespace JungleGame.Tests.AI;

public class SearchBoardTests
{
    private static int Sq(int col, int row) => row * 7 + col;

    private static List<(int From, int To)> MovesOf(SearchBoard board, int side)
    {
        var buf = new SearchMove[128];
        int count = board.GenerateMoves(side, buf);
        var result = new List<(int From, int To)>(count);
        for (int i = 0; i < count; i++)
            result.Add((buf[i].From, buf[i].To));
        return result;
    }

    private static SearchBoard BoardWith(params Piece[] pieces)
    {
        var dict = pieces.ToDictionary(p => p.Position);
        var state = new GameState(
            Board.Initial,
            System.Collections.Immutable.ImmutableDictionary.CreateRange(dict),
            Player.Blue,
            GameStatus.InProgress,
            System.Collections.Immutable.ImmutableList<Piece>.Empty,
            System.Collections.Immutable.ImmutableList<Piece>.Empty);
        return SearchBoard.FromGameState(state);
    }

    [Fact]
    public void Lion_CanJumpHorizontally_AcrossRiver()
    {
        // Lion at (1,2) can jump to (1,6) over the 3-tall river body
        var board = BoardWith(
            new Piece(Animal.Lion, Player.Blue, new Position(1, 2)),
            new Piece(Animal.Rat, Player.Red, new Position(6, 6)));
        var moves = MovesOf(board, 0);

        Assert.Contains((Sq(1, 2), Sq(1, 6)), moves);
    }

    [Fact]
    public void Lion_CanJumpVertically_AcrossRiver()
    {
        // Lion at (0,3) can jump to (3,3) over the 2-wide river body
        var board = BoardWith(
            new Piece(Animal.Lion, Player.Blue, new Position(0, 3)),
            new Piece(Animal.Rat, Player.Red, new Position(6, 6)));
        var moves = MovesOf(board, 0);

        Assert.Contains((Sq(0, 3), Sq(3, 3)), moves);
    }

    [Fact]
    public void Tiger_CanJumpVertically_ButNotHorizontally()
    {
        var board = BoardWith(
            new Piece(Animal.Tiger, Player.Blue, new Position(1, 2)),
            new Piece(Animal.Rat, Player.Red, new Position(6, 6)));
        var moves = MovesOf(board, 0);

        Assert.DoesNotContain((Sq(1, 2), Sq(1, 6)), moves); // no horizontal jump

        var board2 = BoardWith(
            new Piece(Animal.Tiger, Player.Blue, new Position(0, 3)),
            new Piece(Animal.Rat, Player.Red, new Position(6, 6)));
        Assert.Contains((Sq(0, 3), Sq(3, 3)), MovesOf(board2, 0));
    }

    [Fact]
    public void Jump_IsBlocked_ByRatInPath()
    {
        var board = BoardWith(
            new Piece(Animal.Lion, Player.Blue, new Position(1, 2)),
            new Piece(Animal.Rat, Player.Red, new Position(1, 3))); // rat in the water path
        var moves = MovesOf(board, 0);

        Assert.DoesNotContain((Sq(1, 2), Sq(1, 6)), moves);
    }

    [Fact]
    public void Jump_IsNotBlocked_ByPieceOnBank()
    {
        // A piece standing on the river bank is not in the jump path
        var board = BoardWith(
            new Piece(Animal.Lion, Player.Blue, new Position(1, 2)),
            new Piece(Animal.Wolf, Player.Red, new Position(1, 6)));
        var moves = MovesOf(board, 0);

        Assert.Contains((Sq(1, 2), Sq(1, 6)), moves);
    }

    [Fact]
    public void Capture_SwapRemoves_CapturedPiece()
    {
        var board = BoardWith(
            new Piece(Animal.Wolf, Player.Blue, new Position(3, 3)),
            new Piece(Animal.Rat, Player.Red, new Position(3, 4)));

        var move = new SearchMove((byte)Sq(3, 3), (byte)Sq(3, 4), 2); // Red Rat id = (1-1)*2+1+1 = 2
        board.ApplyMove(move);

        Assert.Equal(0, board.PieceCount(1));
        Assert.Equal(1, board.PieceCount(0));
        Assert.Equal(0, board.WinnerSide); // Blue eliminated Red
        Assert.Equal(1, board.Turn);       // Red to move
        Assert.Equal(0, board.Occupant(Sq(3, 3)));
        Assert.Equal(7, board.Occupant(Sq(3, 4))); // Blue Wolf id = (4-1)*2+0+1 = 7
    }

    [Fact]
    public void DenEntry_SetsWinnerSide()
    {
        var board = BoardWith(
            new Piece(Animal.Lion, Player.Blue, new Position(3, 7)),
            new Piece(Animal.Rat, Player.Red, new Position(0, 0)));

        var move = new SearchMove((byte)Sq(3, 7), (byte)Sq(3, 8), 0, entersDen: true);
        board.ApplyMove(move);

        Assert.Equal(0, board.WinnerSide);
    }

    [Fact]
    public void Trap_ReducesEffectiveRank_OfEnemyPieceOnly()
    {
        // Blue Elephant (id 15) on Red's trap (2,8): effective rank 0
        Assert.Equal(0, SearchBoard.EffectiveRankOf(15, Sq(2, 8)));
        // Same piece off the trap keeps rank 8
        Assert.Equal(8, SearchBoard.EffectiveRankOf(15, Sq(2, 7)));
        // Red Elephant (id 16) on its own trap keeps rank 8
        Assert.Equal(8, SearchBoard.EffectiveRankOf(16, Sq(2, 8)));
    }

    [Fact]
    public void Rat_CanEnterWater_OthersCannot()
    {
        var board = BoardWith(
            new Piece(Animal.Rat, Player.Blue, new Position(0, 3)),
            new Piece(Animal.Wolf, Player.Blue, new Position(0, 4)),
            new Piece(Animal.Cat, Player.Red, new Position(6, 6)));

        Assert.Contains((Sq(0, 3), Sq(1, 3)), MovesOf(board, 0)); // rat enters water

        var wolfMoves = MovesOf(board, 0);
        Assert.DoesNotContain((Sq(0, 4), Sq(1, 4)), wolfMoves); // wolf cannot enter water
    }

    [Fact]
    public void Clone_IsIndependent_Copy()
    {
        var board = BoardWith(
            new Piece(Animal.Wolf, Player.Blue, new Position(3, 3)),
            new Piece(Animal.Rat, Player.Red, new Position(3, 4)));
        var clone = board.Clone();

        clone.ApplyMove(new SearchMove((byte)Sq(3, 3), (byte)Sq(3, 4), 2));

        // Original untouched
        Assert.Equal(1, board.PieceCount(1));
        Assert.NotEqual(board.Hash, clone.Hash);
    }
}
