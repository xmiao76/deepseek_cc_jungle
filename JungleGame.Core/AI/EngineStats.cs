namespace JungleGame.Core.AI;

/// <summary>
/// Search statistics snapshot for the UI: completed depth, nodes, nodes per
/// second, tablebase hits, and the current best score (root perspective).
/// Updated after each completed iteration; nps is computed on read.
/// </summary>
public readonly record struct EngineStats(
    int Depth,
    long Nodes,
    long NodesPerSecond,
    long TablebaseHits,
    int Score);
