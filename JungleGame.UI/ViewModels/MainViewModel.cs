using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using JungleGame.Core.AI;
using JungleGame.Core.Engine;
using JungleGame.Core.Model;

namespace JungleGame.UI.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private GameState _state;
    private readonly MinimaxEngine _ai;
    private Position? _selectedPosition;
    private HashSet<Position> _legalMoves = new();
    private bool _isHumanTurn;
    private bool _boardFlipped;
    private bool _aiVsAi;
    private bool _humanFirst = true;
    private bool _aiThinking;
    private CancellationTokenSource? _aiCts;
    private readonly List<string> _moveHistory = new();

    public MainViewModel()
    {
        _state = GameState.CreateInitial();
        _ai = new MinimaxEngine(TimeSpan.FromSeconds(2));
        _isHumanTurn = true;
        NewGameCommand = new RelayCommand(_ => StartNewGame());
        FlipBoardCommand = new RelayCommand(_ => ToggleFlip());
    }

    public GameState State => _state;

    public Player HumanPlayer => _boardFlipped ? Player.Red : Player.Blue;
    public Player AIPlayer => HumanPlayer.Opponent();

    public bool IsHumanTurn
    {
        get => _isHumanTurn;
        set { _isHumanTurn = value; OnPropertyChanged(); }
    }

    public bool BoardFlipped
    {
        get => _boardFlipped;
        set { _boardFlipped = value; OnPropertyChanged(); OnPropertyChanged(nameof(HumanPlayer)); }
    }

    public bool AiVsAi
    {
        get => _aiVsAi;
        set { _aiVsAi = value; }
    }

    public bool HumanFirst
    {
        get => _humanFirst;
        set { _humanFirst = value; }
    }

    public bool AiThinking
    {
        get => _aiThinking;
        set { _aiThinking = value; OnPropertyChanged(); }
    }

    public Position? SelectedPosition
    {
        get => _selectedPosition;
        set { _selectedPosition = value; OnPropertyChanged(); }
    }

    public HashSet<Position> LegalMoves
    {
        get => _legalMoves;
        set { _legalMoves = value; OnPropertyChanged(); }
    }

    public string StatusText
    {
        get
        {
            if (_state.Status == GameStatus.BlueWins)
                return "Blue wins!";
            if (_state.Status == GameStatus.RedWins)
                return "Red wins!";
            if (_aiThinking)
                return "AI thinking...";
            return $"{_state.CurrentTurn}'s turn";
        }
    }

    public string TurnIndicator => IsHumanTurn ? "Your turn" : "AI's turn";

    public int BlueCapturedCount => _state.CapturedBlue.Count;
    public int RedCapturedCount => _state.CapturedRed.Count;

    public ObservableCollection<string> CapturedBlueDisplay => new(_state.CapturedBlue.Select(p => AnimalName(p.Animal)));
    public ObservableCollection<string> CapturedRedDisplay => new(_state.CapturedRed.Select(p => AnimalName(p.Animal)));

    public List<string> MoveHistory => _moveHistory;

    public ICommand NewGameCommand { get; }
    public ICommand FlipBoardCommand { get; }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action<GameStatus>? GameOver;
    public event Action? BoardChanged;
    public event Action? AIThinkingChanged;

    public void StartNewGame()
    {
        _aiCts?.Cancel();
        _state = GameState.CreateInitial();
        SelectedPosition = null;
        LegalMoves = new HashSet<Position>();
        _moveHistory.Clear();

        if (_humanFirst)
        {
            IsHumanTurn = true;
        }
        else
        {
            IsHumanTurn = false;
            _ = DoAIMove();
        }

        NotifyAll();
    }

    public void StartGame(bool humanFirst, bool aiVsAi)
    {
        _humanFirst = humanFirst;
        _aiVsAi = aiVsAi;
        StartNewGame();
    }

    public void HandleCellClick(Position pos)
    {
        if (_state.Status != GameStatus.InProgress) return;
        if (!_isHumanTurn && !_aiVsAi) return;
        if (_aiThinking) return;

        if (_selectedPosition == null)
        {
            // Try to select a piece
            var piece = _state.GetPieceAt(pos);
            if (piece != null && piece.Value.Owner == _state.CurrentTurn)
            {
                SelectedPosition = pos;

                // Generate legal moves for this piece
                var allMoves = MoveGenerator.GenerateLegalMoves(_state, _state.CurrentTurn);
                LegalMoves = allMoves
                    .Where(m => m.From == pos)
                    .Select(m => m.To)
                    .ToHashSet();
            }
        }
        else
        {
            // Try to move to the clicked position
            if (LegalMoves.Contains(pos))
            {
                ExecuteMove(_selectedPosition.Value, pos);
                SelectedPosition = null;
                LegalMoves = new HashSet<Position>();
            }
            else
            {
                // Deselect or select another piece
                SelectedPosition = null;
                LegalMoves = new HashSet<Position>();

                var piece = _state.GetPieceAt(pos);
                if (piece != null && piece.Value.Owner == _state.CurrentTurn)
                {
                    SelectedPosition = pos;
                    var allMoves = MoveGenerator.GenerateLegalMoves(_state, _state.CurrentTurn);
                    LegalMoves = allMoves
                        .Where(m => m.From == pos)
                        .Select(m => m.To)
                        .ToHashSet();
                }
            }
        }

        NotifyAll();
    }

    private void ExecuteMove(Position from, Position to)
    {
        var move = new Move(from, to, _state.GetPieceAt(to));
        _state = GameController.ApplyMove(_state, move);
        _moveHistory.Add(move.ToString());

        if (_state.Status != GameStatus.InProgress)
        {
            GameOver?.Invoke(_state.Status);
            NotifyAll();
            return;
        }

        IsHumanTurn = !_isHumanTurn;
        NotifyAll();

        if (!_isHumanTurn)
        {
            _ = DoAIMove();
        }
    }

    private async Task DoAIMove()
    {
        _aiCts?.Cancel();
        _aiCts = new CancellationTokenSource();
        var token = _aiCts.Token;

        AiThinking = true;
        NotifyAll();

        try
        {
            var move = await Task.Run(() => _ai.FindBestMove(_state), token);

            if (token.IsCancellationRequested) return;

            _state = GameController.ApplyMove(_state, move);
            _moveHistory.Add($"(AI) {move}");

            if (_state.Status != GameStatus.InProgress)
            {
                GameOver?.Invoke(_state.Status);
            }
            else
            {
                IsHumanTurn = !_aiVsAi;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"AI error: {ex.Message}");
        }
        finally
        {
            AiThinking = false;
            NotifyAll();
        }
    }

    public void ToggleFlip()
    {
        BoardFlipped = !BoardFlipped;
        NotifyAll();
    }

    public Position GetVisualPosition(Position logicalPos)
    {
        if (!_boardFlipped) return logicalPos;
        return new Position(6 - logicalPos.Col, 8 - logicalPos.Row);
    }

    private void NotifyAll()
    {
        OnPropertyChanged(nameof(State));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(TurnIndicator));
        OnPropertyChanged(nameof(BlueCapturedCount));
        OnPropertyChanged(nameof(RedCapturedCount));
        OnPropertyChanged(nameof(AiThinking));
        OnPropertyChanged(nameof(IsHumanTurn));
        BoardChanged?.Invoke();
    }

    public static string AnimalName(Animal animal) => animal switch
    {
        Animal.Rat => "Rat",
        Animal.Cat => "Cat",
        Animal.Dog => "Dog",
        Animal.Wolf => "Wolf",
        Animal.Leopard => "Leopard",
        Animal.Tiger => "Tiger",
        Animal.Lion => "Lion",
        Animal.Elephant => "Elephant",
        _ => "?"
    };

    public static string AnimalEmoji(Animal animal) => animal switch
    {
        Animal.Rat => "\U0001F401",     // 🐁
        Animal.Cat => "\U0001F408",     // 🐈
        Animal.Dog => "\U0001F415",     // 🐕
        Animal.Wolf => "\U0001F43A",    // 🐺
        Animal.Leopard => "\U0001F406", // 🐆
        Animal.Tiger => "\U0001F42F",   // 🐯
        Animal.Lion => "\U0001F981",    // 🦁
        Animal.Elephant => "\U0001F418", // 🐘
        _ => "?"
    };

    protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    public RelayCommand(Action<object?> execute) => _execute = execute;
    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => _execute(parameter);
}
