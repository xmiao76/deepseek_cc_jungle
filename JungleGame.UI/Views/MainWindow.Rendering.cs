using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using JungleGame.Core.Model;
using JungleGame.UI.ViewModels;

namespace JungleGame.UI.Views;

public partial class MainWindow
{
    // ---- Terrain ----

    private void DrawTerrain(double x, double y, double cell, Terrain terrain, int logicalCol, int logicalRow)
    {
        var rect = new Rectangle
        {
            Width = cell,
            Height = cell,
            Stroke = Theme.GetBrush("TerrainStrokeBrush"),
            StrokeThickness = 0.5
        };

        switch (terrain)
        {
            case Terrain.Land:
                // Deterministic checker pattern so the board reads as a field,
                // stable across re-renders and independent of the flip state
                rect.Fill = (logicalCol * 7 + logicalRow * 3) % 2 == 0
                    ? Theme.GetBrush("LandBrush")
                    : Theme.GetBrush("LandAltBrush");
                break;
            case Terrain.River:
                rect.Fill = Theme.GetBrush("RiverBrush"); // shared frozen gradient
                break;
            case Terrain.TrapBlue:
            case Terrain.TrapRed:
                rect.Fill = Theme.GetBrush("TrapBrush");
                break;
            case Terrain.DenBlue:
            case Terrain.DenRed:
                rect.Fill = Theme.GetBrush("DenBrush");
                break;
        }

        Canvas.SetLeft(rect, x);
        Canvas.SetTop(rect, y);
        BoardCanvas.Children.Add(rect);

        if (terrain == Terrain.TrapBlue || terrain == Terrain.TrapRed)
        {
            // Inner frame makes the trap read as a pit, not just a red cell
            var frame = new Rectangle
            {
                Width = cell - 6,
                Height = cell - 6,
                Stroke = Theme.GetBrush("TrapMarkerBrush"),
                StrokeThickness = 1,
                RadiusX = 3,
                RadiusY = 3
            };
            Canvas.SetLeft(frame, x + 3);
            Canvas.SetTop(frame, y + 3);
            BoardCanvas.Children.Add(frame);

            var marker = new TextBlock
            {
                Text = "⚠",
                FontSize = cell * 0.35,
                Foreground = Theme.GetBrush("TrapMarkerBrush"),
                TextAlignment = TextAlignment.Center,
                Width = cell,
                Height = cell * 0.5
            };
            Canvas.SetLeft(marker, x);
            Canvas.SetTop(marker, y + cell * 0.25);
            BoardCanvas.Children.Add(marker);
        }
        else if (terrain == Terrain.DenBlue || terrain == Terrain.DenRed)
        {
            var frame = new Rectangle
            {
                Width = cell - 4,
                Height = cell - 4,
                Stroke = Theme.GetBrush("DenFrameBrush"),
                StrokeThickness = 2,
                RadiusX = 4,
                RadiusY = 4
            };
            Canvas.SetLeft(frame, x + 2);
            Canvas.SetTop(frame, y + 2);
            BoardCanvas.Children.Add(frame);

            var marker = new TextBlock
            {
                Text = "\U0001F3E0", // 🏠
                FontSize = cell * 0.4,
                TextAlignment = TextAlignment.Center,
                Width = cell,
                Height = cell * 0.55
            };
            Canvas.SetLeft(marker, x);
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
                    Stroke = Theme.GetBrush("RiverWaveBrush"),
                    StrokeThickness = 1.5
                };
                BoardCanvas.Children.Add(wave);
            }
        }
    }

    // ---- Pieces ----

    private void DrawPiece(double x, double y, double cell, Piece piece) =>
        BoardCanvas.Children.Add(DrawPieceRoot(x, y, cell, piece));

    /// <summary>
    /// Builds the visual tree of one piece disc positioned at (x, y). Also used by
    /// the move animation, which flies this root element from the source cell to
    /// the destination cell.
    /// </summary>
    private Canvas DrawPieceRoot(double x, double y, double cell, Piece piece)
    {
        double margin = cell * 0.12;
        double size = cell - margin * 2;

        (Color main, Color dark, Color light) = piece.Owner == Player.Blue
            ? (Theme.GetColor("BluePieceBrush"), Theme.GetColor("BluePieceDarkBrush"), Theme.GetColor("BluePieceLightBrush"))
            : (Theme.GetColor("RedPieceBrush"), Theme.GetColor("RedPieceDarkBrush"), Theme.GetColor("RedPieceLightBrush"));

        var root = new Canvas { Width = cell, Height = cell };
        Canvas.SetLeft(root, x);
        Canvas.SetTop(root, y);

        // Drop shadow
        var shadow = new Ellipse
        {
            Width = size,
            Height = size,
            Fill = Theme.GetBrush("ShadowBrush")
        };
        Canvas.SetLeft(shadow, margin + 2);
        Canvas.SetTop(shadow, margin + 2);
        root.Children.Add(shadow);

        // Main disc: three-stop radial gradient (top-left highlight → base → rim)
        var gradient = new RadialGradientBrush
        {
            GradientOrigin = new Point(0.35, 0.3),
            Center = new Point(0.5, 0.5),
            RadiusX = 0.75,
            RadiusY = 0.75
        };
        gradient.GradientStops.Add(new GradientStop(light, 0));
        gradient.GradientStops.Add(new GradientStop(main, 0.45));
        gradient.GradientStops.Add(new GradientStop(dark, 1));

        var disc = new Ellipse
        {
            Width = size,
            Height = size,
            Fill = gradient,
            Stroke = new SolidColorBrush(dark),
            StrokeThickness = 2
        };
        Canvas.SetLeft(disc, margin);
        Canvas.SetTop(disc, margin);
        root.Children.Add(disc);

        // Inner ring for depth
        var ring = new Ellipse
        {
            Width = size - 6,
            Height = size - 6,
            Stroke = new SolidColorBrush(light),
            StrokeThickness = 1
        };
        Canvas.SetLeft(ring, margin + 3);
        Canvas.SetTop(ring, margin + 3);
        root.Children.Add(ring);

        // Animal emoji with a 1px offset dark copy underneath for depth
        string glyph = MainViewModel.AnimalEmoji(piece.Animal);
        var emojiShadow = new TextBlock
        {
            Text = glyph,
            FontSize = size * 0.52,
            Foreground = new SolidColorBrush(Color.FromArgb(120, 0, 0, 0)),
            TextAlignment = TextAlignment.Center,
            Width = size,
            Height = size * 0.58
        };
        Canvas.SetLeft(emojiShadow, margin + 1);
        Canvas.SetTop(emojiShadow, margin + size * 0.04 + 1);
        root.Children.Add(emojiShadow);

        var emoji = new TextBlock
        {
            Text = glyph,
            FontSize = size * 0.52,
            TextAlignment = TextAlignment.Center,
            Width = size,
            Height = size * 0.58
        };
        Canvas.SetLeft(emoji, margin);
        Canvas.SetTop(emoji, margin + size * 0.04);
        root.Children.Add(emoji);

        // Rank pip (bottom-right): the animal's rank number
        double pipRadius = size * 0.14;
        double pipX = margin + size - pipRadius * 2 + size * 0.02;
        double pipY = margin + size - pipRadius * 2 + size * 0.02;
        var pip = new Ellipse
        {
            Width = pipRadius * 2,
            Height = pipRadius * 2,
            Fill = new SolidColorBrush(Color.FromArgb(160, 0, 0, 0))
        };
        Canvas.SetLeft(pip, pipX);
        Canvas.SetTop(pip, pipY);
        root.Children.Add(pip);

        var pipText = new TextBlock
        {
            Text = ((int)piece.Animal).ToString(CultureInfo.InvariantCulture),
            FontSize = size * 0.17,
            FontWeight = FontWeights.Bold,
            Foreground = Brushes.White,
            TextAlignment = TextAlignment.Center,
            Width = pipRadius * 2,
            Height = pipRadius * 2
        };
        Canvas.SetLeft(pipText, pipX);
        Canvas.SetTop(pipText, pipY + pipRadius * 2 * 0.05);
        root.Children.Add(pipText);

        // Animal name (centered below emoji)
        var nameLabel = new TextBlock
        {
            Text = MainViewModel.AnimalName(piece.Animal),
            FontSize = size * 0.22,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White,
            TextAlignment = TextAlignment.Center,
            Width = size,
            Height = size * 0.3
        };
        Canvas.SetLeft(nameLabel, margin);
        Canvas.SetTop(nameLabel, margin + size * 0.58);
        root.Children.Add(nameLabel);

        // Trap indicator: piece on enemy trap gets a visual warning
        if (_vm.State.Board.IsTrap(piece.Position, piece.Owner))
        {
            var trapBadge = new TextBlock
            {
                Text = "!",
                FontSize = size * 0.4,
                FontWeight = FontWeights.Bold,
                Foreground = Theme.GetBrush("TrapMarkerBrush"),
                TextAlignment = TextAlignment.Center,
                Width = size * 0.4
            };
            Canvas.SetLeft(trapBadge, margin + size * 0.52);
            Canvas.SetTop(trapBadge, margin - size * 0.12);
            root.Children.Add(trapBadge);
        }

        return root;
    }

    // ---- Overlays ----

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

        var color = Theme.GetColor(isCapture ? "CaptureMoveBrush" : "LegalMoveBrush");
        var dot = new Ellipse
        {
            Width = radius * 2,
            Height = radius * 2,
            Fill = new SolidColorBrush(Color.FromArgb(
                isCapture ? (byte)0xC0 : (byte)0xA0, color.R, color.G, color.B))
        };
        Canvas.SetLeft(dot, centerX - radius);
        Canvas.SetTop(dot, centerY - radius);
        BoardCanvas.Children.Add(dot);
    }

    private void DrawLastMoveCell(double x, double y, double cell, bool isDestination)
    {
        byte alpha = isDestination ? (byte)0x40 : (byte)0x26; // ~0.25 / ~0.15
        var overlay = new Rectangle
        {
            Width = cell,
            Height = cell,
            Fill = new SolidColorBrush(Color.FromArgb(alpha, 0xFF, 0xD7, 0x00))
        };
        Canvas.SetLeft(overlay, x);
        Canvas.SetTop(overlay, y);
        BoardCanvas.Children.Add(overlay);
    }

    /// <summary>Four gold corner brackets marking the keyboard cursor cell.</summary>
    private void DrawCursor(double x, double y, double cell)
    {
        var gold = Theme.GetBrush("GoldBrush");
        double len = cell * 0.25;

        AddBracket(x, y + len, x, y, x + len, y, gold);                             // top-left
        AddBracket(x + cell - len, y, x + cell, y, x + cell, y + len, gold);       // top-right
        AddBracket(x + len, y + cell, x, y + cell, x, y + cell - len, gold);       // bottom-left
        AddBracket(x + cell, y + cell - len, x + cell, y + cell, x + cell - len, y + cell, gold); // bottom-right
    }

    /// <summary>Draws an L-shaped bracket: start → corner → end (3 points).</summary>
    private void AddBracket(double x1, double y1, double x2, double y2, double x3, double y3, Brush brush)
    {
        var path = new Path
        {
            Stroke = brush,
            StrokeThickness = 3,
            Data = new PathGeometry(new[]
            {
                new PathFigure(new Point(x1, y1), new[]
                {
                    new LineSegment(new Point(x2, y2), true),
                    new LineSegment(new Point(x3, y3), true)
                }, false)
            })
        };
        BoardCanvas.Children.Add(path);
    }

    // ---- Grid & coordinates ----

    private void DrawGridLines(double left, double top, double cell)
    {
        var stroke = Theme.GetBrush("GridLineBrush");
        for (int c = 0; c <= BoardCols; c++)
        {
            var line = new Line
            {
                X1 = left + c * cell,
                Y1 = top,
                X2 = left + c * cell,
                Y2 = top + BoardRows * cell,
                Stroke = stroke,
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
                Stroke = stroke,
                StrokeThickness = 1
            };
            BoardCanvas.Children.Add(line);
        }
    }

    /// <summary>
    /// Column letters (a-g) and row numbers (1-9) in the board margin, derived
    /// from visual coordinates so they rotate together with the board flip.
    /// </summary>
    private void DrawCoordinates(double left, double top, double cell)
    {
        var brush = Theme.GetBrush("TextSecondaryBrush");
        double fontSize = cell * 0.18;

        for (int v = 0; v < BoardCols; v++)
        {
            char letter = (char)('a' + (_vm.BoardFlipped ? BoardCols - 1 - v : v));
            var label = new TextBlock
            {
                Text = letter.ToString(),
                FontSize = fontSize,
                Foreground = brush,
                TextAlignment = TextAlignment.Center,
                Width = cell
            };
            Canvas.SetLeft(label, left + v * cell);
            Canvas.SetTop(label, top + BoardRows * cell + 3);
            BoardCanvas.Children.Add(label);
        }

        for (int vr = 0; vr < BoardRows; vr++)
        {
            int number = _vm.BoardFlipped ? BoardRows - vr : vr + 1;
            var label = new TextBlock
            {
                Text = number.ToString(CultureInfo.InvariantCulture),
                FontSize = fontSize,
                Foreground = brush,
                TextAlignment = TextAlignment.Center,
                Width = BoardMargin,
                Height = fontSize
            };
            Canvas.SetLeft(label, 0);
            Canvas.SetTop(label, top + (BoardRows - 1 - vr) * cell + (cell - fontSize) / 2);
            BoardCanvas.Children.Add(label);
        }
    }
}
