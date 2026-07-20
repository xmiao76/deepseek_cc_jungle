using System.ComponentModel;
using JungleGame.Core.Models;

namespace JungleGame.Desktop.ViewModels;

/// <summary>
/// Manages the visual state of the 63 board squares including pieces,
/// selection highlights, and legal move indicators.
/// </summary>
public class BoardViewModel : INotifyPropertyChanged
{
    public List<PieceViewModel> Pieces { get; private set; } = new();
    public List<BoardPosition> HighlightedSquares { get; private set; } = new();
    public BoardPosition? SelectedSquare { get; set; }
    public bool IsFlipped { get; set; }

    // Stores which square, if any, has the last-move highlight
    public BoardPosition? LastMoveFrom { get; set; }
    public BoardPosition? LastMoveTo { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler<PieceViewModel>? PieceSelected;

    public BoardViewModel()
    {
        Pieces = new List<PieceViewModel>();
        HighlightedSquares = new List<BoardPosition>();
    }

    public void UpdateFromGameState(GameState state)
    {
        Pieces.Clear();

        foreach (var kvp in state.Pieces)
        {
            var piece = kvp.Value;
            if (!piece.IsAlive) continue;

            Pieces.Add(new PieceViewModel(
                piece.Type,
                piece.Owner,
                piece.Position.Col,
                piece.Position.Row));
        }

        // Update last move highlights
        LastMoveFrom = state.LastMove?.From;
        LastMoveTo = state.LastMove?.To;

        // Update each piece's last-move state
        foreach (var pvm in Pieces)
        {
            var pos = new BoardPosition(pvm.DisplayCol, pvm.DisplayRow);
            pvm.IsLastMovedFrom = (LastMoveFrom.HasValue && LastMoveFrom.Value == pos);
            pvm.IsLastMovedTo = (LastMoveTo.HasValue && LastMoveTo.Value == pos);
        }

        ClearSelection();
        RefreshAll();
    }

    public void SetLegalMoves(List<BoardPosition> destinations)
    {
        HighlightedSquares = destinations;
        RefreshAll();
    }

    public void ClearSelection()
    {
        SelectedSquare = null;
        HighlightedSquares.Clear();
        foreach (var pvm in Pieces) pvm.IsSelected = false;
        RefreshAll();
    }

    public PieceViewModel? GetPieceAt(BoardPosition pos)
    {
        return Pieces.FirstOrDefault(p => p.DisplayCol == pos.Col && p.DisplayRow == pos.Row);
    }

    public bool IsHighlighted(BoardPosition pos)
    {
        return HighlightedSquares.Any(h => h.Col == pos.Col && h.Row == pos.Row);
    }

    public void RefreshAll()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
    }
}
