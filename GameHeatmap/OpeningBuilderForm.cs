using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        private Button btnCopy;
        private Button btnClear;
        
        // Configuration controls
        private NumericUpDown numMaxBranches;
        private NumericUpDown numPercentThreshold;
        private NumericUpDown numMaxDepth;
        private NumericUpDown numStartMove;
        private CheckBox chkUseTheodoreGames;
        private CheckBox chkUseDatabaseGames;
        private CheckBox chkIncludeTheodoreAnnotations;
        private CheckBox chkIncludeDatabaseAnnotations;
        private CheckBox chkUseShortComments;
        private CheckBox chkDebugOutput;
        private CheckBox chkExtendMainline;
        private NumericUpDown numExtendMainlineToMove;
        private TextBox txtCommentTemplate;
        private TextBox txtDatabaseCommentTemplate;
        private NumericUpDown numMaxOpponentsInComments;
        private Label lblTheodoreColor;

        // Data
        private MoveFrequencyTree? theodoreTree;
        private HeatmapBuilder? theodoreHeatmap;  // Store Theodore's heatmap for game metadata
        private MoveFrequencyTree? databaseTree;
        private bool theodorePlaysWhite;
        private PgnGame? mainlineGame;
        private string generatedPgn = "";
        private string lastPgnPath = "";
        private MoveNode? outputRoot = null;  // Store the generated tree
        private bool isLoadingSettings = false;  // Flag to prevent saving during load

        public OpeningBuilderForm(MoveFrequencyTree? theodoreTree, MoveFrequencyTree? databaseTree, bool theodorePlaysWhite, HeatmapBuilder? theodoreHeatmap = null)
        {
            this.theodoreTree = theodoreTree;
            this.theodoreHeatmap = theodoreHeatmap;
            this.databaseTree = databaseTree;
            this.theodorePlaysWhite = theodorePlaysWhite;

            InitializeComponent();
            LoadSettings();
            AutoLoadLastPgn();

            // Save settings when form closes
            this.FormClosing += (s, e) => SaveSettings();
        }
        
        private void LoadSettings()
        {
            isLoadingSettings = true;  // Set flag to prevent saving during load

            // Load saved settings from registry
            numMaxBranches.Value = RegistryUtils.GetInt("OpeningBuilder_MaxBranches", 3);
            numPercentThreshold.Value = RegistryUtils.GetInt("OpeningBuilder_PercentThreshold", 90);
            numMaxDepth.Value = RegistryUtils.GetInt("OpeningBuilder_MaxDepth", 10);
            numStartMove.Value = RegistryUtils.GetInt("OpeningBuilder_StartMove", 4);
            chkUseTheodoreGames.Checked = RegistryUtils.GetInt("OpeningBuilder_UseTheodoreGames", 1) == 1;
            chkUseDatabaseGames.Checked = RegistryUtils.GetInt("OpeningBuilder_UseDatabaseGames", 1) == 1;
            chkIncludeTheodoreAnnotations.Checked = RegistryUtils.GetInt("OpeningBuilder_IncludeTheodoreAnnotations", 1) == 1;
            chkIncludeDatabaseAnnotations.Checked = RegistryUtils.GetInt("OpeningBuilder_IncludeDatabaseAnnotations", 1) == 1;
            chkUseShortComments.Checked = RegistryUtils.GetInt("OpeningBuilder_UseShortComments", 0) == 1;
            chkDebugOutput.Checked = RegistryUtils.GetInt("OpeningBuilder_DebugOutput", 0) == 1;
            chkExtendMainline.Checked = RegistryUtils.GetInt("OpeningBuilder_ExtendMainline", 0) == 1;
            numExtendMainlineToMove.Value = RegistryUtils.GetInt("OpeningBuilder_ExtendMainlineToMove", 20);
            txtCommentTemplate.Text = RegistryUtils.GetString("OpeningBuilder_CommentTemplate", "{opponent} ({date})");
            txtDatabaseCommentTemplate.Text = RegistryUtils.GetString("OpeningBuilder_DatabaseCommentTemplate", "Database: {count}");
            numMaxOpponentsInComments.Value = RegistryUtils.GetInt("OpeningBuilder_MaxOpponentsInComments", 3);

            isLoadingSettings = false;  // Clear flag after loading complete
        }

        private void SaveSettings()
        {
            // Don't save if we're currently loading settings
            if (isLoadingSettings)
                return;

            // Save settings to registry
            RegistryUtils.SetInt("OpeningBuilder_MaxBranches", (int)numMaxBranches.Value);
            RegistryUtils.SetInt("OpeningBuilder_PercentThreshold", (int)numPercentThreshold.Value);
            RegistryUtils.SetInt("OpeningBuilder_MaxDepth", (int)numMaxDepth.Value);
            RegistryUtils.SetInt("OpeningBuilder_StartMove", (int)numStartMove.Value);
            RegistryUtils.SetInt("OpeningBuilder_UseTheodoreGames", chkUseTheodoreGames.Checked ? 1 : 0);
            RegistryUtils.SetInt("OpeningBuilder_UseDatabaseGames", chkUseDatabaseGames.Checked ? 1 : 0);
            RegistryUtils.SetInt("OpeningBuilder_IncludeTheodoreAnnotations", chkIncludeTheodoreAnnotations.Checked ? 1 : 0);
            RegistryUtils.SetInt("OpeningBuilder_IncludeDatabaseAnnotations", chkIncludeDatabaseAnnotations.Checked ? 1 : 0);
            RegistryUtils.SetInt("OpeningBuilder_UseShortComments", chkUseShortComments.Checked ? 1 : 0);
            RegistryUtils.SetInt("OpeningBuilder_DebugOutput", chkDebugOutput.Checked ? 1 : 0);
            RegistryUtils.SetInt("OpeningBuilder_ExtendMainline", chkExtendMainline.Checked ? 1 : 0);
            RegistryUtils.SetInt("OpeningBuilder_ExtendMainlineToMove", (int)numExtendMainlineToMove.Value);
            RegistryUtils.SetString("OpeningBuilder_CommentTemplate", txtCommentTemplate.Text);
            RegistryUtils.SetString("OpeningBuilder_DatabaseCommentTemplate", txtDatabaseCommentTemplate.Text);
            RegistryUtils.SetInt("OpeningBuilder_MaxOpponentsInComments", (int)numMaxOpponentsInComments.Value);
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
            this.StartPosition = FormStartPosition.CenterScreen;
            
            // Split container for left/right panes
            SplitContainer splitContainer = new SplitContainer
            {
                Dock = DockStyle.Fill,
                SplitterDistance = this.ClientSize.Width / 2,  // 50/50 split
                Orientation = Orientation.Vertical
            };
            
            // LEFT PANEL - Mainline Input
            leftPanel = new Panel { Dock = DockStyle.Fill };

            // Add controls in reverse order (they stack from bottom to top when using Dock.Top)

            // Textbox fills remaining space - add FIRST
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

            // Label - add SECOND (will appear above textbox)
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

            // Button panel - add LAST (will appear at the very top)
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
            
            // TOP PANEL - Configuration (redesigned for better space usage)
            Panel topPanel = new Panel { Dock = DockStyle.Top, Height = 140, Padding = new Padding(10) };

            // ROW 1: Theodore color + Numeric settings
            lblTheodoreColor = new Label
            {
                Text = $"Theodore plays: {(theodorePlaysWhite ? "WHITE" : "BLACK")}",
                Location = new Point(10, 10),
                Size = new Size(150, 25),
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = theodorePlaysWhite ? Color.White : Color.Black,
                BackColor = theodorePlaysWhite ? Color.DarkGreen : Color.White,
                TextAlign = ContentAlignment.MiddleCenter,
                BorderStyle = BorderStyle.FixedSingle
            };
            topPanel.Controls.Add(lblTheodoreColor);

            Label lblMaxBranches = new Label { Text = "Branches:", Location = new Point(170, 12), Size = new Size(65, 20) };
            topPanel.Controls.Add(lblMaxBranches);
            numMaxBranches = new NumericUpDown { Location = new Point(240, 10), Size = new Size(50, 25), Minimum = 1, Maximum = 10, Value = 3 };
            topPanel.Controls.Add(numMaxBranches);

            Label lblPercent = new Label { Text = "%:", Location = new Point(300, 12), Size = new Size(25, 20) };
            topPanel.Controls.Add(lblPercent);
            numPercentThreshold = new NumericUpDown { Location = new Point(325, 10), Size = new Size(50, 25), Minimum = 50, Maximum = 100, Value = 90 };
            topPanel.Controls.Add(numPercentThreshold);

            Label lblMaxDepth = new Label { Text = "Depth:", Location = new Point(385, 12), Size = new Size(45, 20) };
            topPanel.Controls.Add(lblMaxDepth);
            numMaxDepth = new NumericUpDown { Location = new Point(430, 10), Size = new Size(50, 25), Minimum = 1, Maximum = 50, Value = 10 };
            topPanel.Controls.Add(numMaxDepth);

            Label lblStartMove = new Label { Text = "Start after:", Location = new Point(490, 12), Size = new Size(70, 20) };
            topPanel.Controls.Add(lblStartMove);
            numStartMove = new NumericUpDown { Location = new Point(560, 10), Size = new Size(50, 25), Minimum = 1, Maximum = 50, Value = 4 };
            topPanel.Controls.Add(numStartMove);

            chkExtendMainline = new CheckBox { Text = "Extend to:", Location = new Point(620, 12), Size = new Size(85, 20) };
            topPanel.Controls.Add(chkExtendMainline);
            numExtendMainlineToMove = new NumericUpDown { Location = new Point(705, 10), Size = new Size(50, 25), Minimum = 1, Maximum = 100, Value = 20 };
            topPanel.Controls.Add(numExtendMainlineToMove);

            chkDebugOutput = new CheckBox { Text = "Debug", Location = new Point(765, 12), Size = new Size(70, 20) };
            topPanel.Controls.Add(chkDebugOutput);
            
            // ROW 2: Data source checkboxes
            chkUseTheodoreGames = new CheckBox { Text = "Use Theodore Games", Location = new Point(10, 42), Size = new Size(150, 20), Checked = theodoreTree != null };
            topPanel.Controls.Add(chkUseTheodoreGames);

            chkUseDatabaseGames = new CheckBox { Text = "Use Database", Location = new Point(170, 42), Size = new Size(120, 20), Checked = databaseTree != null };
            topPanel.Controls.Add(chkUseDatabaseGames);

            chkIncludeTheodoreAnnotations = new CheckBox { Text = "Theodore Annot", Location = new Point(300, 42), Size = new Size(120, 20), Checked = true };
            chkIncludeTheodoreAnnotations.CheckedChanged += AnnotationCheckbox_CheckedChanged;
            topPanel.Controls.Add(chkIncludeTheodoreAnnotations);

            chkIncludeDatabaseAnnotations = new CheckBox { Text = "Database Annot", Location = new Point(430, 42), Size = new Size(120, 20), Checked = true };
            chkIncludeDatabaseAnnotations.CheckedChanged += AnnotationCheckbox_CheckedChanged;
            topPanel.Controls.Add(chkIncludeDatabaseAnnotations);

            chkUseShortComments = new CheckBox { Text = "Short (T/DB)", Location = new Point(560, 42), Size = new Size(100, 20), Checked = false };
            chkUseShortComments.CheckedChanged += AnnotationCheckbox_CheckedChanged;
            topPanel.Controls.Add(chkUseShortComments);

            // ROW 3: Comment templates and max opponents
            Label lblCommentTemplate = new Label { Text = "Theodore:", Location = new Point(10, 72), Size = new Size(65, 20) };
            topPanel.Controls.Add(lblCommentTemplate);

            txtCommentTemplate = new TextBox
            {
                Location = new Point(75, 70),
                Size = new Size(200, 25),
                Text = "{opponent} ({date})",
                Font = new Font("Consolas", 9)
            };
            topPanel.Controls.Add(txtCommentTemplate);

            Label lblDatabaseTemplate = new Label { Text = "DB:", Location = new Point(285, 72), Size = new Size(30, 20) };
            topPanel.Controls.Add(lblDatabaseTemplate);

            txtDatabaseCommentTemplate = new TextBox
            {
                Location = new Point(315, 70),
                Size = new Size(160, 25),
                Text = "Database: {count}",
                Font = new Font("Consolas", 9)
            };
            topPanel.Controls.Add(txtDatabaseCommentTemplate);

            Label lblMaxOpponents = new Label { Text = "Max:", Location = new Point(485, 72), Size = new Size(35, 20) };
            topPanel.Controls.Add(lblMaxOpponents);

            numMaxOpponentsInComments = new NumericUpDown
            {
                Location = new Point(520, 70),
                Size = new Size(50, 25),
                Minimum = 1,
                Maximum = 25,
                Value = 3
            };
            topPanel.Controls.Add(numMaxOpponentsInComments);

            Label lblTemplateHelp = new Label
            {
                Text = "Use: {opponent} {date} {event} {count}",
                Location = new Point(580, 72),
                Size = new Size(250, 20),
                ForeColor = Color.Gray,
                Font = new Font("Segoe UI", 8, FontStyle.Italic)
            };
            topPanel.Controls.Add(lblTemplateHelp);

            // ROW 4: Buttons
            btnGenerate = new Button
            {
                Text = "Generate Opening Tree",
                Location = new Point(10, 100),
                Size = new Size(180, 35),
                BackColor = Color.LightGreen,
                Font = new Font("Segoe UI", 10, FontStyle.Bold)
            };
            btnGenerate.Click += BtnGenerate_Click;
            topPanel.Controls.Add(btnGenerate);

            btnSave = new Button
            {
                Text = "Save to PGN",
                Location = new Point(200, 100),
                Size = new Size(120, 35),
                Enabled = false
            };
            btnSave.Click += BtnSave_Click;
            topPanel.Controls.Add(btnSave);

            btnCopy = new Button
            {
                Text = "Copy PGN",
                Location = new Point(330, 100),
                Size = new Size(100, 35),
                Enabled = false
            };
            btnCopy.Click += BtnCopy_Click;
            topPanel.Controls.Add(btnCopy);

            btnClear = new Button
            {
                Text = "Clear",
                Location = new Point(440, 100),
                Size = new Size(80, 35)
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
                    
                    //MessageBox.Show($"Loaded mainline with {CountMoves(mainlineGame.MoveTreeRoot)} plies",
                    //    "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
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

            // Save settings to registry
            SaveSettings();

            int maxBranches = (int)numMaxBranches.Value;
            int percentThreshold = (int)numPercentThreshold.Value;
            int maxDepth = (int)numMaxDepth.Value * 2; // Convert full moves to plies
            int startMove = (int)numStartMove.Value;

            // Create output tree by copying mainline
            outputRoot = CopyMoveTree(mainlineGame!.MoveTreeRoot);

            // Extend mainline if requested
            if (chkExtendMainline.Checked)
            {
                int targetMoveNumber = (int)numExtendMainlineToMove.Value;
                ExtendMainlineToMove(outputRoot, targetMoveNumber);
            }

            // Build variations by traversing the mainline and adding opponent responses
            BuildVariations(outputRoot, 0, maxDepth, maxBranches, percentThreshold, startMove);

            // Display in tree view
            DisplayTreeInView(outputRoot);

            // Generate PGN
            generatedPgn = GeneratePgn(outputRoot);
            btnSave.Enabled = true;
            btnCopy.Enabled = true;

            //MessageBox.Show("Opening tree generated successfully!\n\nExpand the tree to see variations.",
            //    "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        
        private void AnnotationCheckbox_CheckedChanged(object? sender, EventArgs e)
        {
            // Save settings whenever annotation checkboxes change
            SaveSettings();
            
            // Refresh the PGN if we have a generated tree
            if (outputRoot != null)
            {
                generatedPgn = GeneratePgn(outputRoot);
            }
        }
        
        private MoveNode CopyMoveTree(MoveNode source, bool isWhiteToMove = true)
        {
            var copy = new MoveNode
            {
                MoveNumber = source.MoveNumber,
                San = source.San,
                Comment = source.Comment,
                IsMainlineMove = true,  // Mainline moves from copied tree
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

        private void ExtendMainlineToMove(MoveNode root, int targetMoveNumber)
        {
            // Find the end of the mainline
            MoveNode current = root;
            while (current.NextMoves.Count > 0)
            {
                current = current.NextMoves[0]; // Follow the mainline (first child)
            }

            // Get current move number and depth
            int currentMoveNumber = current.MoveNumber;
            if (string.IsNullOrEmpty(current.San))
            {
                // This is the root node, start from move 1
                currentMoveNumber = 0;
            }

            // If already at or past target, don't truncate
            if (currentMoveNumber >= targetMoveNumber)
                return;

            // Calculate depth (ply count from root)
            int depth = 0;
            MoveNode temp = current;
            while (temp.Parent != null)
            {
                depth++;
                temp = temp.Parent;
            }

            // Extend by following top database moves
            bool isWhiteToMove = (depth % 2 == 0);

            while (currentMoveNumber < targetMoveNumber)
            {
                // Get the move sequence to this point
                var moveSequence = GetMoveSequence(current);

                // Find top move from database for the current side
                var movesFromDB = new List<(string san, int frequency)>();

                // Check Theodore's games
                if (chkUseTheodoreGames.Checked && theodoreTree != null)
                {
                    var theodoreMoves = FindMovesInTree(theodoreTree.Root, moveSequence, isWhiteToMove);
                    movesFromDB.AddRange(theodoreMoves);
                }

                // Check database
                if (chkUseDatabaseGames.Checked && databaseTree != null)
                {
                    var dbMoves = FindMovesInTree(databaseTree.Root, moveSequence, isWhiteToMove);
                    movesFromDB.AddRange(dbMoves);
                }

                if (movesFromDB.Count == 0)
                {
                    // No more moves in database, stop extending
                    break;
                }

                // Get the top move
                var topMove = movesFromDB
                    .GroupBy(m => m.san)
                    .Select(g => (san: g.Key, freq: g.Sum(x => x.frequency)))
                    .OrderByDescending(m => m.freq)
                    .First();

                // Calculate move number for the new move
                int newMoveNumber = isWhiteToMove ? (depth / 2) + 1 : currentMoveNumber;

                // Create new move node
                var newNode = new MoveNode
                {
                    MoveNumber = newMoveNumber,
                    San = topMove.san,
                    Comment = $"[MAINLINE] Extended via database (freq: {topMove.freq})",
                    Parent = current,
                    isWhiteTurn = !isWhiteToMove
                };

                current.NextMoves.Add(newNode);
                current = newNode;
                depth++;
                isWhiteToMove = !isWhiteToMove;

                // Update current move number
                if (isWhiteToMove)
                {
                    // Just finished black's move, move number stays same
                }
                else
                {
                    // Just finished white's move, increment
                    currentMoveNumber++;
                }
            }
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

                        // Build comment using helper method that respects annotation checkboxes
                        var (comment, hasTheodore, hasDatabase) = BuildCombinedComment(
                            bestMove.Value.theoFreq,
                            bestMove.Value.dbFreq,
                            moveSequence,
                            bestMove.Key);

                        int moveNum = (depth / 2) + 1;

                        var bestMoveNode = new MoveNode
                        {
                            MoveNumber = moveNum,
                            San = bestMove.Key,
                            Comment = comment,
                            HasTheodoreComment = hasTheodore,
                            HasDatabaseComment = hasDatabase,
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
                        // Separate Theodore and Database moves
                        var theodoreMoves = new Dictionary<string, int>();
                        var databaseMoves = new Dictionary<string, int>();

                        // Check Theodore's games
                        if (chkUseTheodoreGames.Checked && theodoreTree != null)
                        {
                            var moves = FindMovesInTree(theodoreTree.Root, moveSequence, isWhiteToMoveNow);
                            foreach (var (san, freq) in moves)
                            {
                                theodoreMoves[san] = freq;
                            }
                        }

                        // Check database
                        if (chkUseDatabaseGames.Checked && databaseTree != null)
                        {
                            var moves = FindMovesInTree(databaseTree.Root, moveSequence, isWhiteToMoveNow);
                            foreach (var (san, freq) in moves)
                            {
                                databaseMoves[san] = freq;
                            }
                        }

                        // Combine and sort
                        var allMoves = new Dictionary<string, (int theoFreq, int dbFreq)>();

                        foreach (var kvp in theodoreMoves)
                        {
                            allMoves[kvp.Key] = (kvp.Value, databaseMoves.GetValueOrDefault(kvp.Key, 0));
                        }

                        foreach (var kvp in databaseMoves)
                        {
                            if (!allMoves.ContainsKey(kvp.Key))
                            {
                                allMoves[kvp.Key] = (0, kvp.Value);
                            }
                        }

                        if (allMoves.Count > 0)
                        {
                            // Sort by total frequency
                            var sortedMoves = allMoves
                                .Select(kvp => (san: kvp.Key, theoFreq: kvp.Value.theoFreq, dbFreq: kvp.Value.dbFreq, totalFreq: kvp.Value.theoFreq + kvp.Value.dbFreq))
                                .OrderByDescending(m => m.totalFreq)
                                .ToList();

                            int totalGames = sortedMoves.Sum(m => m.totalFreq);
                            int cumulativeGames = 0;

                            foreach (var move in sortedMoves)
                            {
                                cumulativeGames += move.totalFreq;
                                double percentage = (cumulativeGames * 100.0) / totalGames;

                                // Build comment using helper method that respects annotation checkboxes
                                var (comment, hasTheodore, hasDatabase) = BuildCombinedComment(
                                    move.theoFreq,
                                    move.dbFreq,
                                    moveSequence,
                                    move.san);

                                var variationNode = new MoveNode
                                {
                                    MoveNumber = moveNum,
                                    San = move.san,
                                    Comment = comment,
                                    HasTheodoreComment = hasTheodore,
                                    HasDatabaseComment = hasDatabase,
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
                foreach (var (san, source, frequency, hasTheodore, hasDatabase) in alternativeMoves)
                {
                    if (san != mainlineMove.San)
                    {
                        var variationNode = new MoveNode
                        {
                            MoveNumber = mainlineMove.MoveNumber,
                            San = san,
                            Comment = source,
                            HasTheodoreComment = hasTheodore,
                            HasDatabaseComment = hasDatabase,
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

                // Annotate mainline with database frequencies if annotations are enabled
                if (alternativeMoves.Count > 0)
                {
                    var mainlineInfo = alternativeMoves.FirstOrDefault(m => m.san == mainlineMove.San);
                    if (mainlineInfo != default)
                    {
                        // Store annotation with proper flags
                        if (!string.IsNullOrEmpty(mainlineInfo.source))
                        {
                            mainlineMove.Comment = $"[MAINLINE]{mainlineInfo.source}";
                            mainlineMove.HasTheodoreComment = mainlineInfo.hasTheodore;
                            mainlineMove.HasDatabaseComment = mainlineInfo.hasDatabase;
                        }
                    }
                }
            }
            
            // ALWAYS continue mainline
            BuildVariations(mainlineMove, depth + 1, maxDepth, maxBranches, percentThreshold, startMove);
        }

        
        private List<(string san, string source, int frequency, bool hasTheodore, bool hasDatabase)> FindOpponentMoves(
            MoveNode currentPosition, string mainlineSan, int maxBranches, int percentThreshold, bool isWhiteToMove)
        {
            // Separate Theodore and Database moves
            var theodoreMoves = new Dictionary<string, int>();
            var databaseMoves = new Dictionary<string, int>();
            var moveSequence = GetMoveSequence(currentPosition);

            // Get Theodore's moves
            if (chkUseTheodoreGames.Checked && theodoreTree != null)
            {
                var moves = FindMovesInTree(theodoreTree.Root, moveSequence, isWhiteToMove);
                foreach (var (san, freq) in moves)
                {
                    theodoreMoves[san] = freq;
                }
            }

            // Get Database moves - apply maxBranches and threshold here
            if (chkUseDatabaseGames.Checked && databaseTree != null)
            {
                var moves = FindMovesInTree(databaseTree.Root, moveSequence, isWhiteToMove);
                var sortedDbMoves = moves.OrderByDescending(m => m.frequency).ToList();

                if (sortedDbMoves.Count > 0)
                {
                    int totalGames = sortedDbMoves.Sum(m => m.frequency);
                    int cumulativeGames = 0;
                    int branchCount = 0;

                    foreach (var (san, freq) in sortedDbMoves)
                    {
                        cumulativeGames += freq;
                        double percentage = (cumulativeGames * 100.0) / totalGames;

                        databaseMoves[san] = freq;
                        branchCount++;

                        // Stop after maxBranches or percentThreshold
                        if (branchCount >= maxBranches || percentage >= percentThreshold)
                            break;
                    }
                }
            }

            // Combine results - ALL Theodore moves + limited Database moves
            var result = new List<(string san, string source, int frequency, bool hasTheodore, bool hasDatabase)>();
            var allMoves = new Dictionary<string, (int theoFreq, int dbFreq, string san)>();

            foreach (var kvp in theodoreMoves)
            {
                allMoves[kvp.Key] = (kvp.Value, databaseMoves.GetValueOrDefault(kvp.Key, 0), kvp.Key);
            }

            foreach (var kvp in databaseMoves)
            {
                if (!allMoves.ContainsKey(kvp.Key))
                {
                    allMoves[kvp.Key] = (0, kvp.Value, kvp.Key);
                }
            }

            // Sort by total frequency
            var sortedMoves = allMoves.OrderByDescending(m => m.Value.theoFreq + m.Value.dbFreq).ToList();

            foreach (var move in sortedMoves)
            {
                int theoFreq = move.Value.theoFreq;
                int dbFreq = move.Value.dbFreq;

                // Build comment using helper method that respects annotation checkboxes
                var (comment, hasTheodore, hasDatabase) = BuildCombinedComment(
                    theoFreq,
                    dbFreq,
                    moveSequence,
                    move.Value.san);

                int totalFreq = theoFreq + dbFreq;
                result.Add((move.Key, comment, totalFreq, hasTheodore, hasDatabase));
            }

            return result;
        }
        
        private string BuildSourceString(int theoFreq, int dbFreq)
        {
            // Fallback method when heatmap is not available
            if (theoFreq > 0 && dbFreq > 0)
            {
                string dbComment = BuildDatabaseCommentWithTemplate(dbFreq);
                return $"Theodore: {theoFreq}, {dbComment}";
            }
            else if (theoFreq > 0)
                return $"Theodore: {theoFreq}";
            else
                return BuildDatabaseCommentWithTemplate(dbFreq);
        }

        /// <summary>
        /// Get games from Theodore's heatmap for a specific move sequence
        /// </summary>
        private List<PgnGame>? GetTheodoreGamesForMoveSequence(List<string> moveSequence)
        {
            if (theodoreHeatmap == null)
                return null;

            var currentNode = theodoreHeatmap.Root;

            // Navigate through the move sequence
            foreach (var moveSan in moveSequence)
            {
                var childNode = currentNode.Children.FirstOrDefault(c => c.San == moveSan);
                if (childNode == null)
                    return null;
                currentNode = childNode;
            }

            return currentNode.Games;
        }

        /// <summary>
        /// Format game metadata using the user's template
        /// </summary>
        private string FormatGameWithTemplate(PgnGame game, string template)
        {
            string result = template;

            // Get opponent name
            string opponent = "Unknown";
            if (game.Tags.ContainsKey("White") && game.Tags.ContainsKey("Black"))
            {
                // Theodore is white, so opponent is black
                opponent = theodorePlaysWhite ? game.Tags["Black"] : game.Tags["White"];
            }

            // Get date
            string date = game.Tags.ContainsKey("Date") ? game.Tags["Date"] : "";

            // Get event
            string eventName = game.Tags.ContainsKey("Event") ? game.Tags["Event"] : "";

            // Replace tokens
            result = result.Replace("{opponent}", opponent);
            result = result.Replace("{date}", date);
            result = result.Replace("{event}", eventName);

            return result;
        }

        /// <summary>
        /// Build a comment string for database moves using the template
        /// </summary>
        private string BuildDatabaseCommentWithTemplate(int count)
        {
            string template = txtDatabaseCommentTemplate.Text;
            return template.Replace("{count}", count.ToString());
        }

        /// <summary>
        /// Build a complete comment combining Theodore and Database parts, respecting annotation checkboxes
        /// </summary>
        private (string comment, bool hasTheodore, bool hasDatabase) BuildCombinedComment(
            int theoFreq, int dbFreq, List<string> moveSequence, string moveSan)
        {
            string theodoreComment = "";
            string databaseComment = "";
            bool hasTheodore = false;
            bool hasDatabase = false;

            // Build Theodore comment if applicable
            if (theoFreq > 0 && chkIncludeTheodoreAnnotations.Checked)
            {
                if (theodoreHeatmap != null)
                {
                    var moveSeqWithMove = new List<string>(moveSequence);
                    moveSeqWithMove.Add(moveSan);
                    theodoreComment = BuildTheodoreCommentWithTemplate(moveSeqWithMove);
                    if (!string.IsNullOrEmpty(theodoreComment))
                        hasTheodore = true;
                }
            }

            // Build Database comment if applicable
            if (dbFreq > 0 && chkIncludeDatabaseAnnotations.Checked)
            {
                databaseComment = BuildDatabaseCommentWithTemplate(dbFreq);
                hasDatabase = true;
            }

            // Combine comments
            string finalComment = "";
            if (hasTheodore && hasDatabase)
                finalComment = $"{theodoreComment}, {databaseComment}";
            else if (hasTheodore)
                finalComment = theodoreComment;
            else if (hasDatabase)
                finalComment = databaseComment;

            return (finalComment, hasTheodore, hasDatabase);
        }

        /// <summary>
        /// Build a comment string for Theodore's moves using the template
        /// </summary>
        private string BuildTheodoreCommentWithTemplate(List<string> moveSequence)
        {
            // Get games from heatmap
            var games = GetTheodoreGamesForMoveSequence(moveSequence);
            if (games == null || games.Count == 0)
                return "";  // Return empty if no games

            // Get template and max opponents
            string template = txtCommentTemplate.Text;
            int maxOpponents = (int)numMaxOpponentsInComments.Value;

            // Sort games by date (most recent first) if date is available
            var sortedGames = games
                .OrderByDescending(g => g.Tags.ContainsKey("Date") ? g.Tags["Date"] : "")
                .Take(maxOpponents)
                .ToList();

            // Format each game using template
            var formattedGames = sortedGames
                .Select(g => FormatGameWithTemplate(g, template))
                .ToList();

            // Combine into a single comment
            if (formattedGames.Count == 0)
                return "";

            string comment = string.Join(", ", formattedGames);

            // If we truncated, add count
            if (games.Count > maxOpponents)
            {
                int remaining = games.Count - maxOpponents;
                comment += $", and {remaining} more {(remaining == 1 ? "game" : "games")}";
            }

            return comment;  // No "Theodore:" prefix
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
                        if (!move.HasBeenWritten)
                            sb.Append($"{move.MoveNumber}.{move.San}");
                        move.HasBeenWritten = true;
                        
                        // Check if next move is black with same move number
                        if (i + 1 < moves.Count && 
                            moves[i + 1].isWhiteTurn &&  // Next move is by black (isWhiteTurn=true means black moves next)
                            moves[i + 1].MoveNumber == move.MoveNumber)
                        {
                            // Add black's response on same line: "1.e4 c5"
                            if (!moves[i + 1].HasBeenWritten)
                                sb.Append($" {moves[i + 1].San}");  // NO move number!
                            moves[i + 1].HasBeenWritten = true;
                            i++; // Skip next move since we already added it
                        }
                    }
                    else
                    {
                        // Black move starting new line: "6...Bg7"
                        if (sb.Length > 0) sb.Append(" ");
                        if (!move.HasBeenWritten)
                            sb.Append($"{move.MoveNumber}...{move.San}");
                    }
                }
                
                var treeNode = new TreeNode(sb.ToString());
                
                // Check if any move in the sequence has a comment (variation)
                // If all moves have no comment or only mainline markers, it's mainline (black/bold)
                // If any move has a variation comment, use that for coloring
                bool hasVariationComment = moves.Any(m => !string.IsNullOrEmpty(m.Comment) && !m.Comment.StartsWith("[MAINLINE]"));
                
                if (!hasVariationComment)
                {
                    // All moves are from mainline - Black and Bold
                    treeNode.ForeColor = Color.Black;
                    treeNode.NodeFont = new Font(treeVariations.Font, FontStyle.Bold);
                }
                else
                {
                    // Use flags to determine color
                    var commentedMove = moves.FirstOrDefault(m => m.HasTheodoreComment || m.HasDatabaseComment || m.IsMainlineMove);
                    if (commentedMove != null)
                    {
                        if (commentedMove.HasTheodoreComment && commentedMove.HasDatabaseComment)
                        {
                            treeNode.ForeColor = Color.Purple;  // Both sources
                        }
                        else if (commentedMove.HasTheodoreComment)
                        {
                            treeNode.ForeColor = Color.DarkRed;  // Theodore only
                        }
                        else if (commentedMove.HasDatabaseComment)
                        {
                            treeNode.ForeColor = Color.DarkGreen;  // Database only
                        }
                        else if (commentedMove.IsMainlineMove)
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
                string comment = !string.IsNullOrEmpty(child.Comment) && !child.Comment.StartsWith("[MAINLINE]") ? $" {{ {child.Comment} }}" : "";
                
                var treeNode = new TreeNode($"{moveNum}{child.San}{comment}");
                
                // Color coding based on source
                if (string.IsNullOrEmpty(child.Comment) || child.Comment.StartsWith("[MAINLINE]"))
                {
                    // No comment OR mainline marker = Original mainline from input PGN - Black and Bold
                    treeNode.ForeColor = Color.Black;
                    treeNode.NodeFont = new Font(treeVariations.Font, FontStyle.Bold);
                }
                else
                {
                    // Variations - color by flags
                    if (child.HasTheodoreComment && child.HasDatabaseComment)
                    {
                        treeNode.ForeColor = Color.Purple;  // Both sources
                    }
                    else if (child.HasTheodoreComment)
                    {
                        treeNode.ForeColor = Color.DarkRed;  // Theodore only
                    }
                    else if (child.HasDatabaseComment)
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

            // Clear all HasBeenWritten flags
            ClearWrittenFlags(root);

            WriteMovesPgn(root, sb);
            sb.AppendLine(" *");

            return sb.ToString();
        }

        private void ClearWrittenFlags(MoveNode node)
        {
            if (node == null) return;
            node.HasBeenWritten = false;
            foreach (var child in node.NextMoves)
            {
                ClearWrittenFlags(child);
            }
        }

        private void WriteMovesPgn(MoveNode node, StringBuilder sb, bool isFirstMove = true, MoveNode? previousMove = null, bool afterVariations = false)
        {
            if (node.NextMoves.Count == 0)
                return;

            // Get mainline move
            var mainline = node.NextMoves[0];
            bool moveByWhite = !mainline.isWhiteTurn;

            Debug.WriteLine($"WriteMovesPgn: Processing mainline={mainline.San}, moveNum={mainline.MoveNumber}, white={moveByWhite}, children={mainline.NextMoves.Count}");
            
            // Determine if we need a move number
            string moveNum = "";
            bool prevWasWhiteSameNumber = previousMove != null && 
                                          !previousMove.isWhiteTurn && 
                                          previousMove.MoveNumber == mainline.MoveNumber;
            
            if (isFirstMove)
            {
                moveNum = moveByWhite ? $"{mainline.MoveNumber}. " : $"{mainline.MoveNumber}... ";
                if (chkDebugOutput.Checked)
                    File.AppendAllText(@"C:\data\debug.txt", $"  First move, moveNum={moveNum}\n");
            }
            else if (moveByWhite)
            {
                moveNum = $"{mainline.MoveNumber}. ";
                if (chkDebugOutput.Checked)
                    File.AppendAllText(@"C:\data\debug.txt", $"  White move, moveNum={moveNum}\n");
            }
            else if (prevWasWhiteSameNumber)
            {
                // Black move following white move with same number
                if (afterVariations)
                {
                    // After variations, need "...": "10. O-O-O (vars) 10... Rc8"
                    moveNum = $"{mainline.MoveNumber}... ";
                    if (chkDebugOutput.Checked)
                        File.AppendAllText(@"C:\data\debug.txt", $"  Black after vars, moveNum={moveNum}\n");
                }
                else
                {
                    // On same line, no move number: "1. e4 c5"
                    moveNum = "";
                    if (chkDebugOutput.Checked)
                        File.AppendAllText(@"C:\data\debug.txt", $"  Black same line, moveNum=(empty)\n");
                }
            }
            else
            {
                // Black move needs full notation
                moveNum = $"{mainline.MoveNumber}... ";
                if (chkDebugOutput.Checked)
                    File.AppendAllText(@"C:\data\debug.txt", $"  Black other, moveNum={moveNum}\n");
            }
            
            string comment = "";
            if (!string.IsNullOrEmpty(mainline.Comment))
            {
                string commentText = mainline.Comment.StartsWith("[MAINLINE]") 
                    ? mainline.Comment.Substring(10) 
                    : mainline.Comment;
                string filteredComment = FilterComment(commentText);
                if (!string.IsNullOrEmpty(filteredComment))
                    comment = $" {{ {filteredComment} }}";
            }
            
            // Write the mainline move
            if (chkDebugOutput.Checked)
                File.AppendAllText(@"C:\data\debug.txt", $"MAIN: {moveNum}{mainline.San}\n");
            Debug.WriteLine($"  Writing: {moveNum}{mainline.San}{comment}");
            if (!mainline.HasBeenWritten)
                sb.Append($"{moveNum}{mainline.San}{comment} ");
            mainline.HasBeenWritten = true;
            
            // Check if mainline has variations (multiple children)
            if (mainline.NextMoves.Count > 1)
            {
                Debug.WriteLine($"  Mainline has {mainline.NextMoves.Count} children (variations present)");

                // Write the first child of mainline (only if not already written)
                var nextMainline = mainline.NextMoves[0];

                if (!nextMainline.HasBeenWritten)
                {
                    bool nextMoveByWhite = !nextMainline.isWhiteTurn;
                    string nextMoveNum = nextMoveByWhite ? $"{nextMainline.MoveNumber}. " : $"{nextMainline.MoveNumber}... ";

                    Debug.WriteLine($"  Writing first child: {nextMoveNum}{nextMainline.San}");

                    string nextComment = "";
                    if (!string.IsNullOrEmpty(nextMainline.Comment))
                    {
                        string commentText = nextMainline.Comment.StartsWith("[MAINLINE]")
                            ? nextMainline.Comment.Substring(10)
                            : nextMainline.Comment;
                        string filteredComment = FilterComment(commentText);
                        if (!string.IsNullOrEmpty(filteredComment))
                            nextComment = $" {{ {filteredComment} }}";
                    }

                    if (chkDebugOutput.Checked)
                        File.AppendAllText(@"C:\data\debug.txt", $"CHILD: {nextMoveNum}{nextMainline.San}\n");
                    if (!nextMainline.HasBeenWritten)
                        sb.Append($"{nextMoveNum}{nextMainline.San}{nextComment} ");
                    nextMainline.HasBeenWritten = true;
                }
                else
                {
                    Debug.WriteLine($"  Skipping first child (already written): {nextMainline.San}");
                }

                // Write variations (siblings of nextMainline)
                Debug.WriteLine($"  Writing {mainline.NextMoves.Count - 1} variations");
                for (int i = 1; i < mainline.NextMoves.Count; i++)
                {
                    Debug.WriteLine($"    Variation {i}: {mainline.NextMoves[i].San}");
                    sb.Append("(");
                    WriteVariationPgn(mainline.NextMoves[i], sb, true, null);
                    sb.Append(") ");
                }

                // Now continue from nextMainline's CHILDREN (nextMainline already written above)
                Debug.WriteLine($"  Continuing from nextMainline children: {nextMainline.San}");
                if (nextMainline.NextMoves.Count > 0)
                {
                    WriteMovesPgn(nextMainline, sb, false, nextMainline, true);  // afterVariations = true!
                }
                return;
            }

            // Write variations at parent level (alternatives to mainline itself)
            if (node.NextMoves.Count > 1)
            {
                Debug.WriteLine($"  Node has {node.NextMoves.Count} children - writing parent-level variations");
            }
            for (int i = 1; i < node.NextMoves.Count; i++)
            {
                var variation = node.NextMoves[i];
                Debug.WriteLine($"    Parent variation {i}: {variation.San}");
                sb.Append("(");
                WriteVariationPgn(variation, sb, true, null);
                sb.Append(") ");
            }

            // Normal continuation
            Debug.WriteLine($"  Normal recursion from mainline: {mainline.San}");
            WriteMovesPgn(mainline, sb, false, mainline);
        }

        private void WriteVariationPgn(MoveNode node, StringBuilder sb, bool isFirstInVariation = true, MoveNode? previousMove = null, bool afterVariations = false)
        {
            bool moveByWhite = !node.isWhiteTurn;
            
            Debug.WriteLine($"  WriteVariationPgn: node={node.San}, moveNum={node.MoveNumber}, white={moveByWhite}, children={node.NextMoves.Count}, isFirst={isFirstInVariation}");
            
            string moveNum = "";
            
            // Determine if we need move number
            bool prevWasWhiteSameNumber = previousMove != null && 
                                          !previousMove.isWhiteTurn && 
                                          previousMove.MoveNumber == node.MoveNumber;
            
            if (isFirstInVariation)
            {
                moveNum = moveByWhite ? $"{node.MoveNumber}. " : $"{node.MoveNumber}... ";
            }
            else if (moveByWhite)
            {
                moveNum = $"{node.MoveNumber}. ";
            }
            else if (prevWasWhiteSameNumber)
            {
                // Black move following white move with same number
                if (afterVariations)
                {
                    // After variations, need "...": "10. O-O-O (vars) 10... Rc8"
                    moveNum = $"{node.MoveNumber}... ";
                }
                else
                {
                    // On same line, no move number: "6. Be2 Bg7"
                    moveNum = "";
                }
            }
            else
            {
                moveNum = $"{node.MoveNumber}... ";
            }
            
            // Handle comment
            string comment = "";
            if (!string.IsNullOrEmpty(node.Comment))
            {
                string commentText = node.Comment.StartsWith("[MAINLINE]") 
                    ? node.Comment.Substring(10) 
                    : node.Comment;
                string filteredComment = FilterComment(commentText);
                if (!string.IsNullOrEmpty(filteredComment))
                    comment = $" {{ {filteredComment} }}";
            }
            
            // Write this move
            if (chkDebugOutput.Checked)
                File.AppendAllText(@"C:\data\debug.txt", $"VAR: {moveNum}{node.San}\n");
            Debug.WriteLine($"    Writing in variation: {moveNum}{node.San}{comment}");
            if (!node.HasBeenWritten)
                sb.Append($"{moveNum}{node.San}{comment} ");
            node.HasBeenWritten = true;
            
            // Check if this node has multiple children (variations)
            if (node.NextMoves.Count > 1)
            {
                Debug.WriteLine($"    Node has {node.NextMoves.Count} children in variation");

                // Write the first child (only if not already written)
                var nextMainline = node.NextMoves[0];

                if (!nextMainline.HasBeenWritten)
                {
                    bool nextMoveByWhite = !nextMainline.isWhiteTurn;
                    string nextMoveNum = nextMoveByWhite ? $"{nextMainline.MoveNumber}. " : $"{nextMainline.MoveNumber}... ";

                    Debug.WriteLine($"    Writing first child in variation: {nextMoveNum}{nextMainline.San}");

                    string nextComment = "";
                    if (!string.IsNullOrEmpty(nextMainline.Comment))
                    {
                        string commentText = nextMainline.Comment.StartsWith("[MAINLINE]")
                            ? nextMainline.Comment.Substring(10)
                            : nextMainline.Comment;
                        string filteredComment = FilterComment(commentText);
                        if (!string.IsNullOrEmpty(filteredComment))
                            nextComment = $" {{ {filteredComment} }}";
                    }

                    if (!nextMainline.HasBeenWritten)
                        sb.Append($"{nextMoveNum}{nextMainline.San}{nextComment} ");
                    nextMainline.HasBeenWritten = true;
                }
                else
                {
                    Debug.WriteLine($"    Skipping first child in variation (already written): {nextMainline.San}");
                }

                // Write variations (siblings of nextMainline)
                Debug.WriteLine($"    Writing {node.NextMoves.Count - 1} sub-variations");
                for (int i = 1; i < node.NextMoves.Count; i++)
                {
                    Debug.WriteLine($"      Sub-variation {i}: {node.NextMoves[i].San}");
                    sb.Append("(");
                    WriteVariationPgn(node.NextMoves[i], sb, true, null);
                    sb.Append(") ");
                }

                // Now continue from nextMainline's CHILDREN (nextMainline already written above)
                Debug.WriteLine($"    Continuing from nextMainline children in variation: {nextMainline.San}");
                if (nextMainline.NextMoves.Count > 0)
                {
                    WriteVariationPgn(nextMainline, sb, false, nextMainline, true);  // afterVariations = true!
                }
                return;
            }

            // Continue with mainline (no variations at this level)
            if (node.NextMoves.Count > 0)
            {
                Debug.WriteLine($"    Normal recursion in variation from: {node.San} to {node.NextMoves[0].San}");
                WriteVariationPgn(node.NextMoves[0], sb, false, node);
            }
            else
            {
                Debug.WriteLine($"    End of variation at: {node.San}");
            }
        }
        
        private string FilterComment(string comment)
        {
            // Check if comment contains Theodore or Database annotations
            bool hasTheodore = comment.Contains("Theodore:");
            bool hasDatabase = comment.Contains("Database:");
            
            // If it has both, rebuild based on checkboxes
            if (hasTheodore && hasDatabase)
            {
                // Parse the frequencies
                // Format: "Theodore: 100, Database: 485"
                var parts = comment.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                string theoGames = "";
                string dbGames = "";
                
                for (int i = 0; i < parts.Length; i++)
                {
                    if (parts[i] == "Theodore:" && i + 1 < parts.Length)
                        theoGames = parts[i + 1];
                    else if (parts[i] == "Database:" && i + 1 < parts.Length)
                        dbGames = parts[i + 1];
                }
                
                // Rebuild based on what's checked and format preference
                List<string> components = new List<string>();
                
                if (chkUseShortComments.Checked)
                {
                    // Short format: "T 100 DB 485"
                    if (chkIncludeTheodoreAnnotations.Checked && !string.IsNullOrEmpty(theoGames))
                        components.Add($"T {theoGames}");
                    if (chkIncludeDatabaseAnnotations.Checked && !string.IsNullOrEmpty(dbGames))
                        components.Add($"DB {dbGames}");
                    return components.Count > 0 ? string.Join(" ", components) : "";
                }
                else
                {
                    // Long format: "Theodore: 100, Database: 485"
                    if (chkIncludeTheodoreAnnotations.Checked && !string.IsNullOrEmpty(theoGames))
                        components.Add($"Theodore: {theoGames}");
                    if (chkIncludeDatabaseAnnotations.Checked && !string.IsNullOrEmpty(dbGames))
                        components.Add($"Database: {dbGames}");
                    return components.Count > 0 ? string.Join(", ", components) : "";
                }
            }
            
            // If only Theodore, include if Theodore checkbox is checked
            if (hasTheodore)
            {
                if (!chkIncludeTheodoreAnnotations.Checked)
                    return "";
                    
                // Extract the number
                if (chkUseShortComments.Checked)
                {
                    // Short format: "T 100"
                    var parts = comment.Split(new[] { ' ', ':' }, StringSplitOptions.RemoveEmptyEntries);
                    for (int i = 0; i < parts.Length; i++)
                    {
                        if (parts[i] == "Theodore" && i + 1 < parts.Length)
                            return $"T {parts[i + 1]}";
                    }
                }
                return comment; // Return original if long format
            }
            
            // If only Database, include if Database checkbox is checked  
            if (hasDatabase)
            {
                if (!chkIncludeDatabaseAnnotations.Checked)
                    return "";
                    
                // Extract the number
                if (chkUseShortComments.Checked)
                {
                    // Short format: "DB 485"
                    var parts = comment.Split(new[] { ' ', ':' }, StringSplitOptions.RemoveEmptyEntries);
                    for (int i = 0; i < parts.Length; i++)
                    {
                        if (parts[i] == "Database" && i + 1 < parts.Length)
                            return $"DB {parts[i + 1]}";
                    }
                }
                return comment; // Return original if long format
            }
            
            // Other comments (like debug messages) - always include
            return comment;
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
        
        private void BtnCopy_Click(object? sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(generatedPgn))
            {
                try
                {
                    Clipboard.SetText(generatedPgn);
                    //MessageBox.Show("PGN copied to clipboard!", "Success",
                        //MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error copying to clipboard: {ex.Message}", "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
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
            btnCopy.Enabled = false;
        }
    }
}
