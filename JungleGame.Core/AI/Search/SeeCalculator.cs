namespace JungleGame.Core.AI;

/// <summary>
/// Static exchange evaluation for capture moves: the material the mover can
/// expect from the exchange on the target square, in centipawns (rank × 100).
/// The mover is committed to the capture; each recapture is optional and uses
/// the side's least-valuable attacker, so the exchange is a single chain of at
/// most 16 captures (every capture removes a piece). Captured pieces are worth
/// their raw rank — traps change only capture legality (effective ranks in
/// SearchBoard.CanCapture), never the material removed.
/// </summary>
internal static class SeeCalculator
{
    private const int MaxExchangeDepth = 16;

    /// <summary>SEE of the given capture move, from the mover's perspective. ≥ 0 = good capture.</summary>
    internal static int See(SearchBoard board, in SearchMove move)
    {
        int victimValue = SearchBoard.RankOf[move.CapturedId] * 100;
        int recapture = Recapture(board, move.To, board.Occupant(move.From), 1);
        return victimValue - recapture;
    }

    /// <summary>
    /// Best material the opponent can win back by capturing the piece standing
    /// on `to` (the piece that just captured), with both sides continuing
    /// least-valuable-first and able to decline (max(0, ...)).
    /// </summary>
    private static int Recapture(SearchBoard board, int to, byte pieceOnTo, int depth)
    {
        if (depth >= MaxExchangeDepth)
            return 0;

        byte attacker = LeastValuableAttacker(board, to, OwnerOf(pieceOnTo) ^ 1, pieceOnTo);
        if (attacker == 0)
            return 0;

        int capturedValue = SearchBoard.RankOf[pieceOnTo] * 100;
        return Math.Max(0, capturedValue - Recapture(board, to, attacker, depth + 1));
    }

    /// <summary>Least valuable attacker of the piece on `to` for the given side (0 = none).</summary>
    private static byte LeastValuableAttacker(SearchBoard board, int to, int side, byte pieceOnTo)
    {
        byte best = 0;
        int bestValue = int.MaxValue;

        foreach (byte from in SearchBoard.Neighbors[to])
        {
            byte candidate = board.Occupant(from);
            if (candidate == 0 || candidate == pieceOnTo || OwnerOf(candidate) != side)
                continue;
            if (!SearchBoard.CanCapture(candidate, from, pieceOnTo, to))
                continue;
            int value = SearchBoard.RankOf[candidate];
            if (value < bestValue)
            {
                bestValue = value;
                best = candidate;
            }
        }

        foreach (var jump in SearchBoard.JumpAttackersTo[to])
        {
            byte candidate = board.Occupant(jump.From);
            if (candidate == 0 || candidate == pieceOnTo || OwnerOf(candidate) != side)
                continue;
            if (jump.IsBlockedByRat(board))
                continue;
            if (!SearchBoard.CanCapture(candidate, jump.From, pieceOnTo, to))
                continue;
            int value = SearchBoard.RankOf[candidate];
            if (value < bestValue)
            {
                bestValue = value;
                best = candidate;
            }
        }

        return best;
    }

    private static int OwnerOf(byte pieceId) =>
        ((pieceId - 1) % SearchBoard.DistinctPieceKinds) & 1;
}
