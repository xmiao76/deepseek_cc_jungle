using JungleGame.Core.AI;
using JungleGame.Core.Model;
using Xunit;

namespace JungleGame.Tests.AI;

public class EvaluationTests
{
    [Fact]
    public void StartingPosition_IsSymmetric()
    {
        var state = GameState.CreateInitial();
        int blueEval = EvaluationFunction.Evaluate(state, Player.Blue);
        int redEval = EvaluationFunction.Evaluate(state, Player.Red);

        // Starting position should be nearly symmetric
        Assert.True(Math.Abs(blueEval + redEval) < 20,
            $"Blue eval ({blueEval}) should be roughly -Red eval ({redEval})");
    }

    [Fact]
    public void DenCapture_IsDecisiveWin()
    {
        // Setup Blue piece on Red's den
        var pieces = new Dictionary<Position, Piece>
        {
            [new Position(3, 8)] = new Piece(Animal.Lion, Player.Blue, new Position(3, 8)),
            [new Position(0, 0)] = new Piece(Animal.Rat, Player.Red, new Position(0, 0))
        };
        var state = new GameState(
            Board.Initial,
            System.Collections.Immutable.ImmutableDictionary.CreateRange(pieces),
            Player.Red,
            GameStatus.BlueWins,
            System.Collections.Immutable.ImmutableList<Piece>.Empty,
            System.Collections.Immutable.ImmutableList<Piece>.Empty);

        int score = EvaluationFunction.Evaluate(state, Player.Blue);
        Assert.Equal(1000000, score);
    }

    [Fact]
    public void TrappedPiece_ReducesValue()
    {
        // Elephant on trap vs Elephant off trap
        var elephantOnTrap = new Piece(Animal.Elephant, Player.Blue, new Position(2, 8));
        int onTrapRank = JungleGame.Core.Rules.CaptureResolver.GetEffectiveRank(elephantOnTrap, Board.Initial);

        var elephantOffTrap = new Piece(Animal.Elephant, Player.Blue, new Position(3, 2));
        int offTrapRank = JungleGame.Core.Rules.CaptureResolver.GetEffectiveRank(elephantOffTrap, Board.Initial);

        Assert.True(onTrapRank < offTrapRank);
    }
}
