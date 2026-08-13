using JungleGame.Core.AI;
using JungleGame.Core.Engine;
using JungleGame.Core.Model;
using Xunit;

namespace JungleGame.Tests.AI;

public class ZobristTests
{
    [Fact]
    public void Hash_IsDeterministic_ForSameState()
    {
        var state1 = GameState.CreateInitial();
        var state2 = GameState.CreateInitial();

        Assert.Equal(Zobrist.ComputeHash(state1), Zobrist.ComputeHash(state2));
        Assert.NotEqual(0UL, Zobrist.ComputeHash(state1));
    }

    [Fact]
    public void Hash_Differs_BySideToMove()
    {
        var pieces = GameState.CreateInitial().Pieces;
        var blueToMove = new GameState(
            Board.Initial, pieces, Player.Blue, GameStatus.InProgress,
            System.Collections.Immutable.ImmutableList<Piece>.Empty,
            System.Collections.Immutable.ImmutableList<Piece>.Empty);
        var redToMove = new GameState(
            Board.Initial, pieces, Player.Red, GameStatus.InProgress,
            System.Collections.Immutable.ImmutableList<Piece>.Empty,
            System.Collections.Immutable.ImmutableList<Piece>.Empty);

        Assert.NotEqual(Zobrist.ComputeHash(blueToMove), Zobrist.ComputeHash(redToMove));
    }

    [Fact]
    public void Hash_Differs_ByPiecePlacement()
    {
        var state1 = GameState.CreateInitial();
        var state2 = GameController.ApplyMove(
            state1, MoveGenerator.GenerateLegalMoves(state1, Player.Blue)[0]);

        Assert.NotEqual(Zobrist.ComputeHash(state1), Zobrist.ComputeHash(state2));
    }

    [Fact]
    public void IncrementalHash_MatchesFullComputation_AfterMoveChain()
    {
        var state = GameState.CreateInitial();
        var board = SearchBoard.FromGameState(state);
        var random = new Random(7);

        for (int i = 0; i < 30 && state.Status == GameStatus.InProgress; i++)
        {
            int side = (int)state.CurrentTurn;
            var publicMoves = MoveGenerator.GenerateLegalMoves(state, state.CurrentTurn);
            var move = publicMoves[random.Next(publicMoves.Count)];

            // Apply the matching search move to the incremental board
            var buf = new SearchMove[128];
            int count = board.GenerateMoves(side, buf);
            var searchMove = default(SearchMove);
            for (int j = 0; j < count; j++)
            {
                if (buf[j].From == move.From.Row * 7 + move.From.Col &&
                    buf[j].To == move.To.Row * 7 + move.To.Col)
                {
                    searchMove = buf[j];
                    break;
                }
            }

            state = GameController.ApplyMove(state, move);
            board.ApplyMove(searchMove);

            Assert.Equal(Zobrist.ComputeHash(state), board.Hash);
        }
    }
}
