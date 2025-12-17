# Chess Game Heatmap

A C# Windows Forms application that creates a TreeView-based heatmap of chess moves showing frequency and statistics from PGN files. Designed to analyze Theodore's games and opening repertoire by color (White or Black).

## Key Features

### 1. PGN File Loading
- Load multiple PGN files at once
- Drag and drop PGN files onto the window
- Automatically remembers last loaded files in registry

### 2. Player and Color Filtering
- Filter games by player name (case-insensitive, partial match)
- **Separate filtering for White and Black** - analyze Theodore's games as White OR as Black, not both together
- Support for multiple keywords separated by commas (e.g., "Theodore, Smith")
- Radio buttons to select which color to analyze

### 3. TreeView Heatmap Display
- Hierarchical tree view showing all moves in order
- Each node shows:
  - Move notation (e.g., "1.e4")
  - Number of games with that move
  - Percentage of games (relative to parent position)
- Color-coded by frequency:
  - **Bold Red**: 80%+ of games (most frequent)
  - **Dark Red**: 50-79% of games  
  - **Dark Orange**: 20-49% of games
  - **Gray**: <20% of games (rare)
- Sorted by frequency (most common first)

### 4. Move Statistics
- **Single Click**: Shows tooltip with:
  - Move notation
  - Number of times played
  - Win/Loss/Draw statistics (from Theodore's perspective)
  - Sample games (up to 5)
  
- **Double Click**: Opens detailed popup with:
  - Complete game list in DataGridView
  - Columns: Color, White, Black, Event, Result, Date
  - Color-coded results (Green for wins, Red for losses, Yellow for draws)
  - Win/Loss/Draw summary at top

### 5. Depth Filter
- Configurable maximum depth (1-200 moves, default 50)
- Prevents very long games from cluttering the tree
- Saved in registry between sessions

### 6. Game List Display
- Shows all filtered games in text format
- Format: "White vs Black [Result]" and Event name
- Updates when filter is applied

## How to Use

### Basic Workflow

1. **Load Games**:
   - Click "Load PGN Files" or drag/drop PGN files
   - Wait for games to load

2. **Filter by Player and Color**:
   - Enter player name in "Player Filter" (e.g., "Theodore")
   - Select radio button: **White** or **Black**
   - Click "Apply Filter"

3. **Analyze the Tree**:
   - TreeView shows all moves in the filtered games
   - Red/bold moves = most frequent
   - Gray moves = rarely played
   - Expand nodes to see continuations

4. **Get Details**:
   - Click a move to see tooltip with quick stats
   - Double-click to see full game list

### Why Separate White/Black?

When Theodore plays White, we want to see:
- What openings Theodore chooses
- How opponents respond to Theodore's moves

When Theodore plays Black, we want to see:
- What openings opponents choose against Theodore
- How Theodore responds to different openings

Mixing both together would create a confusing tree where Theodore's moves and opponent moves are interleaved incorrectly.

## Example Use Cases

### Analyzing Theodore's White Repertoire
1. Filter: "Theodore", Color: **White**
2. Tree shows Theodore's first moves (1.e4, 1.d4, etc.)
3. Each branch shows opponent responses
4. Continuing deeper shows Theodore's subsequent moves

### Analyzing Opponents' Moves Against Theodore (Black)
1. Filter: "Theodore", Color: **Black**  
2. Tree shows opponents' first moves against Theodore
3. Each branch shows Theodore's responses
4. Continuing deeper shows how games develop

### Finding Rare Lines
1. Apply filter
2. Look for gray-colored moves (infrequent)
3. Double-click to see which games used that line
4. Identify if these are losses/unusual positions

## Registry Settings

Stored in: `HKEY_CURRENT_USER\Software\GameHeatmap`

- **PGNFiles**: List of loaded file paths (pipe-separated)
- **PlayerFilter**: Last used player filter keywords
- **MaxDepth**: Maximum depth for game analysis
- **PlayingWhite**: Whether filtering for White or Black

## Color Scheme

The color scheme is based on relative frequency at each level:

- **Bold Red** (80%+): This move is played in most games from this position
- **Dark Red** (50-79%): Frequently played
- **Dark Orange** (20-49%): Occasionally played
- **Gray** (<20%): Rarely played

Colors are calculated relative to siblings, so a "Red" move at depth 1 means it's the most common opening move, while a "Red" move at depth 10 means it's the most common move at that specific position.

## Files

- **Form1.cs**: Main application form with TreeView
- **PGNParser.cs**: PGN file parser from OTBFlashCards
- **RegistryUtils.cs**: Registry management utilities
- **HeatmapBuilder.cs**: Tree building and frequency tracking
- **GameDetailsForm.cs**: Popup for detailed game information

## Building

- .NET 9.0 Windows Forms application
- Open in Visual Studio 2022 and build
- Or use: `dotnet build`

## Notes

- The application does not modify input PGN files
- All settings persist between sessions via registry
- TreeView can be expanded/collapsed to focus on specific lines
- Double-click any move to see which games used it
