# GameHeatmap Testing Checklist

## Pre-Testing Setup
- [ ] Build project successfully (no errors)
- [ ] Have at least one PGN file with games available
- [ ] Know a player name that appears in the PGN file(s)

## Feature Testing

### 1. PGN File Loading
- [ ] Click "Load PGN Files" button
- [ ] Select a single PGN file → Verify games load
- [ ] Click "Load PGN Files" again
- [ ] Select multiple PGN files → Verify all games load
- [ ] Check status label shows correct game count
- [ ] Check games list shows game headers

### 2. Drag and Drop
- [ ] Drag a single .pgn file onto the window
- [ ] Verify games load
- [ ] Drag multiple .pgn files onto the window
- [ ] Verify all games load
- [ ] Try dragging a non-PGN file → Should be rejected

### 3. Player Filtering
- [ ] Enter a player name (e.g., "Theodore")
- [ ] Click "Apply Filter"
- [ ] Verify status shows filtered count vs total
- [ ] Verify games list updates to show only matching games
- [ ] Try partial match (e.g., "Theo" for "Theodore")
- [ ] Try case variations (e.g., "theodore", "THEODORE")
- [ ] Try multiple keywords (e.g., "Theodore, Smith")
- [ ] Verify all matching games appear

### 4. Depth Filter
- [ ] Change depth from 50 to 20
- [ ] Click "Apply Filter"
- [ ] Verify heatmap updates
- [ ] Change depth to 100
- [ ] Click "Apply Filter"
- [ ] Verify heatmap updates
- [ ] Try minimum value (1)
- [ ] Try maximum value (200)

### 5. Heatmap Visualization
- [ ] Verify red squares appear on board
- [ ] Verify brighter red = more frequent moves
- [ ] Verify white text shows move notation
- [ ] Verify numbers show game count
- [ ] Check that multiple moves to same square show "Multiple (N)"

### 6. Interactive Tree Navigation
- [ ] Click on an opening move (e.g., e4)
- [ ] Verify board updates to show next moves
- [ ] Verify title updates with current move
- [ ] Verify "← Back" button becomes enabled
- [ ] Click "← Back"
- [ ] Verify return to previous position
- [ ] Navigate several moves deep
- [ ] Use Back button multiple times
- [ ] Verify you return to opening position

### 7. Mouse Hover Tooltips
- [ ] Hover over a red square
- [ ] Verify tooltip appears showing:
  - [ ] Move notation
  - [ ] Number of games
  - [ ] Win/Loss/Draw statistics
  - [ ] List of games (up to 5)
  - [ ] "...and X more" if applicable
- [ ] Move mouse away
- [ ] Verify tooltip disappears
- [ ] Hover over different squares
- [ ] Verify each shows correct information

### 8. Double-Click Game Details
- [ ] Double-click a red square
- [ ] Verify popup window appears
- [ ] Check popup shows:
  - [ ] Move name in title
  - [ ] Statistics (wins/losses/draws) at top
  - [ ] DataGridView with all games
  - [ ] Columns: Color, White, Black, Event, Result, Date
- [ ] Verify results are color-coded:
  - [ ] Green for wins
  - [ ] Red for losses  
  - [ ] Yellow for draws
- [ ] Try clicking column headers to sort
- [ ] Close popup
- [ ] Double-click a different square
- [ ] Verify correct games shown

### 9. Registry Persistence
- [ ] Load PGN files
- [ ] Set player filter to "Theodore"
- [ ] Set depth to 30
- [ ] Close application
- [ ] Reopen application
- [ ] Verify:
  - [ ] Same files are loaded automatically
  - [ ] Player filter is "Theodore"
  - [ ] Depth is 30
  - [ ] Games are already filtered

### 10. Edge Cases
- [ ] Load PGN with 0 games → Should show "No games loaded"
- [ ] Filter with no matches → Should show "0 of X games"
- [ ] Click on square with no move → Nothing should happen
- [ ] Load very large PGN (100+ games) → Should handle smoothly
- [ ] Enter empty player filter → Should show all games
- [ ] Navigate to end of game tree → Should show no children

### 11. UI Responsiveness
- [ ] Verify all buttons are clickable
- [ ] Verify text boxes are editable
- [ ] Verify numeric control accepts keyboard input
- [ ] Verify window is resizable (if applicable)
- [ ] Verify controls are properly aligned
- [ ] Verify no text is cut off

### 12. Error Handling
- [ ] Try loading a corrupted PGN file
- [ ] Verify error message appears
- [ ] Verify app doesn't crash
- [ ] Try loading a non-PGN file via button
- [ ] Verify appropriate error or rejection

## Performance Testing
- [ ] Load 50+ games → Time should be < 5 seconds
- [ ] Filter 50+ games → Should be instant
- [ ] Click through 10 moves → Should be smooth
- [ ] Open game details with 20+ games → Should display quickly

## Visual Testing
- [ ] Check board square colors (light/dark)
- [ ] Check red gradient is visible
- [ ] Check white text is readable on red background
- [ ] Check labels are properly sized
- [ ] Check status messages are visible
- [ ] Check no UI elements overlap

## Known Limitations (Expected Behavior)
- [ ] Castling moves may not be highlighted on board ✓ Expected
- [ ] Very complex variations may be simplified ✓ Expected
- [ ] No actual piece images on board ✓ Expected
- [ ] Board doesn't show piece positions ✓ Expected

## Testing Sign-Off

Tester: _______________
Date: _______________
Version: 1.0

Pass/Fail: _______________

Notes:
_______________________________________
_______________________________________
_______________________________________
