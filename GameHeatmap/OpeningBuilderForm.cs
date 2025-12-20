using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace GameHeatmap
{
    public class OpeningBuilderForm : Form
    {
        // UI Controls
        private Panel leftPanel;
        private Panel rightPanel;
        private TextBox txtMainline;
        private Button btnLoadMainline;
        private TreeView treeVariations;
        private Button btnGenerate;
        private Button btnSave;
        private Button btnClear;
        
        // Configuration controls
        private NumericUpDown numMaxBranches;
        private NumericUpDown numPercentThreshold;
        private NumericUpDown numMaxDepth;
        private NumericUpDown numStartMove;
        private CheckBox chkUseTheodoreGames;
        private CheckBox chkUseDatabaseGames;
        private Label lblTheodoreColor;
        
        // Data
        private MoveFrequencyTree? theodoreTree;
        private MoveFrequencyTree? databaseTree;
        private bool theodorePlaysWhite;
        private PgnGame? mainlineGame;
        private string generatedPgn = "";
        private string lastPgnPath = "";
        
        public OpeningBuilderForm(MoveFrequencyTree? theodoreTree, MoveFrequencyTree? databaseTree, bool theodorePlaysWhite)
        {
            this.theodoreTree = theodoreTree;
            this.databaseTree = databaseTree;
            this.theodorePlaysWhite = theodorePlaysWhite;
            
            InitializeComponent();
            AutoLoadLastPgn();
        }
        
        private void AutoLoadLastPgn()
        {
            // Try to load last PGN from registry
            string lastPath = RegistryUtils.GetString("OpeningBuilder_LastPGN", "");
            if (!string.IsNullOrEmpty(lastPath) && File.Exists(lastPath))
            {
                LoadMainlineFile(lastPath);
            }
        }
        
        private void InitializeComponent()
        {
            this.Text = "Opening Builder";
            this.Size = new Size(1400, 800);
            this.StartPosition = FormStartPosition.CenterParent;
            
            // Split container for left/right panes
            SplitContainer splitContainer = new SplitContainer
            {
                Dock = DockStyle.Fill,
                SplitterDistance = 700,  // Center of 1400px form
                Orientation = Orientation.Vertical
            };
            
            // LEFT PANEL - Mainline Input
            leftPanel = new Panel { Dock = DockStyle.Fill };
            
            Label lblLeft = new Label
            {
                Text = "MAINLINE (Drag & Drop PGN)",
                Dock = DockStyle.Top,
                Height = 30,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(5),
                BackColor = Color.LightBlue
            };
            leftPanel.Controls.Add(lblLeft);
            
            txtMainline = new TextBox
            {
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Dock = DockStyle.Fill,
                Font = new Font("Consolas", 10),
                AllowDrop = true
            };
            txtMainline.DragEnter += TxtMainline_DragEnter;
            txtMainline.DragDrop += TxtMainline_DragDrop;
            leftPanel.Controls.Add(txtMainline);
            txtMainline.BringToFront();
            
            Panel leftButtonPanel = new Panel { Dock = DockStyle.Top, Height = 40 };
            btnLoadMainline = new Button
            {
                Text = "Load PGN File",
                Location = new Point(5, 5),
                Size = new Size(120, 30)
            };
            btnLoadMainline.Click += BtnLoadMainline_Click;
            leftButtonPanel.Controls.Add(btnLoadMainline);
            leftPanel.Controls.Add(leftButtonPanel);
            leftButtonPanel.BringToFront();
            
            splitContainer.Panel1.Controls.Add(leftPanel);
            
            // RIGHT PANEL - Variation Tree
            rightPanel = new Panel { Dock = DockStyle.Fill };
            
            Label lblRight = new Label
            {
                Text = "GENERATED OPENING TREE",
                Dock = DockStyle.Top,
                Height = 30,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(5),
                BackColor = Color.LightGreen
            };
            rightPanel.Controls.Add(lblRight);
            
            treeVariations = new TreeView
            {
                Dock = DockStyle.Fill,
                Font = new Font("Consolas", 10),
                ShowLines = true,
                ShowPlusMinus = true
            };
            rightPanel.Controls.Add(treeVariations);
            treeVariations.BringToFront();
            
            splitContainer.Panel2.Controls.Add(rightPanel);
            
            // TOP PANEL - Configuration
            Panel topPanel = new Panel { Dock = DockStyle.Top, Height = 140, Padding = new Padding(10) };
            
            // Theodore plays as
            lblTheodoreColor = new Label
            {
                Text = $"Theodore plays: {(theodorePlaysWhite ? "WHITE" : "BLACK")}",
                Location = new Point(10, 10),
                Size = new Size(200, 25),
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = theodorePlaysWhite ? Color.White : Color.Black,
                BackColor = theodorePlaysWhite ? Color.DarkGreen : Color.White,
                TextAlign = ContentAlignment.MiddleCenter,
                BorderStyle = BorderStyle.FixedSingle
            };
            topPanel.Controls.Add(lblTheodoreColor);
            
            // Max branches
            Label lblMaxBranches = new Label
            {
                Text = "Max Branches:",
                Location = new Point(220, 12),
                Size = new Size(100, 20)
            };
            topPanel.Controls.Add(lblMaxBranches);
            
            numMaxBranches = new NumericUpDown
            {
                Location = new Point(330, 10),
                Size = new Size(60, 25),
                Minimum = 1,
                Maximum = 10,
                Value = 3
            };
            topPanel.Controls.Add(numMaxBranches);
            
            // Percentage threshold
            Label lblPercent = new Label
            {
                Text = "% Threshold:",
                Location = new Point(400, 12),
                Size = new Size(100, 20)
            };
            topPanel.Controls.Add(lblPercent);
            
            numPercentThreshold = new NumericUpDown
            {
                Location = new Point(510, 10),
                Size = new Size(60, 25),
                Minimum = 50,
                Maximum = 100,
                Value = 90
            };
            topPanel.Controls.Add(numPercentThreshold);
            
            // Max depth
            Label lblMaxDepth = new Label
            {
                Text = "Max Depth:",
                Location = new Point(580, 12),
                Size = new Size(80, 20)
            };
            topPanel.Controls.Add(lblMaxDepth);
            
            numMaxDepth = new NumericUpDown
            {
                Location = new Point(665, 10),
                Size = new Size(60, 25),
                Minimum = 1,
                Maximum = 50,
                Value = 10
            };
            topPanel.Controls.Add(numMaxDepth);
            
            // Start branching after move
            Label lblStartMove = new Label
            {
                Text = "Start branch after:",
                Location = new Point(735, 12),
                Size = new Size(120, 20)
            };
            topPanel.Controls.Add(lblStartMove);
            
            numStartMove = new NumericUpDown
            {
                Location = new Point(860, 10),
                Size = new Size(60, 25),
                Minimum = 1,
                Maximum = 50,
                Value = 4
            };
            topPanel.Controls.Add(numStartMove);
            
            // Checkboxes
            chkUseTheodoreGames = new CheckBox
            {
                Text = "Use Theodore's Games",
                Location = new Point(10, 45),
                Size = new Size(200, 25),
                Checked = theodoreTree != null
            };
            topPanel.Controls.Add(chkUseTheodoreGames);
            
            chkUseDatabaseGames = new CheckBox
            {
                Text = "Use Database Games",
                Location = new Point(220, 45),
                Size = new Size(200, 25),
                Checked = databaseTree != null
            };
            topPanel.Controls.Add(chkUseDatabaseGames);
            
            // Buttons
            btnGenerate = new Button
            {
                Text = "Generate Opening Tree",
                Location = new Point(10, 80),
                Size = new Size(180, 40),
                BackColor = Color.LightGreen,
                Font = new Font("Segoe UI", 10, FontStyle.Bold)
            };
            btnGenerate.Click += BtnGenerate_Click;
            topPanel.Controls.Add(btnGenerate);
            
            btnSave = new Button
            {
                Text = "Save to PGN",
                Location = new Point(200, 80),
                Size = new Size(120, 40),
                Enabled = false
            };
            btnSave.Click += BtnSave_Click;
            topPanel.Controls.Add(btnSave);
            
            btnClear = new Button
            {
                Text = "Clear",
                Location = new Point(330, 80),
                Size = new Size(100, 40)
            };
            btnClear.Click += BtnClear_Click;
            topPanel.Controls.Add(btnClear);
            
            this.Controls.Add(splitContainer);
            this.Controls.Add(topPanel);
        }
        
        private void TxtMainline_DragEnter(object? sender, DragEventArgs e)
        {
            if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
                e.Effect = DragDropEffects.Copy;
        }
        
        private void TxtMainline_DragDrop(object? sender, DragEventArgs e)
        {
            if (e.Data?.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
            {
                LoadMainlineFile(files[0]);
            }
        }
        
        private void BtnLoadMainline_Click(object? sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Filter = "PGN Files (*.pgn)|*.pgn|All Files (*.*)|*.*";
                ofd.Title = "Load Mainline PGN";
                
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    LoadMainlineFile(ofd.FileName);
                }
            }
        }
        
        private void LoadMainlineFile(string filePath)
        {
            try
            {
                string content = File.ReadAllText(filePath);
                txtMainline.Text = content;
                
                // Parse the PGN
                var parser = new PgnParser();
                var games = parser.ParseGames(content);
                
                if (games.Count > 0)
                {
                    mainlineGame = games[0];
                    lastPgnPath = filePath;
                    RegistryUtils.SetString("OpeningBuilder_LastPGN", filePath);
                    
                    MessageBox.Show($"Loaded mainline with {CountMoves(mainlineGame.MoveTreeRoot)} plies",
                        "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show("No game found in PGN file", "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading file: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        
        private int CountMoves(MoveNode node)
        {
            int count = 0;
            var current = node;
            while (current.NextMoves.Count > 0)
            {
                count++;
                current = current.NextMoves[0];
            }
            return count;
        }
        
        private void BtnGenerate_Click(object? sender, EventArgs e)
        {
            if (mainlineGame == null)
            {
                MessageBox.Show("Please load a mainline PGN first", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            
            if (!chkUseTheodoreGames.Checked && !chkUseDatabaseGames.Checked)
            {
                MessageBox.Show("Please select at least one data source (Theodore's games or Database)",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            
            GenerateOpeningTree();
        }
        
        private void GenerateOpeningTree()
        {
            treeVariations.Nodes.Clear();
            
            int maxBranches = (int)numMaxBranches.Value;
            int percentThreshold = (int)numPercentThreshold.Value;
            int maxDepth = (int)numMaxDepth.Value * 2; // Convert full moves to plies
            int startMove = (int)numStartMove.Value;
            
            // Create output tree by copying mainline
            var outputRoot = CopyMoveTree(mainlineGame!.MoveTreeRoot);
            
            // Build variations by traversing the mainline and adding opponent responses
            BuildVariations(outputRoot, 0, maxDepth, maxBranches, percentThreshold, startMove);
            
            // Display in tree view
            DisplayTreeInView(outputRoot);
            
            // Generate PGN
            generatedPgn = GeneratePgn(outputRoot);
            btnSave.Enabled = true;
            
            MessageBox.Show("Opening tree generated successfully!\n\nExpand the tree to see variations.",
                "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        
        private MoveNode CopyMoveTree(MoveNode source, bool isWhiteToMove = true)
        {
            var copy = new MoveNode
            {
                MoveNumber = source.MoveNumber,
                San = source.San,
                Comment = source.Comment,
                // If it's white to move and this node has a move, the move is by white
                // After white moves, isWhiteTurn = false (black to move next)
                // If this is the root dummy node (no San), don't change isWhiteTurn
                isWhiteTurn = string.IsNullOrEmpty(source.San) ? false : !isWhiteToMove
            };
            
            // After this move, opposite color to move
            bool nextIsWhiteToMove = string.IsNullOrEmpty(source.San) ? true : !isWhiteToMove;
            
            foreach (var child in source.NextMoves)
            {
                var childCopy = CopyMoveTree(child, nextIsWhiteToMove);
                childCopy.Parent = copy;
                copy.NextMoves.Add(childCopy);
            }
            
            return copy;
        }
        
        private void BuildVariations(MoveNode node, int depth, int maxDepth, int maxBranches, int percentThreshold, int startMove)
        {
            // Stop at max depth
            if (depth >= maxDepth)
                return;
            
            // No mainline to continue - need to add moves from database
            if (node.NextMoves.Count == 0)
            {
                // Determine whose turn it is (calculate early before later declarations)
                bool isWhiteToMoveNow = (depth % 2 == 0);
                bool isTheodoreMoveNow = (theodorePlaysWhite && isWhiteToMoveNow) || (!theodorePlaysWhite && !isWhiteToMoveNow);
                
                // Get the position sequence
                var moveSequence = GetMoveSequence(node);
                var movesFromDB = new List<(string san, int frequency)>();
                
                if (isTheodoreMoveNow)
                {
                    // Theodore's turn - find single best move from both databases
                    var theodoreMoves = new List<(string san, int frequency)>();
                    var dbMoves = new List<(string san, int frequency)>();
                    
                    // Check Theodore's games
                    if (chkUseTheodoreGames.Checked && theodoreTree != null)
                    {
                        theodoreMoves = FindMovesInTree(theodoreTree.Root, moveSequence, isWhiteToMoveNow);
                    }
                    
                    // Check database
                    if (chkUseDatabaseGames.Checked && databaseTree != null)
                    {
                        dbMoves = FindMovesInTree(databaseTree.Root, moveSequence, isWhiteToMoveNow);
                    }
                    
                    // Combine moves from both sources
                    var combinedMoves = new Dictionary<string, (int theoFreq, int dbFreq)>();
                    
                    foreach (var (san, freq) in theodoreMoves)
                    {
                        combinedMoves[san] = (freq, 0);
                    }
                    
                    foreach (var (san, freq) in dbMoves)
                    {
                        if (combinedMoves.ContainsKey(san))
                            combinedMoves[san] = (combinedMoves[san].theoFreq, freq);
                        else
                            combinedMoves[san] = (0, freq);
                    }
                    
                    if (combinedMoves.Count > 0)
                    {
                        // Take the most popular move (by total frequency)
                        var bestMove = combinedMoves
                            .OrderByDescending(m => m.Value.theoFreq + m.Value.dbFreq)
                            .First();
                        
                        // Determine source
                        string source;
                        int totalFreq;
                        if (bestMove.Value.theoFreq > 0 && bestMove.Value.dbFreq > 0)
                        {
                            source = "Theodore+DB";
                            totalFreq = bestMove.Value.theoFreq + bestMove.Value.dbFreq;
                        }
                        else if (bestMove.Value.theoFreq > 0)
                        {
                            source = "Theodore";
                            totalFreq = bestMove.Value.theoFreq;
                        }
                        else
                        {
                            source = "Database";
                            totalFreq = bestMove.Value.dbFreq;
                        }
                        
                        int moveNum = (depth / 2) + 1;
                        
                        var bestMoveNode = new MoveNode
                        {
                            MoveNumber = moveNum,
                            San = bestMove.Key,
                            Comment = $"{source}: {totalFreq} games",
                            Parent = node,
                            isWhiteTurn = !isWhiteToMoveNow
                        };
                        
                        node.NextMoves.Add(bestMoveNode);
                        BuildVariations(bestMoveNode, depth + 1, maxDepth, maxBranches, percentThreshold, startMove);
                        return;
                    }
                }
                else
                {
                    // Opponent's turn - find multiple variations (like normal opponent moves)
                    int moveNum = (depth / 2) + 1;
                    if (moveNum > startMove)  // Start branching AFTER this move number
                    {
                        // Check Theodore's games
                        if (chkUseTheodoreGames.Checked && theodoreTree != null)
                        {
                            var theodoreMoves = FindMovesInTree(theodoreTree.Root, moveSequence, isWhiteToMoveNow);
                            movesFromDB.AddRange(theodoreMoves);
                        }
                        
                        // Check database
                        if (chkUseDatabaseGames.Checked && databaseTree != null)
                        {
                            var dbMoves = FindMovesInTree(databaseTree.Root, moveSequence, isWhiteToMoveNow);
                            movesFromDB.AddRange(dbMoves);
                        }
                        
                        if (movesFromDB.Count > 0)
                        {
                            // Get top moves up to maxBranches and threshold
                            var sortedMoves = movesFromDB
                                .GroupBy(m => m.san)
                                .Select(g => (san: g.Key, freq: g.Sum(x => x.frequency)))
                                .OrderByDescending(m => m.freq)
                                .ToList();
                            
                            int totalGames = sortedMoves.Sum(m => m.freq);
                            int cumulativeGames = 0;
                            
                            foreach (var move in sortedMoves)
                            {
                                cumulativeGames += move.freq;
                                double percentage = (cumulativeGames * 100.0) / totalGames;
                                
                                var variationNode = new MoveNode
                                {
                                    MoveNumber = moveNum,
                                    San = move.san,
                                    Comment = $"Database: {move.freq} games",
                                    Parent = node,
                                    isWhiteTurn = !isWhiteToMoveNow
                                };
                                
                                node.NextMoves.Add(variationNode);
                                BuildVariations(variationNode, depth + 1, maxDepth, maxBranches, percentThreshold, startMove);
                                
                                if (node.NextMoves.Count >= maxBranches || percentage >= percentThreshold)
                                    break;
                            }
                            return;
                        }
                    }
                }
                
                // Mark dead ends
                if (depth < maxDepth && !string.IsNullOrEmpty(node.San))
                {
                    int currentMoveNum = (depth / 2) + 1;
                    if (string.IsNullOrEmpty(node.Comment))
                        node.Comment = $"[No data after move {currentMoveNum}]";
                    else
                        node.Comment += $" [No data]";
                }
                return;
            }
            
            // Get mainline move (first child)
            var mainlineMove = node.NextMoves[0];
            
            // Determine whose turn it is (based on depth)
            bool isWhiteToMove = (depth % 2 == 0);
            bool isTheodoreMove = (theodorePlaysWhite && isWhiteToMove) || (!theodorePlaysWhite && !isWhiteToMove);
            
            int moveNumber = (depth / 2) + 1;
            
            // Only add variations for OPPONENT moves (not Theodore's moves)
            if (!isTheodoreMove && moveNumber > startMove)  // Start branching AFTER this move number
            {
                // Find alternative moves for the opponent
                var alternativeMoves = FindOpponentMoves(node, mainlineMove.San, maxBranches, percentThreshold, isWhiteToMove);
                
                // DEBUG: Show if no moves found
                if (alternativeMoves.Count == 0 && depth < maxDepth - 2)
                {
                    if (string.IsNullOrEmpty(mainlineMove.Comment))
                        mainlineMove.Comment = "[No opponent alternatives found]";
                }
                
                // Add variations (excluding mainline)
                foreach (var (san, source, frequency) in alternativeMoves)
                {
                    if (san != mainlineMove.San)
                    {
                        var variationNode = new MoveNode
                        {
                            MoveNumber = mainlineMove.MoveNumber,
                            San = san,
                            Comment = $"{source}: {frequency} games",
                            Parent = node,
                            isWhiteTurn = !isWhiteToMove
                        };
                        
                        node.NextMoves.Add(variationNode);
                        
                        // Continue building this variation recursively
                        if (depth + 1 < maxDepth)
                        {
                            BuildVariations(variationNode, depth + 1, maxDepth, maxBranches, percentThreshold, startMove);
                        }
                    }
                }
                
                // Annotate mainline - SKIP THIS, keep mainline without comments so it stays black/bold
                // if (alternativeMoves.Count > 0)
                // {
                //     var mainlineInfo = alternativeMoves.FirstOrDefault(m => m.san == mainlineMove.San);
                //     if (mainlineInfo != default && string.IsNullOrEmpty(mainlineMove.Comment))
                //     {
                //         mainlineMove.Comment = $"{mainlineInfo.source}: {mainlineInfo.frequency} games";
                //     }
                // }
            }
            
            // ALWAYS continue mainline
            BuildVariations(mainlineMove, depth + 1, maxDepth, maxBranches, percentThreshold, startMove);
        }

        
        private List<(string san, string source, int frequency)> FindOpponentMoves(
            MoveNode currentPosition, string mainlineSan, int maxBranches, int percentThreshold, bool isWhiteToMove)
        {
            var moves = new Dictionary<string, (string source, int frequency)>();
            var moveSequence = GetMoveSequence(currentPosition);
            
            // Theodore's tree
            if (chkUseTheodoreGames.Checked && theodoreTree != null)
            {
                var theodoreMoves = FindMovesInTree(theodoreTree.Root, moveSequence, isWhiteToMove);
                foreach (var (san, freq) in theodoreMoves)
                {
                    if (!moves.ContainsKey(san))
                        moves[san] = ("Theodore", freq);
                    else
                    {
                        var existing = moves[san];
                        moves[san] = ($"{existing.source}+Theo", existing.frequency + freq);
                    }
                }
            }
            
            // Database tree
            if (chkUseDatabaseGames.Checked && databaseTree != null)
            {
                var dbMoves = FindMovesInTree(databaseTree.Root, moveSequence, isWhiteToMove);
                foreach (var (san, freq) in dbMoves)
                {
                    if (!moves.ContainsKey(san))
                        moves[san] = ("Database", freq);
                    else
                    {
                        var existing = moves[san];
                        moves[san] = ($"{existing.source}+DB", existing.frequency + freq);
                    }
                }
            }
            
            var sortedMoves = moves.OrderByDescending(m => m.Value.frequency).ToList();
            if (sortedMoves.Count == 0)
                return new List<(string, string, int)>();
            
            int totalGames = sortedMoves.Sum(m => m.Value.frequency);
            int cumulativeGames = 0;
            var result = new List<(string san, string source, int frequency)>();
            
            foreach (var move in sortedMoves)
            {
                cumulativeGames += move.Value.frequency;
                double percentage = (cumulativeGames * 100.0) / totalGames;
                
                result.Add((move.Key, move.Value.source, move.Value.frequency));
                
                if (result.Count >= maxBranches || percentage >= percentThreshold)
                    break;
            }
            
            return result;
        }
        
        private List<string> GetMoveSequence(MoveNode node)
        {
            var sequence = new List<string>();
            var current = node;
            
            while (current != null && !string.IsNullOrEmpty(current.San))
            {
                sequence.Insert(0, current.San);
                current = current.Parent;
            }
            
            return sequence;
        }
        
        private List<(string san, int frequency)> FindMovesInTree(FrequencyNode treeNode, List<string> moveSequence, bool isWhiteToMove)
        {
            var currentNode = treeNode;
            
            foreach (var move in moveSequence)
            {
                var child = currentNode.Children.Values.FirstOrDefault(c => c.San == move);
                if (child == null)
                    return new List<(string, int)>();
                
                currentNode = child;
            }
            
            return currentNode.Children.Values
                .Where(c => c.IsWhiteMove == isWhiteToMove)
                .Select(c => (c.San, c.Frequency))
                .ToList();
        }
        
        private void DisplayTreeInView(MoveNode root)
        {
            var treeRoot = new TreeNode("Opening Repertoire");
            BuildTreeViewRecursive(root, treeRoot);
            treeVariations.Nodes.Add(treeRoot);
            treeRoot.ExpandAll();  // Auto-expand the entire tree
        }
        
        private void BuildTreeViewRecursive(MoveNode parentMoveNode, TreeNode parentTreeNode)
        {
            if (parentMoveNode.NextMoves.Count == 0)
                return;
            
            // Single child - collect and display as continuous line
            if (parentMoveNode.NextMoves.Count == 1)
            {
                var moves = new List<MoveNode>();
                var current = parentMoveNode.NextMoves[0];
                
                // Collect all single-child moves
                while (current != null)
                {
                    moves.Add(current);
                    if (current.NextMoves.Count != 1)
                        break;
                    current = current.NextMoves[0];
                }
                
                // Format the sequence: "1.e4 c5 2.Nf3 d6 3.d4 cxd4"
                var sb = new StringBuilder();
                for (int i = 0; i < moves.Count; i++)
                {
                    var move = moves[i];
                    // In MoveNode: isWhiteTurn = false means "move was by white"
                    // In HeatmapNode/FrequencyNode: IsWhiteMove = true means "move was by white"
                    // So we need: moveByWhite = !move.isWhiteTurn
                    bool moveByWhite = !move.isWhiteTurn;
                    
                    if (moveByWhite)
                    {
                        // White move: "1.e4"
                        if (sb.Length > 0) sb.Append(" ");
                        sb.Append($"{move.MoveNumber}.{move.San}");
                        
                        // Check if next move is black with same move number
                        if (i + 1 < moves.Count && 
                            moves[i + 1].isWhiteTurn &&  // Next move is by black (isWhiteTurn=true means black moves next)
                            moves[i + 1].MoveNumber == move.MoveNumber)
                        {
                            // Add black's response on same line: "1.e4 c5"
                            sb.Append($" {moves[i + 1].San}");  // NO move number!
                            i++; // Skip next move since we already added it
                        }
                    }
                    else
                    {
                        // Black move starting new line: "6...Bg7"
                        if (sb.Length > 0) sb.Append(" ");
                        sb.Append($"{move.MoveNumber}...{move.San}");
                    }
                }
                
                var treeNode = new TreeNode(sb.ToString());
                
                // Check if any move in the sequence has a comment (variation)
                // If all moves have no comment, it's mainline (black/bold)
                // If any move has a comment, use that for coloring
                bool hasComment = moves.Any(m => !string.IsNullOrEmpty(m.Comment));
                
                if (!hasComment)
                {
                    // All moves are from mainline - Black and Bold
                    treeNode.ForeColor = Color.Black;
                    treeNode.NodeFont = new Font(treeVariations.Font, FontStyle.Bold);
                }
                else
                {
                    // At least one move has a comment - use first comment for color
                    var commentedMove = moves.FirstOrDefault(m => !string.IsNullOrEmpty(m.Comment));
                    if (commentedMove != null)
                    {
                        if (commentedMove.Comment.Contains("Theodore+DB") || commentedMove.Comment.Contains("Theodore+Theo"))
                        {
                            treeNode.ForeColor = Color.Purple;
                        }
                        else if (commentedMove.Comment.Contains("Theodore"))
                        {
                            treeNode.ForeColor = Color.DarkRed;
                        }
                        else if (commentedMove.Comment.Contains("Database"))
                        {
                            treeNode.ForeColor = Color.DarkGreen;
                        }
                        else
                        {
                            treeNode.ForeColor = Color.Black;
                            treeNode.NodeFont = new Font(treeVariations.Font, FontStyle.Bold);
                        }
                    }
                }
                
                parentTreeNode.Nodes.Add(treeNode);
                
                // Recurse from last move
                var lastMove = moves[moves.Count - 1];
                if (lastMove.NextMoves.Count > 0)
                {
                    BuildTreeViewRecursive(lastMove, treeNode);
                }
                
                return;
            }
            
            // Multiple children - show branches and CONTINUE EACH ONE
            foreach (var child in parentMoveNode.NextMoves)
            {
                bool moveByWhite = !child.isWhiteTurn;
                bool isMainline = (parentMoveNode.NextMoves.IndexOf(child) == 0);
                
                // Format like Form1: white = "N.", black = "N..."
                string moveNum = moveByWhite ? $"{child.MoveNumber}." : $"{child.MoveNumber}...";
                string comment = !string.IsNullOrEmpty(child.Comment) ? $" {{ {child.Comment} }}" : "";
                
                var treeNode = new TreeNode($"{moveNum}{child.San}{comment}");
                
                // Color coding based on source
                if (string.IsNullOrEmpty(child.Comment))
                {
                    // No comment = Original mainline from input PGN - Black and Bold
                    treeNode.ForeColor = Color.Black;
                    treeNode.NodeFont = new Font(treeVariations.Font, FontStyle.Bold);
                }
                else
                {
                    // Variations - color by source
                    if (child.Comment.Contains("Theodore+DB") || child.Comment.Contains("Theodore+Theo"))
                    {
                        treeNode.ForeColor = Color.Purple;  // Both databases
                    }
                    else if (child.Comment.Contains("Theodore"))
                    {
                        treeNode.ForeColor = Color.DarkRed;  // Theodore's games only
                    }
                    else if (child.Comment.Contains("Database"))
                    {
                        treeNode.ForeColor = Color.DarkGreen;  // Database only
                    }
                    else
                    {
                        // Has comment but not from our sources - treat as mainline
                        treeNode.ForeColor = Color.Black;
                        treeNode.NodeFont = new Font(treeVariations.Font, FontStyle.Bold);
                    }
                }
                
                parentTreeNode.Nodes.Add(treeNode);
                
                // RECURSE - continue each variation!
                BuildTreeViewRecursive(child, treeNode);
            }
        }
        
        private string GeneratePgn(MoveNode root)
        {
            var sb = new StringBuilder();
            sb.AppendLine("[Event \"Opening Repertoire\"]");
            sb.AppendLine($"[White \"{(theodorePlaysWhite ? "Theodore" : "Opponent")}\"]");
            sb.AppendLine($"[Black \"{(theodorePlaysWhite ? "Opponent" : "Theodore")}\"]");
            sb.AppendLine();
            
            WriteMovesPgn(root, sb);
            sb.AppendLine(" *");
            
            return sb.ToString();
        }
        
        private void WriteMovesPgn(MoveNode node, StringBuilder sb)
        {
            if (node.NextMoves.Count == 0)
                return;
            
            var mainline = node.NextMoves[0];
            bool moveByWhite = !mainline.isWhiteTurn;
            string moveNum = moveByWhite ? $"{mainline.MoveNumber}." : $"{mainline.MoveNumber}...";
            string comment = !string.IsNullOrEmpty(mainline.Comment) ? $" {{ {mainline.Comment} }}" : "";
            
            sb.Append($"{moveNum}{mainline.San}{comment} ");
            
            for (int i = 1; i < node.NextMoves.Count; i++)
            {
                var variation = node.NextMoves[i];
                bool moveByWhite2 = !variation.isWhiteTurn;
                string varMoveNum = moveByWhite2 ? $"{variation.MoveNumber}." : $"{variation.MoveNumber}...";
                string varComment = !string.IsNullOrEmpty(variation.Comment) ? $" {{ {variation.Comment} }}" : "";
                
                sb.Append($"({varMoveNum}{variation.San}{varComment}) ");
            }
            
            WriteMovesPgn(mainline, sb);
        }
        
        private void BtnSave_Click(object? sender, EventArgs e)
        {
            using (SaveFileDialog sfd = new SaveFileDialog())
            {
                sfd.Filter = "PGN Files (*.pgn)|*.pgn";
                sfd.Title = "Save Opening Tree";
                sfd.FileName = "opening_repertoire.pgn";
                
                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        File.WriteAllText(sfd.FileName, generatedPgn);
                        MessageBox.Show("Opening tree saved successfully!", "Success",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error saving file: {ex.Message}", "Error",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }
        
        private void BtnClear_Click(object? sender, EventArgs e)
        {
            txtMainline.Clear();
            treeVariations.Nodes.Clear();
            mainlineGame = null;
            generatedPgn = "";
            btnSave.Enabled = false;
        }
    }
}
