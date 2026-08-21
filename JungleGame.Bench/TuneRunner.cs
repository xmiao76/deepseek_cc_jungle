using JungleGame.Core.AI;
using JungleGame.Core.Engine;
using JungleGame.Core.Model;

namespace JungleGame.Bench;

/// <summary>
/// Data-driven evaluation tuning (texel-style). --gen-data plays fixed-depth
/// self-play games and stores sampled positions with their game results (the
/// side to move's perspective); --tune fits the linear eval weights with
/// logistic loss using Adam and analytic gradients (the eval is linear in the
/// weights, so the gradient is exact). Positions are stored, not features, so
/// the feature set can change without regenerating the data.
/// </summary>
internal static class TuneRunner
{
    // Data format: 64-byte header (magic "JGLTD1", version, engine version,
    // record count) + fixed 68-byte records: 63 bytes square→pieceId, 1 byte
    // turn, 2 bytes ply, 1 byte label (0 = loss, 1 = draw, 2 = win from the
    // side to move's perspective), 1 byte reserved.
    private static readonly byte[] Magic = "JGLTD1"u8.ToArray();
    private const int HeaderSize = 64;
    private const int RecordSize = 68;

    private const double DefaultScale = 400;
    private const double DefaultL2 = 1e-6;

    private static readonly string[] WeightNames =
    {
        "Material", "Forward", "DenOffense", "DenGuard", "Trap", "DoomedPerRank",
        "DenEscort", "RiverBank", "JumpPath", "RatNearWater", "RatInWater",
        "ElephantRatFear", "ThreatStronger", "ThreatEqual", "RatThreatensElephant",
        "Mobility", "DenThreat", "EndgameDenThreat", "EndgameAdvance", "BackRank",
        "RatNearOppDen",
    };

    internal static int RunGenData(string[] args)
    {
        int games = Args.ReadInt(args, "--games", 10_000);
        int depth = Args.ReadInt(args, "--depth", 5);
        int depthB = Args.ReadInt(args, "--depthB", 0);
        int parallel = Args.ReadInt(args, "--parallel", Math.Max(1, Environment.ProcessorCount / 2));
        int seed = Args.ReadInt(args, "--seed", 42);
        int openings = Args.ReadInt(args, "--openings", 4);
        int sampleInterval = Args.ReadInt(args, "--sample-interval", 6);
        int minPly = Args.ReadInt(args, "--min-ply", 20);
        int maxRecordsPerGame = Args.ReadInt(args, "--max-records", 300);
        string path = Args.ReadString(args, "--out", "tuning-data.bin") ?? "tuning-data.bin";

        Console.WriteLine($"Generating {games} games (depth {depth} vs {(depthB > 0 ? depthB : depth)}, " +
            $"{parallel} workers, openings {openings})...");

        var records = new List<byte[]>(capacity: games * 30);
        var gate = new object();
        int completed = 0;
        var sw = System.Diagnostics.Stopwatch.StartNew();

        Parallel.For(0, games, new ParallelOptions { MaxDegreeOfParallelism = parallel }, g =>
        {
            var rng = new Random(seed * 397 + g);
            // Fixed-depth searches are deterministic; a generous time budget
            // guarantees no abort ever fires.
            var engineA = new MinimaxEngine(TimeSpan.FromSeconds(30), maxDepth: depth);
            var engineB = new MinimaxEngine(TimeSpan.FromSeconds(30), maxDepth: depthB > 0 ? depthB : depth);
            var local = new List<byte[]>(capacity: maxRecordsPerGame);
            var seen = new HashSet<ulong>();

            var state = GameState.CreateInitial();
            for (int o = 0; o < openings && state.Status == GameStatus.InProgress; o++)
            {
                var legal = MoveGenerator.GenerateLegalMoves(state, state.CurrentTurn);
                state = GameController.ApplyMove(state, legal[rng.Next(legal.Count)]);
            }

            int ply = 0;
            const int maxMoves = 400;
            while (state.Status == GameStatus.InProgress && ply < maxMoves)
            {
                if (ply >= minPly && (ply - minPly) % sampleInterval == 0 && local.Count < maxRecordsPerGame)
                    local.Add(EncodeRecord(state, ply, seen));

                var engine = state.CurrentTurn == Player.Blue ? engineA : engineB;
                var move = engine.FindBestMove(state);
                if (move == null)
                    break;
                state = GameController.ApplyMove(state, move.Value);
                ply++;
            }

            // Stamp the final result onto every sampled record of this game,
            // from each sample's side-to-move perspective.
            byte result = state.Status switch
            {
                GameStatus.BlueWins => 2,
                GameStatus.RedWins => 0,
                _ => 1,
            };
            foreach (var record in local)
            {
                byte turn = record[63];
                record[66] = result == 1 ? (byte)1 : (byte)(turn == (int)Player.Blue ? result : 2 - result);
            }

            lock (gate)
            {
                records.AddRange(local);
                completed++;
                if (completed % 500 == 0 || completed == games)
                    Console.WriteLine($"{completed}/{games} games, {records.Count} records, {sw.Elapsed.TotalSeconds:F0}s");
            }
        });

        WriteDataFile(path, records);
        Console.WriteLine($"Wrote {records.Count} records to {path} ({new FileInfo(path).Length / (1024 * 1024)} MB) in {sw.Elapsed.TotalSeconds:F0}s.");
        return 0;
    }

    internal static int RunTune(string[] args)
    {
        string path = Args.ReadString(args, "--data", "tuning-data.bin") ?? "tuning-data.bin";
        string outPath = Args.ReadString(args, "--out", "weights.json") ?? "weights.json";
        int epochs = Args.ReadInt(args, "--epochs", 300);
        double lr = ReadDoubleArg(args, "--lr", 0.05);
        double scale = DefaultScale;
        double l2 = DefaultL2;
        double valFraction = 0.05;
        int seed = Args.ReadInt(args, "--seed", 42);

        if (!File.Exists(path))
        {
            Console.Error.WriteLine($"Data file not found: {path}");
            return 1;
        }

        var records = ReadDataFile(path);
        Console.WriteLine($"Loaded {records.Count} records from {path}.");

        // Feature extraction (single-threaded prepass; ~69 features per record).
        var features = new double[records.Count][];
        for (int i = 0; i < records.Count; i++)
            features[i] = ExtractFeatureVector(records[i]);
        Console.WriteLine("Features extracted.");

        // Deterministic hash-based validation split.
        var valIdx = new List<int>();
        var trainIdx = new List<int>();
        for (int i = 0; i < records.Count; i++)
        {
            if (HashRecord(records[i], seed) % 1_000_000 < valFraction * 1_000_000)
                valIdx.Add(i);
            else
                trainIdx.Add(i);
        }
        Console.WriteLine($"Split: {trainIdx.Count} train, {valIdx.Count} validation.");

        var weights = new double[EvalParameters.WeightCount];
        EvalParameters.Default.ToVector(weights, 0);
        var m = new double[weights.Length];
        var v = new double[weights.Length];
        double bestValLoss = double.MaxValue;
        int patienceLeft = 30;
        var bestWeights = (double[])weights.Clone();

        for (int epoch = 0; epoch < epochs; epoch++)
        {
            var grad = new double[weights.Length];
            var gradGate = new object();
            double trainLoss = 0;

            Parallel.ForEach(trainIdx, i =>
            {
                var f = features[i];
                double y = Label(records[i]);
                double score = Dot(weights, f);
                double p = Sigmoid(score / scale);
                double err = (p - y) / scale;

                double[] localGrad = new double[weights.Length];
                for (int j = 0; j < weights.Length; j++)
                    localGrad[j] = err * f[j] + l2 * weights[j];
                lock (gradGate)
                {
                    for (int j = 0; j < weights.Length; j++)
                        grad[j] += localGrad[j];
                    trainLoss += -y * Math.Log(Math.Max(p, 1e-12)) - (1 - y) * Math.Log(Math.Max(1 - p, 1e-12));
                }
            });

            for (int j = 0; j < weights.Length; j++)
            {
                grad[j] /= trainIdx.Count;
                m[j] = 0.9 * m[j] + 0.1 * grad[j];
                v[j] = 0.999 * v[j] + 0.001 * grad[j] * grad[j];
                weights[j] -= lr * m[j] / (Math.Sqrt(v[j]) + 1e-8);
            }

            if ((epoch + 1) % 10 == 0 || epoch == epochs - 1)
            {
                double valLoss = 0;
                foreach (int i in valIdx)
                {
                    double p = Sigmoid(Dot(weights, features[i]) / scale);
                    double y = Label(records[i]);
                    valLoss += -y * Math.Log(Math.Max(p, 1e-12)) - (1 - y) * Math.Log(Math.Max(1 - p, 1e-12));
                }
                valLoss /= valIdx.Count;
                trainLoss /= trainIdx.Count;
                Console.WriteLine($"epoch {epoch + 1}/{epochs}: train {trainLoss:F6} val {valLoss:F6}");

                if (valLoss < bestValLoss - 1e-6)
                {
                    bestValLoss = valLoss;
                    patienceLeft = 30;
                    bestWeights = (double[])weights.Clone();
                }
                else if (--patienceLeft <= 0 && epoch > 50)
                {
                    Console.WriteLine($"Early stop at epoch {epoch + 1} (val loss {bestValLoss:F6}).");
                    break;
                }
            }
        }

        // Report + weights.json.
        var tuned = EvalParameters.Default.Clone();
        tuned.FromVector(bestWeights, 0);
        var lines = new List<string> { "{" };
        for (int j = 0; j < WeightNames.Length; j++)
            lines.Add($"  \"{WeightNames[j]}\": {bestWeights[j]:F2}{(j < WeightNames.Length - 1 ? "," : "")}");
        lines.Add("}");
        File.WriteAllText(outPath, string.Join("\n", lines));
        Console.WriteLine($"Wrote {outPath}. Validation loss {bestValLoss:F6}.");

        var defaultParams = EvalParameters.Default;
        var d = new double[EvalParameters.WeightCount];
        defaultParams.ToVector(d, 0);
        Console.WriteLine("Weight changes (default → tuned):");
        for (int j = 0; j < WeightNames.Length; j++)
            Console.WriteLine($"  {WeightNames[j],-22} {d[j],8:F1} → {bestWeights[j],8:F1}");

        // Degenerate-fit detector: the Aug 2026 20k-game fit produced a negative
        // Mobility weight (inverts "more moves is better") and a negative
        // DoomedPerRank (inverts the trapped-enemy bonus — see the P2b sanity
        // checks in EvaluationTests pins). A fit with either inverted sign is
        // rejected before adoption; surface it loudly.
        var sanity = new (string Name, int Index, string Why)[]
        {
            ("Mobility", 15, "more moves must not lower the eval"),
            ("DoomedPerRank", 5, "a trapped enemy must improve the eval"),
        };
        foreach (var (name, index, why) in sanity)
        {
            if (bestWeights[index] < 0)
                Console.WriteLine($"WARNING: degenerate fit — {name} is {bestWeights[index]:F2} ({why}). DO NOT adopt; retune.");
        }

        return 0;
    }

    // ---- Data encoding ----

    private static byte[] EncodeRecord(GameState state, int ply, HashSet<ulong> seen)
    {
        var record = new byte[RecordSize];
        foreach (var kv in state.Pieces)
        {
            int sq = kv.Key.Row * 7 + kv.Key.Col;
            record[sq] = (byte)(((int)kv.Value.Animal - 1) * 2 + (int)kv.Value.Owner + 1);
        }
        record[63] = (byte)(int)state.CurrentTurn;
        record[64] = (byte)(ply & 0xFF);
        record[65] = (byte)((ply >> 8) & 0xFF);
        record[66] = 1; // label placeholder
        return record;
    }

    private static void WriteDataFile(string path, List<byte[]> records)
    {
        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream);
        writer.Write(Magic);
        writer.Write((byte)1);
        var engineVersion = "P3-harness".PadRight(24, ' ').Substring(0, 24);
        writer.Write(System.Text.Encoding.ASCII.GetBytes(engineVersion));
        writer.Write((uint)records.Count);
        writer.Write(new byte[HeaderSize - 6 - 1 - 24 - 4]);
        foreach (var record in records)
            writer.Write(record);
    }

    private static List<byte[]> ReadDataFile(string path)
    {
        var bytes = File.ReadAllBytes(path);
        using var reader = new BinaryReader(new MemoryStream(bytes));
        if (!reader.ReadBytes(Magic.Length).AsSpan().SequenceEqual(Magic))
            throw new InvalidOperationException("Bad tuning data magic.");
        if (reader.ReadByte() != 1)
            throw new InvalidOperationException("Unsupported tuning data version.");
        string engineVersion = System.Text.Encoding.ASCII.GetString(reader.ReadBytes(24));
        uint count = reader.ReadUInt32();
        reader.ReadBytes(HeaderSize - 6 - 1 - 24 - 4);
        var records = new List<byte[]>((int)count);
        for (int i = 0; i < count; i++)
            records.Add(reader.ReadBytes(RecordSize));
        Console.WriteLine($"Data engine version: {engineVersion}");
        return records;
    }

    private static double Label(byte[] record) => record[66] switch { 2 => 1.0, 1 => 0.5, _ => 0.0 };

    private static uint HashRecord(byte[] record, int seed)
    {
        uint h = (uint)seed;
        foreach (byte b in record)
            h = (h * 2654435761u) ^ b;
        return h;
    }

    private static double[] ExtractFeatureVector(byte[] record)
    {
        // Reconstruct the SearchBoard from the 63 square→pieceId bytes.
        Span<byte> types = stackalloc byte[16];
        Span<byte> squares = stackalloc byte[16];
        int n = 0;
        for (int sq = 0; sq < 63; sq++)
        {
            byte id = record[sq];
            if (id == 0)
                continue;
            types[n] = (byte)((id - 1) % 16);
            squares[n] = (byte)sq;
            n++;
        }

        int stm = record[63];
        var board = SearchBoard.FromPackedPieces(types[..n], squares[..n], stm);
        var f = EvalFeatureExtractor.ExtractStatic(board, stm, legacyEval: false);
        int mobilityDelta = board.CountLegalMoves(stm) - board.CountLegalMoves(stm ^ 1);
        var v = new double[EvalParameters.WeightCount];
        EvalFeatureExtractor.ToVector(f, mobilityDelta, v, 0);
        return v;
    }

    private static double Dot(double[] w, double[] v)
    {
        double s = 0;
        for (int j = 0; j < w.Length; j++)
            s += w[j] * v[j];
        return s;
    }

    private static double Sigmoid(double x) => 1.0 / (1.0 + Math.Exp(-x));

    private static double ReadDoubleArg(string[] args, string name, double fallback)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == name && double.TryParse(args[i + 1], out double value))
                return value;
        }
        return fallback;
    }
}
