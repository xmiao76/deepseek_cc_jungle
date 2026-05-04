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
        // Tiger at (1,2) — bottom bank of left river, column 1
        var pieces = new Dictionary<Position, Piece>
        {
            [new Position(1, 2)] = new Piece(Animal.Tiger, Player.Blue, new Position(1, 2)),
            [new Position(0, 8)] = new Piece(Animal.Rat, Player.Red, new Position(0, 8))
        };
        var state = CreateCustomState(pieces, Player.Blue);

        // Generate legal moves — should include jump (1,2)→(1,6)
        var moves = MoveGenerator.GenerateLegalMoves(state, Player.Blue);
        var jumpTargets = moves.Where(m => m.From == new Position(1, 2) && m.To == new Position(1, 6)).ToList();

        Assert.NotEmpty(jumpTargets);
        Assert.Null(MoveValidator.Validate(state, new Position(1, 2), new Position(1, 6)));
    }

    [Fact]
    public void Tiger_FromStartingPosition_MovesToBank_ThenJumps()
    {
        // Simulate: Blue Tiger starts at (0,0) moves to river bank (1,2) then jumps to (1,6)
        var state = GameState.CreateInitial();

        // Blue Tiger at (0,0). Verify it exists.
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

        // Move 2: Tiger (0,1) → (1,1)
        tiger = state.GetPieceAt(new Position(0, 1));
        Assert.NotNull(tiger);
        var move2 = new Move(new Position(0, 1), new Position(1, 1));
        state = GameController.ApplyMove(state, move2);

        // Red move
        redMoves = MoveGenerator.GenerateLegalMoves(state, Player.Red);
        state = GameController.ApplyMove(state, redMoves[0]);

        // Move 3: Tiger (1,1) → (1,2) — now on river bank
        tiger = state.GetPieceAt(new Position(1, 1));
        Assert.NotNull(tiger);
        var move3 = new Move(new Position(1, 1), new Position(1, 2));
        state = GameController.ApplyMove(state, move3);

        // Red move
        redMoves = MoveGenerator.GenerateLegalMoves(state, Player.Red);
        state = GameController.ApplyMove(state, redMoves[0]);

        // Now Tiger is on river bank at (1,2). Can it jump?
        tiger = state.GetPieceAt(new Position(1, 2));
        Assert.NotNull(tiger);

        var moves = MoveGenerator.GenerateLegalMoves(state, Player.Blue);
        var jumpMove = moves.FirstOrDefault(m => m.From == new Position(1, 2) && m.To == new Position(1, 6));
        Assert.NotEqual(default(Move), jumpMove);
    }

    [Fact]
    public void Tiger_OnRightRiverBank_CanJumpVertically()
    {
        // Right river at cols 4-5. Tiger at (4,2) jumps to (4,6).
        var pieces = new Dictionary<Position, Piece>
        {
            [new Position(4, 2)] = new Piece(Animal.Tiger, Player.Blue, new Position(4, 2)),
            [new Position(0, 8)] = new Piece(Animal.Rat, Player.Red, new Position(0, 8))
        };
        var state = CreateCustomState(pieces, Player.Blue);

        var moves = MoveGenerator.GenerateLegalMoves(state, Player.Blue);
        var jumpTargets = moves.Where(m => m.From == new Position(4, 2) && m.To == new Position(4, 6)).ToList();

        Assert.NotEmpty(jumpTargets);
    }

    [Fact]
    public void Tiger_MustBeOnRiverColumn_ToJump()
    {
        // Tiger at col 0 — no river here, jump should not be possible
        var pieces = new Dictionary<Position, Piece>
        {
            [new Position(0, 2)] = new Piece(Animal.Tiger, Player.Blue, new Position(0, 2)),
            [new Position(0, 8)] = new Piece(Animal.Rat, Player.Red, new Position(0, 8))
        };
        var state = CreateCustomState(pieces, Player.Blue);

        var moves = MoveGenerator.GenerateLegalMoves(state, Player.Blue);
        var jumpTargets = moves.Where(m => m.From == new Position(0, 2) && m.To == new Position(0, 6)).ToList();

        Assert.Empty(jumpTargets);
    }

    [Fact]
    public void Tiger_JumpBlocked_ByRatInRiver()
    {
        // Tiger at (1,2) wants to jump to (1,6), but Red Rat is at (1,3) in the water
        var pieces = new Dictionary<Position, Piece>
        {
            [new Position(1, 2)] = new Piece(Animal.Tiger, Player.Blue, new Position(1, 2)),
            [new Position(1, 3)] = new Piece(Animal.Rat, Player.Red, new Position(1, 3)),
            [new Position(0, 8)] = new Piece(Animal.Cat, Player.Red, new Position(0, 8))
        };
        var state = CreateCustomState(pieces, Player.Blue);

        var moves = MoveGenerator.GenerateLegalMoves(state, Player.Blue);
        var jumpTargets = moves.Where(m => m.From == new Position(1, 2) && m.To == new Position(1, 6)).ToList();

        Assert.Empty(jumpTargets);
    }

    [Fact]
    public void Lion_CanJumpHorizontally()
    {
        // Lion at (0,3) jumps horizontally across left river to (3,3)
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
