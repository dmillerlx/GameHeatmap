using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace GameHeatmap
{
    public class DatabaseTreeForm : Form
    {
        private TreeView treeView;
        private Label lblStats;
        private MoveFrequencyTree? freqTree;

        public DatabaseTreeForm(MoveFrequencyTree freqTree)
        {
            this.freqTree = freqTree;
            InitializeComponent();
            BuildTree();
        }

        private void InitializeComponent()
        {
            this.Text = "Database Move Frequency Tree";
            this.Size = new Size(1000, 700);
            this.StartPosition = FormStartPosition.CenterParent;

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
            this.Controls.Add(treeView);

            // Buttons panel - ADD SECOND (docks at top)
            Panel buttonPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 35,
                Padding = new Padding(5)
            };

            Button btnExpandAll = new Button
            {
                Text = "Expand All",
                Location = new Point(5, 5),
                Size = new Size(100, 25)
            };
            btnExpandAll.Click += (s, e) => treeView.ExpandAll();
            buttonPanel.Controls.Add(btnExpandAll);

            Button btnCollapseAll = new Button
            {
                Text = "Collapse All",
                Location = new Point(115, 5),
                Size = new Size(100, 25)
            };
            btnCollapseAll.Click += (s, e) => treeView.CollapseAll();
            buttonPanel.Controls.Add(btnCollapseAll);

            this.Controls.Add(buttonPanel);

            // Legend - ADD THIRD (docks at top)
            Label lblLegend = new Label
            {
                Text = "Colors: Bold Red (80%+) | Dark Red (50-79%) | Orange (20-49%) | Gray (<20%)  |  Format: Move (frequency = %)",
                Dock = DockStyle.Top,
                Height = 25,
                Font = new Font("Segoe UI", 9),
                ForeColor = Color.DarkSlateGray,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(10, 5, 10, 0),
                BackColor = Color.WhiteSmoke
            };
            this.Controls.Add(lblLegend);

            // Stats label - ADD LAST (docks at top)
            lblStats = new Label
            {
                Dock = DockStyle.Top,
                Height = 30,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(10, 5, 10, 5),
                BackColor = Color.LightSteelBlue
            };
            this.Controls.Add(lblStats);
        }

        private void BuildTree()
        {
            treeView.Nodes.Clear();

            if (freqTree == null || freqTree.Root.Children.Count == 0)
            {
                treeView.Nodes.Add("No data loaded");
                lblStats.Text = "No games processed";
                return;
            }

            var root = new TreeNode($"Database ({freqTree.TotalGamesProcessed:N0} games)");
            BuildTreeRecursive(root, freqTree.Root);
            treeView.Nodes.Add(root);
            root.Expand();

            lblStats.Text = $"Database: {freqTree.TotalGamesProcessed:N0} games | " +
                          $"Unique first moves: {freqTree.Root.Children.Count} | " +
                          $"Max frequency: {freqTree.GetMaxFrequency():N0}";
        }

        private void BuildTreeRecursive(TreeNode parentTreeNode, FrequencyNode parentFreqNode)
        {
            if (parentFreqNode.Children.Count == 0)
                return;

            // Sort children by frequency (most frequent first)
            var sortedChildren = parentFreqNode.Children.Values
                .OrderByDescending(c => c.Frequency)
                .ToList();

            // If there's only one child, show as linear continuation
            if (sortedChildren.Count == 1)
            {
                var child = sortedChildren[0];
                
                // Collect linear sequence
                var sequence = new List<(int moveNum, bool isWhite, string san, FrequencyNode node)>();
                var currentNode = child;
                var lastNode = child;
                
                while (currentNode != null && currentNode.Children.Count <= 1)
                {
                    sequence.Add((currentNode.MoveNumber, currentNode.IsWhiteMove, currentNode.San, currentNode));
                    lastNode = currentNode;
                    currentNode = currentNode.Children.Count == 1 ? currentNode.Children.Values.First() : null;
                }
                
                // Format sequence
                var formatted = new System.Text.StringBuilder();
                
                for (int i = 0; i < sequence.Count; i++)
                {
                    var (moveNum, isWhite, san, node) = sequence[i];
                    
                    if (isWhite)
                    {
                        if (formatted.Length > 0) formatted.Append(" ");
                        formatted.Append($"{moveNum}.{san}");
                        
                        if (i + 1 < sequence.Count && 
                            !sequence[i + 1].isWhite && 
                            sequence[i + 1].moveNum == moveNum)
                        {
                            formatted.Append($" {sequence[i + 1].san}");
                            i++;
                        }
                    }
                    else
                    {
                        if (formatted.Length > 0) formatted.Append(" ");
                        formatted.Append($"{moveNum}...{san}");
                    }
                }
                
                if (formatted.Length > 0)
                {
                    var treeNode = new TreeNode(formatted.ToString());
                    ColorCodeNode(treeNode, child, parentFreqNode);
                    parentTreeNode.Nodes.Add(treeNode);
                    
                    if (lastNode.Children.Count > 0)
                    {
                        BuildTreeRecursive(treeNode, lastNode);
                    }
                }
                
                return;
            }

            // Multiple branches - show each with stats
            foreach (var child in sortedChildren)
            {
                int totalGames = parentFreqNode.Frequency;
                double percentage = totalGames > 0 ? (child.Frequency * 100.0 / totalGames) : 0;

                string moveNum = child.IsWhiteMove ? $"{child.MoveNumber}." : $"{child.MoveNumber}...";
                string nodeText = $"{moveNum}{child.San} ({child.Frequency:N0} = {percentage:F1}%)";

                var treeNode = new TreeNode(nodeText);
                ColorCodeNode(treeNode, child, parentFreqNode);
                parentTreeNode.Nodes.Add(treeNode);

                BuildTreeRecursive(treeNode, child);
            }
        }

        private void ColorCodeNode(TreeNode treeNode, FrequencyNode freqNode, FrequencyNode parent)
        {
            int maxFreqAtLevel = parent.Children.Values.Max(c => c.Frequency);
            float intensity = maxFreqAtLevel > 0 ? (float)freqNode.Frequency / maxFreqAtLevel : 0f;

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
    }
}
