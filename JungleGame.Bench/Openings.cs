using JungleGame.Core.Engine;
using JungleGame.Core.Model;

namespace JungleGame.Bench;

/// <summary>
/// Opening-position sources for the arena: a plies file (one opening per line,
/// each ply "c,r-c,r"), or seeded imbalanced openings (start position minus one
/// random piece per side). Openings are returned as post-opening GameStates;
/// the arena plays each one twice with colors swapped.
/// </summary>
internal static class Openings
{
    internal static List<GameState> Load(string? filePath, int imbalancedCount, int seed)
    {
        if (filePath != null)
            return LoadFile(filePath);
        if (imbalancedCount > 0)
            return LoadImbalanced(imbalancedCount, seed);
        return new List<GameState> { GameState.CreateInitial() };
    }

    internal static List<GameState> LoadFile(string path)
    {
        var openings = new List<GameState>();
        int lineNumber = 0;
        foreach (string line in File.ReadLines(path))
        {
            lineNumber++;
            string trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#'))
                continue;

            var state = ParsePlies(trimmed);
            if (state.Status == GameStatus.InProgress)
                openings.Add(state);
            else
                Console.WriteLine($"Opening skipped (game already over): line {lineNumber}");
        }

        if (openings.Count == 0)
            throw new InvalidOperationException($"No playable openings in {path}.");
        return openings;
    }

    /// <summary>Applies a space-separated list of "c,r-c,r" plies from the start position.</summary>
    internal static GameState ParsePlies(string plies)
    {
        var state = GameState.CreateInitial();
        foreach (string ply in plies.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            string[] parts = ply.Split('-');
            if (parts.Length != 2 || !TryParseSquare(parts[0], out var from) || !TryParseSquare(parts[1], out var to))
                throw new FormatException($"Bad opening ply '{ply}' (expected c,r-c,r).");
            if (state.Status != GameStatus.InProgress)
                throw new FormatException($"Opening continues past game end (ply '{ply}').");

            bool legal = MoveGenerator.GenerateLegalMoves(state, state.CurrentTurn)
                .Any(m => m.From == from && m.To == to);
            if (!legal)
                throw new FormatException($"Illegal opening ply '{ply}' at move {state.History.Count}.");

            state = GameController.ApplyMove(state, new Move(from, to, state.GetPieceAt(to)));
        }
        return state;
    }

    /// <summary>
    /// Seeded imbalanced openings: the standard start position minus one random
    /// piece per side. Decisive-rich positions raise arena gate power at the
    /// ~85% draw rate. At most 64 distinct openings exist.
    /// </summary>
    internal static List<GameState> LoadImbalanced(int count, int seed)
    {
        const int maxDistinct = 64;
        if (count > maxDistinct)
            throw new InvalidOperationException($"At most {maxDistinct} imbalanced openings exist (requested {count}).");

        var rng = new Random(seed);
        var initial = GameState.CreateInitial();
        var blue = initial.Pieces.Values.Where(p => p.Owner == Player.Blue).ToList();
        var red = initial.Pieces.Values.Where(p => p.Owner == Player.Red).ToList();
        var seen = new HashSet<string>();
        var openings = new List<GameState>();

        while (openings.Count < count)
        {
            var b = blue[rng.Next(blue.Count)];
            var r = red[rng.Next(red.Count)];
            if (!seen.Add($"{b.Animal}-{r.Animal}"))
                continue;

            var state = GameState.CreateFromPieces(
                initial.Pieces.Values.Where(p => p.Position != b.Position && p.Position != r.Position),
                Player.Blue);

            // Only live openings (both sides can move) enter the arena.
            if (MoveGenerator.GenerateLegalMoves(state, state.CurrentTurn).Count == 0)
            {
                seen.Remove($"{b.Animal}-{r.Animal}");
                continue;
            }

            openings.Add(state);
        }

        return openings;
    }

    private static bool TryParseSquare(string token, out Position pos)
    {
        pos = default;
        string[] parts = token.Split(',');
        if (parts.Length != 2 ||
            !int.TryParse(parts[0], out int col) ||
            !int.TryParse(parts[1], out int row))
            return false;
        pos = new Position(col, row);
        return pos.IsValid;
    }
}
