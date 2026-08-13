using JungleGame.Core.Engine;
using JungleGame.Core.Model;
using JungleGame.Core.Rules;
using Xunit;

namespace JungleGame.Tests.Rules;

public class RiverJumpTests
{
    [Fact]
    public void Lion_CanJumpHorizontally_AcrossRiver()
    {
        // Horizontal = along rows. Lion at (1,2) jumps to (1,6) crossing river rows 3-5.
        var pieces = new Dictionary<Position, Piece>
        {
            [new Position(1, 2)] = new Piece(Animal.Lion, Player.Blue, new Position(1, 2)),
            [new Position(0, 0)] = new Piece(Animal.Rat, Player.Red, new Position(0, 0))
        };
        var state = CreateCustomState(pieces, Player.Blue);

        var result = MoveValidator.Validate(state, new Position(1, 2), new Position(1, 6));
        Assert.Null(result);
    }

    [Fact]
    public void Lion_CanJumpVertically_AcrossRiver()
    {
        // Vertical = along columns. Lion at (3,3) jumps left to (0,3) crossing left river cols 1-2.
        var pieces = new Dictionary<Position, Piece>
        {
            [new Position(3, 3)] = new Piece(Animal.Lion, Player.Blue, new Position(3, 3)),
            [new Position(0, 0)] = new Piece(Animal.Rat, Player.Red, new Position(0, 0))
        };
        var state = CreateCustomState(pieces, Player.Blue);

        var result = MoveValidator.Validate(state, new Position(3, 3), new Position(0, 3));
        Assert.Null(result);
    }

    [Fact]
    public void Tiger_CanJumpVertically_AcrossRiver()
    {
        // Vertical = along columns. Tiger at (0,3) jumps right to (3,3) crossing left river cols 1-2.
        var pieces = new Dictionary<Position, Piece>
        {
            [new Position(0, 3)] = new Piece(Animal.Tiger, Player.Blue, new Position(0, 3)),
            [new Position(0, 0)] = new Piece(Animal.Rat, Player.Red, new Position(0, 0))
        };
        var state = CreateCustomState(pieces, Player.Blue);

        var result = MoveValidator.Validate(state, new Position(0, 3), new Position(3, 3));
        Assert.Null(result);
    }

    [Fact]
    public void Tiger_CannotJumpHorizontally_AcrossRiver()
    {
        // Horizontal = along rows. Tiger at (1,2) cannot jump to (1,6).
        var pieces = new Dictionary<Position, Piece>
        {
            [new Position(1, 2)] = new Piece(Animal.Tiger, Player.Blue, new Position(1, 2)),
            [new Position(0, 0)] = new Piece(Animal.Rat, Player.Red, new Position(0, 0))
        };
        var state = CreateCustomState(pieces, Player.Blue);

        var result = MoveValidator.Validate(state, new Position(1, 2), new Position(1, 6));
        Assert.NotNull(result);
        Assert.Contains("along rows", result);
    }

    [Fact]
    public void JumpBlocked_ByRatInWater()
    {
        // Lion at (0,3) wants to jump vertically to (3,3), but Red Rat is at (1,3) in the water.
        var pieces = new Dictionary<Position, Piece>
        {
            [new Position(0, 3)] = new Piece(Animal.Lion, Player.Blue, new Position(0, 3)),
            [new Position(1, 3)] = new Piece(Animal.Rat, Player.Red, new Position(1, 3)), // Blocking rat in water
            [new Position(0, 0)] = new Piece(Animal.Cat, Player.Red, new Position(0, 0))
        };
        var state = CreateCustomState(pieces, Player.Blue);

        var result = MoveValidator.Validate(state, new Position(0, 3), new Position(3, 3));
        Assert.NotNull(result);
        Assert.Contains("blocked", result);
    }

    [Fact]
    public void LandingOnEnemy_WithJump_CapturesIfValid()
    {
        var pieces = new Dictionary<Position, Piece>
        {
            [new Position(3, 3)] = new Piece(Animal.Lion, Player.Blue, new Position(3, 3)),
            [new Position(0, 3)] = new Piece(Animal.Cat, Player.Red, new Position(0, 3))
        };
        var state = CreateCustomState(pieces, Player.Blue);

        var result = MoveValidator.Validate(state, new Position(3, 3), new Position(0, 3));
        Assert.Null(result);
    }

    private GameState CreateCustomState(Dictionary<Position, Piece> pieces, Player turn)
    {
        var immutablePieces = System.Collections.Immutable.ImmutableDictionary.CreateRange(pieces);
        return new GameState(
            Board.Initial,
            immutablePieces,
            turn,
            GameStatus.InProgress,
            System.Collections.Immutable.ImmutableList<Piece>.Empty,
            System.Collections.Immutable.ImmutableList<Piece>.Empty);
    }
}
