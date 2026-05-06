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

# Run tests with coverage
dotnet test JungleGame.sln /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura

# Publish the UI as a self-contained win-x64 executable
dotnet publish JungleGame.UI/JungleGame.UI.csproj -c Release -r win-x64 --self-contained true -o release/
```

## Architecture

Three-project solution (`JungleGame.sln`):

### JungleGame.Core (class library, net8.0)
Pure game logic with no UI dependencies. All types are immutable.

**`Model/`** — Domain types:
- `Position` — readonly struct for board coordinates (0-6 col, 0-8 row)
- `Animal` — enum (Rat=1 through Elephant=8, rank = int value)
- `Player` — enum (Blue bottom, Red top; Blue moves first)
- `Terrain` — enum (Land, River, TrapBlue/Red, DenBlue/Red)
- `Piece` — readonly struct combining Animal, Owner, Position; `WithPosition()` creates a new copy
- `Board` — static terrain grid (7×9) with lookup helpers (`IsRiver`, `IsTrap`, `IsDen`, `IsOpponentDen`)
- `GameState` — immutable snapshot: Board, Pieces (ImmutableDictionary), CurrentTurn, Status, CapturedBlue/Red. `CreateInitial()` sets up standard opening positions

**`Rules/`** — Rule enforcement:
- `MoveValidator.Validate(state, from, to)` — returns `string?` (null = legal, otherwise error message). Enforces: orthogonal moves, river entry only for Rat, jump paths for Lion/Tiger, rat blocking, den restrictions, turn order
- `CaptureResolver.CanCapture(attacker, defender, board)` — implements rank comparison, Rat↔Elephant special cases, trap rank reduction, rat-in-water invulnerability

**`Engine/`** — State mutation:
- `GameController.ApplyMove(state, move)` — produces new GameState with piece moved/captured and win conditions checked (den invasion, elimination). Does NOT validate — caller must validate first
- `MoveGenerator.GenerateLegalMoves(state, player)` — enumerates all legal moves by checking each piece's orthogonal steps + jump destinations for Lion/Tiger
- `Move` — readonly struct (From, To, Captured?)

**`AI/`** — Computer opponent:
- `MinaxEngine` — iterative deepening alpha-beta search with 2-second time limit. Move ordering prioritizes captures by victim rank
- `EvaluationFunction` — material (rank×10), trap penalty (-50), den proximity bonus, threatened-by-stronger-penalty, river-bank bonus for Lion/Tiger

### JungleGame.Tests (xunit, net8.0)
Uses xUnit with coverlet for coverage. Tests organized by area: Rules/, Engine/, AI/, Integration/. Test helpers create custom `GameState` instances via `ImmutableDictionary.CreateRange`.

### JungleGame.UI (WPF, net8.0-windows)
MVVM pattern with code-behind board rendering.

- `App.xaml.cs` — shows StartDialog, then creates MainWindow with chosen settings
- `MainViewModel` — owns GameState, MinimaxEngine instance, selection state, board-flip toggle, AI-vs-AI mode. `HandleCellClick(pos)` implements click-to-select/click-to-move. Board flip is display-only (logical game state is never mutated by flip)
- `MainWindow.xaml.cs` — renders the board on a Canvas with terrain colors (land=tan, river=blue gradient, trap=crimson, den=dark red), piece circles (blue/red gradient with animal emoji + rank number), selection highlight, legal move dots. Handles click→logical-position conversion accounting for flip
- `Converters/` — WPF value converters (BoolToVisibility, InvertBool, GameStatusToText)
- Package: no third-party dependencies beyond .NET framework

## Key design decisions

- All Core types are immutable (`readonly struct` or immutable collections). `GameController.ApplyMove` returns a new `GameState`; it never mutates the input
- Lion jumps both horizontally (4 rows across river) and vertically (3 cols); Tiger jumps only vertically(3 cols across river)
- Rat-in-water cannot be captured by any land piece; Rat can capture Rat in water; Rat cannot capture Elephant from water
- A piece on an opponent's trap has effective rank 0 for capture resolution
- The board flip feature maps visual coordinates `(c,r)` → `(6-c, 8-r)` but preserves the logical game state unchanged. `HumanPlayer` always maps to Blue internally; flip only rotates the view
- No NuGet packages in Core or UI beyond System.Collections.Immutable
