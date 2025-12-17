# Chess Game Heatmap - Quick Start Guide

## What is this?

This tool creates a visual "heatmap" of chess moves showing which moves Theodore (or any player) plays most frequently. It helps analyze opening repertoires and identify patterns in game play.

## Getting Started

### 1. Load Your Games

**Method A: Button**
- Click **"Load PGN Files"**
- Select one or more .pgn files
- Click Open

**Method B: Drag & Drop**
- Drag .pgn files from Windows Explorer
- Drop them anywhere on the window

### 2. Filter by Player

- In the **"Player Filter"** box, type: `Theodore`
- Or use multiple names: `Theodore, Smith`
- Click **"Apply Filter"**

The games list will update to show only games where Theodore played.

### 3. Explore the Heatmap

**The Board Shows:**
- Red squares = moves Theodore played
- Darker red = played less often
- Brighter red = played more often
- White text shows the move (e.g., "e4") and count (e.g., "(15)")

**Navigate:**
- **Click** a red square → See what moves followed
- **Click "← Back"** → Return to previous position
- Keep clicking to explore the entire opening repertoire

### 4. Get Details

**Hover your mouse** over a red square to see:
- Move name
- How many times it was played
- Win/Loss/Draw record
- List of games where it was played

**Double-click** a red square to see:
- Full table of all games
- Sortable by White, Black, Event, Result, Date
- Color-coded results (Green=wins, Red=losses, Yellow=draws)

## Settings

### Max Depth
- Default: 50 moves
- Increase to analyze longer games
- Decrease to focus on opening only
- Don't forget to click **"Apply Filter"** after changing!

### What Gets Saved
When you close the app, it remembers:
- Which files you loaded
- Your player filter
- Your depth setting

## Tips

1. **Multiple Players**: Use `Theodore, Smith, Johnson` to see games from any of these players

2. **Case Doesn't Matter**: `theodore`, `Theodore`, and `THEODORE` all work the same

3. **See Opponent Names**: Hover over a move to see who Theodore played against in those games

4. **Quick Overview**: The title at the top shows which position you're viewing and how many games reached it

5. **Full Game Details**: Double-click to see dates, events, and results for each game

## Example Workflow

**Analyzing Theodore's response to 1.e4:**

1. Load Theodore's game collection
2. Filter by `Theodore`
3. Click on the "e4" square (opponent's first move)
4. See all of Theodore's responses (c5, e5, c6, etc.)
5. Click on his most common response (brightest red)
6. Continue exploring the tree

## Troubleshooting

**No games showing?**
- Check that PGN files are valid
- Verify player name is spelled correctly
- Try a partial name (just "Theo" instead of "Theodore")

**Moves not appearing on board?**
- Some special moves (like castling) may not highlight
- This is normal and will be enhanced in future versions

**Colors hard to see?**
- The color scheme can be customized in the code
- Future versions will have color themes

## Keyboard Shortcuts

Currently none, but you can:
- Use Tab to move between controls
- Press Enter after typing in Player Filter to apply

## Need Help?

Check the README.md file for more detailed information about features and technical details.
