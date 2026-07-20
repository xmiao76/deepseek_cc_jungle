using JungleGame.Core.Logic;
using JungleGame.Core.Models;

namespace JungleGame.Tests.CoreTests;

public class GameEngineTests
{
    private readonly GameEngine _engine = new();

    [Fact]
    public void CreateInitialState_BlueMovesFirstByDefault()
    {
        var state = _engine.CreateInitialState();
        Assert.Equal(Player.Blue, state.CurrentPlayer);
        Assert.Equal(GamePhase.Playing, state.Phase);
    }

    [Fact]
    public void CreateInitialState_RespectsFirstPlayer_Red()
    {
        var state = _engine.CreateInitialState(Player.Red);
        Assert.Equal(Player.Red, state.CurrentPlayer);
    }

    [Fact]
    public void GetLegalMoves_ReturnsMovesForCurrentPlayer()
    {
        var state = _engine.CreateInitialState();
        var moves = _engine.GetLegalMoves(state);
        Assert.NotEmpty(moves);
        Assert.All(moves, m => Assert.Equal(Player.Blue, m.Piece.Owner));
    }

    [Fact]
    public void ApplyMove_UpdatesCurrentPlayer()
    {
        var state = _engine.CreateInitialState();
        var moves = _engine.GetLegalMoves(state);
        var move = moves.First();
        var newState = _engine.ApplyMove(state, move);
        Assert.Equal(Player.Red, newState.CurrentPlayer);
    }

    [Fact]
    public void ApplyMove_UpdatesPiecePosition()
    {
        var board = new Board();
        var lion = new Piece(PieceType.Lion, Player.Blue, new BoardPosition(4, 7));
        board.PlacePiece(lion);
        var state = new GameState(board.BuildPieceDictionary(), Player.Blue,
            GamePhase.Playing, null, null, 0, null, new List<Move>(), Player.Blue);

        var moves = _engine.GetLegalMoves(state);
        var move = moves.First();
        var newState = _engine.ApplyMove(state, move);

        Assert.True(newState.Pieces.ContainsKey(move.To));
        Assert.False(newState.Pieces.ContainsKey(move.From));
    }

    [Fact]
    public void ApplyMove_RemovesCapturedPiece()
    {
        var board = new Board();
        var lion = new Piece(PieceType.Lion, Player.Blue, new BoardPosition(4, 3));
        var cat = new Piece(PieceType.Cat, Player.Red, new BoardPosition(4, 4));
        board.PlacePiece(lion);
        board.PlacePiece(cat);

        var state = new GameState(board.BuildPieceDictionary(), Player.Blue,
            GamePhase.Playing, null, null, 0, null, new List<Move>(), Player.Blue);

        var moves = _engine.GetLegalMoves(state);
        var captureMove = moves.First(m => m.To == new BoardPosition(4, 4) && m.CapturedPiece != null);
        var newState = _engine.ApplyMove(state, captureMove);

        Assert.DoesNotContain(newState.Pieces.Values, p => p.Type == PieceType.Cat && p.Owner == Player.Red);
        Assert.True(newState.Pieces.ContainsKey(new BoardPosition(4, 4)));
        Assert.Equal(PieceType.Lion, newState.Pieces[new BoardPosition(4, 4)].Type);
    }

    [Fact]
    public void GetLegalDestinations_ReturnsCorrectSquares()
    {
        var board = new Board();
        var lion = new Piece(PieceType.Lion, Player.Blue, new BoardPosition(4, 7));
        board.PlacePiece(lion);
        var state = new GameState(board.BuildPieceDictionary(), Player.Blue,
            GamePhase.Playing, null, null, 0, null, new List<Move>(), Player.Blue);

        var destinations = _engine.GetLegalDestinations(state, new BoardPosition(4, 7));
        Assert.NotEmpty(destinations);
    }

    [Fact]
    public void GetLegalDestinations_EmptyForWrongPlayer()
    {
        var state = _engine.CreateInitialState();
        // Red Lion at (1,1); it's Blue's turn
        var destinations = _engine.GetLegalDestinations(state, new BoardPosition(1, 1));
        Assert.Empty(destinations);
    }

    [Fact]
    public void DenEntry_WinsGame()
    {
        var board = new Board();
        // Red den at (4,1); Blue Lion enters from (4,2)
        var blueLion = new Piece(PieceType.Lion, Player.Blue, new BoardPosition(4, 2));
        board.PlacePiece(blueLion);
        var state = new GameState(board.BuildPieceDictionary(), Player.Blue,
            GamePhase.Playing, null, null, 0, null, new List<Move>(), Player.Blue);

        var move = new Move(blueLion, new BoardPosition(4, 2), new BoardPosition(4, 1)) { IsDenEntry = true };
        var result = _engine.ApplyMove(state, move);

        Assert.Equal(GamePhase.GameOver, result.Phase);
        Assert.Equal(Player.Blue, result.Winner);
    }
}
