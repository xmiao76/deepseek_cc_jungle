using JungleGame.Core.Engine;
using JungleGame.Core.Model;
using JungleGame.Core.Rules;
using Xunit;

namespace JungleGame.Tests.Rules;

public class MoveValidatorTests
{
    private GameState GetInitialState() => GameState.CreateInitial();

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

    [Fact]
    public void BasicOrthogonalMove_IsLegal()
    {
        var state = GetInitialState();
        // Blue Tiger at (0,0) moves to (1,0) — land, empty
        var result = MoveValidator.Validate(state, new Position(0, 0), new Position(1, 0));
        Assert.Null(result);
    }

    [Fact]
    public void DiagonalMove_IsIllegal()
    {
        var state = GetInitialState();
        // Blue Lion at (6,0) tries diagonal to (5,1)
        var result = MoveValidator.Validate(state, new Position(6, 0), new Position(5, 1));
        Assert.NotNull(result);
        Assert.Contains("diagonals", result);
    }

    [Fact]
    public void MoveTooFar_WithoutJump_IsIllegal()
    {
        var state = GetInitialState();
        // Blue Rat at (3,2) tries to move 2 squares to (3,0) — too far without jump
        var result = MoveValidator.Validate(state, new Position(3, 2), new Position(3, 0));
        Assert.NotNull(result);
    }

    [Fact]
    public void RatCanEnterWater()
    {
        var pieces = new Dictionary<Position, Piece>
        {
            [new Position(1, 2)] = new Piece(Animal.Rat, Player.Blue, new Position(1, 2)),
            [new Position(0, 8)] = new Piece(Animal.Cat, Player.Red, new Position(0, 8))
        };
        var state = CreateCustomState(pieces, Player.Blue);
        // Rat at (1,2) enters water at (1,3) — col 1 is river at row 3
        Assert.Null(MoveValidator.Validate(state, new Position(1, 2), new Position(1, 3)));
    }

    [Fact]
    public void NonRat_CannotEnterWater()
    {
        // Custom state: Blue Cat adjacent to river
        var pieces = new Dictionary<Position, Piece>
        {
            [new Position(1, 2)] = new Piece(Animal.Cat, Player.Blue, new Position(1, 2)),
            [new Position(0, 8)] = new Piece(Animal.Rat, Player.Red, new Position(0, 8))
        };
        var state = CreateCustomState(pieces, Player.Blue);
        // Blue Cat at (1,2) tries to enter water at (1,3) — should be illegal for non-rat
        var result = MoveValidator.Validate(state, new Position(1, 2), new Position(1, 3));
        Assert.NotNull(result);
        Assert.Contains("Rat", result);
    }

    [Fact]
    public void CannotMoveIntoOwnDen()
    {
        var pieces = new Dictionary<Position, Piece>
        {
            [new Position(2, 0)] = new Piece(Animal.Wolf, Player.Blue, new Position(2, 0)),
            [new Position(0, 8)] = new Piece(Animal.Cat, Player.Red, new Position(0, 8))
        };
        var state = CreateCustomState(pieces, Player.Blue);
        // Wolf at (2,0) tries to enter Blue den at (3,0)
        var result = MoveValidator.Validate(state, new Position(2, 0), new Position(3, 0));
        Assert.NotNull(result);
        Assert.Contains("den", result);
    }

    [Fact]
    public void OutOfBounds_IsIllegal()
    {
        var state = GetInitialState();
        // Blue Lion at (6,0) tries to move to column 7 (out of bounds)
        var result = MoveValidator.Validate(state, new Position(6, 0), new Position(7, 0));
        Assert.NotNull(result);
    }

    [Fact]
    public void SameSquare_IsIllegal()
    {
        var state = GetInitialState();
        // Blue Lion at (6,0) tries to stay in place
        var result = MoveValidator.Validate(state, new Position(6, 0), new Position(6, 0));
        Assert.NotNull(result);
    }

    [Fact]
    public void WrongTurn_IsIllegal()
    {
        var state = GetInitialState();
        // Red Lion at (0,8) tries to move on Blue's turn
        var result = MoveValidator.Validate(state, new Position(0, 8), new Position(0, 7));
        Assert.NotNull(result);
        Assert.Contains("turn", result);
    }

    [Fact]
    public void CannotMoveOntoOwnPiece()
    {
        // Custom state: two Blue pieces adjacent
        var pieces = new Dictionary<Position, Piece>
        {
            [new Position(3, 3)] = new Piece(Animal.Lion, Player.Blue, new Position(3, 3)),
            [new Position(3, 4)] = new Piece(Animal.Cat, Player.Blue, new Position(3, 4))
        };
        var state = CreateCustomState(pieces, Player.Blue);
        // Blue Lion at (3,3) tries to move onto Blue Cat at (3,4)
        var result = MoveValidator.Validate(state, new Position(3, 3), new Position(3, 4));
        Assert.NotNull(result);
        Assert.Contains("capture", result);
    }
}
