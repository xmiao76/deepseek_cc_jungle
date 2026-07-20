namespace JungleGame.Core.Models;

/// <summary>
/// Represents a single move action including the piece moved, origin, destination,
/// and optional capture information.
/// </summary>
public class Move
{
    public Piece Piece { get; }
    public BoardPosition From { get; }
    public BoardPosition To { get; }
    public Piece? CapturedPiece { get; set; }
    public bool IsRiverJump { get; set; }
    public bool IsDenEntry { get; set; }

    public Move(Piece piece, BoardPosition from, BoardPosition to)
    {
        Piece = piece;
        From = from;
        To = to;
    }

    public override string ToString()
    {
        var capture = CapturedPiece != null ? $" captures {CapturedPiece.Type}" : "";
        var jump = IsRiverJump ? " [jump]" : "";
        var den = IsDenEntry ? " [DEN!]" : "";
        return $"{Piece.Owner} {Piece.Type}: {From} -> {To}{capture}{jump}{den}";
    }
}
