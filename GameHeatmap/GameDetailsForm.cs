using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace GameHeatmap
{
    public class GameDetailsForm : Form
    {
        private DataGridView dgvGames;
        private Label lblTitle;
        private Label lblStats;

        public GameDetailsForm(HeatmapNode node, bool isFilteredForWhite)
        {
            InitializeComponent(node, isFilteredForWhite);
        }

        private void InitializeComponent(HeatmapNode node, bool isFilteredForWhite)
        {
            this.Text = $"Games with move: {node.San}";
            this.Size = new Size(900, 600);
            this.StartPosition = FormStartPosition.CenterScreen;

            // DataGridView (ADD FIRST - Fill takes remaining space)
            dgvGames = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                RowHeadersVisible = false
            };

            // Add columns
            dgvGames.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Color",
                HeaderText = "Color",
                FillWeight = 60
            });
            dgvGames.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "White",
                HeaderText = "White",
                FillWeight = 150
            });
            dgvGames.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Black",
                HeaderText = "Black",
                FillWeight = 150
            });
            dgvGames.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Event",
                HeaderText = "Event",
                FillWeight = 250
            });
            dgvGames.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Result",
                HeaderText = "Result",
                FillWeight = 70
            });
            dgvGames.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Date",
                HeaderText = "Date",
                FillWeight = 100
            });

            // Populate data
            foreach (var game in node.Games)
            {
                string white = game.Tags.TryGetValue("White", out var w) ? w : "?";
                string black = game.Tags.TryGetValue("Black", out var b) ? b : "?";
                string eventName = game.Tags.TryGetValue("Event", out var e) ? e : "?";
                string result = game.Tags.TryGetValue("Result", out var r) ? r : "*";
                string date = game.Tags.TryGetValue("Date", out var d) ? d : "?";

                // Determine which color the filtered player is playing
                string playerColor = isFilteredForWhite ? "White" : "Black";

                var row = new DataGridViewRow();
                row.CreateCells(dgvGames);
                row.Cells[0].Value = playerColor;
                row.Cells[1].Value = white;
                row.Cells[2].Value = black;
                row.Cells[3].Value = eventName;
                row.Cells[4].Value = result;
                row.Cells[5].Value = date;

                // Color code the result cell
                if (result == "1-0")
                {
                    // White won
                    row.Cells[4].Style.BackColor = isFilteredForWhite ? Color.LightGreen : Color.LightCoral;
                    row.Cells[4].Style.ForeColor = Color.Black;
                }
                else if (result == "0-1")
                {
                    // Black won
                    row.Cells[4].Style.BackColor = isFilteredForWhite ? Color.LightCoral : Color.LightGreen;
                    row.Cells[4].Style.ForeColor = Color.Black;
                }
                else if (result == "1/2-1/2")
                {
                    // Draw
                    row.Cells[4].Style.BackColor = Color.LightYellow;
                    row.Cells[4].Style.ForeColor = Color.Black;
                }

                dgvGames.Rows.Add(row);
            }

            // Add DataGridView first (it will take remaining space)
            this.Controls.Add(dgvGames);

            // Stats label (ADD SECOND - will dock at top above grid)
            var (wins, losses, draws) = node.GetStats(isFilteredForWhite);
            lblStats = new Label
            {
                Text = $"Results: {wins} wins, {losses} losses, {draws} draws",
                Dock = DockStyle.Top,
                Height = 30,
                Font = new Font("Segoe UI", 10),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(10, 5, 10, 5),
                BackColor = Color.WhiteSmoke
            };
            this.Controls.Add(lblStats);

            // Title label (ADD LAST - will dock at very top)
            lblTitle = new Label
            {
                Text = $"Move: {node.San} (played {node.Frequency} times)",
                Dock = DockStyle.Top,
                Height = 35,
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(10, 5, 10, 5),
                BackColor = Color.LightSteelBlue
            };
            this.Controls.Add(lblTitle);
        }
    }
}
