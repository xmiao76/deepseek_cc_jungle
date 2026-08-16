using JungleGame.Core.Engine;
using JungleGame.Core.Model;

namespace JungleGame.Core.AI;

/// <summary>
/// Parses the tactical test-suite format shared by the Bench runner and the
/// xUnit regression suite.
///
///   # comment (blank lines ignored)
///   S D10                    — standard start position, searched at depth 10
///   P Blue 3,1:Lion 0,0:Cat  — position: side to move + "col,row:Animal" pieces
///                               (FEN-style case: capital = Blue, lowercase = Red)
///   R 42 10 D10              — play 10 seeded random legal plies from the start,
///                               then search at depth 10 (deterministic midgame)
///   E 3,1-3,2 D3             — expected best move, minimum search depth 3
///   F 3,4-3,3 D2             — forbidden move (anti-blunder), depth 2
///   N D8                     — bench-only: search at depth 8, assert nothing
///
/// E/F/N lines apply to the most recent P line and produce one entry each.
/// </summary>
public sealed record TestSuiteEntry(
    int LineNumber,
    GameState State,
    Move? ExpectedMove,
    Move? ForbiddenMove,
    int SearchDepth)
{
    public string Description
    {
        get
        {
            if (ExpectedMove != null)
                return $"line {LineNumber}: E {ExpectedMove.Value.From}→{ExpectedMove.Value.To} at depth {SearchDepth}";
            if (ForbiddenMove != null)
                return $"line {LineNumber}: F {ForbiddenMove.Value.From}→{ForbiddenMove.Value.To} at depth {SearchDepth}";
            return $"line {LineNumber}: bench search at depth {SearchDepth}";
        }
    }
}

public static class TestSuiteParser
{
    public static List<TestSuiteEntry> Parse(string text)
    {
        var entries = new List<TestSuiteEntry>();
        GameState? pending = null;

        var lines = text.Replace("\r\n", "\n").Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;

            int lineNumber = i + 1;
            var tokens = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            switch (tokens[0])
            {
                case "S":
                    entries.Add(ParseBenchOnly(lineNumber, tokens, GameState.CreateInitial()));
                    break;
                case "P":
                    pending = ParsePosition(lineNumber, tokens);
                    break;
                case "R":
                    entries.Add(ParseRandomPlay(lineNumber, tokens));
                    break;
                case "E":
                    entries.Add(ParseExpectation(lineNumber, tokens, RequirePending(pending, "E"), forbidden: false));
                    break;
                case "F":
                    entries.Add(ParseExpectation(lineNumber, tokens, RequirePending(pending, "F"), forbidden: true));
                    break;
                case "N":
                    entries.Add(ParseBenchOnly(lineNumber, tokens, RequirePending(pending, "N")));
                    break;
                default:
                    throw new FormatException($"Line {lineNumber}: unknown directive '{tokens[0]}'.");
            }
        }

        return entries;
    }

    public static List<TestSuiteEntry> ParseFile(string path) =>
        Parse(File.ReadAllText(path));

    private static GameState RequirePending(GameState? pending, string directive)
    {
        if (pending == null)
            throw new FormatException($"'{directive}' line must follow a 'P' position line.");
        return pending;
    }

    private static TestSuiteEntry ParseBenchOnly(int lineNumber, string[] tokens, GameState state)
    {
        if (tokens.Length != 2 || !TryReadDepth(tokens[1], out int depth))
            throw new FormatException($"Line {lineNumber}: expected '{tokens[0]} D<depth>'.");
        return new TestSuiteEntry(lineNumber, state, null, null, depth);
    }

    private static TestSuiteEntry ParseExpectation(
        int lineNumber, string[] tokens, GameState state, bool forbidden)
    {
        if (tokens.Length != 3 || !TryReadDepth(tokens[2], out int depth))
            throw new FormatException($"Line {lineNumber}: expected '{tokens[0]} <c,r>-<c,r> D<depth>'.");
        Move move = ParseMove(tokens[1], lineNumber);
        return forbidden
            ? new TestSuiteEntry(lineNumber, state, null, move, depth)
            : new TestSuiteEntry(lineNumber, state, move, null, depth);
    }

    /// <summary>R &lt;seed&gt; &lt;plies&gt; D&lt;depth&gt; — seeded random play from the start position.</summary>
    private static TestSuiteEntry ParseRandomPlay(int lineNumber, string[] tokens)
    {
        if (tokens.Length != 4 ||
            !int.TryParse(tokens[1], out int seed) ||
            !int.TryParse(tokens[2], out int plies) ||
            plies < 0 ||
            !TryReadDepth(tokens[3], out int depth))
            throw new FormatException($"Line {lineNumber}: expected 'R <seed> <plies> D<depth>'.");

        var state = GameState.CreateInitial();
        var rng = new Random(seed);
        for (int i = 0; i < plies && state.Status == GameStatus.InProgress; i++)
        {
            var legal = MoveGenerator.GenerateLegalMoves(state, state.CurrentTurn);
            if (legal.Count == 0)
                break;
            state = GameController.ApplyMove(state, legal[rng.Next(legal.Count)]);
        }
        if (state.Status != GameStatus.InProgress)
            throw new FormatException($"Line {lineNumber}: random play ended the game before the search depth.");

        return new TestSuiteEntry(lineNumber, state, null, null, depth);
    }

    private static GameState ParsePosition(int lineNumber, string[] tokens)
    {
        if (tokens.Length < 3)
            throw new FormatException($"Line {lineNumber}: expected 'P <Blue|Red> <pieces...>'.");
        if (!Enum.TryParse<Player>(tokens[1], ignoreCase: true, out var turn))
            throw new FormatException($"Line {lineNumber}: unknown player '{tokens[1]}'.");

        var pieces = new List<Piece>(tokens.Length - 2);
        for (int t = 2; t < tokens.Length; t++)
        {
            string[] parts = tokens[t].Split(':');
            if (parts.Length != 2 || !TryParsePosition(parts[0], out var pos))
                throw new FormatException($"Line {lineNumber}: bad piece token '{tokens[t]}' (expected c,r:Animal).");
            if (!TryParseAnimal(parts[1], out var animal, out var owner))
                throw new FormatException($"Line {lineNumber}: unknown animal '{parts[1]}'.");
            pieces.Add(new Piece(animal, owner, pos));
        }

        try
        {
            return GameState.CreateFromPieces(pieces, turn);
        }
        catch (ArgumentException ex)
        {
            throw new FormatException($"Line {lineNumber}: {ex.Message}");
        }
    }

    /// <summary>FEN-style owner convention: capital = Blue, lowercase = Red.</summary>
    private static bool TryParseAnimal(string name, out Animal animal, out Player owner)
    {
        animal = default;
        owner = default;
        if (name.Length == 0)
            return false;
        bool isBlue = char.IsUpper(name[0]);
        if (!Enum.TryParse<Animal>(name, ignoreCase: true, out animal))
            return false;
        owner = isBlue ? Player.Blue : Player.Red;
        return true;
    }

    private static bool TryParsePosition(string token, out Position pos)
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

    private static Move ParseMove(string token, int lineNumber)
    {
        string[] parts = token.Split('-');
        if (parts.Length != 2 ||
            !TryParsePosition(parts[0], out var from) ||
            !TryParsePosition(parts[1], out var to))
            throw new FormatException($"Line {lineNumber}: bad move '{token}' (expected c,r-c,r).");
        return new Move(from, to);
    }

    private static bool TryReadDepth(string token, out int depth)
    {
        depth = 0;
        if (!token.StartsWith('D') || token.Length < 2)
            return false;
        return int.TryParse(token.AsSpan(1), out depth) && depth > 0;
    }
}
