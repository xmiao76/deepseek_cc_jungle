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

    private static GameState CreateState(Dictionary<Position, Piece> pieces)
        => new(
            Board.Initial,
            System.Collections.Immutable.ImmutableDictionary.CreateRange(pieces),
            Player.Blue,
            GameStatus.InProgress,
            System.Collections.Immutable.ImmutableList<Piece>.Empty,
            System.Collections.Immutable.ImmutableList<Piece>.Empty);

    [Fact]
    public void EnemyOnMyTrap_EarnsDoomedBonus()
    {
        // A Red Elephant on Blue's trap (3,1) is doomed: any Blue piece can take it.
        // Compared with the same Elephant one step back, Blue's eval must be better.
        // (Both squares are equally close to Blue's den, so the den-threat term is
        // identical in both positions.)
        var trapped = CreateState(new Dictionary<Position, Piece>
        {
            [new Position(3, 1)] = new Piece(Animal.Elephant, Player.Red, new Position(3, 1)),
            [new Position(6, 0)] = new Piece(Animal.Lion, Player.Blue, new Position(6, 0)),
            [new Position(0, 0)] = new Piece(Animal.Wolf, Player.Blue, new Position(0, 0)),
            [new Position(6, 8)] = new Piece(Animal.Rat, Player.Red, new Position(6, 8))
        });
        var free = CreateState(new Dictionary<Position, Piece>
        {
            [new Position(3, 2)] = new Piece(Animal.Elephant, Player.Red, new Position(3, 2)),
            [new Position(6, 0)] = new Piece(Animal.Lion, Player.Blue, new Position(6, 0)),
            [new Position(0, 0)] = new Piece(Animal.Wolf, Player.Blue, new Position(0, 0)),
            [new Position(6, 8)] = new Piece(Animal.Rat, Player.Red, new Position(6, 8))
        });

        int trappedEval = EvaluationFunction.Evaluate(trapped, Player.Blue);
        int freeEval = EvaluationFunction.Evaluate(free, Player.Blue);

        Assert.True(trappedEval > freeEval,
            $"Trapped enemy should raise Blue's eval (trapped: {trappedEval}, free: {freeEval})");
    }

    [Fact]
    public void LionNearOwnDen_UnderThreat_GetsEscortBonus()
    {
        // Blue Lion guards its den while a Red Elephant approaches within 3 of it.
        // The P3 den-escort term adds exactly +30 on top of the legacy eval
        // (no piece in this position sits on an enemy trap, so the doomed-piece
        // term does not interfere).
        var state = CreateState(new Dictionary<Position, Piece>
        {
            [new Position(2, 0)] = new Piece(Animal.Lion, Player.Blue, new Position(2, 0)),
            [new Position(0, 0)] = new Piece(Animal.Wolf, Player.Blue, new Position(0, 0)),
            [new Position(0, 1)] = new Piece(Animal.Cat, Player.Blue, new Position(0, 1)),
            [new Position(0, 2)] = new Piece(Animal.Dog, Player.Blue, new Position(0, 2)),
            [new Position(6, 0)] = new Piece(Animal.Rat, Player.Blue, new Position(6, 0)),
            [new Position(3, 2)] = new Piece(Animal.Elephant, Player.Red, new Position(3, 2)),
            [new Position(6, 8)] = new Piece(Animal.Wolf, Player.Red, new Position(6, 8)),
            [new Position(6, 7)] = new Piece(Animal.Cat, Player.Red, new Position(6, 7)),
            [new Position(6, 6)] = new Piece(Animal.Dog, Player.Red, new Position(6, 6)),
            [new Position(0, 8)] = new Piece(Animal.Rat, Player.Red, new Position(0, 8))
        });

        var board = SearchBoard.FromGameState(state);
        int v2 = EvaluationFunction.Evaluate(board, 0, board.CountLegalMoves(0), board.CountLegalMoves(1), legacyEval: false);
        int legacy = EvaluationFunction.Evaluate(board, 0, board.CountLegalMoves(0), board.CountLegalMoves(1), legacyEval: true);

        Assert.Equal(legacy + 30, v2);
    }

    [Fact]
    public void EscortBonus_AppliesPerSide_WhenThatSideIsThreatened()
    {
        // A Blue Wolf near Red's den makes RED's guarding Lion worth +30 (for Red),
        // i.e. exactly -30 for Blue. No piece sits on an enemy trap, so the
        // doomed-piece term does not interfere.
        var state = CreateState(new Dictionary<Position, Piece>
        {
            [new Position(3, 6)] = new Piece(Animal.Wolf, Player.Blue, new Position(3, 6)),
            [new Position(0, 0)] = new Piece(Animal.Cat, Player.Blue, new Position(0, 0)),
            [new Position(2, 8)] = new Piece(Animal.Lion, Player.Red, new Position(2, 8)),
            [new Position(0, 8)] = new Piece(Animal.Rat, Player.Red, new Position(0, 8))
        });

        var board = SearchBoard.FromGameState(state);
        int v2 = EvaluationFunction.Evaluate(board, 0, board.CountLegalMoves(0), board.CountLegalMoves(1), legacyEval: false);
        int legacy = EvaluationFunction.Evaluate(board, 0, board.CountLegalMoves(0), board.CountLegalMoves(1), legacyEval: true);

        // Blue's pieces get no escort bonus (Red threatens nothing near Blue's den),
        // but Red's guarding Lion does: -30 from Blue's perspective
        Assert.Equal(legacy - 30, v2);
    }

    [Fact]
    public void TrappedEnemy_EarnsDoomedBonus_OfRankTimes5()
    {
        // A Red Elephant on Blue's trap adds exactly 40 (= 40 * 8 / 8) points to
        // Blue's eval on top of the legacy trap penalty (no other piece in this
        // position sits on an enemy trap).
        var state = CreateState(new Dictionary<Position, Piece>
        {
            [new Position(3, 1)] = new Piece(Animal.Elephant, Player.Red, new Position(3, 1)),
            [new Position(6, 0)] = new Piece(Animal.Lion, Player.Blue, new Position(6, 0)),
            [new Position(0, 0)] = new Piece(Animal.Wolf, Player.Blue, new Position(0, 0)),
            [new Position(6, 8)] = new Piece(Animal.Rat, Player.Red, new Position(6, 8))
        });

        var board = SearchBoard.FromGameState(state);
        int v2 = EvaluationFunction.Evaluate(board, 0, board.CountLegalMoves(0), board.CountLegalMoves(1), legacyEval: false);
        int legacy = EvaluationFunction.Evaluate(board, 0, board.CountLegalMoves(0), board.CountLegalMoves(1), legacyEval: true);

        Assert.Equal(legacy + 40, v2);
    }

    [Fact]
    public void LegacyVector_IsFrozenToTheHandTunedConstants()
    {
        // EvalParameters.Legacy pins the pre-tuning hand-tuned constants; once the
        // tuning harness adopts a fitted vector into Default, this test guards the
        // freeze (the legacyEvalWeights A/B gate needs the original numbers).
        var legacy = EvalParameters.Legacy;
        Assert.Equal(100, legacy.MaterialWeight);
        Assert.Equal(1, legacy.ForwardWeight);
        Assert.Equal(12, legacy.DenOffenseWeight);
        Assert.Equal(8, legacy.DenGuardWeight);
        Assert.Equal(80, legacy.TrapPenalty);
        Assert.Equal(5, legacy.DoomedPieceWeightPerRank);
        Assert.Equal(30, legacy.DenEscortBonus);
        Assert.Equal(15, legacy.RiverBankBonus);
        Assert.Equal(10, legacy.JumpPathBonus);
        Assert.Equal(8, legacy.RatNearWaterBonus);
        Assert.Equal(12, legacy.RatInWaterBonus);
        Assert.Equal(15, legacy.ElephantRatFearWeight);
        Assert.Equal(15, legacy.ThreatStrongerWeight);
        Assert.Equal(8, legacy.ThreatEqualWeight);
        Assert.Equal(25, legacy.RatThreatensElephantPenalty);
        Assert.Equal(3, legacy.MobilityWeight);
        Assert.Equal(40, legacy.DenThreatWeight);
        Assert.Equal(200, legacy.EndgameDenThreatPenalty);
        Assert.Equal(25, legacy.EndgameAdvanceWeight);
        Assert.Equal(5, legacy.BackRankPenalty);
        Assert.Equal(0, legacy.RatNearOppDenWeight);
    }
}
