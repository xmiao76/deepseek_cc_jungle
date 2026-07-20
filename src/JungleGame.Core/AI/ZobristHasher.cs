namespace JungleGame.Core.AI;

using JungleGame.Core.Models;

/// <summary>
/// Generates Zobrist hash keys for board positions to enable transposition table lookups.
/// Uses XOR of pre-generated random 64-bit numbers for each (piece type, player, position) combination,
/// plus a side-to-move key. Deterministic: always produces the same hash for the same position.
/// </summary>
public class ZobristHasher
{
    // [pieceType * 2 + player, col-1, row-1]
    private readonly ulong[,,] _pieceKeys;
    private readonly ulong _sideKey;

    private const int PieceTypeCount = 8; // Rat..Elephant
    private const int PlayerCount = 2;    // Blue, Red
    private const int BoardCols = 7;
    private const int BoardRows = 9;

    public ZobristHasher()
    {
        _pieceKeys = new ulong[PieceTypeCount * PlayerCount, BoardCols, BoardRows];
        var rng = new Random(42); // Fixed seed for deterministic hashes

        for (int t = 0; t < PieceTypeCount * PlayerCount; t++)
        {
            for (int c = 0; c < BoardCols; c++)
            {
                for (int r = 0; r < BoardRows; r++)
                {
                    _pieceKeys[t, c, r] = NextULong(rng);
                }
            }
        }

        _sideKey = NextULong(rng);
    }

    /// <summary>
    /// Computes the full Zobrist hash for a game state from scratch.
    /// </summary>
    public ulong ComputeHash(IReadOnlyDictionary<BoardPosition, Piece> pieces, Player currentPlayer)
    {
        ulong hash = 0;

        foreach (var kvp in pieces)
        {
            var piece = kvp.Value;
            int typeIdx = ((int)piece.Type - 1) * 2 + (piece.Owner == Player.Blue ? 0 : 1);
            hash ^= _pieceKeys[typeIdx, piece.Position.Col - 1, piece.Position.Row - 1];
        }

        if (currentPlayer == Player.Red)
            hash ^= _sideKey;

        return hash;
    }

    /// <summary>
    /// Updates an existing hash to reflect a move (add/remove pieces at positions).
    /// </summary>
    public ulong UpdateHash(ulong currentHash, Move move)
    {
        ulong hash = currentHash;

        // Remove piece from old position
        int typeIdx = ((int)move.Piece.Type - 1) * 2 + (move.Piece.Owner == Player.Blue ? 0 : 1);
        hash ^= _pieceKeys[typeIdx, move.From.Col - 1, move.From.Row - 1];

        // Add piece at new position
        hash ^= _pieceKeys[typeIdx, move.To.Col - 1, move.To.Row - 1];

        // Remove captured piece
        if (move.CapturedPiece != null)
        {
            int capTypeIdx = ((int)move.CapturedPiece.Type - 1) * 2 + (move.CapturedPiece.Owner == Player.Blue ? 0 : 1);
            hash ^= _pieceKeys[capTypeIdx, move.CapturedPiece.Position.Col - 1, move.CapturedPiece.Position.Row - 1];
        }

        // Toggle side to move
        hash ^= _sideKey;

        return hash;
    }

    private static ulong NextULong(Random rng)
    {
        var buffer = new byte[8];
        rng.NextBytes(buffer);
        return BitConverter.ToUInt64(buffer, 0);
    }
}
