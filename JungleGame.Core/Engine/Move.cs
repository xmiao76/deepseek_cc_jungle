using JungleGame.Core.Model;

namespace JungleGame.Core.Engine;

public readonly struct Move
{
    public Position From { get; }
    public Position To { get; }
    public Piece? Captured { get; }

    public Move(Position from, Position to, Piece? captured = null)
    {
        From = from;
        To = to;
        Captured = captured;
    }

    public bool IsCapture => Captured.HasValue;

    public override string ToString() => IsCapture
        ? $"{From}→{To} x{Captured!.Value.Animal}"
        : $"{From}→{To}";
}
