using JungleGame.Core.Engine;
using JungleGame.Core.Model;
using JungleGame.Core.Rules;
using Xunit;

namespace JungleGame.Tests.Rules;

public class TigerJumpScenarioTests
{
    [Fact]
    public void Tiger_OnRiverBank_CanJumpVertically()
    {
        // Vertical jump = along columns. Tiger at (0,3) jumps right to (3,3) across left river cols 1-2.
        var pieces = new Dictionary<Position, Piece>
        {
            [new Position(0, 3)] = new Piece(Animal.Tiger, Player.Blue, new Position(0, 3)),
            [new Position(0, 8)] = new Piece(Animal.Rat, Player.Red, new Position(0, 8))
        };
        var state = CreateCustomState(pieces, Player.Blue);

        var moves = MoveGenerator.GenerateLegalMoves(state, Player.Blue);
        var jumpTargets = moves.Where(m => m.From == new Position(0, 3) && m.To == new Position(3, 3)).ToList();

        Assert.NotEmpty(jumpTargets);
        Assert.Null(MoveValidator.Validate(state, new Position(0, 3), new Position(3, 3)));
    }

    [Fact]
    public void Tiger_FromStartingPosition_MovesToBank_ThenJumps()
    {
        // Tiger starts at (0,0), moves down to (0,3) bank, then jumps vertically to (3,3).
        var state = GameState.CreateInitial();

        var tiger = state.GetPieceAt(new Position(0, 0));
        Assert.NotNull(tiger);
        Assert.Equal(Animal.Tiger, tiger!.Value.Animal);

        // Move 1: Tiger (0,0) → (0,1)
        var move1 = new Move(new Position(0, 0), new Position(0, 1));
        state = GameController.ApplyMove(state, move1);

        // Red does some random move
        var redMoves = MoveGenerator.GenerateLegalMoves(state, Player.Red);
        Assert.NotEmpty(redMoves);
        state = GameController.ApplyMove(state, redMoves[0]);

        // Move 2: Tiger (0,1) → (0,2)
        tiger = state.GetPieceAt(new Position(0, 1));
        Assert.NotNull(tiger);
        var move2 = new Move(new Position(0, 1), new Position(0, 2));
        state = GameController.ApplyMove(state, move2);

        // Red move
        redMoves = MoveGenerator.GenerateLegalMoves(state, Player.Red);
        state = GameController.ApplyMove(state, redMoves[0]);

        // Move 3: Tiger (0,2) → (0,3) — now on river bank at row 3
        tiger = state.GetPieceAt(new Position(0, 2));
        Assert.NotNull(tiger);
        var move3 = new Move(new Position(0, 2), new Position(0, 3));
        state = GameController.ApplyMove(state, move3);

        // Red move
        redMoves = MoveGenerator.GenerateLegalMoves(state, Player.Red);
        state = GameController.ApplyMove(state, redMoves[0]);

        // Now Tiger is at (0,3). Can it jump vertically to (3,3)?
        tiger = state.GetPieceAt(new Position(0, 3));
        Assert.NotNull(tiger);

        var moves = MoveGenerator.GenerateLegalMoves(state, Player.Blue);
        var jumpMove = moves.FirstOrDefault(m => m.From == new Position(0, 3) && m.To == new Position(3, 3));
        Assert.NotEqual(default(Move), jumpMove);
    }

    [Fact]
    public void Tiger_OnRightRiverBank_CanJumpVertically()
    {
        // Vertical jump across right river cols 4-5. Tiger at (3,3) jumps right to (6,3).
        var pieces = new Dictionary<Position, Piece>
        {
            [new Position(3, 3)] = new Piece(Animal.Tiger, Player.Blue, new Position(3, 3)),
            [new Position(0, 8)] = new Piece(Animal.Rat, Player.Red, new Position(0, 8))
        };
        var state = CreateCustomState(pieces, Player.Blue);

        var moves = MoveGenerator.GenerateLegalMoves(state, Player.Blue);
        var jumpTargets = moves.Where(m => m.From == new Position(3, 3) && m.To == new Position(6, 3)).ToList();

        Assert.NotEmpty(jumpTargets);
    }

    [Fact]
    public void Tiger_MustBeOnRiverColumn_ToJump()
    {
        // Tiger at col 0, row 0 — no river between (0,0) and (3,0), so vertical jump fails.
        var pieces = new Dictionary<Position, Piece>
        {
            [new Position(0, 0)] = new Piece(Animal.Tiger, Player.Blue, new Position(0, 0)),
            [new Position(0, 8)] = new Piece(Animal.Rat, Player.Red, new Position(0, 8))
        };
        var state = CreateCustomState(pieces, Player.Blue);

        var moves = MoveGenerator.GenerateLegalMoves(state, Player.Blue);
        var jumpTargets = moves.Where(m => m.From == new Position(0, 0) && m.To == new Position(3, 0)).ToList();

        Assert.Empty(jumpTargets);
    }

    [Fact]
    public void Tiger_JumpBlocked_ByRatInRiver()
    {
        // Tiger at (0,3) wants to jump vertically to (3,3), but Red Rat is at (1,3) in the water.
        var pieces = new Dictionary<Position, Piece>
        {
            [new Position(0, 3)] = new Piece(Animal.Tiger, Player.Blue, new Position(0, 3)),
            [new Position(1, 3)] = new Piece(Animal.Rat, Player.Red, new Position(1, 3)),
            [new Position(0, 8)] = new Piece(Animal.Cat, Player.Red, new Position(0, 8))
        };
        var state = CreateCustomState(pieces, Player.Blue);

        var moves = MoveGenerator.GenerateLegalMoves(state, Player.Blue);
        var jumpTargets = moves.Where(m => m.From == new Position(0, 3) && m.To == new Position(3, 3)).ToList();

        Assert.Empty(jumpTargets);
    }

    [Fact]
    public void Lion_CanJumpVertically()
    {
        // Lion at (0,3) jumps vertically across left river to (3,3).
        var pieces = new Dictionary<Position, Piece>
        {
            [new Position(0, 3)] = new Piece(Animal.Lion, Player.Blue, new Position(0, 3)),
            [new Position(0, 8)] = new Piece(Animal.Rat, Player.Red, new Position(0, 8))
        };
        var state = CreateCustomState(pieces, Player.Blue);

        var moves = MoveGenerator.GenerateLegalMoves(state, Player.Blue);
        var jumpTargets = moves.Where(m => m.From == new Position(0, 3) && m.To == new Position(3, 3)).ToList();

        Assert.NotEmpty(jumpTargets);
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
