using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using JungleGame.Core.Model;
using JungleGame.UI.ViewModels;

namespace JungleGame.UI.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;
    private const int BoardCols = 7;
    private const int BoardRows = 9;
    private const double BoardMargin = 20;

    public MainWindow(bool humanFirst = true, bool aiVsAi = false)
    {
        InitializeComponent();
        _vm = new MainViewModel();
        _vm.GameOver += OnGameOver;
        _vm.BoardChanged += RenderBoard;
        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.AiThinking))
                Dispatcher.Invoke(RenderBoard);
        };
        _vm.StartGame(humanFirst, aiVsAi);
        RenderBoard();
        UpdateUI();
    }

    private double CellSize => Math.Min(
        (BoardCanvas.Width - BoardMargin * 2) / BoardCols,
        (BoardCanvas.Height - BoardMargin * 2) / BoardRows);

    private double BoardLeft => BoardMargin + (BoardCanvas.Width - BoardMargin * 2 - CellSize * BoardCols) / 2;
    private double BoardTop => BoardMargin + (BoardCanvas.Height - BoardMargin * 2 - CellSize * BoardRows) / 2;

    private void RenderBoard()
    {
        BoardCanvas.Children.Clear();

        double cell = CellSize;
        double left = BoardLeft;
        double top = BoardTop;

        for (int r = 0; r < BoardRows; r++)
        {
            for (int c = 0; c < BoardCols; c++)
            {
                var logicalPos = new Position(c, r);
                var visualPos = _vm.GetVisualPosition(logicalPos);
                double x = left + visualPos.Col * cell;
                double y = top + (BoardRows - 1 - visualPos.Row) * cell;

                // Draw terrain
                var terrain = _vm.State.Board.GetTerrain(logicalPos);
                DrawTerrain(x, y, cell, terrain);

                // Draw piece
                var piece = _vm.State.GetPieceAt(logicalPos);
                if (piece != null)
                    DrawPiece(x, y, cell, piece.Value);

                // Draw selection highlight
                if (_vm.SelectedPosition == logicalPos)
                    DrawHighlight(x, y, cell, Color.FromRgb(0xFF, 0xD7, 0x00), 0.6);

                // Draw legal move indicator
                if (_vm.LegalMoves.Contains(logicalPos))
                {
                    bool isCapture = _vm.State.HasPieceAt(logicalPos);
                    DrawLegalMoveIndicator(x, y, cell, isCapture);
                }
            }
        }

        // Draw grid lines
        DrawGridLines(left, top, cell);

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
    }

    private void DrawTerrain(double x, double y, double cell, Terrain terrain)
    {
        var rect = new Rectangle
        {
            Width = cell,
            Height = cell,
            Stroke = new SolidColorBrush(Color.FromRgb(100, 80, 60)),
            StrokeThickness = 0.5
        };

        switch (terrain)
        {
            case Terrain.Land:
                rect.Fill = new SolidColorBrush(Color.FromRgb(0xDE, 0xB8, 0x87));
                break;
            case Terrain.River:
                rect.Fill = new LinearGradientBrush(
                    Color.FromRgb(0x41, 0x69, 0xE1),
                    Color.FromRgb(0x21, 0x49, 0xB1),
                    90);
                break;
            case Terrain.TrapBlue:
            case Terrain.TrapRed:
                rect.Fill = new SolidColorBrush(Color.FromRgb(0xCD, 0x5C, 0x5C));
                break;
            case Terrain.DenBlue:
            case Terrain.DenRed:
                rect.Fill = new SolidColorBrush(Color.FromRgb(0x8B, 0x00, 0x00));
                break;
        }

        Canvas.SetLeft(rect, x);
        Canvas.SetTop(rect, y);
        BoardCanvas.Children.Add(rect);

        // Draw trap/den marker
        if (terrain == Terrain.TrapBlue || terrain == Terrain.TrapRed)
        {
            var marker = new TextBlock
            {
                Text = "⚠", // ⚠
                FontSize = cell * 0.35,
                Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xEB, 0x3B)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Canvas.SetLeft(marker, x + cell * 0.32);
            Canvas.SetTop(marker, y + cell * 0.25);
            BoardCanvas.Children.Add(marker);
        }
        else if (terrain == Terrain.DenBlue || terrain == Terrain.DenRed)
        {
            var marker = new TextBlock
            {
                Text = "\U0001F3E0", // 🏠
                FontSize = cell * 0.4,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Canvas.SetLeft(marker, x + cell * 0.28);
            Canvas.SetTop(marker, y + cell * 0.22);
            BoardCanvas.Children.Add(marker);
        }

        // River texture (wave pattern)
        if (terrain == Terrain.River)
        {
            for (int w = 0; w < 3; w++)
            {
                var wave = new Line
                {
                    X1 = x + 5,
                    Y1 = y + cell * (0.3 + w * 0.2),
                    X2 = x + cell - 5,
                    Y2 = y + cell * (0.3 + w * 0.2),
                    Stroke = new SolidColorBrush(Color.FromArgb(80, 255, 255, 255)),
                    StrokeThickness = 1.5
                };
                BoardCanvas.Children.Add(wave);
            }
        }
    }

    private void DrawPiece(double x, double y, double cell, Piece piece)
    {
        // Piece circle
        double margin = cell * 0.12;
        double size = cell - margin * 2;
        Color mainColor = piece.Owner == Player.Blue
            ? Color.FromRgb(0x41, 0x69, 0xE1)
            : Color.FromRgb(0xDC, 0x14, 0x3C);
        Color borderColor = piece.Owner == Player.Blue
            ? Color.FromRgb(0x21, 0x49, 0xB1)
            : Color.FromRgb(0xB0, 0x10, 0x30);

        // Shadow
        var shadow = new Ellipse
        {
            Width = size,
            Height = size,
            Fill = new SolidColorBrush(Color.FromArgb(100, 0, 0, 0))
        };
        Canvas.SetLeft(shadow, x + margin + 2);
        Canvas.SetTop(shadow, y + margin + 2);
        BoardCanvas.Children.Add(shadow);

        // Main circle
        var circle = new Ellipse
        {
            Width = size,
            Height = size,
            Fill = new RadialGradientBrush(
                Color.FromRgb(
                    (byte)Math.Min(255, mainColor.R + 50),
                    (byte)Math.Min(255, mainColor.G + 50),
                    (byte)Math.Min(255, mainColor.B + 50)),
                mainColor),
            Stroke = new SolidColorBrush(borderColor),
            StrokeThickness = 2
        };
        Canvas.SetLeft(circle, x + margin);
        Canvas.SetTop(circle, y + margin);
        BoardCanvas.Children.Add(circle);

        // Animal emoji
        var emoji = new TextBlock
        {
            Text = MainViewModel.AnimalEmoji(piece.Animal),
            FontSize = size * 0.52,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            Width = size,
            Height = size * 0.58
        };
        Canvas.SetLeft(emoji, x + margin);
        Canvas.SetTop(emoji, y + margin + size * 0.04);
        BoardCanvas.Children.Add(emoji);

        // Animal name (centered below emoji)
        var nameLabel = new TextBlock
        {
            Text = MainViewModel.AnimalName(piece.Animal),
            FontSize = size * 0.22,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Colors.White),
            TextAlignment = TextAlignment.Center,
            Width = size,
            Height = size * 0.3
        };
        Canvas.SetLeft(nameLabel, x + margin);
        Canvas.SetTop(nameLabel, y + margin + size * 0.58);
        BoardCanvas.Children.Add(nameLabel);

        // Trap indicator: piece on enemy trap gets a visual warning
        if (_vm.State.Board.IsTrap(piece.Position, piece.Owner))
        {
            var trapIndicator = new TextBlock
            {
                Text = "!",
                FontSize = size * 0.4,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Colors.Yellow),
                TextAlignment = TextAlignment.Center
            };
            Canvas.SetLeft(trapIndicator, x + margin + size * 0.38);
            Canvas.SetTop(trapIndicator, y + margin - size * 0.1);
            BoardCanvas.Children.Add(trapIndicator);
        }
    }

    private void DrawHighlight(double x, double y, double cell, Color color, double opacity)
    {
        var highlight = new Rectangle
        {
            Width = cell,
            Height = cell,
            Fill = new SolidColorBrush(Color.FromArgb(
                (byte)(opacity * 255), color.R, color.G, color.B)),
            Stroke = new SolidColorBrush(color),
            StrokeThickness = 3
        };
        Canvas.SetLeft(highlight, x);
        Canvas.SetTop(highlight, y);
        BoardCanvas.Children.Add(highlight);
    }

    private void DrawLegalMoveIndicator(double x, double y, double cell, bool isCapture)
    {
        double centerX = x + cell / 2;
        double centerY = y + cell / 2;
        double radius = cell * 0.22;

        var dot = new Ellipse
        {
            Width = radius * 2,
            Height = radius * 2,
            Fill = isCapture
                ? new SolidColorBrush(Color.FromArgb(0xC0, 0xFF, 0x44, 0x44))
                : new SolidColorBrush(Color.FromArgb(0xA0, 0x00, 0xCC, 0x00))
        };
        Canvas.SetLeft(dot, centerX - radius);
        Canvas.SetTop(dot, centerY - radius);
        BoardCanvas.Children.Add(dot);
    }

    private void DrawGridLines(double left, double top, double cell)
    {
        for (int c = 0; c <= BoardCols; c++)
        {
            var line = new Line
            {
                X1 = left + c * cell,
                Y1 = top,
                X2 = left + c * cell,
                Y2 = top + BoardRows * cell,
                Stroke = new SolidColorBrush(Color.FromRgb(60, 40, 30)),
                StrokeThickness = 1
            };
            BoardCanvas.Children.Add(line);
        }
        for (int r = 0; r <= BoardRows; r++)
        {
            var line = new Line
            {
                X1 = left,
                Y1 = top + r * cell,
                X2 = left + BoardCols * cell,
                Y2 = top + r * cell,
                Stroke = new SolidColorBrush(Color.FromRgb(60, 40, 30)),
                StrokeThickness = 1
            };
            BoardCanvas.Children.Add(line);
        }
    }

    private void BoardCanvas_Click(object sender, MouseButtonEventArgs e)
    {
        if (_vm.AiThinking) return;

        var pos = e.GetPosition(BoardCanvas);
        double cell = CellSize;
        double left = BoardLeft;
        double top = BoardTop;

        int col = (int)((pos.X - left) / cell);
        int row = (BoardRows - 1) - (int)((pos.Y - top) / cell);

        if (col < 0 || col >= BoardCols || row < 0 || row >= BoardRows)
            return;

        // Convert visual position back to logical position
        var visualPos = new Position(col, row);
        var logicalPos = _vm.BoardFlipped
            ? new Position(6 - visualPos.Col, 8 - visualPos.Row)
            : visualPos;

        _vm.HandleCellClick(logicalPos);
        RenderBoard();
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
            _vm.StartGame(dialog.HumanFirst, dialog.AiVsAi);
            RenderBoard();
        }
    }

    private void FlipBoard_Click(object sender, RoutedEventArgs e)
    {
        _vm.ToggleFlip();
        RenderBoard();
    }

    private void OnGameOver(GameStatus status)
    {
        Dispatcher.Invoke(() =>
        {
            RenderBoard();
            var dialog = new GameOverDialog(status)
            {
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            if (dialog.ShowDialog() == true)
            {
                _vm.StartNewGame();
                RenderBoard();
            }
        });
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
    }
}
