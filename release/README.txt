Jungle (Dou Shou Qi / 鬥獸棋)
===============================

A Windows desktop board game with WPF GUI and AI opponent, shipped as a
single self-contained executable.

System Requirements
-------------------
- Windows 10 or later (x64)
- No installation needed (self-contained single file)

How to Run
----------
Double-click JungleGame.UI.exe to start the game. Pick who moves first,
the AI difficulty, and optionally AI vs AI (watch mode).

Game Rules
----------
- Each player controls 8 animal pieces ranked 1 (Rat) through 8 (Elephant)
- Higher ranked pieces capture lower ranked ones
- Special rules: Rat can capture Elephant from land, Elephant cannot capture Rat
- Rat vs Rat: rats capture each other on land or in water
- Only the Rat can enter river water squares; a rat in water can only be
  captured by another rat
- Lion jumps across the river both ways (row-changing over the 3-square-wide
  river and column-changing over the 2-square-wide river); Tiger jumps only
  column-changing
- A jump is blocked by any rat in the water along the path
- A piece on an opponent's trap has effective rank 0 (anything can capture it)
- Win by entering the opponent's den or capturing all enemy pieces
- A player with no legal moves loses
- If the same position occurs for the third time, the game is a draw

Controls
--------
- Click a piece to select it, then click a highlighted destination to move
- Keyboard: arrow keys move the cursor on the board, Enter/Space selects or
  moves, Escape deselects
- Blue moves first (bottom), Red moves second (top)
- "Flip Board (180°)" rotates the view (display only)
- "New Game" opens the setup dialog again; starting a new game cancels the
  AI's in-progress thinking immediately
- The board scales with the window at any resolution or DPI setting
- The last move is highlighted in gold; piece moves are animated

AI Difficulty
-------------
- Easy:   ~0.3 seconds per AI move
- Medium: ~1 second per AI move (default)
- Hard:   ~2 seconds per AI move (maximum)

Notes
-----
- The AI uses iterative-deepening alpha-beta search (PVS) with aspiration
  windows, late move reductions, null-move and futility pruning, killer/history
  move ordering, quiescence search, a bucketed transposition table,
  mate-distance scoring, repetition detection, and positional evaluation terms
  for den threats, trap handling, and river play. It typically reaches 11+
  plies in 2 seconds.
- The difficulty setting only changes the per-move time budget; the engine
  keeps its search memory across games.
- Game saves are not supported in this version.

Build Info
----------
- Version: 1.1.0
- Built with .NET 8.0 (self-contained, win-x64, single file)
- Code agent: Claude Code (Anthropic CLI)
- AI model: DeepSeek V4 Pro (1M context window)
