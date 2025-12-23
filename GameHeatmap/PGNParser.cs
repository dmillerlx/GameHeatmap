using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;


namespace GameHeatmap
{
    public class PgnGame
    {
        /// <summary>
        /// Stores PGN tags (e.g. [Event "F/S Return Match"])
        /// </summary>
        public Dictionary<string, string> Tags { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// The root node of the move tree.
        /// Often a "dummy" node with MoveNumber=0 and no move.
        /// </summary>
        public MoveNode MoveTreeRoot { get; set; } = new MoveNode();

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();

            string[] events = new string[] { "Event", "White", "Black" };

            string val;
            foreach (var e in events)
            {
                Tags.TryGetValue(e, out val);
                if (sb.Length > 0) { 
                    sb.Append(" / ");
                }
                sb.Append(val);
            }
            if (sb.Length == 0)
            {
                sb.Append("Game");
            }
            return sb.ToString();

        }
    }

    public class MoveNode
    {
        public bool isWhiteTurn = false;
        public string[,] boardCache { get; set; } = null;
        public int MoveNumber { get; set; }
        public string San { get; set; } = null;
        public string SanExtra { get; set; } = null;
        public bool HasBeenWritten { get; set; } = false;  // Track if this move was already written to PGN

        /// <summary>
        /// Optional comment associated with this move
        /// (could come before or after the move).
        /// </summary>
        public string Comment { get; set; } = string.Empty;

        /// <summary>
        /// Numeric Annotation Glyphs, e.g. $1, $2, ...
        /// </summary>
        public List<string> Nags { get; set; } = new List<string>();

        /// <summary>
        /// The next moves (main line or variations).
        /// Each branch in NextMoves is a different line from this position.
        /// </summary>
        public List<MoveNode> NextMoves { get; set; } = new List<MoveNode>();

        public MoveNode Parent { get; set; } = null;

        public int twoSquareRow { get; set; } = -1;
        public int twoSquareCol { get; set;} = -1;
    }

    /// <summary>
    /// A PGN parser that can handle nested variations, curly-brace comments, and NAGs.
    /// </summary>
    public class PgnParser
    {
        // Static regex instances - compile once and reuse for all games (prevents memory leak)
        private static readonly Regex TagRegex = new Regex(@"\[(?<tagName>[A-Za-z0-9_]+)\s+""(?<tagValue>[^""]*)""\]", RegexOptions.Compiled);
        
        private static readonly Regex TokenizeRegex = new Regex(@"
            (?<lparen>\()                |   # (
            (?<rparen>\))                |   # )
            (?<comment>\{[^{}]*\})       |   # { ... } single-level
            (?<nag>\$\d+)                |   # $1, $2, ...
            (?<r>1-0|0-1|1/2-1/2)   |   # Game results
            (?<movenum>\d+\.)           |   # e.g. 7.
            (?<move>[a-zA-Z0-9=+#\-O]{1,6})  # approximate SAN token
        ", RegexOptions.Compiled | RegexOptions.IgnorePatternWhitespace);
        
        /// <summary>
        /// Parses one or more PGN games from a string and returns a list of PgnGame objects.
        /// </summary>
        public List<PgnGame> ParseGames(string pgnText)
        {
            pgnText = pgnText.Replace("\r\n", "\n");
            pgnText += "\n";

            var gameStrings = SplitIntoGames(pgnText);
            var games = new List<PgnGame>();

            foreach (var gameText in gameStrings)
            {
                var trimmed = gameText.Trim();
                if (!string.IsNullOrWhiteSpace(trimmed))
                {
                    var game = ParseSingleGame(trimmed);
                    if (game != null) games.Add(game);
                }
            }

            return games;
        }


        private enum ParseState
        {
            start = 0,
            inHeader = 1,
            afterHeader = 2,
            inGame = 3,
            afterGame = 4
        }

        /// <summary>
        /// Splits the PGN text into individual game blocks by looking for blank lines.
        /// This way, all bracket tags (e.g. [Event "..."], [Site "..."], etc.) stay in the same block.
        /// </summary>
        private List<string> SplitIntoGames(string pgnText)
        {
            // Normalize line endings
            pgnText = pgnText.Replace("\r\n", "\n");

            // Split on a blank line (i.e. one or more consecutive newlines).
            // We'll trim out the empties.
            // Note: some PGN files might separate games by exactly one blank line, others by two.
            // We'll treat any double-newline or more as a separator.
            var rawGames = pgnText
                .Split(new string[] { "\n" }, StringSplitOptions.None);

            ParseState parseState = ParseState.start;

            // Clean up each block and return
            var gameBlocks = new List<string>();
            StringBuilder sb = new StringBuilder();
            foreach (var block in rawGames)
            {
                var trimmed = block.Trim();

                if (trimmed == null)
                {
                    continue;
                }

                bool isHeader = trimmed.StartsWith("[");

                if (parseState == ParseState.start)
                {
                    if (!isHeader)
                    {
                        continue;
                    } else
                    {
                        parseState = ParseState.inHeader;
                    }
                }


                if (parseState == ParseState.inHeader)
                {
                    if (isHeader)
                    {
                        sb.AppendLine(trimmed);
                        parseState = ParseState.inHeader;
                    } else if (String.IsNullOrEmpty(trimmed))
                    {
                        parseState = ParseState.afterHeader;
                    }
                } else if (parseState == ParseState.afterHeader){
                    if (String.IsNullOrEmpty(trimmed))
                    {
                        continue;
                    }

                    parseState = ParseState.inGame;
                }

                if (parseState == ParseState.inGame)
                {
                    if (String.IsNullOrEmpty (trimmed))
                    {
                        parseState = ParseState.start;
                        gameBlocks.Add(sb.ToString());
                        sb.Clear();
                        continue;
                    }

                    sb.AppendLine(trimmed);
                }

            }

            return gameBlocks;
        }


        private PgnGame ParseSingleGame(string gameText)
        {
            var game = new PgnGame();

            // 1) Extract tags using static regex
            var matches = TagRegex.Matches(gameText);
            foreach (Match match in matches)
            {
                string tagName = match.Groups["tagName"].Value;
                string tagValue = match.Groups["tagValue"].Value;
                game.Tags[tagName] = tagValue;
            }

            // 2) Remove the tag lines, leaving just move section
            var moveSection = TagRegex.Replace(gameText, "").Trim();

            // 3) Tokenize
            var tokens = Tokenize(moveSection);

            // 4) Build the move tree
            BuildMoveTree(tokens, game.MoveTreeRoot);

            return game;
        }

        /// <summary>
        /// Tokenize the move text. 
        /// We'll capture:
        ///  - Parentheses '(' and ')'
        ///  - Curly-brace comments '{ ... }'
        ///  - Move numbers like '7.'
        ///  - SAN moves (roughly matching letters, digits, punctuation typical in moves)
        ///  - NAG tokens ($1, $2, etc.)
        ///  - Game results (1-0, 0-1, 1/2-1/2)
        /// 
        /// We do this by a big pattern that uses capturing groups.
        /// </summary>
        private List<Token> Tokenize(string moveSection)
        {
            // We'll combine them into one big pattern with named groups
            // so we know what we captured:
            string pattern = @"
            (?<lparen>\()                |   # (
            (?<rparen>\))                |   # )
            (?<comment>\{[^{}]*\})       |   # { ... } single-level
            (?<nag>\$\d+)                |   # $1, $2, ...
            (?<result>1-0|0-1|1/2-1/2)   |   # Game results
            (?<movenum>\d+\.)           |   # e.g. 7.
            (?<move>[a-zA-Z0-9=+#\-O]{1,6})  # approximate SAN token
        ";

            // Use RegexOptions.IgnorePatternWhitespace so we can comment in the pattern
            var regex = new Regex(pattern, RegexOptions.Compiled | RegexOptions.IgnorePatternWhitespace);
            var matches = regex.Matches(moveSection);

            var tokens = new List<Token>();
            foreach (Match match in matches)
            {
                if (match.Groups["lparen"].Success)
                    tokens.Add(new Token(TokenType.LParen, "("));
                else if (match.Groups["rparen"].Success)
                    tokens.Add(new Token(TokenType.RParen, ")"));
                else if (match.Groups["comment"].Success)
                    tokens.Add(new Token(TokenType.Comment, match.Value));
                else if (match.Groups["nag"].Success)
                    tokens.Add(new Token(TokenType.NAG, match.Value));
                else if (match.Groups["result"].Success)
                    tokens.Add(new Token(TokenType.Result, match.Value));
                else if (match.Groups["movenum"].Success)
                    tokens.Add(new Token(TokenType.MoveNumber, match.Value));
                else if (match.Groups["move"].Success)
                    tokens.Add(new Token(TokenType.SanMove, match.Value));
            }

            return tokens;
        }

        private void BuildMoveTree(List<Token> tokens, MoveNode root)
        {
            int index = 0;
            BuildMovesRecursive(tokens, ref index, root);
        }

        private void BuildMovesRecursive(List<Token> tokens, ref int index, MoveNode parent)
        {
            MoveNode lastNode = parent;
            int currentMoveNumber = 0;
            string pendingComment = "";  // A comment might appear before a move or after it.

            while (index < tokens.Count)
            {
                var token = tokens[index];

                switch (token.Type)
                {
                    case TokenType.Comment:
                        // This could be a comment that belongs to the previous move or
                        // to the next move if it appears before the next SAN.
                        if (string.IsNullOrEmpty(lastNode.Comment))
                        {
                            lastNode.Comment = token.Value.Trim('{', '}').Trim();
                        }
                        else
                        {
                            // If the lastNode already has a comment, we might
                            // append or store it in a separate field. For simplicity:
                            lastNode.Comment += " " + token.Value.Trim('{', '}').Trim();
                        }
                        index++;
                        break;

                    case TokenType.NAG:
                        // NAG applies to the last parsed move
                        lastNode.Nags.Add(token.Value);
                        index++;
                        break;

                    case TokenType.Result:
                        // game-ending token
                        index++;
                        return;

                    case TokenType.MoveNumber:
                        // e.g. "7." => parse out the "7"
                        currentMoveNumber = ParseMoveNumber(token.Value);
                        index++;
                        break;

                    case TokenType.SanMove:

                        if (token.Value == "+" || token.Value == "#")
                        {
                            lastNode.San += token.Value;
                            index++;
                            break;
                        }

                        // It's a regular move
                        var newNode = new MoveNode
                        {
                            MoveNumber = currentMoveNumber,
                            San = token.Value,
                        };

                        // If there's a pending comment to attach, do so
                        if (!string.IsNullOrEmpty(pendingComment))
                        {
                            newNode.Comment = pendingComment;
                            pendingComment = "";
                        }

                        lastNode.NextMoves.Add(newNode);
                        newNode.Parent = lastNode;
                        lastNode = newNode;
                        index++;
                        break;

                    case TokenType.LParen:
                        // Start of a new variation
                        // We'll recursively parse that variation into a "dummy" node
                        // then attach its children as a new line from the SAME parent.
                        index++;
                        var variationRoot = new MoveNode(); // dummy
                        BuildMovesRecursive(tokens, ref index, variationRoot);

                        // Attach all the variationRoot's NextMoves as siblings from 'parent'
                        // so they appear as a separate branch from the parent's position.
                        if (lastNode.Parent != null)
                        {
                            lastNode.Parent.NextMoves.AddRange(variationRoot.NextMoves);
                        }
                        //parent.NextMoves.AddRange(variationRoot.NextMoves);
                        break;

                    case TokenType.RParen:
                        // End of a variation
                        index++;
                        return;

                    default:
                        index++;
                        break;
                }
            }
        }

        private int ParseMoveNumber(string tokenValue)
        {
            // e.g. "13." => we strip the '.' and parse the integer
            var stripped = tokenValue.TrimEnd('.');
            if (int.TryParse(stripped, out int num))
            {
                return num;
            }
            return 0;
        }
    }

    /// <summary>
    /// Simple token type enum for our naive tokenizer.
    /// </summary>
    public enum TokenType
    {
        LParen,
        RParen,
        Comment,
        NAG,
        Result,
        MoveNumber,
        SanMove
    }

    /// <summary>
    /// Represents a token extracted by the tokenizer.
    /// </summary>
    public class Token
    {
        public TokenType Type { get; set; }
        public string Value { get; set; }

        public Token(TokenType type, string value)
        {
            Type = type;
            Value = value;
        }
    }
}
