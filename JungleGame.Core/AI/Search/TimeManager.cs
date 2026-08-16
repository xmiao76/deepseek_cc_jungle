using System.Diagnostics;

namespace JungleGame.Core.AI;

/// <summary>
/// Time/cancellation checking for the search. Wall-clock reads run on a node
/// cadence (every 4096 checks) instead of per node — the per-node
/// Stopwatch.Elapsed + Interlocked reads were a measurable nps cost and the
/// overshoot (a few thousand nodes) is negligible at engine speeds.
/// Cancellation requests are still observed on every check. Once a search is
/// aborted the flag is sticky — that is what keeps partially searched nodes
/// out of the transposition table.
/// </summary>
internal sealed class TimeManager
{
    private const int CheckCadence = 4096;

    private long _timeLimitTicks; // Interlocked: SetTimeLimit may race a running search
    private int _counter;
    private bool _aborted;

    internal TimeManager(TimeSpan timeLimit) => _timeLimitTicks = timeLimit.Ticks;

    /// <summary>Changes the per-move time budget (difficulty); observed at the next check.</summary>
    internal void SetTimeLimit(TimeSpan timeLimit) =>
        Interlocked.Exchange(ref _timeLimitTicks, timeLimit.Ticks);

    internal bool Aborted => _aborted;

    internal void Reset()
    {
        _aborted = false;
        _counter = 0;
    }

    internal bool Check(Stopwatch sw, CancellationToken token)
    {
        if (_aborted)
            return true;
        _counter++;
        if ((_counter & (CheckCadence - 1)) == 0)
        {
            if (sw.Elapsed.Ticks >= Interlocked.Read(ref _timeLimitTicks) || token.IsCancellationRequested)
                _aborted = true;
        }
        else if (token.IsCancellationRequested)
        {
            _aborted = true;
        }
        return _aborted;
    }
}
