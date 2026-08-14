using System.Collections.Immutable;

namespace JungleGame.Core.Model;

public enum GameStatus
{
    InProgress,
    BlueWins,
    RedWins,
    Draw
}

public class GameState
{
    public Board Board { get; }
    public ImmutableDictionary<Position, Piece> Pieces { get; }
    public Player CurrentTurn { get; }
    public GameStatus Status { get; }
    public ImmutableList<Piece> CapturedBlue { get; } // Pieces captured FROM Blue
    public ImmutableList<Piece> CapturedRed { get; }  // Pieces captured FROM Red
    public ImmutableList<ulong> History { get; } // Zobrist hash after each applied move

    public GameState(
        Board board,
        ImmutableDictionary<Position, Piece> pieces,
        Player currentTurn,
        GameStatus status,
        ImmutableList<Piece> capturedBlue,
        ImmutableList<Piece> capturedRed,
        ImmutableList<ulong>? history = null)
    {
        Board = board;
        Pieces = pieces;
        CurrentTurn = currentTurn;
        Status = status;
        CapturedBlue = capturedBlue;
        CapturedRed = capturedRed;
        History = history ?? ImmutableList<ulong>.Empty;
    }

    public static GameState CreateInitial()
    {
        var pieces = ImmutableDictionary.CreateBuilder<Position, Piece>();

        // Blue pieces (bottom, rows 0-2)
        // Layout from Wikipedia board image (180° rotational symmetry):
        // Each side: 2 on back rank, 2 on middle rank, 4 on front rank

        // Row 0 (back rank, a1-g1): Tiger at a1, Lion at g1
        pieces.Add(new Position(0, 0), new Piece(Animal.Tiger, Player.Blue, new Position(0, 0)));
        pieces.Add(new Position(6, 0), new Piece(Animal.Lion, Player.Blue, new Position(6, 0)));

        // Row 1 (middle rank, a2-g2): Cat at b2, Dog at f2
        pieces.Add(new Position(1, 1), new Piece(Animal.Cat, Player.Blue, new Position(1, 1)));
        pieces.Add(new Position(5, 1), new Piece(Animal.Dog, Player.Blue, new Position(5, 1)));

        // Row 2 (front rank, a3-g3): Elephant at a3, Wolf at c3, Leopard at e3, Rat at g3
        pieces.Add(new Position(0, 2), new Piece(Animal.Elephant, Player.Blue, new Position(0, 2)));
        pieces.Add(new Position(2, 2), new Piece(Animal.Wolf, Player.Blue, new Position(2, 2)));
        pieces.Add(new Position(4, 2), new Piece(Animal.Leopard, Player.Blue, new Position(4, 2)));
        pieces.Add(new Position(6, 2), new Piece(Animal.Rat, Player.Blue, new Position(6, 2)));

        // Red pieces (top, rows 6-8)
        // 180° rotational symmetry from Blue

        // Row 6 (front rank, a7-g7): Rat at a7, Leopard at c7, Wolf at e7, Elephant at g7
        pieces.Add(new Position(0, 6), new Piece(Animal.Rat, Player.Red, new Position(0, 6)));
        pieces.Add(new Position(2, 6), new Piece(Animal.Leopard, Player.Red, new Position(2, 6)));
        pieces.Add(new Position(4, 6), new Piece(Animal.Wolf, Player.Red, new Position(4, 6)));
        pieces.Add(new Position(6, 6), new Piece(Animal.Elephant, Player.Red, new Position(6, 6)));

        // Row 7 (middle rank, a8-g8): Dog at b8, Cat at f8
        pieces.Add(new Position(1, 7), new Piece(Animal.Dog, Player.Red, new Position(1, 7)));
        pieces.Add(new Position(5, 7), new Piece(Animal.Cat, Player.Red, new Position(5, 7)));

        // Row 8 (back rank, a9-g9): Lion at a9, Tiger at g9
        pieces.Add(new Position(0, 8), new Piece(Animal.Lion, Player.Red, new Position(0, 8)));
        pieces.Add(new Position(6, 8), new Piece(Animal.Tiger, Player.Red, new Position(6, 8)));

        var initialPieces = pieces.ToImmutable();

        // Seed the repetition history with the opening position so a shuffle back
        // to the start counts toward the three-fold draw
        var history = ImmutableList.Create(Zobrist.ComputeHash(initialPieces, Player.Blue));

        return new GameState(
            Board.Initial,
            initialPieces,
            Player.Blue, // Blue moves first per standard rules
            GameStatus.InProgress,
            ImmutableList<Piece>.Empty,
            ImmutableList<Piece>.Empty,
            history);
    }

    public Piece? GetPieceAt(Position pos) =>
        Pieces.TryGetValue(pos, out var piece) ? piece : null;

    public ImmutableList<Piece> GetPlayerPieces(Player player) =>
        Pieces.Values.Where(p => p.Owner == player).ToImmutableList();

    public bool HasPieceAt(Position pos) => Pieces.ContainsKey(pos);
}
