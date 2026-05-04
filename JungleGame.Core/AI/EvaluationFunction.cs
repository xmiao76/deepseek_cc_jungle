using JungleGame.Core.Engine;
using JungleGame.Core.Model;
using JungleGame.Core.Rules;

namespace JungleGame.Core.AI;

public static class EvaluationFunction
{
    // Material values in centipawns
    private static readonly int[] MaterialValue = { 0, 100, 200, 300, 400, 500, 600, 700, 800 };
    // Indexed by (int)Animal: Rat=1..Elephant=8

    // Positional bonuses: bonus for forward progression toward opponent's den
    // Blue advances toward high rows, Red toward low rows
    private static int ForwardBonus(Animal animal, int row, Player owner)
    {
        int progress = owner == Player.Blue ? row : (8 - row);

        // Aggressive pieces (Lion, Tiger, Leopard) get more forward bonus
        int aggression = animal switch
        {
            Animal.Lion => 6,
            Animal.Tiger => 4,
            Animal.Leopard => 4,
            Animal.Wolf => 3,
            Animal.Dog => 3,
            Animal.Cat => 2,
            Animal.Elephant => 1, // Elephant prefers to stay safe
            Animal.Rat => 2,
            _ => 2
        };

        return progress * aggression;
    }

    // Den proximity: bonus for being near opponent's den, penalty for enemy near our den
    private static int DenProximity(Position pos, Player owner)
    {
        Position ownDen = owner == Player.Blue ? new Position(3, 0) : new Position(3, 8);
        Position oppDen = owner == Player.Blue ? new Position(3, 8) : new Position(3, 0);

        int distToOwnDen = ManhattanDistance(pos, ownDen);
        int distToOppDen = ManhattanDistance(pos, oppDen);

        int score = 0;

        // Bonus for being near opponent's den (offensive)
        if (distToOppDen <= 3)
            score += (4 - distToOppDen) * 12;

        // Bonus for guarding own den (defensive)
        if (distToOwnDen <= 2)
            score += (3 - distToOwnDen) * 8;

        return score;
    }

    public static int Evaluate(GameState state, Player player)
    {
        if (state.Status == GameStatus.BlueWins)
            return player == Player.Blue ? 1000000 : -1000000;
        if (state.Status == GameStatus.RedWins)
            return player == Player.Red ? 1000000 : -1000000;

        int score = 0;
        var opponent = player.Opponent();

        int totalPieces = state.Pieces.Count;
        bool isEndgame = totalPieces <= 8;

        foreach (var piece in state.Pieces.Values)
        {
            int pieceScore = EvaluatePiece(state, piece, isEndgame);
            if (piece.Owner == player)
                score += pieceScore;
            else
                score -= pieceScore;
        }

        // Mobility: small bonus per legal move
        int myMoves = MoveGenerator.CountLegalMoves(state, player);
        int oppMoves = MoveGenerator.CountLegalMoves(state, opponent);
        score += (myMoves - oppMoves) * 3;

        // Den threat detection: opponent piece near our den without defender nearby
        Position ownDen = player == Player.Blue ? new Position(3, 0) : new Position(3, 8);

        int defendersNearDen = 0;
        int attackersNearDen = 0;

        foreach (var piece in state.Pieces.Values)
        {
            int distToOurDen = ManhattanDistance(piece.Position, ownDen);
            if (distToOurDen <= 2)
            {
                if (piece.Owner == player)
                    defendersNearDen++;
                else
                    attackersNearDen++;
            }
        }

        if (attackersNearDen > defendersNearDen)
            score -= 40 * (attackersNearDen - defendersNearDen);
        if (isEndgame && attackersNearDen > 0 && defendersNearDen == 0)
            score -= 200; // Severe den-threat penalty in endgame

        return score;
    }

    private static int EvaluatePiece(GameState state, Piece piece, bool isEndgame)
    {
        int score = MaterialValue[piece.Rank];

        // Forward progression bonus
        score += ForwardBonus(piece.Animal, piece.Position.Row, piece.Owner);

        // Den proximity
        score += DenProximity(piece.Position, piece.Owner);

        // Trap penalty: piece on enemy trap
        if (state.Board.IsTrap(piece.Position, piece.Owner))
            score -= 80;

        // Trap threat: piece threatening opponent piece on our trap (good for attacker)
        if (state.Board.IsTrap(piece.Position, piece.Owner.Opponent()))
        {
            // Check if an enemy piece is on this trap
            var enemy = state.GetPieceAt(piece.Position);
            if (enemy != null && enemy.Value.Owner != piece.Owner)
                score += 60; // Bonus for threatening trapped enemy piece
        }

        // Lion/Tiger river bank bonus
        if (piece.Animal == Animal.Lion || piece.Animal == Animal.Tiger)
        {
            if (MoveValidator.IsRiverBank(state.Board, piece.Position))
                score += 15;

            // Check for clear jump path
            var oppDen = piece.Owner == Player.Blue ? new Position(3, 8) : new Position(3, 0);
            if (HasRiverBetween(piece.Position, oppDen, state.Board))
                score += 10; // Can potentially jump toward opponent's den
        }

        // Rat near water bonus
        if (piece.Animal == Animal.Rat)
        {
            if (IsAdjacentToWater(piece.Position, state.Board))
                score += 8;
            if (state.Board.IsWater(piece.Position))
                score += 12; // Rat in water disrupts enemy jump paths
        }

        // Elephant safety: penalty for being near opponent's Rat
        if (piece.Animal == Animal.Elephant)
        {
            foreach (var enemy in state.GetPlayerPieces(piece.Owner.Opponent()))
            {
                if (enemy.Animal == Animal.Rat && !state.Board.IsWater(enemy.Position))
                {
                    int dist = ManhattanDistance(piece.Position, enemy.Position);
                    if (dist <= 3)
                        score -= (4 - dist) * 15;
                }
            }
        }

        // Threat analysis: being threatened by equal or stronger enemy
        foreach (var enemy in state.GetPlayerPieces(piece.Owner.Opponent()))
        {
            if (!IsAdjacent(piece.Position, enemy.Position))
                continue;

            int ourEffRank = CaptureResolver.GetEffectiveRank(piece, state.Board);
            int enemyEffRank = CaptureResolver.GetEffectiveRank(enemy, state.Board);

            // Rat threatens Elephant
            if (piece.Animal == Animal.Elephant && enemy.Animal == Animal.Rat)
            {
                if (!state.Board.IsWater(enemy.Position))
                    score -= 25;
            }
            else if (enemyEffRank >= ourEffRank)
            {
                if (CaptureResolver.CanCapture(enemy, piece, state.Board))
                {
                    if (enemyEffRank > ourEffRank)
                        score -= piece.Rank * 15; // Losing higher piece is worse
                    else
                        score -= piece.Rank * 8;  // Equal trade
                }
            }
        }

        // Endgame: bonus for advancing toward den more aggressively
        if (isEndgame && piece.Rank >= 5) // Leopard+
        {
            var oppDen = piece.Owner == Player.Blue ? new Position(3, 8) : new Position(3, 0);
            int distToOppDen = ManhattanDistance(piece.Position, oppDen);
            if (distToOppDen <= 2)
                score += (3 - distToOppDen) * 25;
        }

        // Penalty for being on the back rank too long (encourages development)
        int homeRow = piece.Owner == Player.Blue ? 0 : 8;
        if (piece.Position.Row == homeRow && TotalMaterialOnBoard(state) > 20)
            score -= 5; // Small penalty to encourage moving pieces out early

        return score;
    }

    private static int TotalMaterialOnBoard(GameState state)
    {
        int total = 0;
        foreach (var p in state.Pieces.Values)
            total += p.Rank;
        return total;
    }

    private static int ManhattanDistance(Position a, Position b) =>
        Math.Abs(a.Col - b.Col) + Math.Abs(a.Row - b.Row);

    private static bool IsAdjacent(Position a, Position b) =>
        ManhattanDistance(a, b) == 1;

    private static bool IsAdjacentToWater(Position pos, Board board)
    {
        foreach (var (dc, dr) in new[] { (0, 1), (0, -1), (1, 0), (-1, 0) })
        {
            var adj = new Position(pos.Col + dc, pos.Row + dr);
            if (adj.IsValid && board.IsWater(adj))
                return true;
        }
        return false;
    }

    private static bool HasRiverBetween(Position from, Position to, Board board)
    {
        int dCol = to.Col - from.Col;
        int dRow = to.Row - from.Row;

        int stepCol = Math.Sign(dCol);
        int stepRow = Math.Sign(dRow);

        int col = from.Col + stepCol;
        int row = from.Row + stepRow;

        bool foundWater = false;
        while (col != to.Col || row != to.Row)
        {
            var pos = new Position(col, row);
            if (pos.IsValid && board.IsWater(pos))
                foundWater = true;

            col += stepCol;
            row += stepRow;

            if (Math.Abs(col - from.Col) > Math.Abs(dCol) && Math.Abs(row - from.Row) > Math.Abs(dRow))
                break;
        }

        return foundWater;
    }
}
