using JungleGame.Core.AI;
using JungleGame.Core.Engine;
using JungleGame.Core.Model;
using Xunit;

namespace JungleGame.Tests.AI;

/// <summary>
/// Parity guard: the fast internal SearchBoard must produce exactly the same moves,
/// state transitions, and Zobrist hashes as the public GameState-based rule engine.
/// </summary>
public class SearchBoardDifferentialTests
{
    [Fact]
    public void Fuzz_RandomPlay_MoveGenerationMatches_AndStateStaysInSync()
    {
        var random = new Random(42);

        for (int game = 0; game < 20; game++)
        {
            var state = GameState.CreateInitial();
            var board = SearchBoard.FromGameState(state);

            for (int ply = 0; ply < 80 && state.Status == GameStatus.InProgress; ply++)
            {
                Assert.Equal(state.Pieces.Count, board.PieceCount(0) + board.PieceCount(1));
                Assert.Equal(Zobrist.ComputeHash(state), board.Hash);

                int side = (int)state.CurrentTurn;
                var publicMoves = MoveGenerator.GenerateLegalMoves(state, state.CurrentTurn);

                var buf = new SearchMove[128];
                int count = board.GenerateMoves(side, buf);

                // Same move count and same (from,to) set
                Assert.Equal(publicMoves.Count, count);
                var publicSet = publicMoves
                    .Select(m => (From: m.From.Row * 7 + m.From.Col, To: m.To.Row * 7 + m.To.Col))
                    .ToHashSet();
                var fastSet = new HashSet<(int From, int To)>();
                for (int i = 0; i < count; i++)
                    fastSet.Add((buf[i].From, buf[i].To));
                Assert.True(publicSet.SetEquals(fastSet), $"Move sets differ at ply {ply} of game {game}");

                // Same capture/den-entry set
                var capBuf = new SearchMove[128];
                int capCount = board.GenerateCaptures(side, capBuf);
                int expectedCaps = publicMoves.Count(m =>
                    m.IsCapture || state.Board.IsOpponentDen(m.To, state.CurrentTurn));
                Assert.Equal(expectedCaps, capCount);

                // Round-trip conversion
                var roundTrip = board.ToGameState();
                AssertPiecesEqual(state, roundTrip);
                Assert.Equal((int)state.CurrentTurn, (int)roundTrip.CurrentTurn);

                // Apply the same randomly chosen move to both representations
                var move = publicMoves[random.Next(publicMoves.Count)];
                var searchMove = default(SearchMove);
                for (int i = 0; i < count; i++)
                {
                    if (buf[i].From == move.From.Row * 7 + move.From.Col &&
                        buf[i].To == move.To.Row * 7 + move.To.Col)
                    {
                        searchMove = buf[i];
                        break;
                    }
                }

                state = GameController.ApplyMove(state, move);
                board.ApplyMove(searchMove);

                // Hash and pieces must stay in sync after the apply
                Assert.Equal(Zobrist.ComputeHash(state), board.Hash);
                AssertPiecesEqual(state, board.ToGameState());

                // Winner detection parity (no-moves losses are handled below)
                if (state.Status == GameStatus.BlueWins || state.Status == GameStatus.RedWins)
                {
                    if (board.WinnerSide != SearchBoard.NoWinner)
                    {
                        Assert.Equal(state.Status == GameStatus.BlueWins ? 0 : 1, board.WinnerSide);
                    }
                    else
                    {
                        // The win must be a no-moves loss: the losing side has no moves
                        int loser = state.Status == GameStatus.BlueWins ? 1 : 0;
                        Assert.Equal(0, board.GenerateMoves(loser, new SearchMove[128]));
                    }
                }
            }
        }
    }

    private static void AssertPiecesEqual(GameState a, GameState b)
    {
        Assert.Equal(a.Pieces.Count, b.Pieces.Count);
        foreach (var kv in a.Pieces)
            Assert.True(
                b.Pieces.TryGetValue(kv.Key, out var p) && p == kv.Value,
                $"Piece mismatch at {kv.Key}");
    }

    [Fact]
    public void Fuzz_RandomPlacements_MoveGenerationMatches()
    {
        // Random placements of the full piece set (no duplicate kinds) exercise
        // geometric configurations — banks, traps, water entries, jump paths —
        // that random play rarely reaches.
        var random = new Random(1234);
        var allKinds = new (Animal Animal, Player Owner)[16];
        int k = 0;
        foreach (Player owner in new[] { Player.Blue, Player.Red })
            foreach (Animal animal in Enum.GetValues<Animal>())
                allKinds[k++] = (animal, owner);

        for (int position = 0; position < 100; position++)
        {
            var squares = Enumerable.Range(0, 63).OrderBy(_ => random.Next()).ToArray();
            var pieces = new Dictionary<Position, Piece>();
            for (int i = 0; i < allKinds.Length; i++)
            {
                int sq = squares[i];
                var pos = new Position(sq % 7, sq / 7);
                pieces[pos] = new Piece(allKinds[i].Animal, allKinds[i].Owner, pos);
            }
            var state = new GameState(
                Board.Initial,
                System.Collections.Immutable.ImmutableDictionary.CreateRange(pieces),
                random.Next(2) == 0 ? Player.Blue : Player.Red,
                GameStatus.InProgress,
                System.Collections.Immutable.ImmutableList<Piece>.Empty,
                System.Collections.Immutable.ImmutableList<Piece>.Empty);

            int side = (int)state.CurrentTurn;
            var publicSet = MoveGenerator.GenerateLegalMoves(state, state.CurrentTurn)
                .Select(m => (From: m.From.Row * 7 + m.From.Col, To: m.To.Row * 7 + m.To.Col))
                .ToHashSet();

            var board = SearchBoard.FromGameState(state);
            var buf = new SearchMove[128];
            int count = board.GenerateMoves(side, buf);
            var fastSet = new HashSet<(int From, int To)>();
            for (int i = 0; i < count; i++)
                fastSet.Add((buf[i].From, buf[i].To));

            Assert.True(publicSet.SetEquals(fastSet), $"Move sets differ in placement {position}");
        }
    }

    [Fact]
    public void RatOnEnemyTrap_CanCaptureRat_MoveSetsMatch()
    {
        // Targeted case the random-play fuzzer rarely hits: a rat standing on the
        // opponent's trap (effective rank 0) still captures a rat — the rat-vs-rat
        // rule is unconditional.
        var pieces = new Dictionary<Position, Piece>
        {
            [new Position(3, 7)] = new Piece(Animal.Rat, Player.Blue, new Position(3, 7)),
            [new Position(2, 7)] = new Piece(Animal.Rat, Player.Red, new Position(2, 7)),
            [new Position(0, 0)] = new Piece(Animal.Lion, Player.Blue, new Position(0, 0)),
            [new Position(6, 8)] = new Piece(Animal.Wolf, Player.Red, new Position(6, 8))
        };
        var state = new GameState(
            Board.Initial,
            System.Collections.Immutable.ImmutableDictionary.CreateRange(pieces),
            Player.Blue,
            GameStatus.InProgress,
            System.Collections.Immutable.ImmutableList<Piece>.Empty,
            System.Collections.Immutable.ImmutableList<Piece>.Empty);
        var board = SearchBoard.FromGameState(state);

        var publicSet = MoveGenerator.GenerateLegalMoves(state, Player.Blue)
            .Select(m => (From: m.From.Row * 7 + m.From.Col, To: m.To.Row * 7 + m.To.Col))
            .ToHashSet();
        var buf = new SearchMove[128];
        int count = board.GenerateMoves(0, buf);
        var fastSet = new HashSet<(int From, int To)>();
        for (int i = 0; i < count; i++)
            fastSet.Add((buf[i].From, buf[i].To));

        Assert.Contains((Sq(3, 7), Sq(2, 7)), publicSet);
        Assert.True(publicSet.SetEquals(fastSet), "Move sets differ for the rat-on-trap position");

        static int Sq(int col, int row) => row * 7 + col;
    }
}
