using JungleGame.Core.AI;
using JungleGame.Core.Model;

namespace JungleGame.Tests.AI;

public class TestSuiteParserTests
{
    [Fact]
    public void Parse_PositionAndExpectation_ProducesEntry()
    {
        var entries = TestSuiteParser.Parse("""
            P Blue 3,1:Lion 0,0:Cat 2,2:wolf 4,2:dog 6,8:rat
            E 3,1-3,2 D3
            """);

        var entry = Assert.Single(entries);
        Assert.Equal(2, entry.LineNumber);
        Assert.NotNull(entry.ExpectedMove);
        Assert.Equal(new Position(3, 1), entry.ExpectedMove!.Value.From);
        Assert.Equal(new Position(3, 2), entry.ExpectedMove!.Value.To);
        Assert.Null(entry.ForbiddenMove);
        Assert.Equal(3, entry.SearchDepth);
        Assert.Equal(Player.Blue, entry.State.CurrentTurn);
        Assert.Equal(Animal.Lion, entry.State.GetPieceAt(new Position(3, 1))!.Value.Animal);
        Assert.Equal(Player.Blue, entry.State.GetPieceAt(new Position(3, 1))!.Value.Owner);
        Assert.Equal(Animal.Wolf, entry.State.GetPieceAt(new Position(2, 2))!.Value.Animal);
        Assert.Equal(Player.Red, entry.State.GetPieceAt(new Position(2, 2))!.Value.Owner);
    }

    [Fact]
    public void Parse_ForbiddenAndBenchOnly_LinesProduceSeparateEntries()
    {
        var entries = TestSuiteParser.Parse("""
            P Red 3,4:Elephant 3,2:rat
            F 3,4-3,3 D2
            N D8
            """);

        Assert.Equal(2, entries.Count);
        Assert.Null(entries[0].ExpectedMove);
        Assert.Equal(new Position(3, 4), entries[0].ForbiddenMove!.Value.From);
        Assert.Equal(Player.Red, entries[0].State.CurrentTurn);
        Assert.Null(entries[1].ExpectedMove);
        Assert.Null(entries[1].ForbiddenMove);
        Assert.Equal(8, entries[1].SearchDepth);
    }

    [Fact]
    public void Parse_StartLine_CreatesInitialPositionEntry()
    {
        var entries = TestSuiteParser.Parse("S D10");

        var entry = Assert.Single(entries);
        Assert.Equal(10, entry.SearchDepth);
        Assert.Equal(16, entry.State.Pieces.Count);
        Assert.Equal(Player.Blue, entry.State.CurrentTurn);
    }

    [Fact]
    public void Parse_ExpectationWithoutPosition_Throws()
    {
        var ex = Assert.Throws<FormatException>(() => TestSuiteParser.Parse("E 3,1-3,2 D3"));
        Assert.Contains("'P'", ex.Message);
    }

    [Theory]
    [InlineData("P Blue 3,1:Lion 3,1:Cat", "Two pieces on square")]            // duplicate square
    [InlineData("P Blue 3,0:Lion 0,0:rat", "den square")]                     // piece on a den
    [InlineData("P Blue 3,1:Lion", "both sides")]                             // one side only
    [InlineData("P Blue 3,1:Lion 0,0:cat\nE 3,1-3,2 DX", "D<depth>")]         // bad depth
    [InlineData("P Blue 3,1:Lion 0,0:cat\nE 3,1-3,2\n", "D<depth>")]          // missing depth
    [InlineData("P Blue 3,1:Lion 0,0:cat\nX 3,1-3,2 D2", "unknown directive")] // bad directive
    [InlineData("P Blue 3,1:Unicorn 0,0:cat", "unknown animal")]              // bad animal
    [InlineData("P Blue 8,1:Lion 0,0:cat", "bad piece token")]                // bad square
    public void Parse_InvalidInput_Throws(string text, string messagePart)
    {
        var ex = Assert.Throws<FormatException>(() => TestSuiteParser.Parse(text));
        Assert.Contains(messagePart, ex.Message);
    }

    [Fact]
    public void Parse_RandomPlayLine_ProducesDeterministicMidgame()
    {
        var a = TestSuiteParser.Parse("R 42 10 D10");
        var b = TestSuiteParser.Parse("R 42 10 D10");

        var entry = Assert.Single(a);
        Assert.Equal(10, entry.SearchDepth);
        Assert.Equal(GameStatus.InProgress, entry.State.Status);
        // Same seed + same plies = the same position, every time.
        Assert.Equal(b[0].State.CurrentTurn, entry.State.CurrentTurn);
        Assert.Equal(b[0].State.History, entry.State.History);
    }

    [Fact]
    public void Parse_RandomPlayLine_BadFormat_Throws()
    {
        Assert.Throws<FormatException>(() => TestSuiteParser.Parse("R 42 10"));
        Assert.Throws<FormatException>(() => TestSuiteParser.Parse("R x 10 D4"));
    }

    [Fact]
    public void Parse_PositionHistory_SeedsConstructedHash()
    {
        var entries = TestSuiteParser.Parse("P Blue 3,1:Lion 0,0:rat\nN D4");

        // CreateFromPieces seeds History with the position hash so three-fold
        // detection counts a return to the constructed position.
        Assert.Single(entries[0].State.History);
    }
}
