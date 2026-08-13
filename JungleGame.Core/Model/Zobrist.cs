using System.Collections.Immutable;

namespace JungleGame.Core.Model;

/// <summary>
/// Zobrist hashing for game states. Keys are generated once with a fixed seed so
/// hashes are deterministic across runs. Used by the transposition table (search)
/// and by GameState.History for three-fold repetition detection.
/// </summary>
internal static class Zobrist
{
    internal const int PositionCount = 63; // 7×9
    internal static readonly ulong[,] PieceKeys; // [positionIndex, (animal-1)*2 + owner]
    internal static readonly ulong TurnKey;

    static Zobrist()
    {
        var rng = new Random(42); // Fixed seed for reproducibility
        PieceKeys = new ulong[PositionCount, 16]; // 8 animals × 2 owners

        for (int p = 0; p < PositionCount; p++)
            for (int i = 0; i < 16; i++)
                PieceKeys[p, i] = NextULong(rng);

        TurnKey = NextULong(rng);
    }

    private static ulong NextULong(Random rng)
    {
        byte[] buf = new byte[8];
        rng.NextBytes(buf);
        return BitConverter.ToUInt64(buf, 0);
    }

    internal static int PieceIndex(Piece piece) => ((int)piece.Animal - 1) * 2 + (int)piece.Owner;
    internal static int PositionIndex(Position pos) => pos.Row * 7 + pos.Col;

    internal static ulong ComputeHash(GameState state) => ComputeHash(state.Pieces, state.CurrentTurn);

    internal static ulong ComputeHash(ImmutableDictionary<Position, Piece> pieces, Player turn)
    {
        ulong hash = 0;

        foreach (var kv in pieces)
            hash ^= PieceKeys[PositionIndex(kv.Key), PieceIndex(kv.Value)];

        if (turn == Player.Red)
            hash ^= TurnKey; // Differentiate side to move

        return hash;
    }
}
