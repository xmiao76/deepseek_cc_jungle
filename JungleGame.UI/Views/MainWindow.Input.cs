using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using JungleGame.Core.Model;

namespace JungleGame.UI.Views;

public partial class MainWindow
{
    // Keyboard cursor, kept in visual space so arrow keys map intuitively in
    // both orientations; hidden while the AI thinks or in watch mode
    private (int Col, int Row)? _cursorVisual;
    private bool _wasHumanTurn;

    // ---- Mouse ----

    private void BoardCanvas_Click(object sender, MouseButtonEventArgs e)
    {
        if (_vm.AiThinking) return;

        var logical = LogicalPositionFromPoint(e.GetPosition(BoardCanvas));
        if (logical == null)
            return;

        // HandleCellClick raises BoardChanged, which already re-renders the board
        _vm.HandleCellClick(logical.Value);
    }

    /// <summary>Converts a canvas point to a logical board position (null when outside the board).</summary>
    private Position? LogicalPositionFromPoint(Point pos)
    {
        double cell = CellSize;
        double left = BoardLeft;
        double top = BoardTop;

        int col = (int)((pos.X - left) / cell);
        int row = (BoardRows - 1) - (int)((pos.Y - top) / cell);

        if (col < 0 || col >= BoardCols || row < 0 || row >= BoardRows)
            return null;

        // Convert visual position back to logical position
        var visualPos = new Position(col, row);
        return _vm.BoardFlipped
            ? new Position(6 - visualPos.Col, 8 - visualPos.Row)
            : visualPos;
    }

    // ---- Keyboard ----

    private void BoardCanvas_KeyDown(object sender, KeyEventArgs e)
    {
        if (_vm.AiThinking || _vm.AiVsAi || _vm.State.Status != GameStatus.InProgress)
            return;

        switch (e.Key)
        {
            case Key.Up:
                MoveCursor(0, 1);
                break;
            case Key.Down:
                MoveCursor(0, -1);
                break;
            case Key.Left:
                MoveCursor(-1, 0);
                break;
            case Key.Right:
                MoveCursor(1, 0);
                break;
            case Key.Enter:
            case Key.Space:
                CommitCursor();
                break;
            case Key.Escape:
                _vm.ClearSelection();
                break;
            default:
                return;
        }

        e.Handled = true;
    }

    private void MoveCursor(int dCol, int dRow)
    {
        var cursor = _cursorVisual ?? (3, 4);
        int col = Math.Clamp(cursor.Col + dCol, 0, BoardCols - 1);
        int row = Math.Clamp(cursor.Row + dRow, 0, BoardRows - 1);
        _cursorVisual = (col, row);
        RenderBoard();
    }

    /// <summary>Enter/Space act exactly like a click on the cursor cell.</summary>
    private void CommitCursor()
    {
        var cursor = _cursorVisual ?? (3, 4);
        _cursorVisual = cursor;
        var visualPos = new Position(cursor.Col, cursor.Row);
        var logicalPos = _vm.BoardFlipped
            ? new Position(6 - visualPos.Col, 8 - visualPos.Row)
            : visualPos;
        _vm.HandleCellClick(logicalPos);
    }

    // ---- Move animation ----

    /// <summary>
    /// Flies a piece clone from the source cell to the destination cell on top of
    /// the freshly rendered board (the resting piece at the destination is skipped
    /// while the animation runs). The overlay removes itself when the flight ends
    /// and triggers one final render, so it composes with the clear-and-rebuild
    /// rendering loop.
    /// </summary>
    private void StartMoveAnimation((Position From, Position To, bool WasCapture) move)
    {
        _animationInFlight = true;
        _animationCancelled = false;
        _animationOverlays.Clear();

        var piece = _vm.State.GetPieceAt(move.To);
        if (piece == null)
        {
            // Nothing to fly — settle into the final state
            _animationInFlight = false;
            _animatedMoveId = _vm.MoveCounter;
            RenderBoard();
            return;
        }

        var fromVisual = _vm.GetVisualPosition(move.From);
        var toVisual = _vm.GetVisualPosition(move.To);
        (double startX, double startY) = CellOrigin(fromVisual.Col, fromVisual.Row);
        (double endX, double endY) = CellOrigin(toVisual.Col, toVisual.Row);
        double cell = CellSize;

        var flyRoot = DrawPieceRoot(startX, startY, cell, piece.Value);
        BoardCanvas.Children.Add(flyRoot);
        _animationOverlays.Add(flyRoot);

        if (move.WasCapture)
        {
            // Expanding gold ring marking the capture
            double radius = cell * 0.3;
            var ring = new Ellipse
            {
                Width = radius * 2,
                Height = radius * 2,
                Stroke = Theme.GetBrush("GoldBrush"),
                StrokeThickness = 3,
                RenderTransformOrigin = new Point(0.5, 0.5)
            };
            Canvas.SetLeft(ring, endX + cell / 2 - radius);
            Canvas.SetTop(ring, endY + cell / 2 - radius);
            var ringScale = new ScaleTransform(0.5, 0.5);
            ring.RenderTransform = ringScale;
            BoardCanvas.Children.Add(ring);
            _animationOverlays.Add(ring);

            ringScale.BeginAnimation(ScaleTransform.ScaleXProperty,
                new DoubleAnimation(0.5, 1.4, TimeSpan.FromMilliseconds(220))
                { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } });
            ringScale.BeginAnimation(ScaleTransform.ScaleYProperty,
                new DoubleAnimation(0.5, 1.4, TimeSpan.FromMilliseconds(220))
                { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } });
            ring.BeginAnimation(OpacityProperty,
                new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(220)));
        }

        var flyX = new DoubleAnimation(startX, endX, TimeSpan.FromMilliseconds(200))
        { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
        var flyY = new DoubleAnimation(startY, endY, TimeSpan.FromMilliseconds(200))
        { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
        long animId = _vm.MoveCounter;
        flyX.Completed += (_, _) => FinishAnimation(animId);
        flyRoot.BeginAnimation(Canvas.LeftProperty, flyX);
        flyRoot.BeginAnimation(Canvas.TopProperty, flyY);
    }

    private void FinishAnimation(long animId)
    {
        if (_animationCancelled)
            return;

        foreach (var overlay in _animationOverlays)
            BoardCanvas.Children.Remove(overlay);
        _animationOverlays.Clear();

        _animationInFlight = false;

        if (animId == _vm.MoveCounter)
        {
            // The flight belonged to the current move: settle the piece and
            // re-render once so it appears at rest
            _animatedMoveId = animId;
            RenderBoard();
        }
        else
        {
            // A newer move was applied mid-flight and already rendered at rest;
            // acknowledge it so no stale animation triggers
            _animatedMoveId = _vm.MoveCounter;
        }
    }

    /// <summary>Stops a mid-flight animation (board flip / new game) and settles the piece.</summary>
    private void CancelAnimation()
    {
        _animationCancelled = true;
        foreach (var overlay in _animationOverlays)
            BoardCanvas.Children.Remove(overlay);
        _animationOverlays.Clear();
        _animationInFlight = false;
        _animatedMoveId = _vm.MoveCounter;
    }
}
