namespace JungleGame.Core.Models;

/// <summary>
/// Immutable snapshot of the complete game state at a point in time.
/// Used for AI search (thread-safe reads without locking) and for undo.
/// </summary>
public class GameState
{
    /// <summary>
    /// All living pieces mapped by their current position.
    /// </summary>
    public IReadOnlyDictionary<BoardPosition, Piece> Pieces { get; }

    /// <summary>
    /// Which player's turn it is.
    /// </summary>
    public Player CurrentPlayer { get; }

    /// <summary>
    /// Current game phase.
    /// </summary>
    public GamePhase Phase { get; }

    /// <summary>
    /// Winner, if the game is over.
    /// </summary>
    public Player? Winner { get; }

    /// <summary>
    /// How the game was won, if over.
    /// </summary>
    public WinCondition? WinReason { get; }

    /// <summary>
    /// Number of full moves completed (both players).
    /// </summary>
    public int MoveCount { get; }

    /// <summary>
    /// The last move made, for UI highlighting.
    /// </summary>
    public Move? LastMove { get; }

    /// <summary>
    /// All moves made so far in this game.
    /// </summary>
    public IReadOnlyList<Move> MoveHistory { get; }

    /// <summary>
    /// The player who moves first in this game (configurable pre-game).
    /// </summary>
    public Player FirstPlayer { get; }

    public GameState(
        IReadOnlyDictionary<BoardPosition, Piece> pieces,
        Player currentPlayer,
        GamePhase phase,
        Player? winner,
        WinCondition? winReason,
        int moveCount,
        Move? lastMove,
        IReadOnlyList<Move> moveHistory,
        Player firstPlayer)
    {
        Pieces = pieces;
        CurrentPlayer = currentPlayer;
        Phase = phase;
        Winner = winner;
        WinReason = winReason;
        MoveCount = moveCount;
        LastMove = lastMove;
        MoveHistory = moveHistory;
        FirstPlayer = firstPlayer;
    }

    /// <summary>
    /// Creates a deep copy of this game state with modifications.
    /// </summary>
    public GameState With(
        IReadOnlyDictionary<BoardPosition, Piece>? pieces = null,
        Player? currentPlayer = null,
        GamePhase? phase = null,
        Player? winner = null,
        WinCondition? winReason = null,
        int? moveCount = null,
        Move? lastMove = null,
        IReadOnlyList<Move>? moveHistory = null)
    {
        return new GameState(
            pieces ?? Pieces,
            currentPlayer ?? CurrentPlayer,
            phase ?? Phase,
            winner ?? Winner,
            winReason ?? WinReason,
            moveCount ?? MoveCount,
            lastMove ?? LastMove,
            moveHistory ?? MoveHistory,
            FirstPlayer);
    }
}
