namespace JungleGame.Core.Models;

/// <summary>
/// Represents the 8 animal types, where the integer value equals the piece's rank (1-8).
/// Rank 8 (Elephant) is strongest in standard capture; rank 1 (Rat) is weakest
/// but has special capture ability against Elephant.
/// </summary>
public enum PieceType
{
    Rat = 1,
    Cat = 2,
    Dog = 3,
    Wolf = 4,
    Leopard = 5,
    Tiger = 6,
    Lion = 7,
    Elephant = 8
}
