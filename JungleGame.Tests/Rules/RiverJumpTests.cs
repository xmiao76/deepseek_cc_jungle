using JungleGame.Core.Engine;
using JungleGame.Core.Model;
using JungleGame.Core.Rules;
using Xunit;

namespace JungleGame.Tests.Rules;

public class RiverJumpTests
{
    [Fact]
    public void Lion_CanJumpVertically_AcrossRiver()
    {
        // Left river at cols 1-2, rows 3-5. Lion at (1,2) jumps to (1,6).
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
    public void Lion_CanJumpHorizontally_AcrossRiver()
    {
        // Left river at cols 1-2. Lion at (3,3) jumps left to (0,3).
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
        // Right river at cols 4-5. Tiger at (4,2) jumps to (4,6).
        var pieces = new Dictionary<Position, Piece>
        {
            [new Position(4, 2)] = new Piece(Animal.Tiger, Player.Blue, new Position(4, 2)),
            [new Position(0, 0)] = new Piece(Animal.Rat, Player.Red, new Position(0, 0))
        };
        var state = CreateCustomState(pieces, Player.Blue);

        var result = MoveValidator.Validate(state, new Position(4, 2), new Position(4, 6));
        Assert.Null(result);
    }

    [Fact]
    public void Tiger_CannotJumpHorizontally_AcrossRiver()
    {
        var pieces = new Dictionary<Position, Piece>
        {
            [new Position(3, 3)] = new Piece(Animal.Tiger, Player.Blue, new Position(3, 3)),
            [new Position(0, 0)] = new Piece(Animal.Rat, Player.Red, new Position(0, 0))
        };
        var state = CreateCustomState(pieces, Player.Blue);

        var result = MoveValidator.Validate(state, new Position(3, 3), new Position(0, 3));
        Assert.NotNull(result);
        Assert.Contains("horizontally", result);
    }

    [Fact]
    public void JumpBlocked_ByRatInWater()
    {
        var pieces = new Dictionary<Position, Piece>
        {
            [new Position(1, 2)] = new Piece(Animal.Lion, Player.Blue, new Position(1, 2)),
            [new Position(1, 3)] = new Piece(Animal.Rat, Player.Red, new Position(1, 3)), // Blocking rat in water
            [new Position(0, 0)] = new Piece(Animal.Cat, Player.Red, new Position(0, 0))
        };
        var state = CreateCustomState(pieces, Player.Blue);

        var result = MoveValidator.Validate(state, new Position(1, 2), new Position(1, 6));
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
