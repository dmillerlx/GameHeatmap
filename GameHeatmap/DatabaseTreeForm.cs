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
            // LAZY LOADING: Populate children only when node is expanded
            treeView.BeforeExpand += TreeView_BeforeExpand;
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
                Text = "Expand All (WARNING: SLOW!)",
                Location = new Point(5, 5),
                Size = new Size(200, 25),
                BackColor = Color.LightCoral
            };
            btnExpandAll.Click += (s, e) =>
            {
                if (MessageBox.Show("Expanding all nodes in a 5M game database will be VERY slow and may freeze the UI. Continue?",
                    "Warning", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                {
                    treeView.ExpandAll();
                }
            };
            buttonPanel.Controls.Add(btnExpandAll);

            Button btnCollapseAll = new Button
            {
                Text = "Collapse All",
                Location = new Point(215, 5),
                Size = new Size(100, 25)
            };
            btnCollapseAll.Click += (s, e) => treeView.CollapseAll();
            buttonPanel.Controls.Add(btnCollapseAll);

            this.Controls.Add(buttonPanel);

            // Legend - ADD THIRD (docks at top)
            Label lblLegend = new Label
            {
                Text = "Colors: Bold Red (80%+) | Dark Red (50-79%) | Orange (20-49%) | Gray (<20%)  |  Format: Move (frequency = %)  |  LAZY LOADING: Expand nodes to see moves",
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
            root.Tag = freqTree.Root; // Store FrequencyNode for lazy loading

            // LAZY LOADING: Only populate top-level moves initially
            PopulateChildren(root, freqTree.Root);

            treeView.Nodes.Add(root);
            root.Expand();

            lblStats.Text = $"Database: {freqTree.TotalGamesProcessed:N0} games | " +
                          $"Top-level moves: {freqTree.Root.Children.Count} | " +
                          $"Tree uses LAZY LOADING - expand nodes to see continuations";
        }

        private void TreeView_BeforeExpand(object? sender, TreeViewCancelEventArgs e)
        {
            if (e.Node == null) return;

            // Check if this node's children need to be populated
            // Dummy nodes have empty text
            if (e.Node.Nodes.Count == 1 && string.IsNullOrEmpty(e.Node.Nodes[0].Text))
            {
                // Remove dummy node
                e.Node.Nodes.Clear();

                // Populate real children
                var freqNode = e.Node.Tag as FrequencyNode;
                if (freqNode != null)
                {
                    PopulateChildren(e.Node, freqNode);
                }
            }
        }

        private void PopulateChildren(TreeNode parentTreeNode, FrequencyNode parentFreqNode)
        {
            if (parentFreqNode.Children.Count == 0)
                return;

            // Sort children by frequency
            var sortedChildren = parentFreqNode.Children.Values
                .OrderByDescending(c => c.Frequency)
                .ToList();

            foreach (var child in sortedChildren)
            {
                int totalGames = parentFreqNode.Frequency;
                double percentage = totalGames > 0 ? (child.Frequency * 100.0 / totalGames) : 0;

                string moveNum = child.IsWhiteMove ? $"{child.MoveNumber}." : $"{child.MoveNumber}...";
                string nodeText = $"{moveNum}{child.San} ({child.Frequency:N0} = {percentage:F1}%)";

                var treeNode = new TreeNode(nodeText);
                treeNode.Tag = child; // Store FrequencyNode for lazy loading
                ColorCodeNode(treeNode, child, parentFreqNode);

                // Add dummy node if this child has children (shows + icon)
                if (child.Children.Count > 0)
                {
                    treeNode.Nodes.Add(new TreeNode("")); // Empty dummy node
                }

                parentTreeNode.Nodes.Add(treeNode);
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
