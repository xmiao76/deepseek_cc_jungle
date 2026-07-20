using JungleGame.Core.Models;

namespace JungleGame.Core.Logic;

/// <summary>
/// Represents the 7x9 Jungle board with terrain definitions, piece placement,
/// and utilities for querying terrain and pieces. Supports deep cloning for AI search.
/// </summary>
public class Board
{
    public const int Cols = 7;
    public const int Rows = 9;

    private readonly Square[,] _squares;

    public Board()
    {
        _squares = new Square[Cols, Rows];
        for (int c = 0; c < Cols; c++)
        {
            for (int r = 0; r < Rows; r++)
            {
                var pos = new BoardPosition(c + 1, r + 1);
                var terrain = DetermineTerrain(pos);
                _squares[c, r] = new Square(pos, terrain);
            }
        }
    }

    /// <summary>
    /// Static factory: creates a board with all pieces in their standard starting positions.
    /// Layout from position.txt — rotationally symmetric (180°):
    /// Red = NORTH = top (rows 1-3), Blue = SOUTH = bottom (rows 7-9).
    /// </summary>
    public static Board CreateInitial()
    {
        var board = new Board();

        // Red pieces (North/Top, rows 1-3) — lowercase in position.txt
        // Row 1: l . . . . . t
        board.PlacePiece(new Piece(PieceType.Lion, Player.Red, new BoardPosition(1, 1)));
        board.PlacePiece(new Piece(PieceType.Tiger, Player.Red, new BoardPosition(7, 1)));
        // Row 2: . d . . . c .
        board.PlacePiece(new Piece(PieceType.Dog, Player.Red, new BoardPosition(2, 2)));
        board.PlacePiece(new Piece(PieceType.Cat, Player.Red, new BoardPosition(6, 2)));
        // Row 3: r . p . w . e
        board.PlacePiece(new Piece(PieceType.Rat, Player.Red, new BoardPosition(1, 3)));
        board.PlacePiece(new Piece(PieceType.Leopard, Player.Red, new BoardPosition(3, 3)));
        board.PlacePiece(new Piece(PieceType.Wolf, Player.Red, new BoardPosition(5, 3)));
        board.PlacePiece(new Piece(PieceType.Elephant, Player.Red, new BoardPosition(7, 3)));

        // Blue pieces (South/Bottom, rows 7-9) — uppercase in position.txt
        // Row 7: E . W . P . R
        board.PlacePiece(new Piece(PieceType.Elephant, Player.Blue, new BoardPosition(1, 7)));
        board.PlacePiece(new Piece(PieceType.Wolf, Player.Blue, new BoardPosition(3, 7)));
        board.PlacePiece(new Piece(PieceType.Leopard, Player.Blue, new BoardPosition(5, 7)));
        board.PlacePiece(new Piece(PieceType.Rat, Player.Blue, new BoardPosition(7, 7)));
        // Row 8: . C . . . D .
        board.PlacePiece(new Piece(PieceType.Cat, Player.Blue, new BoardPosition(2, 8)));
        board.PlacePiece(new Piece(PieceType.Dog, Player.Blue, new BoardPosition(6, 8)));
        // Row 9: T . . . . . L
        board.PlacePiece(new Piece(PieceType.Tiger, Player.Blue, new BoardPosition(1, 9)));
        board.PlacePiece(new Piece(PieceType.Lion, Player.Blue, new BoardPosition(7, 9)));

        return board;
    }

    public Square GetSquare(BoardPosition pos)
    {
        return _squares[pos.Col - 1, pos.Row - 1];
    }

    public TerrainType GetTerrain(BoardPosition pos)
    {
        return _squares[pos.Col - 1, pos.Row - 1].Terrain;
    }

    public Piece? GetPiece(BoardPosition pos)
    {
        return _squares[pos.Col - 1, pos.Row - 1].Occupant;
    }

    public void PlacePiece(Piece piece)
    {
        _squares[piece.Position.Col - 1, piece.Position.Row - 1].Occupant = piece;
    }

    public void RemovePiece(BoardPosition pos)
    {
        _squares[pos.Col - 1, pos.Row - 1].Occupant = null;
    }

    public void MovePiece(Piece piece, BoardPosition from, BoardPosition to)
    {
        RemovePiece(from);
        piece.Position = to;
        PlacePiece(piece);
    }

    /// <summary>
    /// Returns all living pieces belonging to the specified player.
    /// </summary>
    public List<Piece> GetPieces(Player player)
    {
        var pieces = new List<Piece>();
        for (int c = 0; c < Cols; c++)
        {
            for (int r = 0; r < Rows; r++)
            {
                var occupant = _squares[c, r].Occupant;
                if (occupant != null && occupant.Owner == player && occupant.IsAlive)
                {
                    pieces.Add(occupant);
                }
            }
        }
        return pieces;
    }

    /// <summary>
    /// Returns the position of a specific piece type for a player, or null if captured.
    /// </summary>
    public BoardPosition? FindPiecePosition(PieceType type, Player player)
    {
        for (int c = 0; c < Cols; c++)
        {
            for (int r = 0; r < Rows; r++)
            {
                var occupant = _squares[c, r].Occupant;
                if (occupant != null && occupant.Type == type && occupant.Owner == player && occupant.IsAlive)
                {
                    return occupant.Position;
                }
            }
        }
        return null;
    }

    /// <summary>
    /// Creates a deep copy of the board for AI search.
    /// </summary>
    public Board Clone()
    {
        var clone = new Board();
        for (int c = 0; c < Cols; c++)
        {
            for (int r = 0; r < Rows; r++)
            {
                var occupant = _squares[c, r].Occupant;
                if (occupant != null)
                {
                    var clonedPiece = occupant.Clone();
                    clone.PlacePiece(clonedPiece);
                }
            }
        }
        return clone;
    }

    // ===== Static terrain definitions =====

    public static bool IsWater(BoardPosition pos)
    {
        return pos.Col >= 2 && pos.Col <= 3 && pos.Row >= 4 && pos.Row <= 6
            || pos.Col >= 5 && pos.Col <= 6 && pos.Row >= 4 && pos.Row <= 6;
    }

    public static bool IsTrap(BoardPosition pos, Player owner)
    {
        return owner switch
        {
            Player.Red => (pos.Col == 3 && pos.Row == 1)    // Red = North = top
                       || (pos.Col == 4 && pos.Row == 2)
                       || (pos.Col == 5 && pos.Row == 1),
            Player.Blue => (pos.Col == 3 && pos.Row == 9)   // Blue = South = bottom
                        || (pos.Col == 4 && pos.Row == 8)
                        || (pos.Col == 5 && pos.Row == 9),
            _ => false
        };
    }

    public static bool IsOpponentTrap(BoardPosition pos, Player pieceOwner)
    {
        return IsTrap(pos, Opponent(pieceOwner));
    }

    public static bool IsOwnTrap(BoardPosition pos, Player pieceOwner)
    {
        return IsTrap(pos, pieceOwner);
    }

    public static bool IsDen(BoardPosition pos, Player owner)
    {
        return owner switch
        {
            Player.Red => pos.Col == 4 && pos.Row == 1,    // Red = North = top
            Player.Blue => pos.Col == 4 && pos.Row == 9,   // Blue = South = bottom
            _ => false
        };
    }

    public static bool IsOpponentDen(BoardPosition pos, Player pieceOwner)
    {
        return IsDen(pos, Opponent(pieceOwner));
    }

    public static bool IsOwnDen(BoardPosition pos, Player pieceOwner)
    {
        return IsDen(pos, pieceOwner);
    }

    public static Player Opponent(Player player)
    {
        return player == Player.Blue ? Player.Red : Player.Blue;
    }

    /// <summary>
    /// Returns all squares between from and to in the water (for checking rat blocking of river jumps).
    /// Only call when the move is a validated river jump.
    /// </summary>
    public static List<BoardPosition> GetRiverJumpWaterSquares(BoardPosition from, BoardPosition to)
    {
        var squares = new List<BoardPosition>();

        // Horizontal jump (same column, different rows) — traversing 4 rows
        if (from.Col == to.Col)
        {
            int minRow = Math.Min(from.Row, to.Row);
            int maxRow = Math.Max(from.Row, to.Row);
            for (int r = minRow + 1; r < maxRow; r++)
            {
                var pos = new BoardPosition(from.Col, r);
                if (IsWater(pos))
                    squares.Add(pos);
            }
        }
        // Vertical jump (same row, different columns) — traversing 3 cols
        else if (from.Row == to.Row)
        {
            int minCol = Math.Min(from.Col, to.Col);
            int maxCol = Math.Max(from.Col, to.Col);
            for (int c = minCol + 1; c < maxCol; c++)
            {
                var pos = new BoardPosition(c, from.Row);
                if (IsWater(pos))
                    squares.Add(pos);
            }
        }

        return squares;
    }

    private static TerrainType DetermineTerrain(BoardPosition pos)
    {
        if (pos.Col == 4 && pos.Row == 1) return TerrainType.Den;  // Red den (North/top)
        if (pos.Col == 4 && pos.Row == 9) return TerrainType.Den;  // Blue den (South/bottom)
        if ((pos.Col == 3 && pos.Row == 1) || (pos.Col == 4 && pos.Row == 2) || (pos.Col == 5 && pos.Row == 1)) return TerrainType.Trap;
        if ((pos.Col == 3 && pos.Row == 9) || (pos.Col == 4 && pos.Row == 8) || (pos.Col == 5 && pos.Row == 9)) return TerrainType.Trap;
        if (IsWater(pos)) return TerrainType.Water;
        return TerrainType.Land;
    }

    /// <summary>
    /// Builds the piece position dictionary for GameState.
    /// </summary>
    public Dictionary<BoardPosition, Piece> BuildPieceDictionary()
    {
        var dict = new Dictionary<BoardPosition, Piece>();
        for (int c = 0; c < Cols; c++)
        {
            for (int r = 0; r < Rows; r++)
            {
                var occupant = _squares[c, r].Occupant;
                if (occupant != null && occupant.IsAlive)
                {
                    dict[occupant.Position] = occupant;
                }
            }
        }
        return dict;
    }
}
