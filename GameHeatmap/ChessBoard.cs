using System;
using System.Collections.Generic;

namespace GameHeatmap
{
    public class ChessBoard
    {
        bool writeDebug = false;

        public void debugWriteLine(String s)
        {
            if (writeDebug)
            {
                System.Diagnostics.Debug.WriteLine(s);
            }
        }
        public void debugWrite(String s)
        {
            if (writeDebug)
            {
                System.Diagnostics.Debug.Write(s);
            }
        }

        public void debugWrite(int i)
        {
            if (writeDebug)
            {
                System.Diagnostics.Debug.Write(i);
            }
        }

        public string get(int row, int col)
        {
            if (row < 0 || col < 0)
            {
                return null;
            }
            if (row > 7 || col > 7)
            {
                return null;
            }
            return board[row, col];
        }
        private string[,] board = new string[8, 8];
        public bool isWhiteTurn;

        private string fen = null;

        public ChessBoard(string fen)
        {
            LoadFEN(fen);
            this.fen = fen;
        }

        public void reloadFen()
        {
            LoadFEN(fen);
        }

        private void LoadFEN(string fen)
        {
            board = new string[8, 8];
            string[] parts = fen.Split(' ');
            string boardState = parts[0];
            isWhiteTurn = parts[1] == "w";

            string[] rows = boardState.Split('/');
            for (int r = 0; r < 8; r++)
            {
                int c = 0;
                foreach (char ch in rows[r])
                {
                    if (char.IsDigit(ch))
                    {
                        c += (int)char.GetNumericValue(ch);
                    }
                    else
                    {
                        board[r, c] = ch.ToString();
                        c++;
                    }
                }
            }
        }

        public enum SpecialMoves
        {
            none = -1,
            oo = 0,
            ooo = 1,
            enPassant = 2,
        }

        public (string[,], string[,], bool, int, int) ApplyMove(string move, MoveNode node)
        {
            string[,] boardStateBefore = CopyBoard(board);

            // Parse the move
            (int startRow, int startCol, int endRow, int endCol, bool takes, string piece, string promo, SpecialMoves specialMove) = ParseMove(move, isWhiteTurn, node);

            if (specialMove != SpecialMoves.none)
            {
                if (isWhiteTurn && specialMove == SpecialMoves.oo)
                {
                    board[7, 4] = null; //remove king
                    board[7, 7] = null; //remove rook
                    board[7, 6] = "K";
                    board[7, 5] = "R";
                }
                if (isWhiteTurn && specialMove == SpecialMoves.ooo)
                {
                    board[7, 4] = null; //remove king
                    board[7, 0] = null; //remove rook
                    board[7, 2] = "K";
                    board[7, 3] = "R";
                }
                //black
                if (!isWhiteTurn && specialMove == SpecialMoves.oo)
                {
                    board[0, 4] = null; //remove king
                    board[0, 7] = null; //remove rook
                    board[0, 6] = "k";
                    board[0, 5] = "r";
                }
                if (!isWhiteTurn && specialMove == SpecialMoves.ooo)
                {
                    board[0, 4] = null; //remove king
                    board[0, 0] = null; //remove rook
                    board[0, 2] = "k";
                    board[0, 3] = "r";
                }

                if (specialMove == SpecialMoves.enPassant)
                {
                    // Move piece
                    board[endRow, endCol] = board[startRow, startCol];
                    board[startRow, startCol] = null;

                    //capture en passant piece
                    if (node != null && node.Parent != null && node.Parent.twoSquareCol >= 0 && node.Parent.twoSquareRow >= 0)
                    {
                        board[node.Parent.twoSquareRow, node.Parent.twoSquareCol] = null;
                    }
                }


            }
            else
            {
                // Validate move legality (basic validation for now)
                ValidateMove(startRow, startCol, endRow, endCol);

                // Move piece
                board[endRow, endCol] = board[startRow, startCol];
                board[startRow, startCol] = null;
                if (!String.IsNullOrEmpty(promo))
                {
                    board[endRow, endCol] = promo;
                }
            }

            int twoSquareRow = -1;// endRow;
            int twoSquareCol = -1;// endCol;
            if (piece == "P" || piece == "p")
            {
                if (startRow - endRow > 1 || endRow - startRow > 1)
                {
                    twoSquareCol = endCol;
                    twoSquareRow = endRow;
                }
            }

            // Switch turn
            isWhiteTurn = !isWhiteTurn;
            string[,] boardStateAfter = CopyBoard(board);

            PrintBoard();

            return (boardStateBefore, boardStateAfter, !isWhiteTurn, twoSquareRow, twoSquareCol);  //use !isWhiteTurn since it was already switched and we are returning the cache

        }

        public void SetState(string[,] boardState, bool isWhiteTurn)
        {
            for (int i = 0; i < 8; i++)
            {
                for (int j = 0; j < 8; j++)
                {
                    board[i, j] = boardState[i, j];
                }
            }

            this.isWhiteTurn = isWhiteTurn;
        }

        public string[,] GetBoard()
        {
            return CopyBoard(board);
        }

        private string[,] CopyBoard(string [,] board)
        {
            string[,] copy = new string[8, 8];
            for (int i = 0; i < 8; i++)
            {
                for (int j = 0; j < 8; j++)
                {
                    copy[i, j] = board[i, j];
                }
            }
            return copy;
        }

        
        public enum Direction
        {
            none=-1,
            up = 0,
            down = 1,
            left = 2,
            right = 3,
            upperLeft = 4,
            upperRight = 5,
            lowerLeft = 6,
            lowerRight = 7,
            upTwice = 8,
            downTwice = 9,
            knight1 = 10,
            knight2 = 11,
            knight3 = 12,
            knight4 = 13,
            knight5 = 14,
            knight6 = 15,
            knight7 = 16,
            knight8 = 17,
        }


        public enum IsValidPieceMoveResponse
        {
            valid = 0,
            invalid = 1,
            enPassant = 2,
        }

        private IsValidPieceMoveResponse IsValidPieceMove(int originRow, int originCol, int startRow, int startCol, bool takes, int endRow, int endCol, string piece, MoveNode node, Direction direction=Direction.none, bool recursive = false)
        {
            debugWriteLine("Test: row: " + startRow + " col: " + startCol + " endRow: " + endRow + " endCol: " + endCol);
            if (startRow < 0 || startCol < 0 || startRow > 7 || startCol > 7)
            {
                return IsValidPieceMoveResponse.invalid;
            }
            if (startRow == endRow && startCol == endCol)
            {
                string endPiece = get(endRow, endCol);
                if (takes && !String.IsNullOrEmpty(endPiece))
                {
                    bool isPieceWhite = Char.IsUpper(piece[0]);
                    bool isEndPieceWhite = Char.IsUpper(endPiece[0]);
                    if (isPieceWhite != isEndPieceWhite)
                    {
                        debugWriteLine("Found Good!");
                        return IsValidPieceMoveResponse.valid;
                    } else
                    {
                        debugWriteLine("Found Own Piece - fail");
                        return IsValidPieceMoveResponse.invalid;
                    }
                } else if (takes && String.IsNullOrEmpty(endPiece))
                {
                    //en passant
                    if (piece == "p" || piece == "P")
                    {
                        if (node != null && node.Parent != null)
                        {
                            if (direction == Direction.upperLeft || direction == Direction.lowerLeft)
                            {
                                if (originRow == node.Parent.twoSquareRow && originCol - 1 == node.Parent.twoSquareCol)
                                {
                                    debugWriteLine("En Passant!");
                                    return IsValidPieceMoveResponse.enPassant;
                                }
                            } else if (direction == Direction.upperRight || direction == Direction.lowerRight)
                            {
                                if (originRow == node.Parent.twoSquareRow && originCol + 1 == node.Parent.twoSquareCol)
                                {
                                    debugWriteLine("En Passant!");
                                    return IsValidPieceMoveResponse.enPassant;
                                }
                            }
                        }
                    }
                    debugWriteLine("Found location but is a take move and and location is empty - not good!");
                    return IsValidPieceMoveResponse.invalid;
                } else 
                {
                    debugWriteLine("Found end row/col and location is empty - this is good");
                    return IsValidPieceMoveResponse.valid;
                }
            }

            if (startRow != originRow || startCol != originCol)
            {
                string currentPiece = get(startRow, startCol);
                if (!String.IsNullOrEmpty(currentPiece))
                {
                    debugWriteLine("Found a piece blocking source to target - fail");
                    return IsValidPieceMoveResponse.invalid;
                }
            }

            if (direction == Direction.none)
            {
                List<Direction> directionList = new List<Direction>();
                switch (piece)
                {
                    case "P": directionList.Add(Direction.up);

                        if (get(startRow - 1, startCol + 1) != null)
                        {
                            directionList.Add(Direction.upperRight);
                        }

                        if (get(startRow - 1, startCol - 1) != null)
                        {
                            directionList.Add(Direction.upperLeft);
                        }

                        if (startRow == 6)
                        {
                            if (String.IsNullOrEmpty(get(startRow-1, startCol))){
                                //Can only jump 2 squares if nothing blocking it
                                directionList.Add(Direction.upTwice);
                            }
                        }

                        //en passant
                        if (startRow == 3)
                        {
                            if (node != null && node.Parent != null)
                            {
                                if (node.Parent.twoSquareRow == startRow)
                                {
                                    if (node.Parent.twoSquareCol == startCol - 1)
                                    {
                                        directionList.Add(Direction.upperLeft);
                                    } else if (node.Parent.twoSquareCol == startCol +1)
                                    {
                                        directionList.Add(Direction.upperRight);
                                    }
                                }
                            }
                        }
                            
                        break;
                    case "p": directionList.Add(Direction.down);

                        if (get(startRow + 1, startCol + 1) != null)
                        {
                            directionList.Add(Direction.lowerRight);
                        }

                        if (get(startRow + 1, startCol - 1) != null)
                        {
                            directionList.Add(Direction.lowerLeft);
                        }

                        if (startRow == 1)
                        {
                            if (String.IsNullOrEmpty(get(startRow + 1, startCol)))
                            {
                                //Can only jump 2 squares if nothing blocking it
                                directionList.Add(Direction.downTwice);
                            }
                        }


                        //en passant
                        if (startRow == 4)
                        {
                            if (node != null && node.Parent != null)
                            {
                                if (node.Parent.twoSquareRow == startRow)
                                {
                                    if (node.Parent.twoSquareCol == startCol - 1)
                                    {
                                        directionList.Add(Direction.lowerLeft);
                                    }
                                    else if (node.Parent.twoSquareCol == startCol + 1)
                                    {
                                        directionList.Add(Direction.lowerRight);
                                    }
                                }
                            }
                        }

                        break;
                    case "B":
                    case "b":
                        directionList.Add(Direction.upperRight);
                        directionList.Add(Direction.upperLeft);
                        directionList.Add(Direction.lowerLeft);
                        directionList.Add(Direction.lowerRight);
                        recursive = true;
                        break;
                    case "Q":
                    case "q":
                        directionList.Add(Direction.upperRight);
                        directionList.Add(Direction.upperLeft);
                        directionList.Add(Direction.lowerLeft);
                        directionList.Add(Direction.lowerRight);
                        directionList.Add(Direction.up);
                        directionList.Add(Direction.down);
                        directionList.Add(Direction.left);
                        directionList.Add(Direction.right);
                        recursive = true;
                        break;
                    case "K":
                    case "k":
                        directionList.Add(Direction.upperRight);
                        directionList.Add(Direction.upperLeft);
                        directionList.Add(Direction.lowerLeft);
                        directionList.Add(Direction.lowerRight);
                        directionList.Add(Direction.up);
                        directionList.Add(Direction.down);
                        directionList.Add(Direction.left);
                        directionList.Add(Direction.right);
                        recursive = false;
                        break;
                    case "R":
                    case "r":
                        directionList.Add(Direction.up);
                        directionList.Add(Direction.down);
                        directionList.Add(Direction.left);
                        directionList.Add(Direction.right);
                        recursive=true;
                        break;
                    case "N":
                    case "n":
                        directionList.Add(Direction.knight1);
                        directionList.Add(Direction.knight2);
                        directionList.Add(Direction.knight3);
                        directionList.Add(Direction.knight4);
                        directionList.Add(Direction.knight5);
                        directionList.Add(Direction.knight6);
                        directionList.Add(Direction.knight7);
                        directionList.Add(Direction.knight8);
                        recursive = false;
                        break;
                }


                foreach (Direction d in directionList)
                {
                    IsValidPieceMoveResponse result = testDirection(originRow, originCol, startRow, startCol, takes, endRow, endCol, piece, node, d, recursive);

                    if (result != IsValidPieceMoveResponse.invalid)
                    {
                        return result;
                    }
                }
            }
            else if (recursive)
            {
                return testDirection(originRow, originCol, startRow, startCol, takes, endRow, endCol, piece, node, direction, recursive);
              
            }

            // Add logic for different piece movements
            return IsValidPieceMoveResponse.invalid;
        }

        private IsValidPieceMoveResponse testDirection(int originRow, int originCol, int startRow, int startCol, bool takes, int endRow, int endCol, string piece, MoveNode node, Direction direction = Direction.none, bool recursive = false)
        {
            switch (direction)
            {
                case Direction.upperLeft: return IsValidPieceMove(originRow, originCol, startRow - 1, startCol - 1, takes, endRow, endCol, piece, node, Direction.upperLeft, recursive);
                case Direction.upperRight: return IsValidPieceMove(originRow, originCol, startRow - 1, startCol + 1, takes, endRow, endCol, piece, node, Direction.upperRight, recursive);
                case Direction.lowerLeft: return IsValidPieceMove(originRow, originCol, startRow + 1, startCol - 1, takes, endRow, endCol, piece, node, Direction.lowerLeft, recursive);
                case Direction.lowerRight: return IsValidPieceMove(originRow, originCol, startRow + 1, startCol + 1, takes, endRow, endCol, piece, node, Direction.lowerRight, recursive);
                case Direction.up: return IsValidPieceMove(originRow, originCol, startRow - 1, startCol, takes, endRow, endCol, piece, node, Direction.up, recursive);
                case Direction.down: return IsValidPieceMove(originRow, originCol, startRow + 1, startCol, takes, endRow, endCol, piece, node, Direction.down, recursive);
                case Direction.left: return IsValidPieceMove(originRow, originCol, startRow, startCol - 1, takes, endRow, endCol, piece, node, Direction.left, recursive);
                case Direction.right: return IsValidPieceMove(originRow, originCol, startRow, startCol + 1, takes, endRow, endCol, piece, node, Direction.right, recursive);
                case Direction.upTwice: return IsValidPieceMove(originRow, originCol, startRow - 2, startCol, takes, endRow, endCol, piece, node, Direction.upTwice, recursive);
                case Direction.downTwice: return IsValidPieceMove(originRow, originCol, startRow + 2, startCol, takes, endRow, endCol, piece, node, Direction.downTwice, recursive);
                case Direction.knight1: return IsValidPieceMove(originRow, originCol, startRow - 2, startCol - 1, takes, endRow, endCol, piece, node, Direction.knight1, recursive);
                case Direction.knight2: return IsValidPieceMove(originRow, originCol, startRow - 2, startCol + 1, takes, endRow, endCol, piece, node, Direction.knight2, recursive);
                case Direction.knight3: return IsValidPieceMove(originRow, originCol, startRow - 1, startCol + 2, takes, endRow, endCol, piece, node, Direction.knight3, recursive);
                case Direction.knight4: return IsValidPieceMove(originRow, originCol, startRow + 1, startCol + 2, takes, endRow, endCol, piece, node, Direction.knight4, recursive);
                case Direction.knight5: return IsValidPieceMove(originRow, originCol, startRow + 2, startCol + 1, takes, endRow, endCol, piece, node, Direction.knight5, recursive);
                case Direction.knight6: return IsValidPieceMove(originRow, originCol, startRow + 2, startCol - 1, takes, endRow, endCol, piece, node, Direction.knight6, recursive);
                case Direction.knight7: return IsValidPieceMove(originRow, originCol, startRow - 1, startCol - 2, takes, endRow, endCol, piece, node, Direction.knight7, recursive);
                case Direction.knight8: return IsValidPieceMove(originRow, originCol, startRow + 1, startCol - 2, takes, endRow, endCol, piece, node, Direction.knight8, recursive);
            }
            return IsValidPieceMoveResponse.invalid;
        }

        private void ValidateMove(int startRow, int startCol, int endRow, int endCol)
        {
            if (board[startRow, startCol] == null)
                throw new InvalidOperationException("No piece at the starting position.");
            if (board[endRow, endCol] != null &&
                char.IsUpper(board[startRow, startCol][0]) == char.IsUpper(board[endRow, endCol][0]))
                throw new InvalidOperationException("Cannot capture own piece.");
        }

        public void PrintBoard()
        {
            debugWriteLine("");
            debugWriteLine("Move Turn: " + (isWhiteTurn ? "white" : "black"));
            debugWriteLine("  a b c d e f g h");
            for (int r = 0; r < 8; r++)
            {
                debugWrite(8-r);
                debugWrite(" ");
                for (int c = 0; c < 8; c++)
                {
                    debugWrite(board[r, c] ?? ".");
                    debugWrite(" ");
                }
                debugWriteLine("");
            }
        }


        public (int, int, int, int, bool, string, string, SpecialMoves) ParseMove(string move, bool isWhite, MoveNode node)
        {
            debugWriteLine("");
            debugWriteLine("");
            debugWriteLine(move + " - " + (isWhite?"(white)":"(black)"));
            PrintBoard();

            // Strip check (+) and checkmate (#) symbols
            move = move.TrimEnd('+', '#');

            SpecialMoves specialMove = SpecialMoves.none;
            if (move == "O-O")
            {
                specialMove = SpecialMoves.oo;
            } else if (move == "O-O-O")
            {
                specialMove = SpecialMoves.ooo;
            }

            if (specialMove != SpecialMoves.none)
            {
                return (0, 0, 0, 0, false, "", "", specialMove);
            }

            String promo = "";
            if (move.Contains("="))
            {
                promo = move.Substring(move.IndexOf("=")+1);
                move = move.Substring(0, move.IndexOf("="));
                promo = isWhite? promo.ToUpper() : promo.ToLower();
            }


            if (move.Length < 2)
            {
                throw new ArgumentException("invalid move: " + move);
            }
            string piece = null;
            bool takes = false;
            int? disambiguationFile = null;
            int? disambiguationRank = null;

            int targetCol = move[move.Length - 2] - 'a';
            int targetRow = 8 - (move[move.Length - 1] - '0');

            // Extract piece type and disambiguation
            int currentIndex = 0;
            if (move.Length == 2)
            {
                piece = "P";
            }else if (char.IsUpper(move[0]))
            {
                piece = move[currentIndex].ToString();
                currentIndex++;
            } else
            {
                piece = "P";
            }

            while (currentIndex < move.Length - 2)
            {
                char ch = move[currentIndex];
                if (ch != 'x')
                {
                    if (char.IsLetter(ch))
                        disambiguationFile = ch - 'a';
                    else if (char.IsDigit(ch))
                        disambiguationRank = 8 - (ch - '0');
                }
                currentIndex++;
            }

            if (move.Contains("x"))
            {
                takes = true;
            }

            piece = isWhite ? piece.ToUpper() : piece.ToLower();
            // Find all valid pieces of the specified type
            List<(int, int)> candidates = new List<(int, int)>();
            bool isEnPassant = false;
            for (int r = 0; r < 8; r++)
            {
                for (int c = 0; c < 8; c++)
                {
                    if (board[r, c] != null && board[r, c].Equals(piece))//, StringComparison.OrdinalIgnoreCase))
                    {
                        
                        debugWriteLine("IsValid piece: " + piece + " start row: " + r + " col: " + c);
                        IsValidPieceMoveResponse result = IsValidPieceMove(r, c, r, c, takes, targetRow, targetCol, piece, node);
                        if (result != IsValidPieceMoveResponse.invalid)
                        {
                            candidates.Add((r, c));
                        }
                        if (result == IsValidPieceMoveResponse.enPassant)
                        {
                            isEnPassant = true;
                        }
                    }
                }
            }

            // Apply disambiguation
            if (disambiguationFile.HasValue)
                candidates.RemoveAll(c => c.Item2 != disambiguationFile.Value);
            if (disambiguationRank.HasValue)
                candidates.RemoveAll(c => c.Item1 != disambiguationRank.Value);

            if (candidates.Count != 1)
                throw new InvalidOperationException("Ambiguous move or invalid move.");

            var (startRow, startCol) = candidates[0];
            if (isEnPassant)
            {
                specialMove = SpecialMoves.enPassant;
            }
            return (startRow, startCol, targetRow, targetCol, takes, piece, promo, specialMove);
        }

    }
}
