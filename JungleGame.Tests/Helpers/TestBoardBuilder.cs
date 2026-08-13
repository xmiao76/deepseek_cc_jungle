using System.Collections.Immutable;
using JungleGame.Core.Model;

namespace JungleGame.Tests.Helpers;

/// <summary>
/// Fluent builder for custom game states. Use for all new tests that need a
/// constructed position:
///
///     var state = new TestBoardBuilder()
///         .WithPiece(Animal.Lion, Player.Blue, 3, 7)
///         .WithPiece(Animal.Rat, Player.Red, 0, 0)
///         .WithTurn(Player.Blue)
///         .Build();
/// </summary>
public class TestBoardBuilder
{
    private readonly Dictionary<Position, Piece> _pieces = new();
    private Player _turn = Player.Blue;
    private GameStatus _status = GameStatus.InProgress;

    public TestBoardBuilder WithPiece(Animal animal, Player owner, int col, int row)
    {
        var pos = new Position(col, row);
        _pieces[pos] = new Piece(animal, owner, pos);
        return this;
    }

    public TestBoardBuilder WithTurn(Player turn)
    {
        _turn = turn;
        return this;
    }

    public TestBoardBuilder WithStatus(GameStatus status)
    {
        _status = status;
        return this;
    }

    public GameState Build() => new(
        Board.Initial,
        ImmutableDictionary.CreateRange(_pieces),
        _turn,
        _status,
        ImmutableList<Piece>.Empty,
        ImmutableList<Piece>.Empty);
}
