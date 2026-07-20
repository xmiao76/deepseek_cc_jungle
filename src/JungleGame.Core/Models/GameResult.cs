namespace JungleGame.Core.Models;

/// <summary>
/// Contains the result of a completed game.
/// </summary>
public class GameResult
{
    public Player Winner { get; }
    public WinCondition Condition { get; }
    public int TotalMoves { get; }
    public string Description { get; }

    public GameResult(Player winner, WinCondition condition, int totalMoves)
    {
        Winner = winner;
        Condition = condition;
        TotalMoves = totalMoves;
        Description = condition switch
        {
            WinCondition.DenEntry => $"{winner} wins by entering the opponent's den in {totalMoves} moves!",
            WinCondition.AllPiecesCaptured => $"{winner} wins by capturing all opponent pieces in {totalMoves} moves!",
            _ => $"{winner} wins!"
        };
    }

    public override string ToString() => Description;
}
