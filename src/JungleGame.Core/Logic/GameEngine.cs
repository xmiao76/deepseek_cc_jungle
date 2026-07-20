using JungleGame.Core.Models;

namespace JungleGame.Core.Logic;

/// <summary>
/// Central game logic engine: move generation, rule enforcement, win detection.
/// Operates on immutable GameState snapshots for thread safety.
/// </summary>
public class GameEngine
{
    /// <summary>
    /// Creates the initial game state with all pieces in starting positions.
    /// </summary>
    public GameState CreateInitialState(Player firstPlayer = Player.Blue)
    {
        var board = Board.CreateInitial();
        var pieces = board.BuildPieceDictionary();
        return new GameState(
            pieces,
            firstPlayer,
            GamePhase.Playing,
            null,
            null,
            0,
            null,
            new List<Move>(),
            firstPlayer);
    }

    /// <summary>
    /// Gets all legal moves for the current player in the given state.
    /// Uses a temporary Board reconstructed from the state's piece dictionary.
    /// </summary>
    public List<Move> GetLegalMoves(GameState state)
    {
        var board = StateToBoard(state);
        return GetLegalMoves(board, state.CurrentPlayer);
    }

    /// <summary>
    /// Gets all legal moves for the specified player on the given board.
    /// </summary>
    public List<Move> GetLegalMoves(Board board, Player player)
    {
        var moves = new List<Move>();
        var pieces = board.GetPieces(player);

        foreach (var piece in pieces)
        {
            var pieceMoves = MoveValidator.GetLegalMovesForPiece(board, piece);
            moves.AddRange(pieceMoves);
        }

        return moves;
    }

    /// <summary>
    /// Validates that a move from a given position to a destination is legal.
    /// </summary>
    public bool IsMoveLegal(GameState state, BoardPosition from, BoardPosition to, out Move? move)
    {
        move = null;
        var board = StateToBoard(state);
        var piece = board.GetPiece(from);

        if (piece == null || piece.Owner != state.CurrentPlayer)
            return false;

        return MoveValidator.IsMoveLegal(board, piece, to, out move);
    }

    /// <summary>
    /// Applies a move to the game state, returning a new GameState (immutable).
    /// </summary>
    public GameState ApplyMove(GameState state, Move move)
    {
        var newPieces = new Dictionary<BoardPosition, Piece>(state.Pieces);

        // Remove captured piece (removal from dict is sufficient — do NOT
        // mutate IsAlive on the shared Piece reference, as it would corrupt
        // parent states that share the same object via shallow dict copy)
        if (move.CapturedPiece != null)
        {
            newPieces.Remove(move.CapturedPiece.Position);
        }

        // Move the piece: clone to avoid mutating shared Piece references
        // (shallow dict copy means original state's pieces would be corrupted)
        if (newPieces.TryGetValue(move.From, out var actualPiece))
        {
            newPieces.Remove(move.From);
            var movedPiece = actualPiece.Clone();
            movedPiece.Position = move.To;
            newPieces[move.To] = movedPiece;
        }

        // Check win condition
        var winner = CheckWinner(newPieces, move);
        var newPhase = winner.HasValue ? GamePhase.GameOver : GamePhase.Playing;
        var winReason = winner.HasValue
            ? (move.IsDenEntry ? WinCondition.DenEntry : WinCondition.AllPiecesCaptured)
            : (WinCondition?)null;

        // Increment move count
        int newMoveCount = state.MoveCount;
        Player? nextPlayer = null;
        if (!winner.HasValue)
        {
            nextPlayer = Board.Opponent(state.CurrentPlayer);
            // Increment on full move (when it's back to first player)
            if (nextPlayer == state.FirstPlayer)
                newMoveCount++;
            else
                newMoveCount = state.MoveCount; // Half-move
        }

        // Build new move history
        var newHistory = new List<Move>(state.MoveHistory) { move };

        return new GameState(
            newPieces,
            nextPlayer ?? state.CurrentPlayer, // Keep current if game over
            newPhase,
            winner,
            winReason,
            newMoveCount,
            move,
            newHistory,
            state.FirstPlayer);
    }

    /// <summary>
    /// Returns whether the game is over and who won.
    /// </summary>
    public (bool IsOver, Player? Winner, WinCondition? Reason) CheckGameOver(GameState state)
    {
        return (state.Phase == GamePhase.GameOver, state.Winner, state.WinReason);
    }

    /// <summary>
    /// Gets all legal destination positions for a piece at the given position.
    /// Used by the UI for highlighting legal moves.
    /// </summary>
    public List<BoardPosition> GetLegalDestinations(GameState state, BoardPosition from)
    {
        var board = StateToBoard(state);
        var piece = board.GetPiece(from);
        if (piece == null || piece.Owner != state.CurrentPlayer)
            return new List<BoardPosition>();

        return MoveValidator.GetLegalMovesForPiece(board, piece)
            .Select(m => m.To)
            .ToList();
    }

    // ===== Private helpers =====

    /// <summary>
    /// Reconstructs a Board from a GameState's piece dictionary for move generation.
    /// </summary>
    private static Board StateToBoard(GameState state)
    {
        var board = new Board();
        foreach (var kvp in state.Pieces)
        {
            var piece = kvp.Value;
            board.PlacePiece(new Piece(piece.Type, piece.Owner, piece.Position) { IsAlive = piece.IsAlive });
        }
        return board;
    }

    private static Player? CheckWinner(Dictionary<BoardPosition, Piece> pieces, Move move)
    {
        // Win by den entry
        if (move.IsDenEntry)
            return move.Piece.Owner;

        // Win by capturing all opponent pieces
        var opponent = Board.Opponent(move.Piece.Owner);
        bool opponentHasPieces = pieces.Values.Any(p => p.Owner == opponent);
        if (!opponentHasPieces)
            return move.Piece.Owner;

        return null;
    }
}
