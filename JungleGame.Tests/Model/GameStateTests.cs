using System.Collections.Immutable;
using JungleGame.Core.Model;
using Xunit;

namespace JungleGame.Tests.Model;

public class GameStateTests
{
    private static GameState CreateState(Dictionary<Position, Piece> pieces)
        => new(
            Board.Initial,
            ImmutableDictionary.CreateRange(pieces),
            Player.Blue,
            GameStatus.InProgress,
            ImmutableList<Piece>.Empty,
            ImmutableList<Piece>.Empty);

    [Fact]
    public void GetPieceAt_Miss_ReturnsNull()
    {
        var state = CreateState(new Dictionary<Position, Piece>
        {
            [new Position(0, 0)] = new Piece(Animal.Lion, Player.Blue, new Position(0, 0))
        });

        Assert.NotNull(state.GetPieceAt(new Position(0, 0)));
        Assert.Null(state.GetPieceAt(new Position(3, 4)));
    }

    [Fact]
    public void HasPieceAt_ReflectsOccupancy()
    {
        var state = CreateState(new Dictionary<Position, Piece>
        {
            [new Position(0, 0)] = new Piece(Animal.Lion, Player.Blue, new Position(0, 0))
        });

        Assert.True(state.HasPieceAt(new Position(0, 0)));
        Assert.False(state.HasPieceAt(new Position(3, 4)));
    }

    [Fact]
    public void History_DefaultsToEmpty()
    {
        var state = CreateState(new Dictionary<Position, Piece>());
        Assert.Empty(state.History);
    }

    [Fact]
    public void CreateInitial_SeedsHistoryWithOpeningHash()
    {
        var state = GameState.CreateInitial();
        Assert.Single(state.History);
        Assert.Equal(Zobrist.ComputeHash(state.Pieces, Player.Blue), state.History[0]);
    }

    [Fact]
    public void GetPlayerPieces_ReturnsOnlyOwnedPieces()
    {
        var state = CreateState(new Dictionary<Position, Piece>
        {
            [new Position(0, 0)] = new Piece(Animal.Lion, Player.Blue, new Position(0, 0)),
            [new Position(1, 0)] = new Piece(Animal.Tiger, Player.Blue, new Position(1, 0)),
            [new Position(6, 8)] = new Piece(Animal.Wolf, Player.Red, new Position(6, 8))
        });

        var blue = state.GetPlayerPieces(Player.Blue);
        var red = state.GetPlayerPieces(Player.Red);

        Assert.Equal(2, blue.Count);
        Assert.All(blue, p => Assert.Equal(Player.Blue, p.Owner));
        Assert.Single(red);
        Assert.Equal(Player.Red, red[0].Owner);
    }
}
