namespace JungleGame.Core.Model;

public enum Player
{
    Blue,
    Red
}

public static class PlayerExtensions
{
    public static Player Opponent(this Player p) => p == Player.Blue ? Player.Red : Player.Blue;
}
