using JungleGame.Core.Engine;
using JungleGame.Core.Model;
using JungleGame.Core.Rules;
using Xunit;
using MoveValidator = JungleGame.Core.Rules.MoveValidator;

namespace JungleGame.Tests.Engine;

public class MoveGeneratorTests
{
    [Fact]
    public void StartingPosition_HasLegalMoves()
    {
        var state = GameState.CreateInitial();
        var moves = MoveGenerator.GenerateLegalMoves(state, Player.Blue);
        Assert.NotEmpty(moves);
        Assert.All(moves, m =>
        {
            var error = MoveValidator.Validate(state, m.From, m.To);
            Assert.Null(error);
        });
    }

    [Fact]
    public void AllGeneratedMoves_AreValid()
    {
        var state = GameState.CreateInitial();
        for (int i = 0; i < 10; i++)
        {
            var moves = MoveGenerator.GenerateLegalMoves(state, state.CurrentTurn);
            Assert.NotEmpty(moves);

            // Apply a random move
            var random = new Random(i);
            var move = moves[random.Next(moves.Count)];
            state = GameController.ApplyMove(state, move);

            if (state.Status != GameStatus.InProgress)
                break;
        }
    }

    [Fact]
    public void Blue_StartingMoves_Count()
    {
        var state = GameState.CreateInitial();
        int count = MoveGenerator.CountLegalMoves(state, Player.Blue);
        Assert.True(count > 0);
        Assert.True(count <= 30); // Reasonable upper bound for starting position
    }

    [Fact]
    public void DenInvasion_EndsGame()
    {
        // Create a custom state where Blue piece is next to Red's den
        var pieces = new Dictionary<Position, Piece>
        {
            [new Position(3, 7)] = new Piece(Animal.Lion, Player.Blue, new Position(3, 7)),
            [new Position(0, 8)] = new Piece(Animal.Lion, Player.Red, new Position(0, 8))
        };

        var state = new GameState(
            Board.Initial,
            System.Collections.Immutable.ImmutableDictionary.CreateRange(pieces),
            Player.Blue,
            GameStatus.InProgress,
            System.Collections.Immutable.ImmutableList<Piece>.Empty,
            System.Collections.Immutable.ImmutableList<Piece>.Empty);

        // Move Blue Lion into Red's den at (3,8)
        var result = MoveValidator.Validate(state, new Position(3, 7), new Position(3, 8));
        Assert.Null(result);

        state = GameController.ApplyMove(state, new Move(new Position(3, 7), new Position(3, 8)));
        Assert.Equal(GameStatus.BlueWins, state.Status);
    }
}
