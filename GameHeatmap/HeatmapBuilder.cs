using System;
using System.Collections.Generic;
using System.Linq;

namespace GameHeatmap
{
    /// <summary>
    /// Represents a node in the heatmap tree with game frequency information
    /// </summary>
    public class HeatmapNode
    {
        public string San { get; set; }
        public int MoveNumber { get; set; }
        public bool IsWhiteMove { get; set; }
        
        /// <summary>
        /// List of games that passed through this node
        /// </summary>
        public List<PgnGame> Games { get; set; } = new List<PgnGame>();
        
        /// <summary>
        /// Child moves from this position
        /// </summary>
        public List<HeatmapNode> Children { get; set; } = new List<HeatmapNode>();
        
        /// <summary>
        /// Parent node
        /// </summary>
        public HeatmapNode Parent { get; set; }
        
        /// <summary>
        /// The board state at this position (for visualization)
        /// </summary>
        public string[,] BoardState { get; set; }
        
        /// <summary>
        /// Frequency of this move (number of games)
        /// </summary>
        public int Frequency => Games.Count;
        
        /// <summary>
        /// Get color intensity based on frequency (0.0 to 1.0)
        /// </summary>
        public float GetIntensity(int maxFrequency)
        {
            if (maxFrequency == 0) return 0f;
            return (float)Frequency / maxFrequency;
        }

        /// <summary>
        /// Get statistics for games through this node
        /// </summary>
        public (int wins, int losses, int draws) GetStats(bool forWhite)
        {
            int wins = 0, losses = 0, draws = 0;
            
            foreach (var game in Games)
            {
                if (game.Tags.TryGetValue("Result", out string result))
                {
                    if (result == "1/2-1/2")
                        draws++;
                    else if (forWhite && result == "1-0")
                        wins++;
                    else if (forWhite && result == "0-1")
                        losses++;
                    else if (!forWhite && result == "0-1")
                        wins++;
                    else if (!forWhite && result == "1-0")
                        losses++;
                }
            }
            
            return (wins, losses, draws);
        }

        /// <summary>
        /// Find or create a child node with the given move
        /// </summary>
        public HeatmapNode FindOrCreateChild(string san, int moveNumber, bool isWhiteMove)
        {
            var child = Children.FirstOrDefault(c => c.San == san);
            if (child == null)
            {
                child = new HeatmapNode
                {
                    San = san,
                    MoveNumber = moveNumber,
                    IsWhiteMove = isWhiteMove,
                    Parent = this
                };
                Children.Add(child);
            }
            return child;
        }
    }

    /// <summary>
    /// Builds a heatmap tree from filtered games
    /// </summary>
    public class HeatmapBuilder
    {
        public HeatmapNode Root { get; private set; }
        private int maxDepth;

        public HeatmapBuilder(int maxDepth = 50)
        {
            Root = new HeatmapNode { San = "START", MoveNumber = 0, IsWhiteMove = true };
            // Double the depth since we count plies (half-moves) but users think in full moves
            this.maxDepth = maxDepth * 2;
        }

        /// <summary>
        /// Add a game to the heatmap tree
        /// </summary>
        public void AddGame(PgnGame game)
        {
            AddGameMainLine(game, game.MoveTreeRoot, Root, 0);
        }

        private void AddGameMainLine(PgnGame game, MoveNode moveNode, HeatmapNode heatmapNode, int depth)
        {
            // Check depth limit
            if (depth >= maxDepth)
                return;

            // Add game to current node
            if (!heatmapNode.Games.Contains(game))
            {
                heatmapNode.Games.Add(game);
            }

            // Only follow the FIRST move (main line) - ignore variations
            if (moveNode.NextMoves.Count > 0)
            {
                var nextMove = moveNode.NextMoves[0]; // First move is always the main line
                
                if (!string.IsNullOrEmpty(nextMove.San))
                {
                    // Determine if this is a white or black move
                    // White moves: depth is even (0, 2, 4, ...) -> moves 1, 2, 3...
                    // Black moves: depth is odd (1, 3, 5, ...) -> moves 1, 2, 3...
                    bool isWhiteMove = (depth % 2 == 0);
                    int actualMoveNumber = (depth / 2) + 1;
                    
                    // Find or create child node
                    var childHeatmapNode = heatmapNode.FindOrCreateChild(
                        nextMove.San,
                        actualMoveNumber,
                        isWhiteMove
                    );

                    // Recursively add game through main line only
                    AddGameMainLine(game, nextMove, childHeatmapNode, depth + 1);
                }
            }
        }

        /// <summary>
        /// Get maximum frequency at any depth level (for normalization)
        /// </summary>
        public int GetMaxFrequency()
        {
            return GetMaxFrequencyRecursive(Root);
        }

        private int GetMaxFrequencyRecursive(HeatmapNode node)
        {
            int max = node.Frequency;
            foreach (var child in node.Children)
            {
                max = Math.Max(max, GetMaxFrequencyRecursive(child));
            }
            return max;
        }

        /// <summary>
        /// Get all moves at a specific depth
        /// </summary>
        public List<HeatmapNode> GetMovesAtDepth(int depth)
        {
            var moves = new List<HeatmapNode>();
            GetMovesAtDepthRecursive(Root, 0, depth, moves);
            return moves;
        }

        private void GetMovesAtDepthRecursive(HeatmapNode node, int currentDepth, int targetDepth, List<HeatmapNode> moves)
        {
            if (currentDepth == targetDepth)
            {
                if (node != Root) // Don't include the root
                    moves.Add(node);
                return;
            }

            foreach (var child in node.Children)
            {
                GetMovesAtDepthRecursive(child, currentDepth + 1, targetDepth, moves);
            }
        }
    }
}
