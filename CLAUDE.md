# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

Jungle (Dou Shou Qi / 鬥獸棋) — a Windows desktop board game with WPF GUI and AI opponent. Built with C# / .NET 8.

## Build & Test

```bash
# Build the solution
dotnet build JungleGame.sln

# Build Release configuration
dotnet build JungleGame.sln -c Release

# Run all tests
dotnet test JungleGame.sln

# Run a specific test class
dotnet test JungleGame.sln --filter "FullyQualifiedName~RiverJumpTests"

# Run a single test
dotnet test JungleGame.sln --filter "FullyQualifiedName~Lion_CanJumpVertically_AcrossRiver"

# Run tests with coverage (report lands in JungleGame.Tests/TestResults/*/coverage.cobertura.xml;
# Core line coverage must stay >= 80% — currently ~96%)
dotnet test JungleGame.sln --collect:"XPlat Code Coverage"

# Run the opt-in performance gates (also run by default, serialized)
dotnet test JungleGame.sln --filter "Category=Perf"

# Engine benchmark: nodes, nodes/s, depth reached from the start position
dotnet run --project JungleGame.Bench -c Release -- --bench --time 2000

# Self-play tournament: engine A vs engine B (time budgets in ms).
# --legacyB makes B use the pre-P3 evaluation for A/B strength testing.
# Sanity: more time should win clearly (2s vs 0.5s). For eval changes:
# same time for both, v2 (default) vs legacy; accept if v2 wins >= 55%.
dotnet run --project JungleGame.Bench -c Release -- --selfplay --games 40 --timeA 2000 --timeB 500
dotnet run --project JungleGame.Bench -c Release -- --selfplay --games 40 --timeA 2000 --timeB 2000 --legacyB

# Recorded results:
# - 2s vs 0.5s, 10 games: A 6 wins / 3 draws / B 1 win (86% of decisive games)
# - v2 vs legacy eval at 1.5s, 10 games: v2 3 wins / 5 draws / legacy 2 wins
#   (60% of decisive games — provisional; the 40-game protocol above is the
#   definitive gate for eval changes)

# Publish the UI as a self-contained win-x64 executable
dotnet publish JungleGame.UI/JungleGame.UI.csproj -c Release -r win-x64 --self-contained true -o release/
```

## Architecture

Three-project solution (`JungleGame.sln`) plus a console harness (`JungleGame.Bench`):

### JungleGame.Core (class library, net8.0)
Pure game logic with no UI dependencies. All public types are immutable.

**`Model/`** — Domain types:
- `Position` — readonly struct for board coordinates (0-6 col, 0-8 row)
- `Animal` — enum (Rat=1 through Elephant=8, rank = int value)
- `Player` — enum (Blue bottom, Red top; Blue moves first)
- `Terrain` — enum (Land, River, TrapBlue/Red, DenBlue/Red)
- `Piece` — readonly struct combining Animal, Owner, Position; `WithPosition()` creates a new copy
- `Board` — static terrain grid (7×9) with lookup helpers (`IsRiver`, `IsTrap`, `IsDen`, `IsOpponentDen`, `GetTerrain` throws on out-of-bounds)
- `GameState` — immutable snapshot: Board, Pieces (ImmutableDictionary), CurrentTurn, Status, CapturedBlue/Red, and `History` (ImmutableList of Zobrist hashes after each move, used for three-fold repetition). `CreateInitial()` sets up standard opening positions and seeds History with the opening hash
- `Zobrist` — internal static Zobrist hashing (fixed-seed keys, deterministic across runs). TurnKey is XORed only for Red-to-move, so the hash distinguishes side to move

**`Rules/`** — Rule enforcement:
- `MoveValidator.Validate(state, from, to)` — returns `string?` (null = legal, otherwise error message). Enforces: orthogonal moves, river entry only for Rat, jump paths for Lion/Tiger, rat blocking, den restrictions, turn order
- `CaptureResolver.CanCapture(attacker, defender, board)` — rank comparison, Rat↔Elephant special cases (rat captures elephant from land only; elephant never captures rat), Rat-vs-Rat unconditional (land or water), trap rank reduction, rat-in-water invulnerability to other land pieces

**`Engine/`** — State mutation:
- `GameController.ApplyMove(state, move)` — produces new GameState; throws on finished games. Declares wins (den invasion, elimination), loss for a side to move with zero legal moves, and Draw on the third occurrence of a position (3-fold repetition). Does NOT validate — caller must validate first
- `MoveGenerator.GenerateLegalMoves(state, player)` / `CountLegalMoves` — public rule engine; the AI does not use these in search (see SearchBoard)
- `Move` — readonly struct (From, To, Captured?)

**`AI/`** — Computer opponent:
- `MinimaxEngine` — PVS + iterative deepening, aspiration windows, LMR, killer moves (full from|to), history heuristic with aging, quiescence search (all captures + enemy-den entries), transposition table, mate scores with distance preference (`MATE - ply`), three-fold repetition handling, no-moves = loss. Time-limited via Stopwatch + `CancellationToken`; `FindBestMove` returns `Move?` (null when the game is over or no moves exist). Searches are serialized per engine instance by an internal lock. `SetTimeLimit(ts)` changes difficulty mid-game (TT persists). `NodesSearched` / `LastCompletedDepth` for benchmarking. Ctor: `MinimaxEngine(TimeSpan? timeLimit = null, int? maxDepth = null, bool legacyEval = false)`
- `SearchBoard` — internal mutable search board (the performance core). Square indices `row*7+col`; piece ids 1..32 = `((animal-1)*2 + owner) + 1 + 16*copy` (duplicate (animal, owner) pairs from constructed positions get a second id range; Zobrist index = `(id-1) % 16`). Precomputed neighbor and Lion/Tiger jump tables (with mid-square rat blocking), incremental Zobrist hash, `GenerateMoves`/`GenerateCaptures`/`CountLegalMoves` into caller buffers, `ApplyMove` with swap-remove and winner detection. Nodes are CopyTo/Clone copies ~120 bytes, pooled by the engine. **Parity with the public rules is enforced by SearchBoardDifferentialTests (fuzzing) and PerftTests — any rule change must be mirrored here.**
- `EvaluationFunction` — material (rank×100), forward progression, den proximity, trap penalty (−80 + −40·rank/8 doomed bonus), Lion/Tiger river-bank and jump-path bonuses, rat-water bonuses, elephant-vs-rat safety, static threat penalties, mobility delta ×3, den-threat terms, endgame den advance, back-rank development penalty, den-escort bonus (+30 for strong piece near own den under threat). Fast path evaluates SearchBoard with caller-supplied mobility counts; the public `Evaluate(GameState, Player)` delegates to it. `legacyEval` disables the P3 terms (doomed bonus, den escort) for A/B testing
- `TranspositionTable` — 1M-entry always-replace table, Zobrist keyed; mate scores stored node-relative and converted to root-relative on probe before bound comparisons (`MateScore`/`MateRange` constants live here)

### JungleGame.Tests (xunit, net8.0)
xUnit + coverlet. Deterministic tests only (fixed seeds, depth-limited engines). Test helpers: `Helpers/TestBoardBuilder` (fluent state construction — use for new tests), `Helpers/NaiveMoveGenerator` (independent rules implementation for perft-style differentials). Differential fuzzing (`SearchBoardDifferentialTests`) and `PerftTests` guard SearchBoard/GameState parity. `PerfSmokeTests` (Category=Perf) run serialized; calibrate thresholds on a Debug build (~250k nodes/s from the start position).

### JungleGame.Bench (console, net8.0)
`--bench` performance report; `--selfplay` engine-vs-engine tournament (alternating colors, draws via the 3-fold rule; `--legacyB` for eval A/B tests).

### JungleGame.UI (WPF, net8.0-windows)
MVVM pattern with code-behind board rendering.

- `App.xaml.cs` — shows StartDialog (difficulty: Easy 0.3s / Medium 1s / Hard 2s per move — Hard caps at 2 seconds; who moves first; AI-vs-AI), then creates MainWindow. The dialog auto-sizes to its content (SizeToContent)
- `MainViewModel` — owns GameState, MinimaxEngine instance, selection state, board-flip toggle, AI-vs-AI mode. `HandleCellClick(pos)` implements click-to-select/click-to-move (disabled in AI-vs-AI). `DoAIMove` runs the search on a thread-pool thread with a CancellationToken (New Game cancels promptly) and chains the opponent's move in AI-vs-AI mode so watch games play to completion. Board flip is display-only (logical game state is never mutated by flip)
- `MainWindow.xaml(.cs)` — renders the board on a 500×650 Canvas inside a Viewbox, so it scales uniformly with the window at any resolution/DPI. Terrain colors (land=tan, river=blue gradient, trap=crimson, den=dark red), piece circles (blue/red gradient with animal emoji + rank number), selection highlight, legal move dots
- `Converters/` — WPF value converters (BoolToVisibility, InvertBool, GameStatusToText)
- Package: no third-party dependencies beyond .NET framework

## Key design decisions

- All Core public types are immutable (`readonly struct` or immutable collections). `GameController.ApplyMove` returns a new `GameState`; it never mutates the input. The engine's mutable `SearchBoard` is internal and converted once at search entry; results come back as public `Move`s
- Lion jumps both horizontally (row-changing, across the 3-tall river) and vertically (column-changing, across the 2-wide river); Tiger jumps only vertically (column-changing)
- Rat-in-water cannot be captured by any land piece except another Rat; Rat can capture Rat in water; Rat cannot capture Elephant from water
- A piece on an opponent's trap has effective rank 0 for capture resolution (attacker and defender both)
- No legal moves = loss for the side to move; third occurrence of a position = Draw (`GameStatus.Draw`)
- The board flip feature maps visual coordinates `(c,r)` → `(6-c, 8-r)` but preserves the logical game state unchanged. `HumanPlayer` always maps to Blue internally; flip only rotates the view
- AI difficulty maps to per-move time: Easy 0.3s, Medium 1s (default), Hard 2s (the cap); the engine instance (and its TT) persists across games
- No NuGet packages in Core or UI beyond System.Collections.Immutable
- `InternalsVisibleTo("JungleGame.Tests")` is declared in JungleGame.Core.csproj
