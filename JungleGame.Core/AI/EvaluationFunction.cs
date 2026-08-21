using JungleGame.Core.Engine;
using JungleGame.Core.Model;

namespace JungleGame.Core.AI;

/// <summary>
/// Evaluation entry points. The static evaluation is the dot product of
/// <see cref="EvalFeatureExtractor"/>'s side-relative features with
/// <see cref="EvalParameters"/>; mobility is added separately because it costs
/// two move generations per call.
/// </summary>
public static class EvaluationFunction
{
    /// <summary>
    /// Fast-path evaluation on the internal search board. Mobility counts are supplied
    /// by the caller (the side-to-move count comes from move generation it already
    /// performed; the opponent count from one cheap SearchBoard.CountLegalMoves call).
    /// With legacyEval, the P3 terms (doomed-piece bonus, den escort) are disabled —
    /// used only by the self-play harness for A/B strength validation.
    /// </summary>
    private static EvalParameters _current = EvalParameters.Default;

    /// <summary>
    /// The weight vector in use. The tuning harness adopts a fitted vector via
    /// <see cref="SetParameters"/>; the default is the historical hand-tuned
    /// constants.
    /// </summary>
    internal static void SetParameters(EvalParameters parameters) => _current = parameters;

    /// <summary>The process-wide weight vector (used when an engine has no explicit weights).</summary>
    internal static EvalParameters Current => _current;

    internal static int Evaluate(SearchBoard board, int side, int myMobility, int oppMobility, bool legacyEval = false)
        => EvaluateStatic(board, side, legacyEval) + (myMobility - oppMobility) * _current.MobilityWeight;

    internal static int Evaluate(SearchBoard board, int side, int myMobility, int oppMobility, EvalParameters weights, bool legacyEval = false)
        => EvaluateStatic(board, side, weights, legacyEval) + (myMobility - oppMobility) * weights.MobilityWeight;

    /// <summary>
    /// Everything <see cref="Evaluate"/> computes except the mobility term, which
    /// costs two move generations per call. Used for futility pruning at depth 1
    /// and for lazy mobility in the quiescence stand-pat.
    /// </summary>
    internal static int EvaluateStatic(SearchBoard board, int side, bool legacyEval = false)
        => _current.Dot(EvalFeatureExtractor.ExtractStatic(board, side, legacyEval));

    internal static int EvaluateStatic(SearchBoard board, int side, EvalParameters weights, bool legacyEval = false)
        => weights.Dot(EvalFeatureExtractor.ExtractStatic(board, side, legacyEval));

    /// <summary>
    /// Public evaluation of a game state (delegates to the fast internal path).
    /// </summary>
    public static int Evaluate(GameState state, Player player)
    {
        // Terminal scores match MinimaxEngine's mate convention; the search itself
        // handles terminal nodes before reaching this method.
        if (state.Status == GameStatus.BlueWins)
            return player == Player.Blue ? MinimaxEngine.MateScore : -MinimaxEngine.MateScore;
        if (state.Status == GameStatus.RedWins)
            return player == Player.Red ? MinimaxEngine.MateScore : -MinimaxEngine.MateScore;
        if (state.Status == GameStatus.Draw)
            return 0;

        var board = SearchBoard.FromGameState(state);
        int side = (int)player;
        return Evaluate(board, side, board.CountLegalMoves(side), board.CountLegalMoves(side ^ 1));
    }
}
