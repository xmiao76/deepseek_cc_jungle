Jungle (Dou Shou Qi / 鬥獸棋)
===============================

A Windows desktop board game with WPF GUI and AI opponent.

System Requirements
-------------------
- Windows 10 or later (x64)
- .NET 8.0 runtime (self-contained, no separate install needed)

How to Run
----------
Double-click JungleGame.UI.exe to start the game.

Game Rules
----------
- Each player controls 8 animal pieces ranked 1 (Rat) through 8 (Elephant)
- Higher ranked pieces capture lower ranked ones
- Special rules: Rat can capture Elephant, Elephant cannot capture Rat
- Rat can enter river water squares; other animals cannot
- Lion can jump across the river horizontally (along rows) and vertically (along columns)
- Tiger can jump across the river vertically (along columns) only
- A piece on an opponent's trap has effective rank 0
- Win by entering the opponent's den or capturing all enemy pieces

Controls
--------
- Click a piece to select it, then click a highlighted destination to move
- Blue moves first (bottom), Red moves second (top)
- Toggle "Flip Board" to rotate the view
- Enable "AI vs AI" to watch the computer play itself

Notes
-----
- The AI opponent uses Minimax with iterative deepening alpha-beta search
- Move time limit: approximately 2 seconds per AI turn
- Game saves are not supported in this version

Build Info
----------
- Version: 1.0.0
- Built with .NET 8.0
- Code agent: Claude Code (Anthropic CLI)
- AI model: DeepSeek V4 Pro (1M context window)
