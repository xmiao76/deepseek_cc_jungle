using System.ComponentModel;
using System.Runtime.CompilerServices;
using JungleGame.Core.AI;
using JungleGame.Core.Engine;
using JungleGame.Core.Model;

namespace JungleGame.UI.ViewModels;

public class MainViewModel : INotifyPropertyChanged, IDisposable
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
    private CancellationTokenSource? _ponderCts;
    private Move? _ponderPrediction; // the engine's predicted human reply (logical coords)
    private Move? _ponderedMove;     // the engine's reply if the prediction held
    private readonly List<string> _moveHistory = new();
    private readonly System.Windows.Threading.DispatcherTimer _statsTimer;

    public MainViewModel(int aiTimeMs = 1000) // Medium difficulty
    {
        _state = GameState.CreateInitial();
        _ai = new MinimaxEngine(TimeSpan.FromMilliseconds(aiTimeMs));
        _isHumanTurn = true;

        // Refresh the status line while the AI searches so the depth/nps
        // stats update live.
        _statsTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(250),
        };
        _statsTimer.Tick += (_, _) => OnPropertyChanged(nameof(StatusText));
    }

    public GameState State => _state;

    public bool IsHumanTurn
    {
        get => _isHumanTurn;
        set { _isHumanTurn = value; OnPropertyChanged(); }
    }

    public bool BoardFlipped
    {
        get => _boardFlipped;
        set { _boardFlipped = value; OnPropertyChanged(); }
    }

    public bool AiVsAi
    {
        get => _aiVsAi;
        set { _aiVsAi = value; OnPropertyChanged(); }
    }

    public bool HumanFirst
    {
        get => _humanFirst;
        set { _humanFirst = value; OnPropertyChanged(); }
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
            if (_state.Status != GameStatus.InProgress)
                return GameStrings.StatusText(_state.Status);
            if (_aiThinking)
            {
                var stats = _ai.Stats;
                string tb = TablebaseInfo.TotalHits > 0 || TablebaseInfo.Availability == TablebaseAvailability.Loaded
                    ? $" · TB {stats.TablebaseHits}"
                    : "";
                return stats.Depth > 0
                    ? $"AI thinking... depth {stats.Depth} · {stats.NodesPerSecond / 1000.0:F0}k nps{tb}"
                    : "AI thinking...";
            }
            return $"{_state.CurrentTurn}'s turn";
        }
    }

    public string TurnIndicator => _aiVsAi
        ? $"{_state.CurrentTurn}'s turn"
        : IsHumanTurn ? "Your turn" : "AI's turn";

    public int BlueCapturedCount => _state.CapturedBlue.Count;
    public int RedCapturedCount => _state.CapturedRed.Count;

    public List<string> MoveHistory => _moveHistory;

    /// <summary>The most recently applied move, in logical coordinates (null before the first move).</summary>
    public (Position From, Position To, bool WasCapture)? LastMove { get; private set; }

    /// <summary>Increments on every applied move; the view uses it to detect a new move to animate.</summary>
    public long MoveCounter { get; private set; }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action<GameStatus>? GameOver;
    public event Action? BoardChanged;

    public void StartNewGame()
    {
        _aiCts?.Cancel();
        CancelPonder();
        _state = GameState.CreateInitial();
        SelectedPosition = null;
        LegalMoves = new HashSet<Position>();
        _moveHistory.Clear();
        LastMove = null;
        MoveCounter = 0;

        if (!_humanFirst || _aiVsAi)
        {
            IsHumanTurn = false;
            _ = DoAIMove();
        }
        else
        {
            IsHumanTurn = true;
        }

        NotifyAll();
    }

    public void StartGame(bool humanFirst, bool aiVsAi, int aiTimeMs = 1000) // Medium difficulty
    {
        _humanFirst = humanFirst;
        _aiVsAi = aiVsAi;
        _ai.SetTimeLimit(TimeSpan.FromMilliseconds(aiTimeMs));
        StartNewGame();
    }

    public void HandleCellClick(Position pos)
    {
        if (_state.Status != GameStatus.InProgress) return;
        if (_aiVsAi) return; // Watch mode: the AI plays both sides
        if (!_isHumanTurn) return;
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
        LastMove = (from, to, move.IsCapture);
        MoveCounter++;

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
            // Pondering: if the human just played the predicted reply, the engine
            // already has the reply computed — skip the search entirely.
            var prediction = _ponderPrediction;
            Move? premove = null;
            if (prediction != null && prediction.Value.From == from && prediction.Value.To == to && _ponderedMove != null)
            {
                // The pondered move is legal in this exact position (the human
                // matched the prediction, so the state is what was searched).
                premove = _ponderedMove;
            }
            _ = DoAIMove(premove);
        }
    }

    private async Task DoAIMove(Move? premove = null)
    {
        CancelPonder();
        _aiCts?.Cancel();
        _aiCts?.Dispose();
        _aiCts = new CancellationTokenSource();
        var token = _aiCts.Token;

        bool isInstant = premove != null; // pondered reply: no visible thinking
        if (!isInstant)
        {
            AiThinking = true;
            _statsTimer.Start();
            NotifyAll();
        }

        var continueChain = false;

        try
        {
            var move = premove ?? await Task.Run(() => _ai.FindBestMove(_state, token), token);

            if (token.IsCancellationRequested) return;

            if (move == null)
            {
                // Unreachable through normal play (ApplyMove already declares a
                // terminal status before the AI's turn), but never leave the game
                // hanging: the AI has no legal moves, so the side to move loses
                // (mirrors GameController.CheckWinCondition).
                GameOver?.Invoke(_state.CurrentTurn == Player.Blue
                    ? GameStatus.RedWins
                    : GameStatus.BlueWins);
                return;
            }

            _state = GameController.ApplyMove(_state, move.Value);
            _moveHistory.Add($"(AI) {move.Value}");
            LastMove = (move.Value.From, move.Value.To, move.Value.IsCapture);
            MoveCounter++;

            if (_state.Status != GameStatus.InProgress)
            {
                // Clear the thinking indicator before the game-over dialog opens
                AiThinking = false;
                NotifyAll();
                GameOver?.Invoke(_state.Status);
            }
            else
            {
                IsHumanTurn = !_aiVsAi;
                continueChain = _aiVsAi;
                if (!_aiVsAi)
                    StartPondering();
            }
        }
        catch (Exception ex)
        {
            // Never leave the game hung: log and hand the turn back to the human.
            // Mirror the normal completion path so an AI-first game does not
            // transiently show "Your turn".
            System.Diagnostics.Debug.WriteLine($"AI error: {ex.Message}");
            System.Diagnostics.Trace.TraceError($"AI error: {ex}");
            IsHumanTurn = !_aiVsAi;
        }
        finally
        {
            _statsTimer.Stop();
            AiThinking = false;
            NotifyAll();
        }

        // Watch mode: chain the opponent's move so the game plays itself to completion
        if (continueChain)
            _ = DoAIMove();
    }

    public void ToggleFlip()
    {
        BoardFlipped = !BoardFlipped;
        NotifyAll();
    }

    /// <summary>Keyboard navigation: deselect without moving.</summary>
    public void ClearSelection()
    {
        SelectedPosition = null;
        LegalMoves = new HashSet<Position>();
        NotifyAll();
    }

    /// <summary>Cancels any in-flight AI search (the window calls this on close).</summary>
    public void Dispose()
    {
        _aiCts?.Cancel();
        _aiCts?.Dispose();
        _aiCts = null;
        CancelPonder();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Pondering: the engine searches the position after its own move plus its
    /// predicted human reply during the human's think time. If the human plays
    /// the predicted reply, the reply move is already computed (see ExecuteMove);
    /// on a different human move the unfinished ponder is cancelled and its
    /// transposition-table entries still warm the next real search. Never in
    /// AI-vs-AI mode.
    /// </summary>
    private void StartPondering()
    {
        var predicted = _ai.LastPredictedReply;
        if (predicted == null)
            return;

        _ponderPrediction = predicted;
        _ponderedMove = null;
        _ponderCts?.Cancel();
        _ponderCts?.Dispose();
        _ponderCts = new CancellationTokenSource();
        var token = _ponderCts.Token;
        var ponderState = _state; // immutable snapshot; _state is only replaced

        _ = Task.Run(() =>
        {
            try
            {
                var replyPosition = GameController.ApplyMove(ponderState, predicted.Value);
                var move = _ai.Ponder(replyPosition, token);
                if (!token.IsCancellationRequested && move != null)
                    _ponderedMove = move;
            }
            catch (Exception ex)
            {
                // Pondering must never surface to the user: a failure just means
                // a normal search on the human's actual move.
                System.Diagnostics.Debug.WriteLine($"Ponder error: {ex.Message}");
            }
        });
    }

    private void CancelPonder()
    {
        _ponderCts?.Cancel();
        _ponderCts?.Dispose();
        _ponderCts = null;
        _ponderPrediction = null;
        _ponderedMove = null;
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
