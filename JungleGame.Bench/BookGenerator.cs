using System.Text;
using JungleGame.Core.AI;
using JungleGame.Core.Engine;
using JungleGame.Core.Model;

namespace JungleGame.Bench;

/// <summary>
/// Opening-book builder: plays engine-vs-engine games and records the root
/// moves of the first plies (per position hash, with visit weights). The same
/// games yield an openings file in the arena's "--openings-file" plies format,
/// so the book's own opening lines can power decisive variety in future arena
/// runs. Sequential and seeded: a fixed --seed reproduces the book exactly.
/// </summary>
internal static class BookGenerator
{
    private const int MaxMoves = 400;

    internal static int Run(string[] args)
    {
        int games = Args.ReadInt(args, "--games", 2000);
        int timeMs = Args.ReadInt(args, "--time", 200);
        int plyLimit = Args.ReadInt(args, "--ply-limit", 8);
        int seed = Args.ReadInt(args, "--seed", 42);
        string outPath = Args.ResolveDataPath(Args.ReadString(args, "--out", "jungle-book.bk")!);
        string? openingsPath = Args.ReadString(args, "--openings-out");
        if (openingsPath != null)
            openingsPath = Args.ResolveDataPath(openingsPath);

        var rng = new Random(seed);
        var openingLines = new StringBuilder();
        var book = new BookRecorder();

        for (int g = 0; g < games; g++)
        {
            var state = GameState.CreateInitial();
            var engines = new[]
            {
                new MinimaxEngine(TimeSpan.FromMilliseconds(timeMs)),
                new MinimaxEngine(TimeSpan.FromMilliseconds(timeMs)),
            };

            var plies = new List<string>();
            int moves = 0;
            while (state.Status == GameStatus.InProgress && moves < MaxMoves)
            {
                var engine = engines[(int)state.CurrentTurn];
                var move = engine.FindBestMove(state);
                if (move == null)
                    break;

                if (moves < plyLimit)
                {
                    book.Record(TranspositionTable.ComputeHash(state), move.Value);
                    plies.Add($"{move.Value.From.Col},{move.Value.From.Row}-{move.Value.To.Col},{move.Value.To.Row}");
                }

                state = GameController.ApplyMove(state, move.Value);
                moves++;
            }

            if (plies.Count >= 2)
                openingLines.AppendLine(string.Join(' ', plies));
        }

        book.Save(outPath);
        Console.WriteLine($"Book: {book.EntryCount} entries -> {outPath}");
        if (openingsPath != null)
        {
            File.WriteAllText(openingsPath, openingLines.ToString());
            Console.WriteLine($"Openings: {openingLines.Length} chars -> {openingsPath}");
        }
        return 0;
    }

    /// <summary>Collects (hash, move) occurrences; writes the packed book on Save.</summary>
    private sealed class BookRecorder
    {
        private readonly Dictionary<ulong, List<(byte From, byte To, ushort Weight)>> _entries = new();

        internal int EntryCount => _entries.Count;

        internal void Record(ulong hash, Move move)
        {
            byte from = (byte)(move.From.Row * 7 + move.From.Col);
            byte to = (byte)(move.To.Row * 7 + move.To.Col);
            if (!_entries.TryGetValue(hash, out var list))
                _entries[hash] = list = new List<(byte, byte, ushort)>();
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].From == from && list[i].To == to)
                {
                    if (list[i].Weight < ushort.MaxValue)
                        list[i] = (from, to, (ushort)(list[i].Weight + 1));
                    return;
                }
            }
            list.Add((from, to, 1));
        }

        internal void Save(string path)
        {
            var list = new List<OpeningBook.BookEntry>();
            foreach (var (hash, moves) in _entries)
                foreach (var (from, to, weight) in moves)
                    list.Add(new OpeningBook.BookEntry(hash, from, to, weight));
            OpeningBook.ReplaceAll(list);
            OpeningBook.Save(path);
        }
    }
}
