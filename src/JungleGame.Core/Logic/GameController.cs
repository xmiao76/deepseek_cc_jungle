using JungleGame.Core.Models;

namespace JungleGame.Core.Logic;

/// <summary>
/// High-level game orchestrator that the UI/AI interacts with.
/// Manages game lifecycle, fires events on state changes, supports undo.
/// </summary>
public class GameController
{
    private readonly GameEngine _engine;
    private GameState? _currentState;
    private readonly Stack<GameState> _undoStack;

    public GameState? CurrentState => _currentState;
    public bool HasUndo => _undoStack.Count > 0;

    public event EventHandler<GameState>? StateChanged;
    public event EventHandler<Move>? MoveExecuted;
    public event EventHandler<GameResult>? GameOver;

    public GameController()
    {
        _engine = new GameEngine();
        _undoStack = new Stack<GameState>();
    }

    /// <summary>
    /// Starts a new game with the specified player moving first.
    /// </summary>
    public void NewGame(Player firstPlayer = Player.Blue)
    {
        _undoStack.Clear();
        _currentState = _engine.CreateInitialState(firstPlayer);
        OnStateChanged(_currentState);
    }

    /// <summary>
    /// Attempts to make a move from one position to another.
    /// Returns true if the move was legal and was applied.
    /// </summary>
    public bool TryMakeMove(BoardPosition from, BoardPosition to)
    {
        if (_currentState == null || _currentState.Phase != GamePhase.Playing)
            return false;

        if (!_engine.IsMoveLegal(_currentState, from, to, out var move) || move == null)
            return false;

        // Save current state for undo
        _undoStack.Push(_currentState);

        // Apply the move
        _currentState = _engine.ApplyMove(_currentState, move);

        OnMoveExecuted(move);
        OnStateChanged(_currentState);

        // Check for game over
        if (_currentState.Phase == GamePhase.GameOver && _currentState.Winner.HasValue)
        {
            var result = new GameResult(
                _currentState.Winner.Value,
                _currentState.WinReason ?? WinCondition.AllPiecesCaptured,
                _currentState.MoveHistory.Count);
            OnGameOver(result);
        }

        return true;
    }

    /// <summary>
    /// Applies a pre-validated Move object directly (used by AI).
    /// </summary>
    public bool ApplyMove(Move move)
    {
        if (_currentState == null || _currentState.Phase != GamePhase.Playing)
            return false;

        // Validate the move is still legal (board may have changed since AI search)
        if (!_currentState.Pieces.TryGetValue(move.From, out var piece) ||
            piece.Type != move.Piece.Type || piece.Owner != move.Piece.Owner)
            return false;

        _undoStack.Push(_currentState);
        _currentState = _engine.ApplyMove(_currentState, move);

        OnMoveExecuted(move);
        OnStateChanged(_currentState);

        if (_currentState.Phase == GamePhase.GameOver && _currentState.Winner.HasValue)
        {
            var result = new GameResult(
                _currentState.Winner.Value,
                _currentState.WinReason ?? WinCondition.AllPiecesCaptured,
                _currentState.MoveHistory.Count);
            OnGameOver(result);
        }

        return true;
    }

    /// <summary>
    /// Gets all legal moves for the given position in the current state.
    /// </summary>
    public List<BoardPosition> GetLegalDestinations(BoardPosition from)
    {
        if (_currentState == null) return new List<BoardPosition>();
        return _engine.GetLegalDestinations(_currentState, from);
    }

    /// <summary>
    /// Gets all legal moves for the current player.
    /// </summary>
    public List<Move> GetLegalMoves()
    {
        if (_currentState == null) return new List<Move>();
        return _engine.GetLegalMoves(_currentState);
    }

    /// <summary>
    /// Undo the last move. Returns to the previous state.
    /// </summary>
    public bool Undo()
    {
        if (_undoStack.Count == 0) return false;
        _currentState = _undoStack.Pop();
        OnStateChanged(_currentState);
        return true;
    }

    protected virtual void OnStateChanged(GameState state)
    {
        try { StateChanged?.Invoke(this, state); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"StateChanged event error: {ex}"); }
    }

    protected virtual void OnMoveExecuted(Move move)
    {
        try { MoveExecuted?.Invoke(this, move); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"MoveExecuted event error: {ex}"); }
    }

    protected virtual void OnGameOver(GameResult result)
    {
        try { GameOver?.Invoke(this, result); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"GameOver event error: {ex}"); }
    }
}
