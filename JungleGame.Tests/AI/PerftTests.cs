using JungleGame.Core.AI;
using JungleGame.Core.Engine;
using JungleGame.Core.Model;
using JungleGame.Tests.Helpers;
using Xunit;

namespace JungleGame.Tests.AI;

/// <summary>
/// Perft-style differential counting: the independent reference generator
/// (NaiveMoveGenerator), the public MoveGenerator, and the internal SearchBoard
/// must all produce the same move counts at depth 1-3 on a variety of positions.
/// </summary>
public class PerftTests
{
    public static IEnumerable<object[]> PerftPositions()
    {
        // Initial position
        yield return new object[] { "initial", GameState.CreateInitial() };

        // Midgame position from seeded random play
        var midgame = GameState.CreateInitial();
        var random = new Random(42);
        for (int i = 0; i < 10 && midgame.Status == GameStatus.InProgress; i++)
        {
            var moves = MoveGenerator.GenerateLegalMoves(midgame, midgame.CurrentTurn);
            midgame = GameController.ApplyMove(midgame, moves[random.Next(moves.Count)]);
        }
        yield return new object[] { "midgame", midgame };

        // Tactical position with capture threats
        yield return new object[] { "tactical", new TestBoardBuilder()
            .WithPiece(Animal.Wolf, Player.Blue, 3, 2)
            .WithPiece(Animal.Lion, Player.Blue, 0, 0)
            .WithPiece(Animal.Dog, Player.Red, 3, 4)
            .WithPiece(Animal.Rat, Player.Red, 3, 5)
            .WithPiece(Animal.Cat, Player.Red, 0, 1)
            .WithPiece(Animal.Rat, Player.Red, 6, 8)
            .Build() };

        // Rats in water (jump blocking and water moves)
        yield return new object[] { "ratsInWater", new TestBoardBuilder()
            .WithPiece(Animal.Rat, Player.Blue, 1, 3)
            .WithPiece(Animal.Lion, Player.Blue, 1, 2)
            .WithPiece(Animal.Rat, Player.Red, 4, 4)
            .WithPiece(Animal.Wolf, Player.Red, 3, 5)
            .Build() };

        // Endgame: few pieces around the dens
        yield return new object[] { "endgame", new TestBoardBuilder()
            .WithPiece(Animal.Lion, Player.Blue, 3, 7)
            .WithPiece(Animal.Wolf, Player.Blue, 0, 0)
            .WithPiece(Animal.Dog, Player.Red, 2, 6)
            .WithPiece(Animal.Rat, Player.Red, 0, 8)
            .Build() };
    }

    [Theory]
    [MemberData(nameof(PerftPositions))]
    public void MoveCounts_MatchReferenceGenerator_AtDepths1To3(string name, GameState state)
    {
        var reference = NaiveMoveGenerator.Generate(state);
        var publicMoves = MoveGenerator.GenerateLegalMoves(state, state.CurrentTurn);
        var board = SearchBoard.FromGameState(state);
        var buf = new SearchMove[128];
        int fastCount = board.GenerateMoves((int)state.CurrentTurn, buf);

        Assert.True(reference.Count == publicMoves.Count,
            $"[{name}] reference {reference.Count} vs public {publicMoves.Count}");
        Assert.True(reference.Count == fastCount,
            $"[{name}] reference {reference.Count} vs SearchBoard {fastCount}");
    }

    [Theory]
    [MemberData(nameof(PerftPositions))]
    public void PerftCounts_MatchBetweenGenerators(string name, GameState state)
    {
        for (int depth = 1; depth <= 3; depth++)
        {
            long reference = PerftReference(state, depth);
            long publicCount = PerftPublic(state, depth);
            Assert.True(reference == publicCount,
                $"[{name}] depth {depth}: reference {reference} vs public {publicCount}");
        }
    }

    private static long PerftReference(GameState state, int depth)
    {
        if (depth == 0)
            return 1;

        long total = 0;
        foreach (var (from, to) in NaiveMoveGenerator.Generate(state))
        {
            var move = new Move(from, to, state.GetPieceAt(to));
            var next = GameController.ApplyMove(state, move);
            total += next.Status == GameStatus.InProgress ? PerftReference(next, depth - 1) : 1;
        }
        return total;
    }

    private static long PerftPublic(GameState state, int depth)
    {
        if (depth == 0)
            return 1;

        long total = 0;
        foreach (var move in MoveGenerator.GenerateLegalMoves(state, state.CurrentTurn))
        {
            var next = GameController.ApplyMove(state, move);
            total += next.Status == GameStatus.InProgress ? PerftPublic(next, depth - 1) : 1;
        }
        return total;
    }
}
