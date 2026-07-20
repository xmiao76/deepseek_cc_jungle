using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using JungleGame.Core.Logic;
using JungleGame.Core.Models;
using JungleGame.Desktop.Helpers;
using JungleGame.Desktop.ViewModels;

namespace JungleGame.Desktop.Controls;

public class JungleBoardControl : FrameworkElement
{
    private readonly VisualCollection _visuals;
    private readonly DrawingVisual _bgVisual = new();
    private readonly DrawingVisual _terrainVisual = new();
    private readonly DrawingVisual _gridVisual = new();
    private readonly DrawingVisual _piecesVisual = new();
    private readonly DrawingVisual _highlightsVisual = new();

    private MainViewModel? _viewModel;

    private const int Cols = 7;
    private const int Rows = 9;

    // --- Brushes (cached) ---
    private static readonly Pen BoardBorderPen = new(new SolidColorBrush(Color.FromRgb(0x5C, 0x3A, 0x1E)), 3);

    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(nameof(ViewModel), typeof(MainViewModel), typeof(JungleBoardControl),
            new PropertyMetadata(null, OnViewModelChanged));

    public MainViewModel? ViewModel
    {
        get => (MainViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    public JungleBoardControl()
    {
        _visuals = new VisualCollection(this);
        _visuals.Add(_bgVisual);
        _visuals.Add(_terrainVisual);
        _visuals.Add(_gridVisual);
        _visuals.Add(_piecesVisual);
        _visuals.Add(_highlightsVisual);
        ClipToBounds = true;
        Loaded += (_, _) => RenderAll();
        SizeChanged += (_, _) => RenderAll();
    }

    private static void OnViewModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var c = (JungleBoardControl)d;
        if (e.OldValue is MainViewModel ov) ov.BoardNeedsRefresh -= c.OnRefresh;
        if (e.NewValue is MainViewModel nv) { c._viewModel = nv; nv.BoardNeedsRefresh += c.OnRefresh; }
        c.RenderAll();
    }

    private void OnRefresh(object? s, EventArgs e) => Dispatcher.Invoke(RenderAll);

    // ==================== Layout helpers ====================

    private double CellSize => Math.Min(ActualWidth / Cols, ActualHeight / Rows);
    private double OffsetX => (ActualWidth - CellSize * Cols) / 2;
    private double OffsetY => (ActualHeight - CellSize * Rows) / 2;

    private Point PosToPixel(BoardPosition pos)
    {
        var d = BoardFlipHelper.LogicalToDisplay(pos, _viewModel?.IsBoardFlipped ?? false);
        return new Point(OffsetX + (d.Col - 1) * CellSize + CellSize / 2,
                         OffsetY + (d.Row - 1) * CellSize + CellSize / 2);
    }

    private BoardPosition? PixelToPos(Point p)
    {
        int col = (int)((p.X - OffsetX) / CellSize) + 1;
        int row = (int)((p.Y - OffsetY) / CellSize) + 1;
        if (col < 1 || col > Cols || row < 1 || row > Rows) return null;
        return BoardFlipHelper.DisplayToLogical(new BoardPosition(col, row), _viewModel?.IsBoardFlipped ?? false);
    }

    // ==================== RenderAll ====================

    public void RenderAll()
    {
        if (ActualWidth <= 0 || ActualHeight <= 0) return;
        DrawBackground();
        DrawTerrain();
        DrawGrid();
        DrawPieces();
        DrawHighlights();
    }

    // ==================== Background ====================

    private void DrawBackground()
    {
        var dc = _bgVisual.RenderOpen();
        double cell = CellSize, ox = OffsetX, oy = OffsetY;
        double bw = cell * Cols, bh = cell * Rows;
        var boardRect = new Rect(ox, oy, bw, bh);

        // Wood-toned board
        var woodGrad = new LinearGradientBrush(
            Color.FromRgb(0xE8, 0xD5, 0xA3),
            Color.FromRgb(0xC4, 0xA6, 0x6B),
            new Point(0, 0), new Point(1, 1));
        dc.DrawRectangle(woodGrad, null, boardRect);

        // Subtle inner glow (vignette)
        var vignette = new RadialGradientBrush(
            Color.FromArgb(0x00, 0xFF, 0xFF, 0xFF),
            Color.FromArgb(0x30, 0x40, 0x20, 0x10));
        vignette.Center = new Point(0.5, 0.5);
        vignette.GradientOrigin = new Point(0.5, 0.5);
        vignette.RadiusX = 0.7; vignette.RadiusY = 0.7;
        dc.DrawRectangle(vignette, null, boardRect);

        // Dark border
        var borderRect = new Rect(ox - 2, oy - 2, bw + 4, bh + 4);
        dc.DrawRectangle(null, BoardBorderPen, borderRect);

        dc.Close();
    }

    // ==================== Terrain ====================

    private void DrawTerrain()
    {
        var dc = _terrainVisual.RenderOpen();
        double cell = CellSize, ox = OffsetX, oy = OffsetY;
        bool flip = _viewModel?.IsBoardFlipped ?? false;

        for (int c = 1; c <= Cols; c++)
        for (int r = 1; r <= Rows; r++)
        {
            var lp = new BoardPosition(c, r);
            var dp = BoardFlipHelper.LogicalToDisplay(lp, flip);
            double x = ox + (dp.Col - 1) * cell, y = oy + (dp.Row - 1) * cell;
            var rect = new Rect(x, y, cell, cell);
            double cx = x + cell / 2, cy = y + cell / 2;

            var terrain = DetermineTerrain(lp);
            switch (terrain)
            {
                case TerrainType.Water:
                    // Deep blue water
                    var waterGrad = new LinearGradientBrush(
                        Color.FromArgb(0xCC, 0x2E, 0x7D, 0xB2),
                        Color.FromArgb(0xCC, 0x1A, 0x5C, 0x8A),
                        new Point(0, 0), new Point(0, 1));
                    dc.DrawRectangle(waterGrad, null, rect);
                    // Subtle ripple lines
                    var ripplePen = new Pen(new SolidColorBrush(Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF)), Math.Max(0.5, cell * 0.015));
                    for (int i = 1; i <= 2; i++)
                    {
                        double ry = y + cell * i / 3;
                        dc.DrawLine(ripplePen, new Point(x + cell * 0.15, ry), new Point(x + cell * 0.85, ry));
                    }
                    break;

                case TerrainType.Trap:
                    // Dark danger zone
                    var trapBg = new SolidColorBrush(Color.FromArgb(0x66, 0xCC, 0x22, 0x22));
                    dc.DrawRectangle(trapBg, null, rect);
                    // X pattern
                    double pad = cell * 0.25;
                    var trapPen = new Pen(new SolidColorBrush(Color.FromArgb(0xDD, 0xAA, 0x00, 0x00)), Math.Max(1.5, cell * 0.05));
                    dc.DrawLine(trapPen, new Point(x + pad, y + pad), new Point(x + cell - pad, y + cell - pad));
                    dc.DrawLine(trapPen, new Point(x + cell - pad, y + pad), new Point(x + pad, y + cell - pad));
                    break;

                case TerrainType.Den:
                    // Golden palace
                    var denGrad = new RadialGradientBrush(
                        Color.FromArgb(0xBB, 0xFF, 0xE8, 0x80),
                        Color.FromArgb(0x66, 0xCC, 0xAA, 0x00));
                    denGrad.Center = new Point(0.5, 0.5);
                    denGrad.GradientOrigin = new Point(0.5, 0.5);
                    denGrad.RadiusX = 0.8; denGrad.RadiusY = 0.8;
                    dc.DrawRectangle(denGrad, null, rect);
                    // Concentric circles
                    double r1 = cell * 0.38, r2 = cell * 0.22;
                    var denPen = new Pen(new SolidColorBrush(Color.FromArgb(0xCC, 0x9B, 0x75, 0x00)), Math.Max(1.5, cell * 0.035));
                    dc.DrawEllipse(null, denPen, new Point(cx, cy), r1, r1);
                    dc.DrawEllipse(null, denPen, new Point(cx, cy), r2, r2);
                    dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(0x88, 0xFF, 0xD7, 0x00)), null, new Point(cx, cy), r2, r2);
                    break;
            }
        }
        dc.Close();
    }

    // ==================== Grid ====================

    private void DrawGrid()
    {
        var dc = _gridVisual.RenderOpen();
        double cell = CellSize, ox = OffsetX, oy = OffsetY;
        double bw = cell * Cols, bh = cell * Rows;
        var pen = new Pen(new SolidColorBrush(Color.FromArgb(0x55, 0x5C, 0x3A, 0x1E)), Math.Max(0.5, cell * 0.012));
        for (int i = 0; i <= Cols; i++) { double x = ox + i * cell; dc.DrawLine(pen, new Point(x, oy), new Point(x, oy + bh)); }
        for (int i = 0; i <= Rows; i++) { double y = oy + i * cell; dc.DrawLine(pen, new Point(ox, y), new Point(ox + bw, y)); }
        dc.Close();
    }

    // ==================== Pieces ====================

    private void DrawPieces()
    {
        var dc = _piecesVisual.RenderOpen();
        if (_viewModel?.BoardVM == null) { dc.Close(); return; }

        double cell = CellSize, ox = OffsetX, oy = OffsetY;
        bool flip = _viewModel?.IsBoardFlipped ?? false;
        double radius = cell * 0.44;
        double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;

        foreach (var pv in _viewModel.BoardVM.Pieces)
        {
            var lp = new BoardPosition(pv.DisplayCol, pv.DisplayRow);
            var dp = BoardFlipHelper.LogicalToDisplay(lp, flip);
            double cx = ox + (dp.Col - 1) * cell + cell / 2;
            double cy = oy + (dp.Row - 1) * cell + cell / 2;
            var center = new Point(cx, cy);

            bool isBlue = pv.IsBlue;
            Color main = isBlue ? Color.FromRgb(0x25, 0x6E, 0xD6) : Color.FromRgb(0xD6, 0x25, 0x25);
            Color dark = isBlue ? Color.FromRgb(0x0D, 0x3B, 0x8E) : Color.FromRgb(0x8E, 0x0D, 0x0D);
            Color light = isBlue ? Color.FromRgb(0x6B, 0xAB, 0xF5) : Color.FromRgb(0xF5, 0x6B, 0x6B);

            // Shadow
            dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(0x55, 0x00, 0x00, 0x00)), null,
                new Point(cx + cell * 0.04, cy + cell * 0.05), radius, radius);

            // Last-move / selection glows
            if (pv.IsLastMovedFrom)
                dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(0x55, 0xFF, 0xFF, 0x00)), null, center, radius * 1.35, radius * 1.35);
            if (pv.IsLastMovedTo)
                dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(0x66, 0xFF, 0xA5, 0x00)), null, center, radius * 1.35, radius * 1.35);
            if (pv.IsSelected)
                dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(0xAA, 0xFF, 0xD7, 0x00)), null, center, radius * 1.45, radius * 1.45);

            // Main piece circle with radial gradient (3D look)
            var pieceGrad = new RadialGradientBrush(light, main);
            pieceGrad.Center = new Point(0.35, 0.3);
            pieceGrad.GradientOrigin = new Point(0.35, 0.3);
            pieceGrad.RadiusX = 0.9; pieceGrad.RadiusY = 0.9;

            var strokePen = new Pen(new SolidColorBrush(dark), Math.Max(2, cell * 0.035));
            dc.DrawEllipse(pieceGrad, strokePen, center, radius, radius);

            // Inner highlight (specular spot)
            var spec = new RadialGradientBrush(Color.FromArgb(0x44, 0xFF, 0xFF, 0xFF), Color.FromArgb(0x00, 0xFF, 0xFF, 0xFF));
            spec.Center = new Point(0.3, 0.25); spec.GradientOrigin = new Point(0.3, 0.25);
            spec.RadiusX = 0.4; spec.RadiusY = 0.4;
            dc.DrawEllipse(spec, null, center, radius * 0.85, radius * 0.85);

            // ---- Animal icon (large emoji) ----
            string icon = GetAnimalIcon(pv.Type);
            var emojiFont = new Typeface(new System.Windows.Media.FontFamily("Segoe UI Emoji, Segoe UI Symbol, Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
            double iconSize = radius * 0.95;
            var iconText = new FormattedText(icon, System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, emojiFont, iconSize, Brushes.White, dpi);
            // Center the icon vertically, slightly above center to leave room for name
            double iconY = cy - iconText.Height / 2 - radius * 0.12;
            dc.DrawText(iconText, new Point(cx - iconText.Width / 2, iconY));

            // ---- Short English name (small, at bottom) ----
            string name = GetAnimalName(pv.Type);
            var nameFont = new Typeface(new System.Windows.Media.FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
            double nameSize = radius * 0.38;
            var nameText = new FormattedText(name, System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, nameFont, nameSize, Brushes.White, dpi);
            double nameY = cy + radius * 0.15;
            dc.DrawText(nameText, new Point(cx - nameText.Width / 2, nameY));
        }
        dc.Close();
    }

    // ==================== Highlights ====================

    private void DrawHighlights()
    {
        var dc = _highlightsVisual.RenderOpen();
        if (_viewModel?.BoardVM == null) { dc.Close(); return; }

        double cell = CellSize, ox = OffsetX, oy = OffsetY;
        bool flip = _viewModel?.IsBoardFlipped ?? false;

        foreach (var pos in _viewModel.BoardVM.HighlightedSquares)
        {
            var dp = BoardFlipHelper.LogicalToDisplay(pos, flip);
            double cx = ox + (dp.Col - 1) * cell + cell / 2;
            double cy = oy + (dp.Row - 1) * cell + cell / 2;
            var center = new Point(cx, cy);

            var pieceAt = _viewModel.BoardVM.GetPieceAt(pos);
            bool isCapture = pieceAt != null && pieceAt.Owner != _viewModel.HumanPlayer;

            if (isCapture)
            {
                double r = cell * 0.44;
                dc.DrawEllipse(null, new Pen(Brushes.Red, Math.Max(2.5, cell * 0.05)), center, r, r);
            }
            else
            {
                double dotR = cell * 0.17;
                dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(0xCC, 0x00, 0xCC, 0x00)), null, center, dotR, dotR);
            }
        }
        dc.Close();
    }

    // ==================== Mouse ====================

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);
        var bp = PixelToPos(e.GetPosition(this));
        if (bp.HasValue && _viewModel != null)
        {
            if (_viewModel.HandleSquareClick(bp.Value))
                RenderAll();
        }
        e.Handled = true;
    }

    protected override int VisualChildrenCount => _visuals.Count;
    protected override Visual GetVisualChild(int index) => _visuals[index];

    // ==================== Helpers ====================

    private static TerrainType DetermineTerrain(BoardPosition pos)
    {
        if (Board.IsWater(pos)) return TerrainType.Water;
        if (Board.IsDen(pos, Player.Red) || Board.IsDen(pos, Player.Blue)) return TerrainType.Den;
        if (Board.IsTrap(pos, Player.Red) || Board.IsTrap(pos, Player.Blue)) return TerrainType.Trap;
        return TerrainType.Land;
    }

    private static string GetAnimalIcon(PieceType type) => type switch
    {
        PieceType.Elephant => "\U0001F418", // 🐘
        PieceType.Lion => "\U0001F981",     // 🦁
        PieceType.Tiger => "\U0001F42F",    // 🐯
        PieceType.Leopard => "\U0001F406",  // 🐆
        PieceType.Wolf => "\U0001F43A",     // 🐺
        PieceType.Dog => "\U0001F415",      // 🐕
        PieceType.Cat => "\U0001F408",      // 🐈
        PieceType.Rat => "\U0001F400",      // 🐀
        _ => "?"
    };

    private static string GetAnimalName(PieceType type) => type switch
    {
        PieceType.Elephant => "Elephant",
        PieceType.Lion => "Lion",
        PieceType.Tiger => "Tiger",
        PieceType.Leopard => "Leopard",
        PieceType.Wolf => "Wolf",
        PieceType.Dog => "Dog",
        PieceType.Cat => "Cat",
        PieceType.Rat => "Rat",
        _ => "?"
    };
}
