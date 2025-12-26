using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using System.IO;

namespace GameHeatmap
{
    public partial class Form1 : Form
    {
        private List<PgnGame> allGames = new List<PgnGame>();
        private List<PgnGame> filteredGames = new List<PgnGame>();
        private HeatmapBuilder? heatmapBuilder;

        // UI Controls
        private TreeView treeView = null!;
        private ListBox lstGames = null!;
        private TextBox txtPlayerFilter = null!;
        private Button btnLoadFiles = null!;
        private Button btnApplyFilter = null!;
        private RadioButton rbWhite = null!;
        private RadioButton rbBlack = null!;
        private Label lblStatus = null!;
        private NumericUpDown numDepth = null!;
        private Label lblDepth = null!;
        private Label lblStats = null!;
        private ToolTip toolTip = null!;
        private CheckBox chkShowTooltips = null!;
        private bool showTooltips = true;
        
        // Database frequency tree
        private MoveFrequencyTree? databaseTree = null;
        private Button btnLoadDatabase = null!;
        private Button btnViewDatabase = null!;
        private Button btnSaveDatabase = null!;
        private Button btnLoadCachedDatabase = null!;

        private Dictionary<TreeNode, HeatmapNode> nodeToHeatmap = new Dictionary<TreeNode, HeatmapNode>();
        private float dpiScale = 1.0f;

        public Form1()
        {
            InitializeComponent();
            InitializeCustomComponents();
            LoadSettings();
        }

        private void InitializeCustomComponents()
        {
            // Get DPI scaling factor
            using (Graphics g = this.CreateGraphics())
            {
                dpiScale = g.DpiX / 96f; // 96 DPI is 100% scaling
            }

            this.Text = "Chess Game Heatmap - Theodore's Games";
            this.Size = new Size(Scale(1200), Scale(800));
            this.AllowDrop = true;

            toolTip = new ToolTip();
            toolTip.AutoPopDelay = 5000;
            toolTip.InitialDelay = 500;

            // Left panel for controls
            Panel leftPanel = new Panel
            {
                Dock = DockStyle.Left,
                Width = Scale(320),
                Padding = new Padding(Scale(10))
            };

            int yPos = Scale(10);

            // Player filter
            Label lblPlayerFilterLabel = new Label
            {
                Text = "Player Filter (comma-separated):",
                Location = new Point(Scale(10), yPos),
                Size = new Size(Scale(300), Scale(20))
            };
            leftPanel.Controls.Add(lblPlayerFilterLabel);
            yPos += Scale(25);

            txtPlayerFilter = new TextBox
            {
                Location = new Point(Scale(10), yPos),
                Size = new Size(Scale(300), Scale(25)),
                PlaceholderText = "Theodore, !bot (use ! to exclude)"
            };
            leftPanel.Controls.Add(txtPlayerFilter);
            yPos += Scale(35);

            // Color selection
            Label lblColorLabel = new Label
            {
                Text = "Theodore plays as:",
                Location = new Point(Scale(10), yPos),
                Size = new Size(Scale(300), Scale(20))
            };
            leftPanel.Controls.Add(lblColorLabel);
            yPos += Scale(25);

            rbWhite = new RadioButton
            {
                Text = "White",
                Location = new Point(Scale(10), yPos),
                Size = new Size(Scale(100), Scale(25)),
                Checked = true
            };
            leftPanel.Controls.Add(rbWhite);

            rbBlack = new RadioButton
            {
                Text = "Black",
                Location = new Point(Scale(120), yPos),
                Size = new Size(Scale(100), Scale(25))
            };
            leftPanel.Controls.Add(rbBlack);
            yPos += Scale(35);

            // Depth filter
            lblDepth = new Label
            {
                Text = "Max Depth (moves):",
                Location = new Point(Scale(10), yPos),
                Size = new Size(Scale(150), Scale(20))
            };
            leftPanel.Controls.Add(lblDepth);

            numDepth = new NumericUpDown
            {
                Location = new Point(Scale(170), yPos - Scale(2)),
                Size = new Size(Scale(140), Scale(25)),
                Minimum = 1,
                Maximum = 200,
                Value = 50
            };
            leftPanel.Controls.Add(numDepth);
            yPos += Scale(35);

            // Apply filter button
            btnApplyFilter = new Button
            {
                Text = "Apply Filter",
                Location = new Point(Scale(10), yPos),
                Size = new Size(Scale(300), Scale(35)),
                Font = new Font("Segoe UI", 10, FontStyle.Bold)
            };
            btnApplyFilter.Click += BtnApplyFilter_Click;
            leftPanel.Controls.Add(btnApplyFilter);
            yPos += Scale(45);

            // Load files button
            btnLoadFiles = new Button
            {
                Text = "Load PGN Files",
                Location = new Point(Scale(10), yPos),
                Size = new Size(Scale(300), Scale(30))
            };
            btnLoadFiles.Click += BtnLoadFiles_Click;
            leftPanel.Controls.Add(btnLoadFiles);
            yPos += Scale(35);

            // Database buttons
            btnLoadDatabase = new Button
            {
                Text = "Load Database",
                Location = new Point(Scale(10), yPos),
                Size = new Size(Scale(145), Scale(30)),
                BackColor = Color.LightYellow
            };
            btnLoadDatabase.Click += BtnLoadDatabase_Click;
            leftPanel.Controls.Add(btnLoadDatabase);

            btnViewDatabase = new Button
            {
                Text = "View Database",
                Location = new Point(Scale(165), yPos),
                Size = new Size(Scale(145), Scale(30)),
                BackColor = Color.LightGreen,
                Enabled = false
            };
            btnViewDatabase.Click += BtnViewDatabase_Click;
            leftPanel.Controls.Add(btnViewDatabase);
            yPos += Scale(35);

            // Save/Load cache buttons
            btnSaveDatabase = new Button
            {
                Text = "Save Cache",
                Location = new Point(Scale(10), yPos),
                Size = new Size(Scale(145), Scale(30)),
                BackColor = Color.LightCyan,
                Enabled = false
            };
            btnSaveDatabase.Click += BtnSaveDatabase_Click;
            leftPanel.Controls.Add(btnSaveDatabase);

            btnLoadCachedDatabase = new Button
            {
                Text = "Load Cache",
                Location = new Point(Scale(165), yPos),
                Size = new Size(Scale(145), Scale(30)),
                BackColor = Color.LightSalmon
            };
            btnLoadCachedDatabase.Click += BtnLoadCachedDatabase_Click;
            leftPanel.Controls.Add(btnLoadCachedDatabase);
            yPos += Scale(40);

            // Opening Builder button
            Button btnOpeningBuilder = new Button
            {
                Text = "Opening Builder",
                Location = new Point(Scale(10), yPos),
                Size = new Size(Scale(300), Scale(35)),
                BackColor = Color.LightGoldenrodYellow,
                Font = new Font("Segoe UI", 10, FontStyle.Bold)
            };
            btnOpeningBuilder.Click += BtnOpeningBuilder_Click;
            leftPanel.Controls.Add(btnOpeningBuilder);
            yPos += Scale(40);

            // Games list
            Label lblGamesLabel = new Label
            {
                Text = "Loaded Games (multi-select):",
                Location = new Point(Scale(10), yPos),
                Size = new Size(Scale(300), Scale(20))
            };
            leftPanel.Controls.Add(lblGamesLabel);
            yPos += Scale(25);

            lstGames = new ListBox
            {
                Location = new Point(Scale(10), yPos),
                Size = new Size(Scale(300), Scale(400)),
                SelectionMode = SelectionMode.MultiExtended,
                Font = new Font("Consolas", 8),
                IntegralHeight = false
            };
            lstGames.SelectedIndexChanged += LstGames_SelectedIndexChanged;
            lstGames.KeyDown += LstGames_KeyDown;
            leftPanel.Controls.Add(lstGames);
            yPos += Scale(410);

            // Status label
            lblStatus = new Label
            {
                Location = new Point(Scale(10), yPos),
                Size = new Size(Scale(300), Scale(60)),
                Text = "No games loaded"
            };
            leftPanel.Controls.Add(lblStatus);

            // Right panel for tree (ADD FIRST - Fill takes remaining space)
            Panel rightPanel = new Panel
            {
                Dock = DockStyle.Fill
            };

            // TreeView - ADD FIRST so it fills remaining space
            treeView = new TreeView
            {
                Dock = DockStyle.Fill,
                Font = new Font("Consolas", 10),
                HideSelection = false,
                ShowLines = true,
                ShowPlusMinus = true,
                ShowRootLines = true,
                Scrollable = true
            };
            treeView.NodeMouseClick += TreeView_NodeMouseClick;
            treeView.NodeMouseDoubleClick += TreeView_NodeMouseDoubleClick;
            rightPanel.Controls.Add(treeView);

            // Top control panel for tree - ADD SECOND so it docks at top
            Panel treeControlPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = Scale(80),
                Padding = new Padding(Scale(5))
            };

            // Stats label
            lblStats = new Label
            {
                Text = "Tree View",
                Location = new Point(Scale(5), Scale(5)),
                Size = new Size(Scale(800), Scale(25)),
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            treeControlPanel.Controls.Add(lblStats);

            // Legend label
            Label lblLegend = new Label
            {
                Text = "Colors: Bold Red (80%+) | Dark Red (50-79%) | Orange (20-49%) | Gray (<20%)  |  Format: Move (games/total = %)",
                Location = new Point(Scale(5), Scale(33)),
                Size = new Size(Scale(800), Scale(20)),
                Font = new Font("Segoe UI", 9),
                ForeColor = Color.DarkSlateGray,
                TextAlign = ContentAlignment.MiddleLeft,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            treeControlPanel.Controls.Add(lblLegend);

            // Expand/Collapse buttons
            Button btnExpandAll = new Button
            {
                Text = "Expand All",
                Location = new Point(Scale(5), Scale(55)),
                Size = new Size(Scale(100), Scale(25))
            };
            btnExpandAll.Click += (s, e) => treeView.ExpandAll();
            treeControlPanel.Controls.Add(btnExpandAll);

            Button btnCollapseAll = new Button
            {
                Text = "Collapse All",
                Location = new Point(Scale(115), Scale(55)),
                Size = new Size(Scale(100), Scale(25))
            };
            btnCollapseAll.Click += (s, e) => treeView.CollapseAll();
            treeControlPanel.Controls.Add(btnCollapseAll);

            // Show tooltips checkbox
            chkShowTooltips = new CheckBox
            {
                Text = "Show Tooltips",
                Location = new Point(Scale(225), Scale(57)),
                Size = new Size(Scale(120), Scale(25)),
                Checked = true
            };
            chkShowTooltips.CheckedChanged += (s, e) => { showTooltips = chkShowTooltips.Checked; };
            treeControlPanel.Controls.Add(chkShowTooltips);

            rightPanel.Controls.Add(treeControlPanel);

            // Add in reverse Z-order: Fill first, then splitter, then Left
            this.Controls.Add(rightPanel);

            // Add a splitter for resizing
            Splitter splitter = new Splitter
            {
                Dock = DockStyle.Left,
                Width = Scale(5),
                BackColor = Color.DarkGray
            };
            this.Controls.Add(splitter);

            // Add left panel last
            this.Controls.Add(leftPanel);

            // Drag and drop handlers
            this.DragEnter += Form1_DragEnter;
            this.DragDrop += Form1_DragDrop;
        }

        private void LoadSettings()
        {
            // Load player filter
            string savedFilter = RegistryUtils.GetString("PlayerFilter", "Theodore");
            txtPlayerFilter.Text = savedFilter;

            // Load depth filter
            int savedDepth = RegistryUtils.GetInt("MaxDepth", 50);
            numDepth.Value = savedDepth;

            // Load color selection
            bool playingWhite = RegistryUtils.GetBool("PlayingWhite", true);
            rbWhite.Checked = playingWhite;
            rbBlack.Checked = !playingWhite;

            // Load tooltip preference
            bool showTooltipsPref = RegistryUtils.GetBool("ShowTooltips", true);
            chkShowTooltips.Checked = showTooltipsPref;
            showTooltips = showTooltipsPref;

            // Auto-load last cache file
            string lastCachePath = RegistryUtils.GetString("LastCachePath", "");
            if (!string.IsNullOrEmpty(lastCachePath) && File.Exists(lastCachePath))
            {
                AutoLoadCacheFile(lastCachePath);
            }

            // Auto-load last Theodore PGN files
            var lastPgnFiles = RegistryUtils.GetFileList();
            if (lastPgnFiles.Count > 0 && lastPgnFiles.All(File.Exists))
            {
                LoadPGNFiles(lastPgnFiles);
            }
        }

        private void SaveSettings()
        {
            RegistryUtils.SetString("PlayerFilter", txtPlayerFilter.Text);
            RegistryUtils.SetInt("MaxDepth", (int)numDepth.Value);
            RegistryUtils.SetBool("PlayingWhite", rbWhite.Checked);
            RegistryUtils.SetBool("ShowTooltips", chkShowTooltips.Checked);
        }

        private void AutoLoadCacheFile(string filePath)
        {
            // Use the same progress dialog as BtnLoadCachedDatabase_Click
            using (var progressForm = new Form())
            {
                progressForm.Text = "Loading Database Cache (Startup)";
                progressForm.Size = new Size(Scale(500), Scale(260));
                progressForm.StartPosition = FormStartPosition.CenterScreen;
                progressForm.FormBorderStyle = FormBorderStyle.FixedDialog;
                progressForm.MaximizeBox = false;
                progressForm.MinimizeBox = false;
                progressForm.ControlBox = false;

                Label lblTitle = new Label
                {
                    Text = $"Loading cache: {Path.GetFileName(filePath)}...",
                    Location = new Point(Scale(30), Scale(25)),
                    Size = new Size(Scale(440), Scale(25)),
                    Font = new Font("Segoe UI", 11, FontStyle.Bold),
                    TextAlign = ContentAlignment.MiddleLeft
                };
                progressForm.Controls.Add(lblTitle);

                Label lblStatus = new Label
                {
                    Text = "This may take a minute for large databases.",
                    Location = new Point(Scale(30), Scale(55)),
                    Size = new Size(Scale(440), Scale(25)),
                    Font = new Font("Segoe UI", 9),
                    ForeColor = Color.DarkGray,
                    TextAlign = ContentAlignment.MiddleLeft
                };
                progressForm.Controls.Add(lblStatus);

                Label lblGamesLoaded = new Label
                {
                    Text = "Games loaded: 0",
                    Location = new Point(Scale(30), Scale(85)),
                    Size = new Size(Scale(440), Scale(25)),
                    Font = new Font("Segoe UI", 10, FontStyle.Bold),
                    ForeColor = Color.DarkBlue,
                    TextAlign = ContentAlignment.MiddleLeft
                };
                progressForm.Controls.Add(lblGamesLoaded);

                ProgressBar progressBar = new ProgressBar
                {
                    Location = new Point(Scale(30), Scale(120)),
                    Size = new Size(Scale(440), Scale(35)),
                    Style = ProgressBarStyle.Marquee,
                    MarqueeAnimationSpeed = 30
                };
                progressForm.Controls.Add(progressBar);

                // Declare variables first to avoid circular dependency
                bool userCancelled = false;
                bool usePartial = false;
                Button btnCancel = null!;
                Button btnStopAndUse = null!;

                btnStopAndUse = new Button
                {
                    Text = "Stop & Use Partial",
                    Location = new Point(Scale(260), Scale(175)),
                    Size = new Size(Scale(140), Scale(30)),
                    BackColor = Color.LightGreen
                };
                btnStopAndUse.Click += (s, args) =>
                {
                    usePartial = true;
                    userCancelled = true;
                    lblStatus.Text = "Stopping... will use partial data";
                    btnCancel.Enabled = false;
                    btnStopAndUse.Enabled = false;
                };
                progressForm.Controls.Add(btnStopAndUse);

                btnCancel = new Button
                {
                    Text = "Cancel",
                    Location = new Point(Scale(140), Scale(175)),
                    Size = new Size(Scale(100), Scale(30)),
                    DialogResult = DialogResult.Cancel
                };
                btnCancel.Click += (s, args) =>
                {
                    userCancelled = true;
                    lblStatus.Text = "Cancelling...";
                    btnCancel.Enabled = false;
                    btnStopAndUse.Enabled = false;
                };
                progressForm.Controls.Add(btnCancel);

                progressForm.Show();
                Application.DoEvents();

                try
                {
                    var startTime = DateTime.Now;
                    
                    // Track progress
                    long bytesRead = 0;
                    long totalBytes = 0;
                    object progressLock = new object();
                    var loadProgress = new Progress<(long bytes, long total)>(tuple =>
                    {
                        lock (progressLock)
                        {
                            bytesRead = tuple.bytes;
                            totalBytes = tuple.total;
                        }
                    });
                    
                    // Load on background thread to keep UI responsive
                    MoveFrequencyTree? loadedTree = null;
                    Exception? loadException = null;
                    var loadTask = System.Threading.Tasks.Task.Run(() =>
                    {
                        try
                        {
                            loadedTree = MoveFrequencyTree.LoadFromFile(
                                filePath,
                                loadProgress,
                                () => userCancelled
                            );
                        }
                        catch (Exception ex)
                        {
                            loadException = ex;
                        }
                    });

                    // Update UI while loading
                    while (!loadTask.IsCompleted)
                    {
                        Application.DoEvents();
                        var elapsed = DateTime.Now - startTime;
                        
                        lock (progressLock)
                        {
                            if (totalBytes > 0)
                            {
                                double percent = (bytesRead * 100.0) / totalBytes;
                                double mbRead = bytesRead / (1024.0 * 1024.0);
                                double mbTotal = totalBytes / (1024.0 * 1024.0);
                                lblStatus.Text = $"Loading... {percent:F1}% - Elapsed: {elapsed.TotalSeconds:F1}s";
                                lblGamesLoaded.Text = $"Read: {mbRead:F1} MB / {mbTotal:F1} MB";
                            }
                            else
                            {
                                lblStatus.Text = $"Loading... Elapsed time: {elapsed.TotalSeconds:F1}s";
                                lblGamesLoaded.Text = $"Starting...";
                            }
                        }
                        
                        System.Threading.Thread.Sleep(100);
                    }

                    // Wait for task to complete
                    try
                    {
                        loadTask.Wait();
                    }
                    catch { }

                    progressForm.Close();
                    
                    if (userCancelled && !usePartial)
                    {
                        // User cancelled - continue without database
                        return;
                    }
                    
                    if (loadedTree != null)
                    {
                        databaseTree = loadedTree;
                        btnViewDatabase.Enabled = true;
                        btnSaveDatabase.Enabled = true;
                        lblStatus.Text = $"Auto-loaded cache: {databaseTree.TotalGamesProcessed:N0} games";
                    }
                    // Silently fail if error - don't show error on startup
                }
                catch (Exception)
                {
                    progressForm.Close();
                    // Silently fail - don't show error on startup
                }
            }
        }

        private void BtnLoadFiles_Click(object? sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Filter = "PGN Files (*.pgn)|*.pgn|All Files (*.*)|*.*";
                ofd.Multiselect = true;
                ofd.Title = "Select PGN Files";

                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    LoadPGNFiles(ofd.FileNames.ToList());
                    RegistryUtils.SetFileList(ofd.FileNames.ToList());
                }
            }
        }

        private void LoadPGNFiles(List<string> filePaths)
        {
            allGames.Clear();
            lstGames.Items.Clear();

            PgnParser parser = new PgnParser();

            foreach (string filePath in filePaths)
            {
                try
                {
                    // CRITICAL: Check file size before loading
                    FileInfo fileInfo = new FileInfo(filePath);
                    long fileSizeMB = fileInfo.Length / (1024 * 1024);
                    
                    if (fileSizeMB > 100)
                    {
                        var result = MessageBox.Show(
                            $"File '{Path.GetFileName(filePath)}' is {fileSizeMB:N0} MB.\n\n" +
                            $"Large files should be loaded using 'Load Database' button instead.\n\n" +
                            $"Skip this file?",
                            "Large File Detected",
                            MessageBoxButtons.YesNo,
                            MessageBoxIcon.Warning);
                        
                        if (result == DialogResult.Yes)
                        {
                            continue; // Skip this file
                        }
                    }
                    
                    string pgnText = File.ReadAllText(filePath);
                    var games = parser.ParseGames(pgnText);
                    allGames.AddRange(games);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error loading {Path.GetFileName(filePath)}: {ex.Message}",
                        "Load Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }

            lblStatus.Text = $"Loaded {allGames.Count} games from {filePaths.Count} file(s)";
            
            // Auto-apply filter if we have games and a filter is set
            if (allGames.Count > 0 && !string.IsNullOrWhiteSpace(txtPlayerFilter.Text))
            {
                ApplyFilter();
            }
        }

        private void BtnApplyFilter_Click(object? sender, EventArgs e)
        {
            SaveSettings();
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            if (allGames.Count == 0)
            {
                lblStatus.Text = "No games loaded";
                treeView.Nodes.Clear();
                return;
            }

            string filterText = txtPlayerFilter.Text.Trim();
            bool filterForWhite = rbWhite.Checked;

            if (string.IsNullOrEmpty(filterText))
            {
                MessageBox.Show("Please enter a player name to filter.", "No Filter",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Split by comma and process each keyword
            var includeKeywords = new List<string>();
            var excludeKeywords = new List<string>();
            
            foreach (var keyword in filterText.Split(','))
            {
                var trimmed = keyword.Trim().ToLowerInvariant();
                if (string.IsNullOrEmpty(trimmed))
                    continue;
                    
                if (trimmed.StartsWith("!"))
                {
                    // Exclude keyword (remove the ! prefix)
                    var exclude = trimmed.Substring(1).Trim();
                    if (!string.IsNullOrEmpty(exclude))
                        excludeKeywords.Add(exclude);
                }
                else
                {
                    // Include keyword
                    includeKeywords.Add(trimmed);
                }
            }

            // Must have at least one include keyword
            if (includeKeywords.Count == 0)
            {
                MessageBox.Show("Please enter at least one player name to include (without !).", "No Include Filter",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Filter games where the player is playing the selected color
            filteredGames = allGames.Where(game =>
            {
                string white = game.Tags.TryGetValue("White", out var w) ? w.ToLowerInvariant() : "";
                string black = game.Tags.TryGetValue("Black", out var b) ? b.ToLowerInvariant() : "";

                bool matchesWhite = includeKeywords.Any(keyword => white.Contains(keyword));
                bool matchesBlack = includeKeywords.Any(keyword => black.Contains(keyword));
                
                // Check exclusions
                bool excludedWhite = excludeKeywords.Any(keyword => white.Contains(keyword));
                bool excludedBlack = excludeKeywords.Any(keyword => black.Contains(keyword));

                // Return true if the player is in the correct color and not excluded
                if (filterForWhite)
                    return matchesWhite && !excludedWhite;
                else
                    return matchesBlack && !excludedBlack;
            }).ToList();

            // Update games list
            lstGames.Items.Clear();
            foreach (var game in filteredGames)
            {
                string white = game.Tags.TryGetValue("White", out var w) ? w : "?";
                string black = game.Tags.TryGetValue("Black", out var b) ? b : "?";
                string result = game.Tags.TryGetValue("Result", out var r) ? r : "*";
                string eventName = game.Tags.TryGetValue("Event", out var ev) ? ev : "?";

                // Create display string
                string displayText = $"{white} vs {black} [{result}] - {eventName}";
                lstGames.Items.Add(displayText);
            }

            // Select all items by default
            for (int i = 0; i < lstGames.Items.Count; i++)
            {
                lstGames.SetSelected(i, true);
            }

            lblStatus.Text = $"Filtered: {filteredGames.Count} of {allGames.Count} games\n" +
                           $"(Theodore as {(filterForWhite ? "White" : "Black")})\n" +
                           $"Selected: {lstGames.SelectedItems.Count}";

            // Build heatmap tree with all selected games
            BuildHeatmapTreeFromSelection();
        }

        private void LstGames_SelectedIndexChanged(object? sender, EventArgs e)
        {
            // Rebuild tree when selection changes
            if (lstGames.SelectedItems.Count > 0)
            {
                BuildHeatmapTreeFromSelection();
                bool filterForWhite = rbWhite.Checked;
                lblStatus.Text = $"Filtered: {filteredGames.Count} of {allGames.Count} games\n" +
                               $"(Theodore as {(filterForWhite ? "White" : "Black")})\n" +
                               $"Selected: {lstGames.SelectedItems.Count}";
            }
            else
            {
                treeView.Nodes.Clear();
                lblStats.Text = "No games selected";
            }
        }

        private void LstGames_KeyDown(object? sender, KeyEventArgs e)
        {
            // Handle Ctrl+A to select all
            if (e.Control && e.KeyCode == Keys.A)
            {
                for (int i = 0; i < lstGames.Items.Count; i++)
                {
                    lstGames.SetSelected(i, true);
                }
                e.Handled = true;
            }
        }

        private void BuildHeatmapTreeFromSelection()
        {
            // Get selected games based on ListBox indices
            var selectedGames = new List<PgnGame>();
            foreach (int index in lstGames.SelectedIndices)
            {
                if (index < filteredGames.Count)
                {
                    selectedGames.Add(filteredGames[index]);
                }
            }

            if (selectedGames.Count == 0)
            {
                treeView.Nodes.Clear();
                lblStats.Text = "No games selected";
                return;
            }

            // Build heatmap with selected games only
            int maxDepth = (int)numDepth.Value;
            heatmapBuilder = new HeatmapBuilder(maxDepth);

            foreach (var game in selectedGames)
            {
                heatmapBuilder.AddGame(game);
            }

            BuildTreeView();
        }

        private void BuildTreeView()
        {
            treeView.Nodes.Clear();
            nodeToHeatmap.Clear();

            if (heatmapBuilder == null)
            {
                treeView.Nodes.Add("No games to display");
                lblStats.Text = "No games loaded";
                return;
            }

            // Check if root has children
            if (heatmapBuilder.Root.Children.Count == 0)
            {
                treeView.Nodes.Add($"No moves found (checked {heatmapBuilder.Root.Frequency} games)");
                lblStats.Text = $"Error: Games loaded but no moves found";
                return;
            }

            var root = new TreeNode($"Opening ({heatmapBuilder.Root.Frequency} games)");
            root.Tag = heatmapBuilder.Root;
            nodeToHeatmap[root] = heatmapBuilder.Root;

            BuildTreeRecursive(root, heatmapBuilder.Root);

            treeView.Nodes.Add(root);
            root.Expand();

            // Update stats
            int maxFreq = heatmapBuilder.GetMaxFrequency();
            lblStats.Text = $"Selected Games: {heatmapBuilder.Root.Frequency} | " +
                          $"Unique Moves: {heatmapBuilder.Root.Children.Count} | " +
                          $"Most Frequent: {maxFreq} | " +
                          $"Theodore as {(rbWhite.Checked ? "White" : "Black")}";
        }

        private void ColorCodeNodeByFrequency(TreeNode treeNode, HeatmapNode heatmapNode, HeatmapNode parent)
        {
            int maxFreqAtLevel = parent.Children.Max(c => c.Frequency);
            float intensity = maxFreqAtLevel > 0 ? (float)heatmapNode.Frequency / maxFreqAtLevel : 0f;

            if (intensity >= 0.8f)
            {
                treeNode.ForeColor = Color.Red;
                treeNode.NodeFont = new Font(treeView.Font, FontStyle.Bold);
            }
            else if (intensity >= 0.5f)
            {
                treeNode.ForeColor = Color.DarkRed;
                treeNode.NodeFont = new Font(treeView.Font, FontStyle.Bold);
            }
            else if (intensity >= 0.2f)
            {
                treeNode.ForeColor = Color.DarkOrange;
            }
            else
            {
                treeNode.ForeColor = Color.Gray;
            }
        }

        private void BuildTreeRecursive(TreeNode parentTreeNode, HeatmapNode parentHeatmapNode)
        {
            if (parentHeatmapNode.Children.Count == 0)
                return;

            // Sort children by frequency (most frequent first)
            var sortedChildren = parentHeatmapNode.Children
                .OrderByDescending(c => c.Frequency)
                .ToList();

            // If there's only one child, continue inline without showing stats
            if (sortedChildren.Count == 1)
            {
                var child = sortedChildren[0];
                
                // Collect all moves in the linear sequence
                var sequence = new List<(int moveNum, bool isWhite, string san, HeatmapNode node)>();
                var currentNode = child;
                var lastNode = child;
                
                while (currentNode != null && currentNode.Children.Count <= 1)
                {
                    sequence.Add((currentNode.MoveNumber, currentNode.IsWhiteMove, currentNode.San, currentNode));
                    lastNode = currentNode;
                    currentNode = currentNode.Children.Count == 1 ? currentNode.Children[0] : null;
                }
                
                // Format the sequence: "1.e4 c5 2.Nf3 d6 3.d4 cxd4"
                var formatted = new System.Text.StringBuilder();
                
                for (int i = 0; i < sequence.Count; i++)
                {
                    var (moveNum, isWhite, san, node) = sequence[i];
                    
                    if (isWhite)
                    {
                        // Add space before if not first
                        if (formatted.Length > 0) formatted.Append(" ");
                        formatted.Append($"{moveNum}.{san}");
                        
                        // Check if next move is black and has same move number
                        if (i + 1 < sequence.Count && 
                            !sequence[i + 1].isWhite && 
                            sequence[i + 1].moveNum == moveNum)
                        {
                            // Add black's response: "1.e4 c5"
                            formatted.Append($" {sequence[i + 1].san}");
                            i++; // Skip next move since we already added it
                        }
                    }
                    else
                    {
                        // Black move without preceding white move in this sequence
                        // This happens at branch points
                        if (formatted.Length > 0) formatted.Append(" ");
                        formatted.Append($"{moveNum}...{san}");
                    }
                }
                
                var treeNode = new TreeNode(formatted.ToString());
                // Store the FIRST node (which has the most games) for tooltip/double-click
                treeNode.Tag = child;
                nodeToHeatmap[treeNode] = child;
                ColorCodeNode(treeNode, child, parentHeatmapNode);
                
                // Only add if we actually formatted something
                if (formatted.Length > 0)
                {
                    parentTreeNode.Nodes.Add(treeNode);
                    
                    // Continue recursively from the last node in the sequence
                    if (lastNode.Children.Count > 0)
                    {
                        BuildTreeRecursive(treeNode, lastNode);
                    }
                }
                else
                {
                    // Fallback: if formatting failed, show as a branch with stats
                    int totalGames = parentHeatmapNode.Frequency;
                    double percentage = totalGames > 0 ? (child.Frequency * 100.0 / totalGames) : 0;
                    string moveNum = GetMoveNumberString(child.MoveNumber, child.IsWhiteMove);
                    string nodeText = $"{moveNum}{child.San} ({child.Frequency}/{totalGames} = {percentage:F1}%)";
                    
                    var fallbackNode = new TreeNode(nodeText);
                    fallbackNode.Tag = child;
                    nodeToHeatmap[fallbackNode] = child;
                    ColorCodeNode(fallbackNode, child, parentHeatmapNode);
                    parentTreeNode.Nodes.Add(fallbackNode);
                    
                    // Continue from the fallback node
                    if (lastNode.Children.Count > 0)
                    {
                        BuildTreeRecursive(fallbackNode, child);
                    }
                    return;
                }
                
                return;
            }

            // Multiple branches - show each with stats
            foreach (var child in sortedChildren)
            {
                int totalGames = parentHeatmapNode.Frequency;
                double percentage = totalGames > 0 ? (child.Frequency * 100.0 / totalGames) : 0;

                string moveNum = GetMoveNumberString(child.MoveNumber, child.IsWhiteMove);
                string nodeText = $"{moveNum}{child.San} ({child.Frequency}/{totalGames} = {percentage:F1}%)";

                var treeNode = new TreeNode(nodeText);
                treeNode.Tag = child;
                nodeToHeatmap[treeNode] = child;
                ColorCodeNode(treeNode, child, parentHeatmapNode);
                parentTreeNode.Nodes.Add(treeNode);

                // Recurse
                BuildTreeRecursive(treeNode, child);
            }
        }

        private void ColorCodeNode(TreeNode treeNode, HeatmapNode heatmapNode, HeatmapNode parent)
        {
            int maxFreqAtLevel = parent.Children.Max(c => c.Frequency);
            float intensity = maxFreqAtLevel > 0 ? (float)heatmapNode.Frequency / maxFreqAtLevel : 0f;

            // Black (rare) -> Red (frequent) gradient
            if (intensity >= 0.8f)
            {
                treeNode.ForeColor = Color.Red;
                treeNode.NodeFont = new Font(treeView.Font, FontStyle.Bold);
            }
            else if (intensity >= 0.5f)
            {
                treeNode.ForeColor = Color.DarkRed;
                treeNode.NodeFont = new Font(treeView.Font, FontStyle.Bold);
            }
            else if (intensity >= 0.2f)
            {
                treeNode.ForeColor = Color.DarkOrange;
            }
            else
            {
                treeNode.ForeColor = Color.Gray;
            }
        }

        private string GetMoveNumberString(int moveNumber, bool isWhiteMove)
        {
            if (isWhiteMove)
            {
                return $"{moveNumber}.";
            }
            else
            {
                return $"{moveNumber}...";
            }
        }

        private void TreeView_NodeMouseClick(object? sender, TreeNodeMouseClickEventArgs e)
        {
            if (e.Node != null && nodeToHeatmap.ContainsKey(e.Node))
            {
                var heatmapNode = nodeToHeatmap[e.Node];
                ShowNodeTooltip(e.Node, heatmapNode);
            }
        }

        private void TreeView_NodeMouseDoubleClick(object? sender, TreeNodeMouseClickEventArgs e)
        {
            if (e.Node != null && nodeToHeatmap.ContainsKey(e.Node))
            {
                var heatmapNode = nodeToHeatmap[e.Node];
                ShowGameDetails(heatmapNode);
            }
        }

        private void ShowNodeTooltip(TreeNode treeNode, HeatmapNode heatmapNode)
        {
            if (!showTooltips)
            {
                toolTip.SetToolTip(treeView, "");
                return;
            }

            // Build tooltip text
            var (wins, losses, draws) = heatmapNode.GetStats(rbWhite.Checked);

            string tooltip = $"Move: {heatmapNode.San}\n";
            tooltip += $"Played: {heatmapNode.Frequency} times\n";
            tooltip += $"Results: {wins}W-{losses}L-{draws}D\n\n";
            tooltip += "Sample games:\n";

            int count = 0;
            foreach (var game in heatmapNode.Games.Take(5))
            {
                string white = game.Tags.TryGetValue("White", out var w) ? w : "?";
                string black = game.Tags.TryGetValue("Black", out var b) ? b : "?";
                string result = game.Tags.TryGetValue("Result", out var r) ? r : "*";

                tooltip += $"  {white} vs {black} [{result}]\n";
                count++;
            }

            if (heatmapNode.Games.Count > 5)
            {
                tooltip += $"  ... and {heatmapNode.Games.Count - 5} more";
            }

            toolTip.SetToolTip(treeView, tooltip);
        }

        private void ShowGameDetails(HeatmapNode node)
        {
            GameDetailsForm detailsForm = new GameDetailsForm(node, rbWhite.Checked);
            detailsForm.ShowDialog(this);
        }

        private void Form1_DragEnter(object? sender, DragEventArgs e)
        {
            if (e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[]? files = e.Data.GetData(DataFormats.FileDrop) as string[];
                if (files != null && files.Any(f => f.EndsWith(".pgn", StringComparison.OrdinalIgnoreCase)))
                {
                    e.Effect = DragDropEffects.Copy;
                    return;
                }
            }
            e.Effect = DragDropEffects.None;
        }

        private void Form1_DragDrop(object? sender, DragEventArgs e)
        {
            if (e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[]? files = e.Data.GetData(DataFormats.FileDrop) as string[];
                if (files != null)
                {
                    var pgnFiles = files.Where(f => f.EndsWith(".pgn", StringComparison.OrdinalIgnoreCase)).ToList();

                    if (pgnFiles.Count > 0)
                    {
                        LoadPGNFiles(pgnFiles);
                        RegistryUtils.SetFileList(pgnFiles);
                    }
                }
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            SaveSettings();
            base.OnFormClosing(e);
        }

        private void BtnLoadDatabase_Click(object? sender, EventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[BtnLoadDatabase] Button clicked");
                
                // Let user browse for the database file
                using (OpenFileDialog ofd = new OpenFileDialog())
                {
                    System.Diagnostics.Debug.WriteLine("[BtnLoadDatabase] OpenFileDialog created");
                    
                    ofd.Filter = "PGN Files (*.pgn)|*.pgn|All Files (*.*)|*.*";
                    ofd.Title = "Select Database PGN File";
                    ofd.InitialDirectory = @"C:\data\chess\chessDatabase";
                    ofd.FileName = "caissabase_export_2024-07-21.pgn";

                    System.Diagnostics.Debug.WriteLine("[BtnLoadDatabase] Showing OpenFileDialog...");
                    if (ofd.ShowDialog() != DialogResult.OK)
                    {
                        System.Diagnostics.Debug.WriteLine("[BtnLoadDatabase] Dialog cancelled");
                        return;
                    }

                    System.Diagnostics.Debug.WriteLine($"[BtnLoadDatabase] File selected: {ofd.FileName}");
                    string selectedFile = ofd.FileName;

                    // Ask user for number of games to test with
                    System.Diagnostics.Debug.WriteLine("[BtnLoadDatabase] Creating input form...");
                    using (var inputForm = new Form())
                {
                    inputForm.Text = "Load Database";
                    inputForm.Size = new Size(450, 200);
                    inputForm.StartPosition = FormStartPosition.CenterParent;
                    inputForm.FormBorderStyle = FormBorderStyle.FixedDialog;
                    inputForm.MaximizeBox = false;
                    inputForm.MinimizeBox = false;

                    Label lblInfo = new Label
                    {
                        Text = $"File: {Path.GetFileName(selectedFile)}\n\nFor testing, enter number of games to load (0 = all):",
                        Location = new Point(20, 20),
                        Size = new Size(400, 60)
                    };
                    inputForm.Controls.Add(lblInfo);

                    NumericUpDown numGames = new NumericUpDown
                    {
                        Location = new Point(20, 90),
                        Size = new Size(150, 25),
                        Minimum = 0,
                        Maximum = 10000000,
                        Value = 10000, // Default to 10000 games
                        Increment = 1000
                    };
                    inputForm.Controls.Add(numGames);

                    Label lblThreads = new Label
                    {
                        Text = "Threads:",
                        Location = new Point(190, 92),
                        Size = new Size(60, 20)
                    };
                    inputForm.Controls.Add(lblThreads);

                    NumericUpDown numThreads = new NumericUpDown
                    {
                        Location = new Point(250, 90),
                        Size = new Size(60, 25),
                        Minimum = 1,
                        Maximum = 16,
                        Value = 4 // Default to 4 threads (sweet spot for I/O)
                    };
                    inputForm.Controls.Add(numThreads);

                    Button btnOK = new Button
                    {
                        Text = "Load",
                        DialogResult = DialogResult.OK,
                        Location = new Point(250, 125),
                        Size = new Size(75, 30)
                    };
                    inputForm.Controls.Add(btnOK);

                    Button btnCancel = new Button
                    {
                        Text = "Cancel",
                        DialogResult = DialogResult.Cancel,
                        Location = new Point(335, 125),
                        Size = new Size(75, 30)
                    };
                    inputForm.Controls.Add(btnCancel);

                    inputForm.AcceptButton = btnOK;
                    inputForm.CancelButton = btnCancel;

                    if (inputForm.ShowDialog() != DialogResult.OK)
                        return;

                    int maxGames = (int)numGames.Value;
                    int threads = (int)numThreads.Value;
                    LoadDatabaseFile(selectedFile, maxGames, threads);
                }
            }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[BtnLoadDatabase] EXCEPTION: {ex.GetType().Name}: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[BtnLoadDatabase] Stack: {ex.StackTrace}");
                MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void LoadDatabaseFile(string filePath, int maxGames, int numThreads)
        {
            if (!File.Exists(filePath))
            {
                MessageBox.Show($"File not found: {filePath}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // Show file info
            FileInfo fileInfo = new FileInfo(filePath);
            var result = MessageBox.Show(
                $"File: {fileInfo.Name}\n" +
                $"Size: {fileInfo.Length / (1024.0 * 1024.0):F1} MB\n" +
                $"Games to load: {(maxGames == 0 ? "ALL" : maxGames.ToString())}\n" +
                $"Threads: {numThreads}\n\n" +
                $"Continue?",
                "Confirm Load",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result != DialogResult.Yes)
                return;

            // Create progress form
            using (var progressForm = new Form())
            {
                progressForm.Text = "Loading Database";
                progressForm.Size = new Size(400, 150);
                progressForm.StartPosition = FormStartPosition.CenterParent;
                progressForm.FormBorderStyle = FormBorderStyle.FixedDialog;
                progressForm.MaximizeBox = false;
                progressForm.MinimizeBox = false;
                progressForm.ControlBox = false;

                Label lblProgress = new Label
                {
                    Text = "Processing games...",
                    Location = new Point(20, 20),
                    Size = new Size(350, 20),
                    Font = new Font("Segoe UI", 10)
                };
                progressForm.Controls.Add(lblProgress);

                Label lblTime = new Label
                {
                    Text = "Elapsed: 0s",
                    Location = new Point(20, 45),
                    Size = new Size(350, 20),
                    Font = new Font("Segoe UI", 9),
                    ForeColor = Color.DarkGray
                };
                progressForm.Controls.Add(lblTime);

                ProgressBar progressBar = new ProgressBar
                {
                    Location = new Point(20, 60),
                    Size = new Size(350, 25),
                    Style = ProgressBarStyle.Continuous
                };
                progressForm.Controls.Add(progressBar);

                // Show form and start processing on background thread
                progressForm.Show();
                Application.DoEvents();

                try
                {
                    int maxDepth = (int)numDepth.Value;
                    System.Diagnostics.Debug.WriteLine($"[Form1] Creating MoveFrequencyTree with maxDepth={maxDepth}");
                    databaseTree = new MoveFrequencyTree(maxDepth);
                    System.Diagnostics.Debug.WriteLine($"[Form1] MoveFrequencyTree created successfully");

                    var startTime = DateTime.Now;
                    bool isMerging = false;
                    int lastProcessed = 0;
                    var progress = new Progress<(int gamesProcessed, int totalGames)>(update =>
                    {
                        var elapsed = DateTime.Now - startTime;
                        
                        // Detect if we're in merge phase (game count stopped increasing)
                        if (update.gamesProcessed == lastProcessed && !isMerging && update.gamesProcessed > 0)
                        {
                            isMerging = true;
                            lblProgress.Text = $"Merging {numThreads} trees... ({update.gamesProcessed:N0} games)";
                            lblTime.Text = $"Elapsed: {elapsed.TotalSeconds:F1}s";
                            return;
                        }
                        
                        lastProcessed = update.gamesProcessed;
                        lblProgress.Text = isMerging ? 
                            $"Merged: {update.gamesProcessed:N0} games" : 
                            $"Processed: {update.gamesProcessed:N0} games";
                        lblTime.Text = $"Elapsed: {elapsed.TotalSeconds:F1}s  |  Rate: {(update.gamesProcessed / Math.Max(1, elapsed.TotalSeconds)):F0} games/sec";
                        
                        if (maxGames > 0)
                        {
                            int percentage = (int)((update.gamesProcessed * 100.0) / maxGames);
                            progressBar.Value = Math.Min(100, percentage);
                        }
                        else
                        {
                            // Indeterminate progress - just show count
                            progressBar.Style = ProgressBarStyle.Marquee;
                        }
                    });

                    // Process on background thread
                    var task = System.Threading.Tasks.Task.Run(() =>
                    {
                        if (maxGames == 0)
                        {
                            // Full database - use chunked processing
                            databaseTree.ProcessPGNFileChunked(filePath, 50000, progress);
                        }
                        else
                        {
                            // Limited games - use parallel processing
                            databaseTree.ProcessPGNFileParallel(filePath, maxGames, progress, numThreads);
                        }
                    });

                    // Wait for completion while keeping UI responsive
                    while (!task.IsCompleted)
                    {
                        Application.DoEvents();
                        System.Threading.Thread.Sleep(100);
                    }

                    progressForm.Close();

                    MessageBox.Show($"Successfully loaded {databaseTree.TotalGamesProcessed:N0} games!\n\n" +
                                  $"Click 'View Database' to see the move frequency tree.\n" +
                                  $"Click 'Save Cache' to save for instant loading next time.",
                                  "Database Loaded", MessageBoxButtons.OK, MessageBoxIcon.Information);

                    btnViewDatabase.Enabled = true;
                    btnSaveDatabase.Enabled = true;
                }
                catch (Exception ex)
                {
                    progressForm.Close();
                    MessageBox.Show($"Error loading database: {ex.Message}", "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void BtnViewDatabase_Click(object? sender, EventArgs e)
        {
            if (databaseTree == null || databaseTree.TotalGamesProcessed == 0)
            {
                MessageBox.Show("No database loaded. Click 'Load Database' first.", "No Data",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var dbForm = new DatabaseTreeForm(databaseTree);
            dbForm.ShowDialog(this);
        }

        private void BtnSaveDatabase_Click(object? sender, EventArgs e)
        {
            if (databaseTree == null || databaseTree.TotalGamesProcessed == 0)
            {
                MessageBox.Show("No database loaded to save.", "No Data",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (SaveFileDialog sfd = new SaveFileDialog())
            {
                sfd.Filter = "Database Cache (*.dbcache)|*.dbcache|All Files (*.*)|*.*";
                sfd.Title = "Save Database Cache";
                sfd.FileName = $"chess_database_{databaseTree.TotalGamesProcessed}games.dbcache";

                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        databaseTree.SaveToFile(sfd.FileName);
                        MessageBox.Show($"Database cache saved successfully!\n\n" +
                                      $"File: {Path.GetFileName(sfd.FileName)}\n" +
                                      $"Games: {databaseTree.TotalGamesProcessed:N0}\n\n" +
                                      $"Use 'Load Cache' to load instantly next time.",
                                      "Cache Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error saving cache: {ex.Message}", "Error",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void BtnLoadCachedDatabase_Click(object? sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Filter = "Database Cache (*.dbcache)|*.dbcache|All Files (*.*)|*.*";
                ofd.Title = "Load Database Cache";

                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    // Show progress form for loading
                    using (var progressForm = new Form())
                    {
                        progressForm.Text = "Loading Database Cache";
                        progressForm.Size = new Size(500, 260);
                        progressForm.StartPosition = FormStartPosition.CenterParent;
                        progressForm.FormBorderStyle = FormBorderStyle.FixedDialog;
                        progressForm.MaximizeBox = false;
                        progressForm.MinimizeBox = false;
                        progressForm.ControlBox = false;

                        Label lblTitle = new Label
                        {
                            Text = "Loading database cache...",
                            Location = new Point(30, 25),
                            Size = new Size(440, 25),
                            Font = new Font("Segoe UI", 11, FontStyle.Bold),
                            TextAlign = ContentAlignment.MiddleLeft
                        };
                        progressForm.Controls.Add(lblTitle);

                        Label lblStatus = new Label
                        {
                            Text = "This may take a minute for large databases.",
                            Location = new Point(30, 55),
                            Size = new Size(440, 25),
                            Font = new Font("Segoe UI", 9),
                            ForeColor = Color.DarkGray,
                            TextAlign = ContentAlignment.MiddleLeft
                        };
                        progressForm.Controls.Add(lblStatus);

                        Label lblGamesLoaded = new Label
                        {
                            Text = "Games loaded: 0",
                            Location = new Point(30, 85),
                            Size = new Size(440, 25),
                            Font = new Font("Segoe UI", 10, FontStyle.Bold),
                            ForeColor = Color.DarkBlue,
                            TextAlign = ContentAlignment.MiddleLeft
                        };
                        progressForm.Controls.Add(lblGamesLoaded);

                        ProgressBar progressBar = new ProgressBar
                        {
                            Location = new Point(30, 120),
                            Size = new Size(440, 35),
                            Style = ProgressBarStyle.Marquee,
                            MarqueeAnimationSpeed = 30
                        };
                        progressForm.Controls.Add(progressBar);

                        // Declare variables first to avoid circular dependency
                        bool userCancelled = false;
                        bool usePartial = false;
                        Button btnCancel = null!;
                        Button btnStopAndUse = null!;

                        btnStopAndUse = new Button
                        {
                            Text = "Stop & Use Partial",
                            Location = new Point(260, 175),
                            Size = new Size(140, 30),
                            BackColor = Color.LightGreen
                        };
                        btnStopAndUse.Click += (s, args) =>
                        {
                            usePartial = true;
                            userCancelled = true; // Stop loading
                            lblStatus.Text = "Stopping... will use partial data";
                            btnCancel.Enabled = false;
                            btnStopAndUse.Enabled = false;
                        };
                        progressForm.Controls.Add(btnStopAndUse);

                        btnCancel = new Button
                        {
                            Text = "Cancel",
                            Location = new Point(140, 175),
                            Size = new Size(100, 30),
                            DialogResult = DialogResult.Cancel
                        };
                        btnCancel.Click += (s, args) =>
                        {
                            userCancelled = true;
                            lblStatus.Text = "Cancelling...";
                            btnCancel.Enabled = false;
                            btnStopAndUse.Enabled = false;
                        };
                        progressForm.Controls.Add(btnCancel);

                        progressForm.Show();
                        Application.DoEvents();

                        try
                        {
                            var startTime = DateTime.Now;
                            
                            // Track progress
                            long bytesRead = 0;
                            long totalBytes = 0;
                            int totalGames = 0;
                            object progressLock = new object();
                            var loadProgress = new Progress<(long bytes, long total)>(tuple =>
                            {
                                lock (progressLock)
                                {
                                    bytesRead = tuple.bytes;
                                    totalBytes = tuple.total;
                                }
                            });
                            
                            // Load on background thread to keep UI responsive
                            MoveFrequencyTree? loadedTree = null;
                            Exception? loadException = null;
                            var loadTask = System.Threading.Tasks.Task.Run(() =>
                            {
                                try
                                {
                                    loadedTree = MoveFrequencyTree.LoadFromFile(
                                        ofd.FileName,
                                        loadProgress,
                                        () => userCancelled
                                    );
                                    // Capture total games after loading metadata
                                    if (loadedTree != null)
                                    {
                                        lock (progressLock)
                                        {
                                            totalGames = loadedTree.TotalGamesProcessed;
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    loadException = ex;
                                }
                            });

                            // Update UI while loading
                            while (!loadTask.IsCompleted)
                            {
                                Application.DoEvents();
                                var elapsed = DateTime.Now - startTime;
                                
                                lock (progressLock)
                                {
                                    if (totalBytes > 0)
                                    {
                                        double percent = (bytesRead * 100.0) / totalBytes;
                                        double mbRead = bytesRead / (1024.0 * 1024.0);
                                        double mbTotal = totalBytes / (1024.0 * 1024.0);
                                        lblStatus.Text = $"Loading... {percent:F1}% - Elapsed: {elapsed.TotalSeconds:F1}s";
                                        lblGamesLoaded.Text = $"Read: {mbRead:F1} MB / {mbTotal:F1} MB";
                                    }
                                    else
                                    {
                                        lblStatus.Text = $"Loading... Elapsed time: {elapsed.TotalSeconds:F1}s";
                                        lblGamesLoaded.Text = $"Starting...";
                                    }
                                }
                                
                                System.Threading.Thread.Sleep(100);
                            }

                            // Wait for task to complete and get final exception if any
                            try
                            {
                                loadTask.Wait();
                            }
                            catch { }

                            var totalElapsed = DateTime.Now - startTime;
                            
                            // Get final percentage
                            double finalPercent;
                            lock (progressLock)
                            {
                                finalPercent = totalBytes > 0 ? (bytesRead * 100.0) / totalBytes : 100.0;
                            }
                            
                            progressForm.Close();
                            
                            if (userCancelled && !usePartial)
                            {
                                MessageBox.Show("Cache loading cancelled by user.",
                                    "Cancelled", MessageBoxButtons.OK, MessageBoxIcon.Information);
                                return;
                            }

                            if (loadException != null && !usePartial)
                            {
                                MessageBox.Show($"Error loading cache: {loadException.Message}", "Error",
                                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                                return;
                            }
                            
                            if (loadedTree != null)
                            {
                                databaseTree = loadedTree;
                                RegistryUtils.SetString("LastCachePath", ofd.FileName);
                                string partialMsg = usePartial ? $" (PARTIAL - {finalPercent:F1}% loaded)" : "";
                                MessageBox.Show($"Database cache loaded successfully!{partialMsg}\n\n" +
                                              $"Games: {databaseTree.TotalGamesProcessed:N0}\n" +
                                              $"Load time: {totalElapsed.TotalSeconds:F1} seconds\n\n" +
                                              $"Click 'View Database' to see the tree.",
                                              "Cache Loaded", MessageBoxButtons.OK, MessageBoxIcon.Information);
                                btnViewDatabase.Enabled = true;
                                btnSaveDatabase.Enabled = true;
                            }
                            else
                            {
                                MessageBox.Show("Failed to load cache file.", "Error",
                                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                            }
                        }
                        catch (Exception ex)
                        {
                            progressForm.Close();
                            MessageBox.Show($"Error loading cache: {ex.Message}", "Error",
                                MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                }
            }
        }

        private void BtnOpeningBuilder_Click(object? sender, EventArgs e)
        {
            // Get Theodore's tree from heatmapBuilder
            MoveFrequencyTree? theodoreTree = null;
            if (heatmapBuilder != null)
            {
                // Convert HeatmapBuilder to MoveFrequencyTree
                theodoreTree = ConvertHeatmapToFrequencyTree(heatmapBuilder);
            }

            // Open the Opening Builder form - pass both the frequency tree and the heatmap
            var builderForm = new OpeningBuilderForm(theodoreTree, databaseTree, rbWhite.Checked, heatmapBuilder);
            builderForm.ShowDialog(this);
        }

        private MoveFrequencyTree ConvertHeatmapToFrequencyTree(HeatmapBuilder heatmapBuilder)
        {
            // Create a new frequency tree with same depth
            var tree = new MoveFrequencyTree((int)numDepth.Value);
            
            // Convert the heatmap tree to frequency tree
            ConvertHeatmapNode(heatmapBuilder.Root, tree.Root);
            
            return tree;
        }
        
        private void ConvertHeatmapNode(HeatmapNode heatmapNode, FrequencyNode freqNode)
        {
            foreach (var child in heatmapNode.Children)
            {
                var childFreqNode = new FrequencyNode
                {
                    San = child.San,
                    MoveNumber = child.MoveNumber,
                    IsWhiteMove = child.IsWhiteMove,
                    Frequency = child.Frequency
                };

                freqNode.Children[child.San] = childFreqNode;

                // Recurse
                ConvertHeatmapNode(child, childFreqNode);
            }
        }

        // Helper method to scale sizes based on DPI
        private int Scale(int value)
        {
            return (int)(value * dpiScale);
        }
    }
}
