using JungleGame.Core.Model;

namespace JungleGame.UI;

/// <summary>
/// Single source of the human-readable text for a finished game, shared by the
/// main window status label and the game-over dialog (they must never drift).
/// </summary>
public static class GameStrings
{
    public static string StatusText(GameStatus status) => status switch
    {
        GameStatus.BlueWins => "Blue wins!",
        GameStatus.RedWins => "Red wins!",
        GameStatus.Draw => "Draw",
        _ => ""
    };
}
