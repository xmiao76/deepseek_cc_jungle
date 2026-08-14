# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

Jungle (Dou Shou Qi / 鬥獸棋) — a Windows desktop board game with WPF GUI and AI opponent. Built with C# / .NET 8.

## Build & Test

```bash
# Build the solution (TreatWarningsAsErrors + latest-recommended analyzers are on;
# targeted suppressions live in .editorconfig with justifications)
dotnet build JungleGame.sln

# Build Release configuration
dotnet build JungleGame.sln -c Release

# Run all tests (~15s; perf gates run by default, serialized)
dotnet test JungleGame.sln

# Run a specific test class
dotnet test JungleGame.sln --filter "FullyQualifiedName~RiverJumpTests"

# Run a single test
dotnet test JungleGame.sln --filter "FullyQualifiedName~Lion_CanJumpVertically_AcrossRiver"

# Run tests with coverage (report lands in JungleGame.Tests/TestResults/*/coverage.cobertura.xml)
dotnet test JungleGame.sln --collect:"XPlat Code Coverage"

# Coverage gate (also run by CI; Core line coverage must stay >= 80% — currently ~98.6%)
powershell -File scripts/check-coverage.ps1

# Run the opt-in performance gates (also run by default, serialized)
dotnet test JungleGame.sln --filter "Category=Perf"

# Engine benchmark: nodes, nodes/s, depth reached from the start position
dotnet run --project JungleGame.Bench -c Release -- --bench --time 2000

# Self-play tournament: engine A vs engine B (time budgets in ms).
# --legacyB makes B use the pre-P3 evaluation; --legacySearchB makes B use the
# pre-P4 search (LMR scaling, null-move, futility, delta pruning, lazy mobility,
# incremental aspiration). --openings N plays N random legal plies before the
# engines take over (deterministic per game, seeded by --seed) — the default 0
# keeps the classic start-position protocol byte-identical.
# A/B protocol: same time for both, B on the legacy feature set; accept a change
# when A wins >= 55% of decisive games. Sanity: more time should win clearly.
dotnet run --project JungleGame.Bench -c Release -- --selfplay --games 40 --timeA 2000 --timeB 500
dotnet run --project JungleGame.Bench -c Release -- --selfplay --games 40 --timeA 2000 --timeB 2000 --legacyB
dotnet run --project JungleGame.Bench -c Release -- --selfplay --games 40 --timeA 2000 --timeB 2000 --legacySearchB
dotnet run --project JungleGame.Bench -c Release -- --selfplay --games 40 --timeA 2000 --timeB 2000 --legacyB --openings 4

# Recorded results (A/B = equal time, B on legacy feature set; accept A >= 55%
# of decisive games; equal-strength engines draw ~85% of games, so decisive
# samples are small and the gate is weak — treat a pass as "no regression found"):
# - Search A/B (P4 search vs legacy search, negated-root-window build): 40 games
#   classic start: A 4 / B 3 / 33 draws (57% of decisive); 20 games with 4
#   opening plies: A 1 / B 0 / 19 draws (100% of decisive, 1 decisive game).
#   Final-config build (original root window), 10 games with openings:
#   A 1 / B 3 / 6 draws (25% of decisive, 4 decisive games). Combined decisive
#   record across all runs: A 6 / B 6 — the search features are strength-NEUTRAL
#   at equal time; their value is node efficiency (same depth for ~22% fewer
#   nodes) and the infrastructure for future strength work. Kept on that basis.
# - Root-window experiment: the "correct" negated root window + proper cutoffs
#   made the engine draw-seek (sanity 10 games: 0 decisive; 6-game check with
#   legacy search on both sides: 1 decisive). Reverting to the original
#   un-negated root window restored decisive play. Do not "fix" the root window.
# - P4 eval terms (trap control, own-trap defense, river control, rat-threat,
#   den-race): A/B gate FAILED — A 1 / B 2 / 17 draws (33% of decisive) — the
#   terms were dropped. Do not re-add eval terms without passing this gate.
# - 2s vs 0.5s sanity, 10 games, final binaries: A 3 wins / B 1 win / 6 draws
#   (75% of decisive) — passes (pre-session baseline: A 3 / B 2 / 5 draws).
# - bench 2s from start: depth 11 at ~870k nps (pre-session); depth 11 at ~680k
#   nps with the P4 search features — same depth for fewer nodes (the pruning
#   spends the budget on the deeper parts of the tree)

# Publish the UI as a self-contained win-x64 single-file executable
# (WPF native DLLs are embedded as resources and extracted at startup by
# NativeLibraryExtractor; delete the loose .pdb files from release/ afterwards
# and re-zip the exe + README.txt into the untracked release/JungleGame.zip)
dotnet publish JungleGame.UI/JungleGame.UI.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o release/
```

## Architecture

Four-project solution (`JungleGame.sln`) plus a console harness (`JungleGame.Bench`).

### JungleGame.Core (class library, net8.0)
Pure game logic with no UI dependencies. All public types are immutable.

**`Model/`** — Domain types:
- `Position` — readonly struct for board coordinates (0-6 col, 0-8 row)
- `Animal` — enum (Rat=1 through Elephant=8, rank = int value)
- `Player` — enum (Blue bottom, Red top; Blue moves first)
- `Terrain` — enum (Land, River, TrapBlue/Red, DenBlue/Red)
- `Piece` — readonly struct combining Animal, Owner, Position; `WithPosition()` creates a new copy
- `Board` — stateless domain object over a static terrain grid (7×9) with lookup helpers (`IsRiver`, `IsTrap`, `IsDen`, `IsOpponentDen`, `GetTerrain` throws on out-of-bounds). Deliberately instance-shaped API (`state.Board.IsTrap(...)`); CA1822 is suppressed for this file for that reason
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
- `MinimaxEngine` — PVS + iterative deepening, aspiration windows (fail low/high widens straight to full width), LMR with move-index/depth-scaled reductions (den entries and the last couple of moves exempt), null-move pruning (R=2 + verification re-search; guards: disabled with <= 6 total pieces or <= 2 pieces for the side to move, depth < 3, or beta in the mate range — Dou Shou Qi zugzwangs early), futility pruning at depth 1 (mate-window guard, trap-square exemptions, static-eval fallback when every move is pruned), delta pruning in qsearch (trap-aware victim gain, den entries never pruned), lazy mobility in the qsearch stand-pat (full eval only when the mobility-free value falls inside the window), killer moves (full from|to), history heuristic with aging, quiescence search (all captures + enemy-den entries), transposition table, mate scores with distance preference (`MATE - ply`), three-fold repetition handling, no-moves = loss. Time-limited via Stopwatch + `CancellationToken`; `FindBestMove` returns `Move?` (null when the game is over or no moves exist). Searches are serialized per engine instance by an internal lock. `SetTimeLimit(ts)` changes difficulty mid-game (TT persists). `NodesSearched` / `LastCompletedDepth` for benchmarking. Ctor: `MinimaxEngine(TimeSpan? timeLimit = null, int? maxDepth = null, bool legacyEval = false, bool legacySearch = false)` — the legacy flags disable the P3 eval terms / P4 search features for A/B strength testing; both default false. **Two deliberately quirky details, both empirically validated (see recorded results):** (1) the root PV child is searched with the UN-negated aspiration window `(searchAlpha, searchBeta)` — the "correct" negated form was tried and produced draw-seeking play (the sanity gate went 10/10 draws); (2) aspiration failures widen straight to full width — incremental widening was tried and reverted with it
- `SearchBoard` — internal mutable search board (the performance core). Square indices `row*7+col`; piece ids 1..32 = `((animal-1)*2 + owner) + 1 + 16*copy` (duplicate (animal, owner) pairs from constructed positions get a second id range; Zobrist index = `(id-1) % 16`). Precomputed neighbor and Lion/Tiger jump tables (with mid-square rat blocking), incremental Zobrist hash, `GenerateMoves`/`GenerateCaptures`/`CountLegalMoves` into caller buffers, `ApplyMove` with swap-remove and winner detection, `MakeNullMove`/`UnmakeNullMove` for null-move pruning, `MaxMovesPerPly` buffer constant. Nodes are CopyTo/Clone copies ~120 bytes, pooled by the engine. **Parity with the public rules is enforced by SearchBoardDifferentialTests (fuzzing) and PerftTests — any rule change must be mirrored here.**
- `EvaluationFunction` — all term weights are named constants at the top of the file. `Evaluate(board, side, myMobility, oppMobility, legacyEval)` = `EvaluateStatic(...)` + mobility; `EvaluateStatic` (no move generation) is used by futility pruning and the lazy qsearch stand-pat. Terms: material (rank×100), forward progression, den proximity, trap penalty (−80 + doomed bonus), P3 den-escort, Lion/Tiger river-bank and jump-path bonuses, rat-water bonuses, elephant-vs-rat safety, static threat penalties, mobility delta ×3, den-threat terms, endgame den advance, back-rank development penalty. (A P4 set of extra positional terms was implemented and A/B-tested, failed the ≥55% gate, and was dropped — see the recorded results above.) The public `Evaluate(GameState, Player)` delegates to the fast path
- `TranspositionTable` — 1M-entry table (262,144 slots × 4 buckets), depth-preferred replacement (same-hash entries updated in place; shallow entries never evict deeper ones; Depth==0 = empty slot). Entries are generation-tagged: `NewGeneration()` runs per search; probes only accept the current or previous generation and stale entries are the first replacement candidates, bounding the lifetime of window-dependent bounds. Mate scores stored node-relative and converted to root-relative on probe before bound comparisons (`MateScore`/`MateRange` constants live here)

### JungleGame.Tests (xunit, net8.0)
xUnit + coverlet. Deterministic tests only (fixed seeds, depth-limited engines). Test helpers: `Helpers/TestBoardBuilder` (fluent state construction — use for new tests), `Helpers/NaiveMoveGenerator` (independent rules implementation for perft-style differentials). Differential fuzzing (`SearchBoardDifferentialTests`) and `PerftTests` guard SearchBoard/GameState parity. `PerfSmokeTests` (Category=Perf) run serialized; calibrate thresholds on a Debug build (~250k nodes/s from the start position). `EngineLifecycleTests` covers SetTimeLimit mid-search, cancellation, and TT integrity after aborts. `EvaluationTests` pins the legacyEval deltas exactly (P3 +30/−30/+40 and P4 +12/+8/+10/+30/−35) — new eval terms must not fire in those positions or must update the pinned deltas. Test method names use the xUnit underscore convention (CA1707/CA1711 suppressed for the Tests project).

### JungleGame.Bench (console, net8.0)
`--bench` performance report; `--selfplay` engine-vs-engine tournament (alternating colors, draws via the 3-fold rule; `--legacyB` for eval A/B, `--legacySearchB` for search A/B, `--seed`/`--openings` for deterministic opening variety).

### JungleGame.UI (WPF, net8.0-windows)
MVVM-ish: `MainViewModel` owns game state; board rendering is code-behind Canvas drawing (no data bindings).

- `App.xaml.cs` — shows StartDialog (difficulty: Easy 0.3s / Medium 1s / Hard 2s per move — Hard caps at 2 seconds; who moves first; AI-vs-AI), then creates MainWindow. The dialog auto-sizes to its content (SizeToContent)
- `App.xaml` — merges `Themes/Theme.xaml`, the single source of colors/brushes/button styles (cards, hover/pressed/focused states)
- `Theme.cs` — code-behind access to theme brushes (`Theme.GetBrush(key)`, magenta fallback so a missing key shows up visually instead of throwing)
- `GameStrings.cs` — the single GameStatus→text mapping (main window status label and game-over dialog share it)
- `MainViewModel` — owns GameState, MinimaxEngine instance, selection state, board-flip toggle, AI-vs-AI mode, `LastMove`/`MoveCounter` (drives the view's last-move highlight and move animation), `ClearSelection()` for keyboard Escape. Implements IDisposable (cancels the in-flight AI search on window close). `HandleCellClick(pos)` implements click-to-select/click-to-move (disabled in AI-vs-AI). `DoAIMove` runs the search on a thread-pool thread with a CancellationToken (New Game cancels promptly) and chains the opponent's move in AI-vs-AI mode so watch games play to completion. Board flip is display-only (logical game state is never mutated by flip)
- `Views/MainWindow.xaml(.cs)` — renders the board on a 500×650 Canvas inside a Viewbox, so it scales uniformly with the window at any resolution/DPI. Split into partials: `MainWindow.xaml.cs` (wiring, RenderBoard orchestration, UpdateUI, dialogs), `MainWindow.Rendering.cs` (DrawTerrain/DrawPiece/DrawHighlight/DrawLegalMoveIndicator/DrawGridLines/DrawCoordinates/DrawCursor/DrawLastMoveCell), `MainWindow.Input.cs` (mouse, keyboard, move animation). Polished piece discs (3-stop radial gradient, inner ring, rank pip, emoji + offset shadow), checkerboard land, themed terrain, last-move gold overlay, coordinate labels that rotate with the flip, keyboard navigation (arrow keys move a visual-space cursor, Enter/Space = click, Escape deselects), move animation (a transient fly-overlay of the piece + capture burst, self-removing — composes with the clear-and-rebuild render loop)
- `Views/StartDialog.xaml(.cs)` / `Views/GameOverDialog.xaml(.cs)` — themed dialogs with IsDefault/IsCancel, AutomationProperties, winner accent strip on game over
- `Assets/jungle.ico` — window/taskbar icon (multi-size, PNG-compressed; regenerated by `scripts/generate-icon.ps1`)
- `NativeLibraryExtractor.cs` — extracts the embedded WPF native DLLs to a per-version temp directory at startup (single-EXE publishing); re-extracts when a cached file is truncated and prints an actionable diagnostic if extraction fails
- Package: no third-party dependencies beyond .NET framework

## Tooling

- `.editorconfig` — style defaults + targeted analyzer suppressions, each with a justification comment (CA2007 WPF async, CA1416 platform noise, CA1822 for the instance-shaped Board API, CA1707/CA1711 for xUnit naming). Do not add suppressions without a comment.
- `Directory.Build.props` — TreatWarningsAsErrors + `latest-recommended` analyzers for all projects; run the build immediately after touching this file and triage every warning (fix or suppress with justification).
- `global.json` — pins SDK 8.0.x (`latestFeature` roll-forward).
- `.github/workflows/ci.yml` — build → test with coverage → `scripts/check-coverage.ps1` gate (Core line rate ≥ 80%) → 4-game engine smoke self-play.

## Key design decisions

- All Core public types are immutable (`readonly struct` or immutable collections). `GameController.ApplyMove` returns a new `GameState`; it never mutates the input. The engine's mutable `SearchBoard` is internal and converted once at search entry; results come back as public `Move`s
- Lion jumps both horizontally (row-changing, across the 3-tall river) and vertically (column-changing, across the 2-wide river); Tiger jumps only vertically (column-changing)
- Rat-in-water cannot be captured by any land piece except another Rat; Rat can capture Rat in water; Rat cannot capture Elephant from water
- A piece on an opponent's trap has effective rank 0 for capture resolution (attacker and defender both)
- No legal moves = loss for the side to move; third occurrence of a position = Draw (`GameStatus.Draw`)
- The board flip feature maps visual coordinates `(c,r)` → `(6-c, 8-r)` but preserves the logical game state unchanged. `HumanPlayer` always maps to Blue internally; flip only rotates the view
- AI difficulty maps to per-move time: Easy 0.3s, Medium 1s (default), Hard 2s (the cap); the engine instance (and its TT) persists across games
- Search pruning keeps the horizon sound in Dou Shou Qi: null-move is material-guarded + verified (the small board zugzwangs), futility never prunes near mate scores or trap-related moves and falls back to the static eval when every move is pruned, den entries are never delta-pruned in quiescence
- A partially searched node (aborted) never stores in the TT; the repetition check runs before the TT probe so probed scores cannot mask draws
- No NuGet packages in Core or UI beyond System.Collections.Immutable
- `InternalsVisibleTo("JungleGame.Tests")` is declared in JungleGame.Core.csproj
