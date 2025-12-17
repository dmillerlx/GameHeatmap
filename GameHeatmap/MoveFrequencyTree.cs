using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GameHeatmap
{
    /// <summary>
    /// Lightweight node that only tracks move frequencies, not game data
    /// </summary>
    public class FrequencyNode
    {
        public string San { get; set; } = "";
        public int MoveNumber { get; set; }
        public bool IsWhiteMove { get; set; }
        public int Frequency { get; set; } = 0;
        public Dictionary<string, FrequencyNode> Children { get; set; } = new Dictionary<string, FrequencyNode>();

        public FrequencyNode FindOrCreateChild(string san, int moveNumber, bool isWhiteMove)
        {
            if (!Children.ContainsKey(san))
            {
                Children[san] = new FrequencyNode
                {
                    San = san,
                    MoveNumber = moveNumber,
                    IsWhiteMove = isWhiteMove
                };
            }
            return Children[san];
        }
    }

    /// <summary>
    /// Builds a lightweight frequency tree from a massive PGN file
    /// </summary>
    public class MoveFrequencyTree
    {
        public FrequencyNode Root { get; private set; }
        private int maxDepth;
        private int totalGamesProcessed = 0;

        public int TotalGamesProcessed => totalGamesProcessed;

        public MoveFrequencyTree(int maxDepth = 50)
        {
            Root = new FrequencyNode { San = "START", MoveNumber = 0, IsWhiteMove = true };
            // Double the depth since we count plies (half-moves) but users think in full moves
            this.maxDepth = maxDepth * 2;
        }

        /// <summary>
        /// Process a massive PGN file and build frequency tree
        /// </summary>
        public void ProcessPGNFile(string filePath, int maxGamesToProcess = 0, IProgress<(int gamesProcessed, int totalGames)>? progress = null)
        {
            totalGamesProcessed = 0;
            PgnParser parser = new PgnParser();

            int estimatedGames = maxGamesToProcess;
            int linesRead = 0;
            int gamesAttempted = 0;

            using (StreamReader reader = new StreamReader(filePath))
            {
                StringBuilder currentGame = new StringBuilder();
                StringBuilder moveText = new StringBuilder();
                string? line;
                bool hasGameTags = false;
                bool inMoveText = false;

                while ((line = reader.ReadLine()) != null)
                {
                    linesRead++;
                    
                    // Check if we've hit the max
                    if (maxGamesToProcess > 0 && totalGamesProcessed >= maxGamesToProcess)
                        break;

                    // Skip completely blank lines at start
                    if (currentGame.Length == 0 && string.IsNullOrWhiteSpace(line))
                        continue;

                    // Detect PGN tag
                    if (line.StartsWith("["))
                    {
                        hasGameTags = true;
                        inMoveText = false;
                        currentGame.AppendLine(line);
                    }
                    // Non-tag, non-blank line (move text)
                    else if (!string.IsNullOrWhiteSpace(line))
                    {
                        inMoveText = true;
                        // Concatenate move lines with a space (moves are wrapped across lines)
                        moveText.Append(line);
                        moveText.Append(" ");
                    }
                    // Blank line - end of game
                    else if (hasGameTags)
                    {
                        // Add the complete move text to the game
                        if (moveText.Length > 0)
                        {
                            // Add a blank line between tags and moves (PGN standard)
                            currentGame.AppendLine();
                            currentGame.AppendLine(moveText.ToString().Trim());
                            moveText.Clear();
                        }
                        
                        if (currentGame.Length > 0)
                        {
                            // Process the game
                            gamesAttempted++;
                            try
                            {
                                string gameText = currentGame.ToString();
                                
                                var games = parser.ParseGames(gameText);
                                if (games.Count > 0)
                                {
                                    AddGame(games[0]);
                                    totalGamesProcessed++;

                                    // Report progress every 1000 games (not every 100)
                                    if (totalGamesProcessed % 1000 == 0)
                                    {
                                        progress?.Report((totalGamesProcessed, estimatedGames));
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                // Skip bad games silently
                            }
                        }

                        // Reset for next game
                        currentGame.Clear();
                        moveText.Clear();
                        hasGameTags = false;
                        inMoveText = false;
                    }
                }

                // Process last game if exists
                if (hasGameTags)
                {
                    // Add any remaining move text
                    if (moveText.Length > 0)
                    {
                        // Add a blank line between tags and moves (PGN standard)
                        currentGame.AppendLine();
                        currentGame.AppendLine(moveText.ToString().Trim());
                        moveText.Clear();
                    }
                    
                    if (currentGame.Length > 0)
                    {
                        gamesAttempted++;
                        try
                        {
                            string gameText = currentGame.ToString();
                            var games = parser.ParseGames(gameText);
                            if (games.Count > 0)
                            {
                                AddGame(games[0]);
                                totalGamesProcessed++;
                            }
                        }
                        catch (Exception)
                        {
                            // Skip bad games silently
                        }
                    }
                }
            }

            // Debug output
            System.Diagnostics.Debug.WriteLine($"Lines read: {linesRead}, Games attempted: {gamesAttempted}, Games processed: {totalGamesProcessed}");
            progress?.Report((totalGamesProcessed, estimatedGames));
        }

        private void AddGame(PgnGame game)
        {
            AddGameMainLine(game.MoveTreeRoot, Root, 0);
        }

        private void AddGameMainLine(MoveNode moveNode, FrequencyNode freqNode, int depth)
        {
            // Check depth limit
            if (depth >= maxDepth)
                return;

            // Increment frequency
            freqNode.Frequency++;

            // Only follow the FIRST move (main line) - ignore variations
            if (moveNode.NextMoves.Count > 0)
            {
                var nextMove = moveNode.NextMoves[0];

                if (!string.IsNullOrEmpty(nextMove.San))
                {
                    // Determine if this is a white or black move
                    bool isWhiteMove = (depth % 2 == 0);
                    int actualMoveNumber = (depth / 2) + 1;

                    // Find or create child node
                    var childFreqNode = freqNode.FindOrCreateChild(
                        nextMove.San,
                        actualMoveNumber,
                        isWhiteMove
                    );

                    // Recursively add game through main line only
                    AddGameMainLine(nextMove, childFreqNode, depth + 1);
                }
            }
        }

        public int GetMaxFrequency()
        {
            return GetMaxFrequencyRecursive(Root);
        }

        private int GetMaxFrequencyRecursive(FrequencyNode node)
        {
            int max = node.Frequency;
            foreach (var child in node.Children.Values)
            {
                max = Math.Max(max, GetMaxFrequencyRecursive(child));
            }
            return max;
        }

        /// <summary>
        /// Save the tree to a binary file for fast loading
        /// </summary>
        public void SaveToFile(string filePath)
        {
            using (var writer = new BinaryWriter(File.Open(filePath, FileMode.Create)))
            {
                // Write metadata
                writer.Write(maxDepth);
                writer.Write(totalGamesProcessed);
                
                // Write tree structure
                WriteNode(writer, Root);
            }
        }

        private void WriteNode(BinaryWriter writer, FrequencyNode node)
        {
            writer.Write(node.San);
            writer.Write(node.MoveNumber);
            writer.Write(node.IsWhiteMove);
            writer.Write(node.Frequency);
            writer.Write(node.Children.Count);
            
            foreach (var child in node.Children.Values)
            {
                WriteNode(writer, child);
            }
        }

        /// <summary>
        /// Load a tree from a binary file
        /// </summary>
        public static MoveFrequencyTree? LoadFromFile(string filePath)
        {
            if (!File.Exists(filePath))
                return null;

            try
            {
                using (var reader = new BinaryReader(File.Open(filePath, FileMode.Open)))
                {
                    var tree = new MoveFrequencyTree();
                    
                    // Read metadata
                    tree.maxDepth = reader.ReadInt32();
                    tree.totalGamesProcessed = reader.ReadInt32();
                    
                    // Read tree structure
                    tree.Root = ReadNode(reader);
                    
                    return tree;
                }
            }
            catch
            {
                return null;
            }
        }

        private static FrequencyNode ReadNode(BinaryReader reader)
        {
            var node = new FrequencyNode
            {
                San = reader.ReadString(),
                MoveNumber = reader.ReadInt32(),
                IsWhiteMove = reader.ReadBoolean(),
                Frequency = reader.ReadInt32()
            };
            
            int childCount = reader.ReadInt32();
            for (int i = 0; i < childCount; i++)
            {
                var child = ReadNode(reader);
                node.Children[child.San] = child;
            }
            
            return node;
        }
    }
}
