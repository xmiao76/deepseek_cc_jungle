using System.ComponentModel;
using JungleGame.Core.Models;

namespace JungleGame.Desktop.ViewModels;

/// <summary>
/// View model wrapping a single piece for UI display.
/// </summary>
public class PieceViewModel : INotifyPropertyChanged
{
    public PieceType Type { get; }
    public Player Owner { get; }
    public int DisplayCol { get; set; }
    public int DisplayRow { get; set; }
    public bool IsSelected { get; set; }
    public bool IsHighlighted { get; set; }
    public bool IsCaptured { get; set; }
    public bool IsLastMovedFrom { get; set; }
    public bool IsLastMovedTo { get; set; }

    public string AnimalName => Type.ToString();
    public string PlayerName => Owner.ToString();

    public PieceViewModel(PieceType type, Player owner, int col, int row)
    {
        Type = type;
        Owner = owner;
        DisplayCol = col;
        DisplayRow = row;
    }

    // Computed properties for rendering
    public int Rank => (int)Type;
    public bool IsBlue => Owner == Player.Blue;

    public event PropertyChangedEventHandler? PropertyChanged;

    public void Refresh()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
    }
}
