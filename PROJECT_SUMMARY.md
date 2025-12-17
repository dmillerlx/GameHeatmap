# GameHeatmap Project Summary

## Project Structure

```
GameHeatmap/
├── GameHeatmap.sln
├── README.md
└── GameHeatmap/
    ├── GameHeatmap.csproj
    ├── Program.cs              (Entry point)
    ├── Form1.cs                (Main UI and logic)
    ├── Form1.Designer.cs       (Form designer code)
    ├── Form1.resx              (Form resources)
    ├── PGNParser.cs            (PGN file parser - from OTBFlashCards)
    ├── RegistryUtils.cs        (Registry management)
    ├── HeatmapBuilder.cs       (Heatmap tree building logic)
    └── GameDetailsForm.cs      (Game details popup)
```

## Implementation Details

### Core Features Implemented

1. **PGN Loading** ✓
   - Load multiple PGN files via button
   - Drag and drop support for entire window
   - Remembers last loaded files in registry
   - No modification of input files

2. **Player Filtering** ✓
   - Case-insensitive partial match
   - Multiple keywords with comma separation
   - Example: "Theodore, Smith" matches games where either player has these names
   - Automatically detects whether filtered player is white or black

3. **Game Display** ✓
   - Text box shows game headers (White vs Black, Event)
   - Updates when filter is applied
   - Shows filtered game count vs total

4. **Interactive Heatmap Tree** ✓
   - 8x8 chess board visualization
   - Click to navigate through moves
   - Back button to return to previous positions
   - Color-coded frequency (Black → Red gradient with bold text)
   - Handles multiple moves to same square

5. **Mouse Over Info** ✓
   - Displays move notation (SAN)
   - Number of times played
   - Win/Loss/Draw statistics
   - List of games (up to 5) with:
     - White vs Black
     - Event name
   - Shows "...and X more" if more games exist

6. **Double-Click Popup** ✓
   - DataGridView with sortable columns:
     - Color (which color the filtered player played)
     - White player name
     - Black player name
     - Event
     - Result (color-coded: green=win, red=loss, yellow=draw)
     - Date
   - Shows statistics (wins/losses/draws) at top
   - Modal dialog

7. **Depth Filter** ✓
   - Configurable via NumericUpDown control
   - Range: 1-200 moves
   - Default: 50 moves
   - Stored in registry
   - Global filter applied to all games

8. **Registry Settings** ✓
   - Stored in: HKEY_CURRENT_USER\Software\GameHeatmap
   - PGNFiles: Pipe-separated list of file paths
   - PlayerFilter: Last used player name(s)
   - MaxDepth: Maximum depth setting

### Color Scheme

**Heatmap Colors** (easy to change in code):
- Base gradient: Black (0% frequency) → Bright Red (100% frequency)
- Calculated per position relative to maximum at that depth
- Formula: `Color.FromArgb(255 * intensity, 0, 0)`
- Bold white text on all moves for visibility

To change colors, modify the `GetHeatmapColor` method in Form1.cs:
```csharp
private Color GetHeatmapColor(int frequency, int maxFrequency)
{
    if (maxFrequency == 0) return Color.Black;
    float intensity = (float)frequency / maxFrequency;
    
    // CUSTOMIZE THIS LINE:
    int red = (int)(255 * intensity);
    int green = 0;  // Change for different colors
    int blue = 0;   // Change for different colors
    
    return Color.FromArgb(red, green, blue);
}
```

### Key Classes

**PgnGame**: Stores game tags and move tree
**MoveNode**: Individual move in game tree with variations
**HeatmapNode**: Node in heatmap tree with game frequency
**HeatmapBuilder**: Builds merged tree from multiple games
**GameDetailsForm**: Popup showing detailed game information
**RegistryUtils**: Static helper for registry operations

### Technical Notes

1. **PGN Parser**: Reused from OTBFlashCards project
   - Handles variations and nested lines
   - Supports comments and NAG annotations
   - Robust tokenization

2. **Move Grouping**: Multiple moves to same square are combined
   - Shows "Multiple (N)" if >1 move to square
   - Click/double-click uses most frequent move

3. **Player Color Detection**: 
   - Counts keyword matches in White vs Black across all games
   - Uses majority to determine which color filtered player plays
   - Affects statistics display (wins/losses perspective)

4. **Destination Square Parsing**:
   - Simplified SAN parser
   - Extracts last two characters (file + rank)
   - Handles basic moves (doesn't highlight castling)
   - Can be enhanced for special moves

### Limitations & Future Enhancements

**Current Limitations**:
- Doesn't show actual piece positions on board
- Castling moves not highlighted on board
- No UCI or FEN import
- No game export or statistics export

**Suggested Enhancements**:
- Add actual chess piece images on squares
- Show full board position at each node
- Add move recommendation based on statistics
- Export heatmap data to CSV/JSON
- Add win percentage colorization
- ECO code classification
- Multiple player comparison
- Time controls filtering
- Save/load analysis sessions

## Testing Recommendations

1. Load a PGN file with multiple games
2. Filter by "Theodore" (or another player name)
3. Verify game count in status label
4. Check that games list shows filtered games
5. Click on opening moves (e.g., e4, d4)
6. Verify back button works
7. Hover over moves to see tooltips
8. Double-click to see game details grid
9. Change depth filter and reapply
10. Close and reopen app - verify settings persist
11. Drag and drop a PGN file
12. Test multiple keywords: "Theodore, Smith"

## Build Instructions

1. Open GameHeatmap.sln in Visual Studio 2022
2. Ensure .NET 9.0 SDK is installed
3. Build → Build Solution (Ctrl+Shift+B)
4. Run (F5)

Or from command line:
```bash
cd C:\data\chess\apps\GameHeatmap
dotnet build
dotnet run --project GameHeatmap\GameHeatmap.csproj
```

## Files Not Modified

As requested, no input PGN files are modified. The application is read-only with respect to game data.
