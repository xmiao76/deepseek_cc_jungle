using JungleGame.Core.Logic;
using JungleGame.Core.Models;

namespace JungleGame.Core.AI;

/// <summary>
/// Multi-factor board evaluation for Jungle.
/// Weights: Material=50%, Position=20%, Mobility=12%, Threats=10%, Safety=8%.
/// Red's den at row 1 (top). Blue's den at row 9 (bottom).
/// </summary>
public class EvaluationFunction
{
    private static readonly Dictionary<PieceType, int> PieceValues = new()
    {
        { PieceType.Elephant, 1000 }, { PieceType.Lion, 600 }, { PieceType.Tiger, 500 },
        { PieceType.Leopard, 350 }, { PieceType.Wolf, 250 }, { PieceType.Dog, 150 },
        { PieceType.Cat, 100 }, { PieceType.Rat, 50 }
    };

    private const double W_MATERIAL = 0.50;
    private const double W_POSITION = 0.20;
    private const double W_MOBILITY = 0.12;
    private const double W_THREATS = 0.10;
    private const double W_SAFETY = 0.08;

    public int Evaluate(GameState state, Player perspective)
    {
        if (state.Phase == GamePhase.GameOver)
        {
            if (state.Winner == perspective) return 1_000_000;
            if (state.Winner != null) return -1_000_000;
        }

        var opp = Board.Opponent(perspective);
        int mat = EvalMaterial(state, perspective);
        int pos = EvalPosition(state, perspective);
        int mob = EvalMobility(state, perspective, opp);
        int thr = EvalThreats(state, perspective, opp);
        int saf = EvalSafety(state, perspective);

        return (int)(W_MATERIAL * mat + W_POSITION * pos + W_MOBILITY * mob
                    + W_THREATS * thr + W_SAFETY * saf);
    }

    // ==================== Material ====================

    private int EvalMaterial(GameState state, Player player)
    {
        int score = 0;
        bool enemyElephantAlive = false;
        bool ownRatAlive = false;

        foreach (var kvp in state.Pieces)
        {
            var p = kvp.Value;
            if (p.Owner == player)
            {
                score += PieceValues[p.Type];
                if (p.Type == PieceType.Rat) ownRatAlive = true;
            }
            else
            {
                score -= PieceValues[p.Type];
                if (p.Type == PieceType.Elephant) enemyElephantAlive = true;
            }
        }

        // Rat is more valuable when enemy Elephant is alive (it's the only counter)
        if (ownRatAlive && enemyElephantAlive) score += 60;

        return score;
    }

    // ==================== Position (piece-square) ====================

    private int EvalPosition(GameState state, Player player)
    {
        int score = 0;
        foreach (var kvp in state.Pieces)
        {
            var p = kvp.Value;
            int sign = p.Owner == player ? 1 : -1;
            score += sign * PieceSquareBonus(p);
        }
        return score;
    }

    private int PieceSquareBonus(Piece piece)
    {
        int bonus = 0;
        int c = piece.Position.Col, r = piece.Position.Row;
        var pos = piece.Position;
        bool isBlue = piece.Owner == Player.Blue;

        // --- Forward progress: toward opponent's den ---
        // Blue attacks Red's den at row 1 → wants smaller row (up)
        // Red attacks Blue's den at row 9 → wants larger row (down)
        int advance = isBlue ? (10 - r) : r;  // 1 at back rank, 9 at opponent den
        bonus += advance * 4;

        // Central columns are more flexible
        bonus += (4 - Math.Abs(c - 4)) * 6;

        // --- Piece-specific bonuses ---
        switch (piece.Type)
        {
            case PieceType.Rat:
                if (Board.IsWater(pos)) bonus += 40;       // Safe in water
                if (IsNearOpponentDen(piece)) bonus += 25; // Rat can sneak into den
                break;

            case PieceType.Lion:
                if (IsNearRiver(pos)) bonus += 35;           // Can jump river
                if (IsJumpEligible(pos)) bonus += 25;        // On jump square
                if (IsNearOpponentDen(piece)) bonus += 50;   // Threaten win
                break;

            case PieceType.Tiger:
                if (IsNearRiver(pos)) bonus += 30;
                if (IsNearOpponentDen(piece)) bonus += 40;
                break;

            case PieceType.Elephant:
                if (c == 1 || c == 7) bonus -= 15;            // Avoid edges — easy to trap
                if (IsAdjacentToWater(pos)) bonus -= 15;      // Vulnerable to Rat in water
                if (IsNearOpponentDen(piece)) bonus += 30;
                break;

            case PieceType.Leopard:
            case PieceType.Wolf:
                if (IsNearOpponentDen(piece)) bonus += 30;
                break;
        }

        // --- Trap awareness ---
        if (Board.IsOpponentTrap(pos, piece.Owner))
            bonus -= 250;  // On enemy trap = can be captured by anything!
        if (Board.IsOwnTrap(pos, piece.Owner))
            bonus += 10;   // Own trap is safe defensive position

        // Adjacent to opponent trap (threat) — good for attacker, bad for defender
        foreach (var nb in pos.Neighbors())
        {
            if (Board.IsOpponentTrap(nb, piece.Owner))
                bonus += 15; // Can step onto opponent trap
        }

        return bonus;
    }

    // ==================== Mobility ====================

    private int EvalMobility(GameState state, Player player, Player opp)
    {
        // Count moves for perspective player
        var engine = new GameEngine();
        int myMoves = engine.GetLegalMoves(state).Count;

        // For opponent, estimate from piece count * avg moves/piece
        int oppPieces = 0;
        foreach (var kvp in state.Pieces)
            if (kvp.Value.Owner == opp) oppPieces++;

        int oppMovesEst = oppPieces * 3;
        return (myMoves - oppMovesEst) * 10;
    }

    // ==================== Threats ====================

    private int EvalThreats(GameState state, Player player, Player opp)
    {
        int score = 0;
        int myDenRow = player == Player.Blue ? 9 : 1;
        int oppDenRow = opp == Player.Blue ? 9 : 1;

        foreach (var kvp in state.Pieces)
        {
            var p = kvp.Value;
            int dist = Math.Abs(p.Position.Row - oppDenRow);
            int colDist = Math.Abs(p.Position.Col - 4);

            if (p.Owner == player)
            {
                // Our piece near opponent's den
                if (dist <= 2 && colDist <= 2)
                    score += (3 - dist) * 40;   // Immediate den threat!
                score += (9 - dist) * 4;         // General proximity
            }
            else
            {
                // Opponent piece near our den
                if (dist <= 2 && colDist <= 2)
                    score -= (3 - dist) * 50;   // Immediate threat to US!
                score -= (9 - dist) * 3;
            }
        }
        return score;
    }

    // ==================== Safety ====================

    private int EvalSafety(GameState state, Player player)
    {
        int score = 0;

        foreach (var kvp in state.Pieces)
        {
            var p = kvp.Value;
            int sign = p.Owner == player ? 1 : -1;

            // High-value pieces need protection
            if (p.Type >= PieceType.Tiger)
            {
                bool defended = false;
                foreach (var nb in p.Position.Neighbors())
                {
                    if (state.Pieces.TryGetValue(nb, out var neighbor) &&
                        neighbor.Owner == p.Owner && neighbor.Type < p.Type)
                        defended = true;
                }
                if (defended) score += sign * 20;
                else score -= sign * 10; // Undefended high-value piece
            }

            // Rat in water is well-defended
            if (p.Type == PieceType.Rat && Board.IsWater(p.Position))
                score += sign * 25;
        }
        return score;
    }

    // ==================== Helpers ====================

    private static bool IsNearRiver(BoardPosition pos)
    {
        return Board.IsWater(new BoardPosition(Math.Min(7, pos.Col + 1), pos.Row))
            || Board.IsWater(new BoardPosition(Math.Max(1, pos.Col - 1), pos.Row))
            || Board.IsWater(new BoardPosition(pos.Col, Math.Min(9, pos.Row + 1)))
            || Board.IsWater(new BoardPosition(pos.Col, Math.Max(1, pos.Row - 1)));
    }

    private static bool IsJumpEligible(BoardPosition pos)
    {
        return (pos.Col == 2 || pos.Col == 3 || pos.Col == 5 || pos.Col == 6)
               && (pos.Row == 3 || pos.Row == 7);
    }

    private static bool IsAdjacentToWater(BoardPosition pos)
    {
        return IsNearRiver(pos);
    }

    private bool IsNearOpponentDen(Piece piece)
    {
        // Opponent's den: Blue attacks Red den at row 1, Red attacks Blue den at row 9
        int oppDenRow = piece.Owner == Player.Blue ? 1 : 9;
        return Math.Abs(piece.Position.Row - oppDenRow) <= 2
            && Math.Abs(piece.Position.Col - 4) <= 2;
    }
}
