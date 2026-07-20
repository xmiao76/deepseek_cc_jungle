using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using JungleGame.Core.AI;
using JungleGame.Core.Logic;
using JungleGame.Core.Models;

namespace JungleGame.Desktop.ViewModels;

/// <summary>
/// Root view model for the main window. Orchestrates game flow,
/// AI integration, and all UI state.
/// </summary>
public class MainViewModel : INotifyPropertyChanged
{
    private readonly GameController _gameController;
    private readonly AiController _aiController;

    // ===== Public bindable properties =====

    public BoardViewModel BoardVM { get; }
    public Player? HumanPlayer { get; private set; } = Player.Blue;

    private string _statusText = "Welcome to Jungle! Press New Game to start.";
    public string StatusText
    {
        get => _statusText;
        set { _statusText = value; OnPropertyChanged(nameof(StatusText)); }
    }

    private bool _isBoardFlipped;
    public bool IsBoardFlipped
    {
        get => _isBoardFlipped;
        set
        {
            _isBoardFlipped = value;
            BoardVM.IsFlipped = value;
            BoardVM.RefreshAll();
            OnPropertyChanged(nameof(IsBoardFlipped));
        }
    }

    private bool _isHumanFirst = true;
    public bool IsHumanFirst
    {
        get => _isHumanFirst;
        set { _isHumanFirst = value; OnPropertyChanged(nameof(IsHumanFirst)); }
    }

    private int _selectedDifficulty = 1; // Medium
    public int SelectedDifficulty
    {
        get => _selectedDifficulty;
        set
        {
            _selectedDifficulty = value;
            _aiController.Difficulty = (DifficultyLevel)value;
            OnPropertyChanged(nameof(SelectedDifficulty));
        }
    }

    private bool _isAIVsAI;
    public bool IsAIVsAI
    {
        get => _isAIVsAI;
        set { _isAIVsAI = value; OnPropertyChanged(nameof(IsAIVsAI)); }
    }

    private bool _isGameOver;
    public bool IsGameOver
    {
        get => _isGameOver;
        set { _isGameOver = value; OnPropertyChanged(nameof(IsGameOver)); }
    }

    private bool _isWaitingForAI;
    public bool IsWaitingForAI
    {
        get => _isWaitingForAI;
        set { _isWaitingForAI = value; OnPropertyChanged(nameof(IsWaitingForAI)); }
    }

    private string _currentTurnText = "";
    public string CurrentTurnText
    {
        get => _currentTurnText;
        set { _currentTurnText = value; OnPropertyChanged(nameof(CurrentTurnText)); }
    }

    private string _gameOverMessage = "";
    public string GameOverMessage
    {
        get => _gameOverMessage;
        set { _gameOverMessage = value; OnPropertyChanged(nameof(GameOverMessage)); }
    }

    public ObservableCollection<string> MoveHistory { get; } = new();
    public ObservableCollection<string> BlueCapturedPieces { get; } = new();
    public ObservableCollection<string> RedCapturedPieces { get; } = new();

    // ===== Commands =====

    public ICommand NewGameCommand { get; }
    public ICommand ToggleFlipCommand { get; }
    public ICommand AIVsAICommand { get; }
    public ICommand UndoCommand { get; }

    // ===== Events =====

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? BoardNeedsRefresh;
    public event EventHandler<string>? StatusMessageChanged;

    // ===== Selection state =====

    private BoardPosition? _selectedPosition;

    public MainViewModel()
    {
        _gameController = new GameController();
        _aiController = new AiController();
        BoardVM = new BoardViewModel();

        _gameController.StateChanged += OnGameStateChanged;
        _gameController.MoveExecuted += OnMoveExecuted;
        _gameController.GameOver += OnGameOver;
        _aiController.MoveFound += OnAiMoveFound;

        NewGameCommand = new RelayCommand(_ => StartNewGame());
        ToggleFlipCommand = new RelayCommand(_ => ToggleFlip());
        AIVsAICommand = new RelayCommand(_ => StartAIVsAI());
        UndoCommand = new RelayCommand(_ => UndoLastMove(), _ => _gameController.HasUndo);
    }

    // ===== Game actions =====

    public void StartNewGame()
    {
        IsGameOver = false;
        IsWaitingForAI = false;
        IsAIVsAI = false;
        GameOverMessage = "";
        MoveHistory.Clear();
        BlueCapturedPieces.Clear();
        RedCapturedPieces.Clear();

        _aiController.CancelSearch();

        Player firstPlayer = IsHumanFirst ? Player.Blue : Player.Red;
        HumanPlayer = Player.Blue; // Human always plays Blue

        _gameController.NewGame(firstPlayer);
        _selectedPosition = null;
        BoardVM.ClearSelection();

        UpdateStatusText();

        // If AI goes first, trigger it
        if (!IsHumanFirst)
        {
            RequestAiMove();
        }
    }

    public void StartAIVsAI()
    {
        StartNewGame();
        IsAIVsAI = true;
        HumanPlayer = null; // No human player
        StatusText = "AI vs AI — watching...";
        RequestAiMove();
    }

    /// <summary>
    /// Handles a square click on the board. Returns to the control
    /// whether selection state changed (for re-rendering).
    /// </summary>
    public bool HandleSquareClick(BoardPosition logicalPos)
    {
        if (_gameController.CurrentState == null) return false;
        if (_gameController.CurrentState.Phase != GamePhase.Playing) return false;
        if (IsWaitingForAI) return false; // AI is thinking
        if (IsAIVsAI) return false; // No human input in AI-vs-AI

        var state = _gameController.CurrentState;

        // Check if a piece at this position belongs to the human
        var pieceDict = state.Pieces;
        bool hasPiece = pieceDict.TryGetValue(logicalPos, out var piece);
        bool isHumanPiece = hasPiece && piece!.Owner == HumanPlayer;

        if (!isHumanPiece && _selectedPosition == null)
        {
            // Clicked empty or opponent piece with nothing selected — ignore
            return false;
        }

        if (isHumanPiece)
        {
            // Select this piece
            _selectedPosition = logicalPos;
            var destinations = _gameController.GetLegalDestinations(logicalPos);
            BoardVM.SelectedSquare = logicalPos;
            BoardVM.SetLegalMoves(destinations);

            // Mark the selected piece
            var pvm = BoardVM.GetPieceAt(logicalPos);
            if (pvm != null) pvm.IsSelected = true;

            BoardVM.RefreshAll();
            return true;
        }
        else if (_selectedPosition != null)
        {
            // Try to move selected piece to clicked position
            bool moved = _gameController.TryMakeMove(_selectedPosition.Value, logicalPos);

            if (moved)
            {
                _selectedPosition = null;
                BoardVM.ClearSelection();
                return true;
            }

            return false; // Illegal move
        }

        return false;
    }

    // ===== AI integration =====

    private void RequestAiMove()
    {
        if (_gameController.CurrentState == null) return;
        if (_gameController.CurrentState.Phase != GamePhase.Playing) return;
        if (IsGameOver) return;

        try
        {
            IsWaitingForAI = true;
            StatusText = "AI is thinking...";
            BoardNeedsRefresh?.Invoke(this, EventArgs.Empty);
            _aiController.RequestMove(_gameController.CurrentState);
        }
        catch (Exception ex)
        {
            IsWaitingForAI = false;
            System.Diagnostics.Debug.WriteLine($"RequestAiMove error: {ex}");
        }
    }

    private void OnAiMoveFound(object? sender, Move move)
    {
        try
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                try
                {
                    IsWaitingForAI = false;

                    // Only apply if game is still playing (not already over)
                    if (_gameController.CurrentState?.Phase == GamePhase.Playing && !IsGameOver)
                    {
                        _gameController.ApplyMove(move);
                    }

                    // In AI-vs-AI mode, schedule next move with a small delay
                    if (IsAIVsAI && _gameController.CurrentState?.Phase == GamePhase.Playing)
                    {
                        Task.Delay(400).ContinueWith(_ =>
                        {
                            try
                            {
                                Application.Current?.Dispatcher.Invoke(() =>
                                {
                                    if (IsAIVsAI && _gameController.CurrentState?.Phase == GamePhase.Playing)
                                        RequestAiMove();
                                });
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"AI-vs-AI next move error: {ex}");
                            }
                        }, TaskScheduler.Default);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"AI move handler error: {ex}");
                }
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"AI move dispatch error: {ex}");
        }
    }

    // ===== Event handlers =====

    private void OnGameStateChanged(object? sender, GameState state)
    {
        try
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                try
                {
                    BoardVM.UpdateFromGameState(state);
                    UpdateStatusText();

                    if (state.Phase == GamePhase.Playing && !IsGameOver && !IsWaitingForAI)
                    {
                        bool isHumanTurn = state.CurrentPlayer == HumanPlayer;
                        if (!isHumanTurn && !IsAIVsAI)
                        {
                            RequestAiMove();
                        }
                        else if (IsAIVsAI)
                        {
                            RequestAiMove();
                        }
                    }

                    BoardNeedsRefresh?.Invoke(this, EventArgs.Empty);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"StateChanged handler error: {ex}");
                }
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"StateChanged dispatch error: {ex}");
        }
    }

    private void OnMoveExecuted(object? sender, Move move)
    {
        try
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                try
                {
                    string moveText = $"{move.Piece.Owner} {move.Piece.Type}: {move.From} → {move.To}";
                    if (move.CapturedPiece != null)
                        moveText += $" (captures {move.CapturedPiece.Type})";
                    if (move.IsDenEntry)
                        moveText += " [DEN!]";
                    MoveHistory.Insert(0, moveText);

                    if (move.CapturedPiece != null)
                    {
                        string captured = $"{move.CapturedPiece.Owner} {move.CapturedPiece.Type}";
                        if (move.CapturedPiece.Owner == Player.Blue)
                            RedCapturedPieces.Insert(0, captured);
                        else
                            BlueCapturedPieces.Insert(0, captured);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"MoveExecuted handler error: {ex}");
                }
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"MoveExecuted dispatch error: {ex}");
        }
    }

    private void OnGameOver(object? sender, GameResult result)
    {
        try
        {
            _aiController.CancelSearch();
            IsWaitingForAI = false;
            IsAIVsAI = false;
            IsGameOver = true;

            Application.Current?.Dispatcher.Invoke(() =>
            {
                try
                {
                    GameOverMessage = result.Description;
                    StatusText = result.Description;
                    BoardNeedsRefresh?.Invoke(this, EventArgs.Empty);
                    OnPropertyChanged(nameof(IsGameOver));
                    OnPropertyChanged(nameof(GameOverMessage));
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"GameOver handler error: {ex}");
                }
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"GameOver error: {ex}");
        }
    }

    // ===== Helpers =====

    public void ToggleFlip()
    {
        IsBoardFlipped = !IsBoardFlipped;
    }

    public void UndoLastMove()
    {
        if (IsAIVsAI || IsWaitingForAI) return;
        _aiController.CancelSearch();
        _gameController.Undo();
        // If was AI turn before undo, also undo one more (AI's move)
        if (_gameController.CurrentState?.CurrentPlayer == HumanPlayer)
        {
            IsWaitingForAI = false;
        }
        _selectedPosition = null;
        BoardVM.ClearSelection();
    }

    private void UpdateStatusText()
    {
        if (_gameController.CurrentState == null)
        {
            StatusText = "Welcome to Jungle! Press New Game to start.";
            return;
        }

        var state = _gameController.CurrentState;
        if (state.Phase == GamePhase.GameOver)
        {
            StatusText = GameOverMessage;
            return;
        }

        string turnOwner = state.CurrentPlayer == HumanPlayer ? "Your" : "AI's";
        CurrentTurnText = $"{state.CurrentPlayer}'s turn ({turnOwner})";
        StatusText = $"{state.CurrentPlayer}'s turn — {turnOwner} move";
    }

    protected void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
