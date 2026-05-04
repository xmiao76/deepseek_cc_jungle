using System.Collections.Immutable;
using JungleGame.Core.Model;

namespace JungleGame.Core.Engine;

public static class GameController
{
    /// <summary>
    /// Applies a move to the given game state and returns the new state.
    /// Does NOT validate the move — caller must validate first.
    /// </summary>
    public static GameState ApplyMove(GameState state, Move move)
    {
        var newPieces = state.Pieces.Remove(move.From);

        // Add the moving piece at its new position
        var movingPiece = state.GetPieceAt(move.From)!.Value;
        var movedPiece = movingPiece.WithPosition(move.To);
        newPieces = newPieces.SetItem(move.To, movedPiece);

        var capturedBlue = state.CapturedBlue;
        var capturedRed = state.CapturedRed;

        // Handle capture
        if (move.IsCapture)
        {
            if (move.Captured!.Value.Owner == Player.Blue)
                capturedBlue = capturedBlue.Add(move.Captured.Value);
            else
                capturedRed = capturedRed.Add(move.Captured.Value);
        }

        var nextPlayer = state.CurrentTurn.Opponent();

        // Check win conditions
        var status = CheckWinCondition(
            state.Board,
            newPieces,
            capturedBlue,
            capturedRed,
            movedPiece,
            nextPlayer);

        return new GameState(
            state.Board,
            newPieces,
            nextPlayer,
            status,
            capturedBlue,
            capturedRed);
    }

    private static GameStatus CheckWinCondition(
        Board board,
        ImmutableDictionary<Position, Piece> pieces,
        ImmutableList<Piece> capturedBlue,
        ImmutableList<Piece> capturedRed,
        Piece movingPiece,
        Player nextPlayer)
    {
        // Den invasion: the moving piece entered the opponent's den
        if (board.IsOpponentDen(movingPiece.Position, movingPiece.Owner))
            return movingPiece.Owner == Player.Blue ? GameStatus.BlueWins : GameStatus.RedWins;

        // Total elimination: next player has no pieces left
        if (capturedBlue.Count == 8)
            return GameStatus.RedWins;
        if (capturedRed.Count == 8)
            return GameStatus.BlueWins;

        // Check elimination via piece count
        int blueCount = 0;
        int redCount = 0;
        foreach (var piece in pieces.Values)
        {
            if (piece.Owner == Player.Blue) blueCount++;
            else redCount++;
        }

        if (blueCount == 0) return GameStatus.RedWins;
        if (redCount == 0) return GameStatus.BlueWins;

        return GameStatus.InProgress;
    }
}
