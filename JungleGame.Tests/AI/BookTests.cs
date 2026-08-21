using JungleGame.Core.AI;
using JungleGame.Core.Engine;
using JungleGame.Core.Model;
using Xunit;

namespace JungleGame.Tests.AI;

/// <summary>
/// Opening-book tests. The book is static state (like the tablebase), so every
/// test resets it around itself; the [Collection("Tablebase")]-style separation
/// is not needed since only BookTests touch it and xUnit serializes per class
/// by default for shared-state classes like this one.
/// </summary>
public class BookTests : IDisposable
{
    public BookTests() => OpeningBook.ResetForTesting();

    public void Dispose()
    {
        OpeningBook.ResetForTesting();
        GC.SuppressFinalize(this);
    }

    private static string TempBookPath() =>
        Path.Combine(Path.GetTempPath(), $"jungle-book-test-{Guid.NewGuid():N}.bk");

    [Fact]
    public void SaveLoad_RoundTrips_SortedByKey()
    {
        var path = TempBookPath();
        try
        {
            OpeningBook.ReplaceAll(new[]
            {
                new OpeningBook.BookEntry(42, 3, 4, 5),
                new OpeningBook.BookEntry(1, 0, 1, 2),
                new OpeningBook.BookEntry(42, 5, 6, 9), // same key, different move
            });
            OpeningBook.Save(path);

            var loaded = OpeningBook.Load(path);
            Assert.NotNull(loaded);
            Assert.Equal(3, loaded!.Length);
            Assert.Equal(1ul, loaded[0].Key);
            Assert.Equal(42ul, loaded[1].Key);
            Assert.Equal(3, loaded[1].From);
            Assert.Equal(42ul, loaded[2].Key);
            Assert.Equal(5, loaded[2].From);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void TryGetMove_ReturnsHighestWeightMove_ForHash()
    {
        OpeningBook.ReplaceAll(new[]
        {
            new OpeningBook.BookEntry(7, 6, 13, 2),  // (6,0)→(6,1)
            new OpeningBook.BookEntry(7, 20, 27, 9), // (6,2)→(6,3): more visits
        });

        Assert.True(OpeningBook.TryGetMove(7, out var move));
        Assert.Equal(new Position(6, 2), move.From);
        Assert.Equal(new Position(6, 3), move.To);
        Assert.False(OpeningBook.TryGetMove(8, out _));
    }

    [Fact]
    public void Load_CorruptOrTampered_ReturnsNull()
    {
        var path = TempBookPath();
        try
        {
            File.WriteAllBytes(path, new byte[100]);
            Assert.Null(OpeningBook.Load(path)); // bad magic

            OpeningBook.ReplaceAll(new[] { new OpeningBook.BookEntry(9, 1, 2, 3) });
            OpeningBook.Save(path);
            var bytes = File.ReadAllBytes(path);
            bytes[^1] ^= 0xFF; // tamper the payload: the SHA-256 must reject it
            File.WriteAllBytes(path, bytes);
            Assert.Null(OpeningBook.Load(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Engine_UseBook_ReturnsLegalMove()
    {
        // The book seeds root ordering only; the search decides. Whatever the
        // book contains, the engine must return a legal move from the position.
        var state = GameState.CreateInitial();
        OpeningBook.ReplaceAll(new[]
        {
            new OpeningBook.BookEntry(TranspositionTable.ComputeHash(state), 6, 13, 1),
        });

        var engine = new MinimaxEngine(
            TimeSpan.FromSeconds(2), maxDepth: 4, maxNodes: 200_000, useBook: true);
        var move = engine.FindBestMove(state);
        Assert.NotNull(move);

        var legal = MoveGenerator.GenerateLegalMoves(state, state.CurrentTurn);
        Assert.Contains(legal, m => m.From == move!.Value.From && m.To == move.Value.To);
    }
}
