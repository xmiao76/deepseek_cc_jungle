Jungle (Dou Shou Qi / 鬥獸棋)
===============================

A Windows desktop board game where a human plays against the computer AI.

Launch
------
Double-click JungleGame.UI.exe to start.

Gameplay
--------
- Blue moves first. Click a piece, then click a highlighted square to move.
- Capture enemy pieces by moving onto their square (higher rank wins).
- Win by moving any piece into the opponent's Den or capturing all enemy pieces.

River Jumping (Lion & Tiger)
----------------------------
- Rivers are the blue squares in columns b-c and e-f, rows 4-6.
- Tiger can jump VERTICALLY across the river (along a column).
- Lion can jump both HORIZONTALLY and VERTICALLY.
- To jump: move your Tiger/Lion to a square directly next to the river,
  then click it — the jump destination across the river will highlight.
- Tiger/Lion must be on a river column (b, c, e, or f) to jump.
- A Rat in the water blocks the jump.

Special Rules
-------------
- Rat (1) can enter river squares and can capture Elephant (8).
- Elephant cannot capture Rat.
- A piece on an enemy trap can be captured by any piece regardless of rank.

Controls
--------
- New Game: Start a new game (choose who moves first).
- Flip Board: Rotate the board 180 degrees for a different view angle.
- AI vs AI: Check the box to watch two AI opponents play.

Notes
-----
- Built with .NET 8 and WPF.
- AI uses PVS alpha-beta search with quiescence search at 4-second time limit.
- Game model: DeepSeek V4 Pro. Code agent: Claude Code (Anthropic).
