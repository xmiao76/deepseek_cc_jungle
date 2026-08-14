using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using JungleGame.Core.Model;
using JungleGame.UI.ViewModels;

namespace JungleGame.UI.Views;

public partial class MainWindow : Window, IDisposable
{
    private readonly MainViewModel _vm;

    /// <summary>Disposes the view model (cancels any in-flight AI search).</summary>
    public void Dispose()
    {
        _vm.Dispose();
        GC.SuppressFinalize(this);
    }

    private const int BoardCols = 7;
    private const int BoardRows = 9;
    private const double BoardMargin = 20;

    // Move animation state (see MainWindow.Input.cs)
    private long _animatedMoveId = -1;
    private bool _animationInFlight;
    private bool _animationCancelled;
    private readonly List<UIElement> _animationOverlays = new();

    public MainWindow(bool humanFirst = true, bool aiVsAi = false, int aiTimeMs = 1000) // Medium difficulty
    {
        InitializeComponent();
        _vm = new MainViewModel(aiTimeMs);
        _vm.GameOver += OnGameOver;
        _vm.BoardChanged += RenderBoard;
        _vm.PropertyChanged += (_, e) =>
        {
            // Property changes originate on the UI thread (the VM's async
            // continuations capture the WPF SynchronizationContext)
            if (e.PropertyName == nameof(MainViewModel.AiThinking))
                RenderBoard();
        };
        BoardCanvas.KeyDown += BoardCanvas_KeyDown;
        _vm.StartGame(humanFirst, aiVsAi, aiTimeMs);
        RenderBoard();
        UpdateUI();

        Closed += (_, _) => Dispose(); // Cancel any in-flight search on close
    }

    private double CellSize => Math.Min(
        (BoardCanvas.Width - BoardMargin * 2) / BoardCols,
        (BoardCanvas.Height - BoardMargin * 2) / BoardRows);

    private double BoardLeft => BoardMargin + (BoardCanvas.Width - BoardMargin * 2 - CellSize * BoardCols) / 2;
    private double BoardTop => BoardMargin + (BoardCanvas.Height - BoardMargin * 2 - CellSize * BoardRows) / 2;

    /// <summary>Top-left canvas coordinates of the cell holding the given visual position.</summary>
    private (double X, double Y) CellOrigin(int visualCol, int visualRow)
    {
        double cell = CellSize;
        return (BoardLeft + visualCol * cell, BoardTop + (BoardRows - 1 - visualRow) * cell);
    }

    private void RenderBoard()
    {
        BoardCanvas.Children.Clear();

        double cell = CellSize;
        double left = BoardLeft;
        double top = BoardTop;

        var lastMove = _vm.LastMove;
        bool animating = lastMove != null
            && _vm.MoveCounter != _animatedMoveId
            && !_animationInFlight;

        for (int r = 0; r < BoardRows; r++)
        {
            for (int c = 0; c < BoardCols; c++)
            {
                var logicalPos = new Position(c, r);
                var visualPos = _vm.GetVisualPosition(logicalPos);
                double x = left + visualPos.Col * cell;
                double y = top + (BoardRows - 1 - visualPos.Row) * cell;

                // Terrain
                var terrain = _vm.State.Board.GetTerrain(logicalPos);
                DrawTerrain(x, y, cell, terrain, c, r);

                // Last-move highlight beneath pieces
                if (lastMove != null && (logicalPos == lastMove.Value.To || logicalPos == lastMove.Value.From))
                    DrawLastMoveCell(x, y, cell, logicalPos == lastMove.Value.To);

                // Piece (skipped at the destination while the fly animation runs)
                var piece = _vm.State.GetPieceAt(logicalPos);
                if (piece != null && !(animating && logicalPos == lastMove!.Value.To))
                    DrawPiece(x, y, cell, piece.Value);

                // Selection highlight
                if (_vm.SelectedPosition == logicalPos)
                    DrawHighlight(x, y, cell, Theme.GetColor("GoldBrush"), 0.6);

                // Legal move indicator
                if (_vm.LegalMoves.Contains(logicalPos))
                    DrawLegalMoveIndicator(x, y, cell, _vm.State.HasPieceAt(logicalPos));

                // Keyboard cursor
                if (_cursorVisual != null &&
                    visualPos.Col == _cursorVisual.Value.Col &&
                    visualPos.Row == _cursorVisual.Value.Row)
                    DrawCursor(x, y, cell);
            }
        }

        DrawGridLines(left, top, cell);
        DrawCoordinates(left, top, cell);

        // Transparent overlay for click handling (must be on top)
        var clickOverlay = new Rectangle
        {
            Width = BoardCanvas.Width,
            Height = BoardCanvas.Height,
            Fill = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0)) // Fully transparent
        };
        clickOverlay.MouseLeftButtonDown += BoardCanvas_Click;
        Canvas.SetLeft(clickOverlay, 0);
        Canvas.SetTop(clickOverlay, 0);
        BoardCanvas.Children.Add(clickOverlay);

        UpdateUI();

        if (animating && lastMove != null)
            StartMoveAnimation(lastMove.Value);
    }

    private void NewGame_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new StartDialog
        {
            Owner = this,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        if (dialog.ShowDialog() == true)
        {
            CancelAnimation(); // A mid-flight overlay must not leak into the new game
            _vm.StartGame(dialog.HumanFirst, dialog.AiVsAi, dialog.AiTimeMs);
            RenderBoard();
        }
    }

    private void FlipBoard_Click(object sender, RoutedEventArgs e)
    {
        CancelAnimation(); // A mid-flight overlay would show stale coordinates after the flip
        _vm.ToggleFlip();
        RenderBoard();
    }

    private void OnGameOver(GameStatus status)
    {
        RenderBoard();
        var dialog = new GameOverDialog(status)
        {
            Owner = this,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        if (dialog.ShowDialog() == true)
        {
            CancelAnimation(); // A mid-flight overlay must not leak into the new game
            _vm.StartNewGame();
            RenderBoard();
        }
    }

    private void UpdateUI()
    {
        StatusLabel.Text = _vm.StatusText;

        if (_vm.AiThinking)
            ThinkingIndicator.Text = "Thinking...";
        else if (_vm.State.Status == GameStatus.InProgress)
            ThinkingIndicator.Text = _vm.TurnIndicator;
        else
            ThinkingIndicator.Text = "";

        CapturedBlueLabel.Text = string.Join(" ",
            _vm.State.CapturedBlue.Select(p => MainViewModel.AnimalEmoji(p.Animal)));
        CapturedRedLabel.Text = string.Join(" ",
            _vm.State.CapturedRed.Select(p => MainViewModel.AnimalEmoji(p.Animal)));

        if (string.IsNullOrEmpty(CapturedBlueLabel.Text))
            CapturedBlueLabel.Text = "—";
        if (string.IsNullOrEmpty(CapturedRedLabel.Text))
            CapturedRedLabel.Text = "—";

        // Update move history
        MoveHistoryLabel.Text = string.Join("\n",
            _vm.MoveHistory.Select((m, i) =>
            {
                string prefix = i % 2 == 0 ? $"{i / 2 + 1}. " : "   ";
                return $"{prefix}{m}";
            }));

        // Keyboard support: reset the cursor on a new game and give the board
        // focus whenever a human turn begins
        if (_vm.LastMove == null && _vm.MoveCounter == 0)
            _cursorVisual = (3, 4);

        bool humanTurnNow = _vm.IsHumanTurn && !_vm.AiVsAi && _vm.State.Status == GameStatus.InProgress;
        if (humanTurnNow && !_wasHumanTurn)
            BoardCanvas.Focus();
        _wasHumanTurn = humanTurnNow;
    }
}
