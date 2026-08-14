using JungleGame.Core.AI;
using JungleGame.Core.Engine;
using JungleGame.Core.Model;
using Xunit;

namespace JungleGame.Tests.Integration;

public class FullGameTests
{
    [Fact]
    public void FullGame_RandomPlay_CompletesWithoutCrash()
    {
        var state = GameState.CreateInitial();
        var random = new Random(12345);
        int moveCount = 0;
        const int maxMoves = 500;

        while (state.Status == GameStatus.InProgress && moveCount < maxMoves)
        {
            var moves = MoveGenerator.GenerateLegalMoves(state, state.CurrentTurn);
            if (moves.Count == 0)
            {
                Assert.Fail($"No legal moves for {state.CurrentTurn} at move {moveCount}");
            }

            var move = moves[random.Next(moves.Count)];

            // Verify move is valid
            var error = JungleGame.Core.Rules.MoveValidator.Validate(state, move.From, move.To);
            Assert.Null(error);

            state = GameController.ApplyMove(state, move);
            moveCount++;
        }

        Assert.True(moveCount > 0);
        // Game should have ended one way or another
        if (moveCount >= maxMoves)
        {
            // 500-move draw-like situation — acceptable
            Assert.Equal(GameStatus.InProgress, state.Status);
        }
    }

    [Fact]
    public void AIvsAI_QuickGame_Completes()
    {
        var state = GameState.CreateInitial();
        // Depth-limited so the whole game finishes in a few seconds even at the
        // 100-move cap; the point of the test is engine/game integration, not strength.
        var engine = new MinimaxEngine(TimeSpan.FromMilliseconds(100), maxDepth: 5);
        int moveCount = 0;
        const int maxMoves = 100;

        while (state.Status == GameStatus.InProgress && moveCount < maxMoves)
        {
            var move = engine.FindBestMove(state);
            if (move == null)
            {
                Assert.Fail($"AI returned no move for {state.CurrentTurn} at move {moveCount}");
            }

            var error = JungleGame.Core.Rules.MoveValidator.Validate(state, move.Value.From, move.Value.To);
            Assert.Null(error);

            state = GameController.ApplyMove(state, move.Value);
            moveCount++;
        }

        Assert.True(moveCount > 0);
    }

    [Fact]
    public void InitialState_HasCorrectPieceCount()
    {
        var state = GameState.CreateInitial();
        Assert.Equal(8, state.GetPlayerPieces(Player.Blue).Count);
        Assert.Equal(8, state.GetPlayerPieces(Player.Red).Count);
        Assert.Equal(16, state.Pieces.Count);
    }

    [Fact]
    public void ElectricRat_CannotCaptureElephant_FromWater()
    {
        // Direct rule check: a rat in water cannot strike an elephant on land
        var ratInWater = new Piece(Animal.Rat, Player.Blue, new Position(1, 3));
        var elephantOnLand = new Piece(Animal.Elephant, Player.Red, new Position(1, 2));
        Assert.True(Board.Initial.IsWater(ratInWater.Position));
        Assert.False(JungleGame.Core.Rules.CaptureResolver.CanCapture(ratInWater, elephantOnLand, Board.Initial));

        // In-game: the capture move must not be generated, while the rat still
        // has its other water moves
        var pieces = new Dictionary<Position, Piece>
        {
            [new Position(1, 3)] = ratInWater,
            [new Position(1, 2)] = elephantOnLand,
            [new Position(0, 0)] = new Piece(Animal.Lion, Player.Blue, new Position(0, 0)),
            [new Position(6, 8)] = new Piece(Animal.Wolf, Player.Red, new Position(6, 8))
        };
        var state = new GameState(
            Board.Initial,
            System.Collections.Immutable.ImmutableDictionary.CreateRange(pieces),
            Player.Blue,
            GameStatus.InProgress,
            System.Collections.Immutable.ImmutableList<Piece>.Empty,
            System.Collections.Immutable.ImmutableList<Piece>.Empty);

        var moves = MoveGenerator.GenerateLegalMoves(state, Player.Blue);
        Assert.DoesNotContain(moves, m => m.From == new Position(1, 3) && m.To == new Position(1, 2));
        Assert.Contains(moves, m => m.From == new Position(1, 3) && m.To == new Position(1, 4));
    }
}
