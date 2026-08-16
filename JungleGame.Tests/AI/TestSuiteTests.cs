using System.Reflection;
using JungleGame.Core.AI;

namespace JungleGame.Tests.AI;

/// <summary>
/// Executes the checked-in tactical suite (Resources/tactical.tsuite) at fixed
/// search depths and asserts every entry passes. This is the fast, deterministic
/// CI version of the Bench --testsuite gate.
/// </summary>
public class TestSuiteTests
{
    [Fact]
    public void TacticalSuite_AllEntries_Pass()
    {
        var entries = LoadEmbeddedSuite();
        Assert.NotEmpty(entries);

        var results = TacticalSuiteRunner.RunAll(entries);
        var failures = results.Where(r => !r.Passed).ToList();

        Assert.True(failures.Count == 0,
            $"{failures.Count} of {results.Count} suite entries failed:" +
            string.Concat(failures.Select(f => $"\n  {f.Entry.Description}: {f.Detail}")));
    }

    private static List<TestSuiteEntry> LoadEmbeddedSuite()
    {
        using var stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("JungleGame.Tests.Resources.tactical.tsuite")
            ?? throw new InvalidOperationException("tactical.tsuite embedded resource missing.");
        using var reader = new StreamReader(stream);
        return TestSuiteParser.Parse(reader.ReadToEnd());
    }
}
