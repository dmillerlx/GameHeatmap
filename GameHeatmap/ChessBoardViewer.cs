using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;

namespace GameHeatmap
{
    public partial class ChessBoardViewer : Form
    {
        public ChessBoardViewer()
        {
            InitializeComponent();
            hidePictureBox();
        }

        private Panel chessBoard;
        private Label[,] labels = new Label[8, 8];
        private Label StatusLabel = new Label();
        private Dictionary<string, Image> pieceImages = new Dictionary<string, Image>();
        private bool isWhiteBottom = true;
        private Label selectedLabel;
        private TransparentControl floatingPiece;
        private Point originalMousePosition;
        private const int DefaultImageSize = 40;
        private ChessBoard board;
        PictureBox topLevelPictureBox = new PictureBox();

        // Callback for when user makes a move
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Action<string> OnMoveCompleted { get; set; }

        // Constructor for Opening Builder - simple board visualization and move detection
        public ChessBoardViewer(string[,] initialBoard, bool isWhiteToMove, bool whiteOnBottom = true)
        {
            InitializeComponent();
            hidePictureBox();

            // Initialize chess board logic with standard starting FEN
            board = new ChessBoard("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1");
            board.SetState(initialBoard, isWhiteToMove);

            isWhiteBottom = whiteOnBottom;

            // Find piece images - try multiple locations
            string imagesPath = FindPieceImagesPath();
            LoadPieceImages(imagesPath);

            InitializeChessBoardControls();
            InitializeBoard();

            this.StartPosition = FormStartPosition.CenterParent;
            this.Resize += OnResize;
            this.KeyDown += Form_KeyDown;
            this.Width = 600;
            this.Height = 600;
            this.Text = isWhiteToMove ? "White to move" : "Black to move";
        }

        private string FindPieceImagesPath()
        {
            // Try to find images in various possible locations
            string[] possiblePaths = {
                "C:\\data\\chess\\apps\\ChessPuzzleSimulator\\images",
                "C:\\data\\chess\\apps\\GameHeatmap\\images",
                Path.Combine(Application.StartupPath, "images")
            };

            foreach (var path in possiblePaths)
            {
                if (Directory.Exists(path) && File.Exists(Path.Combine(path, "white_pawn.png")))
                {
                    return path;
                }
            }

            // Default to first path
            return possiblePaths[0];
        }

        // Update the board position (called when tree selection changes)
        public void UpdatePosition(string[,] boardState, bool isWhiteToMove)
        {
            board.SetState(boardState, isWhiteToMove);
            this.Text = isWhiteToMove ? "White to move" : "Black to move";
            InitializeBoard();
        }

        // Flip the board orientation
        public void FlipBoard()
        {
            isWhiteBottom = !isWhiteBottom;
            InitializeBoard();
        }


        private void LoadPieceImages(string directoryPath)
        {
            pieceImages["p"] = LoadTransparentImage(Path.Combine(directoryPath, "black_pawn.png"));
            pieceImages["n"] = LoadTransparentImage(Path.Combine(directoryPath, "black_knight.png"));
            pieceImages["b"] = LoadTransparentImage(Path.Combine(directoryPath, "black_bishop.png"));
            pieceImages["r"] = LoadTransparentImage(Path.Combine(directoryPath, "black_rook.png"));
            pieceImages["q"] = LoadTransparentImage(Path.Combine(directoryPath, "black_queen.png"));
            pieceImages["k"] = LoadTransparentImage(Path.Combine(directoryPath, "black_king.png"));
            pieceImages["P"] = LoadTransparentImage(Path.Combine(directoryPath, "white_pawn.png"));
            pieceImages["N"] = LoadTransparentImage(Path.Combine(directoryPath, "white_knight.png"));
            pieceImages["B"] = LoadTransparentImage(Path.Combine(directoryPath, "white_bishop.png"));
            pieceImages["R"] = LoadTransparentImage(Path.Combine(directoryPath, "white_rook.png"));
            pieceImages["Q"] = LoadTransparentImage(Path.Combine(directoryPath, "white_queen.png"));
            pieceImages["K"] = LoadTransparentImage(Path.Combine(directoryPath, "white_king.png"));
        }

        private Image LoadTransparentImage(string filePath)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("Image file not found: " + filePath);
            }

            Bitmap bmp = new Bitmap(filePath);
            bmp.MakeTransparent(Color.White); // Set white background as transparent

            Bitmap transparentImage = new Bitmap(bmp.Width, bmp.Height);
            using (Graphics g = Graphics.FromImage(transparentImage))
            {
                g.Clear(Color.Transparent); // Explicitly set transparent background
                g.DrawImage(bmp, 0, 0);
            }

            return transparentImage;// ResizeImage(transparentImage, new Size(DefaultImageSize, DefaultImageSize));
        }


        private void InitializeChessBoardControls()
        {
            chessBoard = new Panel
            {
                Location = new Point(10, 10),
                BackColor = Color.Transparent // Ensure the parent is transparent
            };
            this.Controls.Add(chessBoard);

            floatingPiece = new TransparentControl
            {
                BackColor = Color.Transparent,
                Visible = false,
                Parent = pictureBoxTop// chessBoard // Ensure it's part of the board to maintain transparency
            };

            StatusLabel = new Label
            {
                Visible = false,
                Parent = chessBoard
            };
            chessBoard.Controls.Add(StatusLabel);

            chessBoard.Controls.Add(floatingPiece);

            topLevelPictureBox = new PictureBox();
            topLevelPictureBox.Image = null;
            topLevelPictureBox.BackColor = Color.Transparent;
            

            ResizeChessBoard();
        }

        //Write labels around board
        private void InitializeBoardLabels()
        {
            if (isWhiteBottom)
            {
                for (int i = 0; i < 8; i++)
                {
                    var rowLabel = new Label
                    {
                        Text = isWhiteBottom ? (8 - i).ToString() : (i + 1).ToString(),
                        AutoSize = true
                    };
                    this.Controls.Add(rowLabel);

                    var colLabel = new Label
                    {
                        Text = isWhiteBottom ? ((char)('a' + i)).ToString() : ((char)('h' - i)).ToString(),
                        AutoSize = true
                    };
                    this.Controls.Add(colLabel);
                }
            } else
            {
                for (int i = 0; i < 8; i++)
                {
                    var rowLabel = new Label
                    {
                        Text = isWhiteBottom ? (8 - i).ToString() : (i + 1).ToString(),
                        AutoSize = true
                    };
                    this.Controls.Add(rowLabel);

                    var colLabel = new Label
                    {
                        Text = isWhiteBottom ? ((char)('a' + i)).ToString() : ((char)('h' - i)).ToString(),
                        AutoSize = true
                    };
                    this.Controls.Add(colLabel);
                }
            }            
        }

        private void InitializeBoard()
        {
            int squareSize = Math.Min(this.ClientSize.Width - 20, this.ClientSize.Height - 20) / 8;
            if (isWhiteBottom)
            {
                for (int row = 0; row < 8; row++)
                {
                    for (int col = 0; col < 8; col++)
                    {
                        string piece = board.get(row, col);
                        if (!String.IsNullOrEmpty(piece) && pieceImages.ContainsKey(piece))
                        {
                            labels[row, col].Image = ResizeImage(pieceImages[piece], new Size(squareSize - 10, squareSize - 10));
                            labels[row, col].Tag = piece;
                        }
                        else
                        {
                            labels[row, col].Image = null;
                            labels[row, col].Text = "";// piece;
                            labels[row, col].Font = new Font("Arial", 24, FontStyle.Bold);
                            labels[row, col].Tag = null;
                        }
                    }
                }
            }
            else
            {
                for (int row = 0; row < 8; row++)
                {
                    for (int col = 0; col < 8; col++)
                    {
                        string piece = board.get(row, col);
                        if (!String.IsNullOrEmpty(piece) && pieceImages.ContainsKey(piece))
                        {
                            labels[7-row, 7-col].Image = ResizeImage(pieceImages[piece], new Size(squareSize - 10, squareSize - 10));
                            labels[7-row, 7-col].Tag = piece;
                        }
                        else
                        {
                            labels[7-row, 7-col].Image = null;
                            labels[7-row, 7-col].Text = "";// piece;
                            labels[7-row, 7-col].Font = new Font("Arial", 24, FontStyle.Bold);
                            labels[7-row, 7-col].Tag = null;
                        }
                    }
                }
            }
        }



        private void Form_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                this.Close();
            }
            else if (e.KeyCode == Keys.F)
            {
                FlipBoard();
            }
        }


        private void ResizeChessBoard()
        {
            int squareSize = Math.Min(this.ClientSize.Width - 20, this.ClientSize.Height - 20) / 8;
            chessBoard.Size = new Size(squareSize * 8, squareSize * 8);

            for (int i = 0; i < 8; i++)
            {
                for (int j = 0; j < 8; j++)
                {
                    if (labels[i, j] == null)
                    {
                        labels[i, j] = new Label
                        {
                            BorderStyle = BorderStyle.FixedSingle,
                            BackColor = (i + j) % 2 == 0 ? Color.FromArgb(235, 236, 208) : Color.FromArgb(115, 149, 82),
                            TextAlign = ContentAlignment.MiddleCenter
                        };
                        labels[i, j].MouseDown += ChessPieceMouseDown;
                        labels[i, j].MouseMove += ChessPieceMouseMove;
                        labels[i, j].MouseUp += ChessPieceMouseUp;
                        chessBoard.Controls.Add(labels[i, j]);
                    }
                    labels[i, j].Size = new Size(squareSize, squareSize);
                    labels[i, j].Location = new Point(j * squareSize, i * squareSize);

                    // Resize and reassign the image if the piece key exists
                    if (!string.IsNullOrEmpty((string)labels[i, j].Tag) && pieceImages.ContainsKey((string)labels[i, j].Tag))
                    {
                        labels[i, j].Image = ResizeImage(pieceImages[(string)labels[i, j].Tag], new Size(squareSize - 10, squareSize - 10));
                    }
                }
            }

            floatingPiece.Size = new Size(squareSize, squareSize);
            floatingPiece.BackColor = Color.Transparent; // Reinforce transparency

            pictureBoxTop.BackColor = Color.Transparent;
            //pictureBoxTop.Image = pieceImages["VariationComplete"];


            //chessBoard.Controls.Add(topLevelPictureBox);
        }


        public void showPictureBox()
        {
            this.pictureBoxTop.BringToFront();
            this.pictureBoxTop.Show();
        }

        public void hidePictureBox()
        {
            this.pictureBoxTop.Hide();
            this.pictureBoxTop.SendToBack();
        }

        public bool isPictureBoxVisible()
        {
            return this.pictureBoxTop.Visible;
        }

        private void CaptureClientAreaIntoPictureBox()
        {
            // 1. Get the client rectangle in *screen* coordinates.
            Rectangle clientRect = this.RectangleToScreen(this.ClientRectangle);

            // 2. Create a Bitmap just the size of the client area.
            Bitmap bmp = new Bitmap(clientRect.Width, clientRect.Height);

            // 3. Copy from screen using client rectangle's position & size.
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.CopyFromScreen(clientRect.Location, new Point(0, 0), clientRect.Size);
            }

            pictureBoxTop.Image = GetPanelBitmap(chessBoard);
            pictureBoxTop.Top = chessBoard.Top;
            pictureBoxTop.Left = chessBoard.Left;
            pictureBoxTop.Width = chessBoard.Width;
            pictureBoxTop.Height = chessBoard.Height;

            // 4. Assign the bitmap to your PictureBox
            //pictureBoxTop.Image = bmp;
        }

        public static Bitmap GetPanelBitmap(Panel panel)
        {
            // Create a Bitmap that matches the panel’s client size
            Bitmap bmp = new Bitmap(panel.ClientSize.Width, panel.ClientSize.Height);

            // Draw the panel (and its child controls) onto the bitmap
            panel.DrawToBitmap(bmp, new Rectangle(Point.Empty, bmp.Size));
            //string debugPath = @"C:\data\bitmap.bmp";
            //bmp.Save(debugPath, ImageFormat.Bmp);
            return bmp;
        }




        private void OnResize(object sender, EventArgs e)
        {
            ResizeChessBoard();
        }

        private Image ResizeImage(Image image, Size size)
        {
            if (size.Width <= 0 || size.Height <= 0)
            {
                size = new Size(1, 1);
            }
            Bitmap resizedImage = new Bitmap(size.Width, size.Height);
            using (Graphics g = Graphics.FromImage(resizedImage))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.Clear(Color.Transparent);
                g.DrawImage(image, 0, 0, size.Width, size.Height);
            }
            return resizedImage;
        }



        private void ChessPieceMouseDown(object sender, MouseEventArgs e)
        {
            selectedLabel = sender as Label;
            if (selectedLabel != null && selectedLabel.Image != null)
            {
                originalMousePosition = e.Location;
                floatingPiece.PieceImage = selectedLabel.Image;
                floatingPiece.Size = selectedLabel.Size;
                floatingPiece.BringToFront();
                floatingPiece.Visible = true;
                floatingPiece.Location = chessBoard.PointToClient(Cursor.Position);
                floatingPiece.Parent = pictureBoxTop;

                CaptureClientAreaIntoPictureBox();
                showPictureBox();
                floatingPiece.BringToFront();

                floatingPiece.Invalidate(); // Force redraw
            }
        }





        private void ChessPieceMouseMove(object sender, MouseEventArgs e)
        {
            if (floatingPiece.Visible && e.Button == MouseButtons.Left)
            {
                Point mousePosition = chessBoard.PointToClient(Cursor.Position);
                floatingPiece.Location = new Point(mousePosition.X - floatingPiece.Width / 2, mousePosition.Y - floatingPiece.Height / 2);
                floatingPiece.Invalidate(); // Redraw the control
            }
        }



        private void ChessPieceMouseUp(object sender, MouseEventArgs e)
        {
            if (selectedLabel != null && floatingPiece.Visible)
            {
                hidePictureBox();
                floatingPiece.Visible = false;

                Point mouseLocation = chessBoard.PointToClient(Cursor.Position);
                Label targetLabel = GetLabelAtPosition(mouseLocation);

                if (targetLabel != null && selectedLabel != targetLabel)
                {
                    string move = GenerateMoveFromLabels(selectedLabel, targetLabel);
                    HandleMove(move);
                }

                selectedLabel = null;
            }
        }



        public static (int row, int col) TranslateToIndex(string coordinate)
        {
            if (string.IsNullOrEmpty(coordinate) || coordinate.Length != 2)
                throw new ArgumentException("Invalid coordinate format. Must be a letter (a-h) followed by a number (1-8).");

            char column = coordinate[0];
            char row = coordinate[1];

            // Validate input
            if (column < 'a' || column > 'h' || row < '1' || row > '8')
                throw new ArgumentException("Coordinate out of range. Must be between a1 and h8.");

            // Convert to indices
            int colIndex = column - 'a'; // 'a' is 0, 'h' is 7
            int rowIndex = row - '1';   // '1' is 0, '8' is 7

            return (rowIndex, colIndex);
        }

        private void HandleMove(string move)
        {
            if (String.IsNullOrEmpty(move))
            {
                return;
            }

            string moveSource = move.Substring(0, 2);
            string moveTarget = move.Substring(2);

            (int sourceRow, int sourceCol) = TranslateToIndex(moveSource);
            (int targetRow, int targetCol) = TranslateToIndex(moveTarget);

            string pieceSource = board.get(7-sourceRow, sourceCol);
            string pieceTarget = board.get(7-targetRow, targetCol);

            if (String.IsNullOrEmpty(pieceSource))
            {
                System.Diagnostics.Debug.WriteLine("Piece source does not exist");
                return;
            }

            bool takes = false;
            if (!String.IsNullOrEmpty(pieceTarget))
            {
                takes = true;
            }

            bool isPawn = false;
            if (pieceSource == "P" || pieceSource == "p")
            {
                isPawn = true;
                pieceSource = "";
            }
            else
            {
                pieceSource = pieceSource.ToUpper();
            }

            List<string> moveList = new List<string>();

            // Check for castling
            if (pieceSource == "K")
            {
                if (sourceRow == 0 && sourceCol == 4 && targetRow == 0 && targetCol == 2)
                {
                    moveList.Add("O-O-O");
                }
                else if (sourceRow == 0 && sourceCol == 4 && targetRow == 0 && targetCol == 6)
                {
                    moveList.Add("O-O");
                }
                else if (sourceRow == 7 && sourceCol == 4 && targetRow == 7 && targetCol == 6)
                {
                    moveList.Add("O-O");
                }
                else if (sourceRow == 7 && sourceCol == 4 && targetRow == 7 && targetCol == 2)
                {
                    moveList.Add("O-O-O");
                }
            }

            // Generate possible SAN notation for this move
            if (isPawn)
            {
                if (takes)
                {
                    // For pawn captures, include the source file (e.g., "axb4", "exd5")
                    moveList.Add(moveSource[0] + "x" + moveTarget);
                }
                else
                {
                    // For non-capturing pawn moves, just the target square (e.g., "e4", "d5")
                    moveList.Add(moveTarget);
                }
            }
            else
            {
                // For non-pawn moves (pieces)
                moveList.Add(pieceSource + (takes ? "x" : "") + moveTarget);
                moveList.Add(pieceSource + moveSource[0] + (takes ? "x" : "") + moveTarget);
                moveList.Add(pieceSource + moveSource[1] + (takes ? "x" : "") + moveTarget);
                moveList.Add(pieceSource + moveSource + (takes ? "x" : "") + moveTarget);
            }

            if (isPawn)
            {
                // Only add promotion if pawn is moving to the 8th or 1st rank
                bool isPromotion = (targetRow == 0 || targetRow == 7);
                if (isPromotion)
                {
                    string[] promotePieceList = { "Q", "N", "B", "R" };
                    List<string> moveListWithPromote = new List<string>();

                    foreach (var moveItem in moveList)
                    {
                        foreach (var promoPiece in promotePieceList)
                        {
                            moveListWithPromote.Add(moveItem + "=" + promoPiece);
                        }
                    }
                    moveList.AddRange(moveListWithPromote);
                }
            }

            // Try to determine the correct SAN using the chess board
            string san = null;
            foreach (var sanCandidate in moveList)
            {
                try
                {
                    // Try parsing this move - if it works, use it
                    var testNode = new MoveNode { twoSquareRow = -1, twoSquareCol = -1 };
                    var (fromRow, fromCol, toRow, toCol, _, _, _, _) = board.ParseMove(sanCandidate, board.isWhiteTurn, testNode);
                    if (fromRow == 7 - sourceRow && fromCol == sourceCol &&
                        toRow == 7 - targetRow && toCol == targetCol)
                    {
                        san = sanCandidate;
                        break;
                    }
                }
                catch
                {
                    // This SAN didn't work, try next one
                }
            }

            if (san != null)
            {
                // Call the callback with the SAN move
                OnMoveCompleted?.Invoke(san);
            }
        }





        private Label GetLabelAtPosition(Point position)
        {
            foreach (Label label in labels)
            {
                if (label.Bounds.Contains(position))
                {
                    return label;
                }
            }
            return null;
        }

        private string GenerateMoveFromLabels(Label source, Label target)
        {
            int sourceRow = -1, sourceCol = -1, targetRow = -1, targetCol = -1;

            for (int i = 0; i < 8; i++)
            {
                for (int j = 0; j < 8; j++)
                {
                    if (labels[i, j] == source)
                    {
                        sourceRow = i;
                        sourceCol = j;
                    }
                    if (labels[i, j] == target)
                    {
                        targetRow = i;
                        targetCol = j;
                    }
                }
            }

            if (sourceRow == -1 || sourceCol == -1 || targetRow == -1 || targetCol == -1)
            {
                throw new InvalidOperationException("Source or target label not found on the board.");
            }

            char sourceFile = (char)('a' + sourceCol);
            char sourceRank = (char)('8' - sourceRow);
            char targetFile = (char)('a' + targetCol);
            char targetRank = (char)('8' - targetRow);

            if (!isWhiteBottom)
            {
                sourceFile = (char)('h' - sourceCol);
                sourceRank = (char)('1' + sourceRow);
                targetFile = (char)('h' - targetCol);
                targetRank = (char)('1' + targetRow);
            }

            return $"{sourceFile}{sourceRank}{targetFile}{targetRank}";
        }

        public class TransparentControl : Control
        {
            [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
            public Image PieceImage { get; set; }

            public TransparentControl()
            {
                this.SetStyle(ControlStyles.SupportsTransparentBackColor, true);
                this.BackColor = Color.Transparent;
                this.DoubleBuffered = true;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);

                //Pen pen = new Pen(Color.Blue, 5);
                //e.Graphics.DrawLine(pen, new Point(0, 0), new Point(50, 50));


                if (PieceImage != null)
                {
                    //e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    //e.Graphics.Clear(Color.Transparent);
                    e.Graphics.DrawImage(PieceImage, 0, 0, this.Width, this.Height);
                }
            }
        }

        private void timerCheckDone_Tick(object sender, EventArgs e)
        {
            // Removed puzzle-specific logic
        }

        private void timerStatus_Tick(object sender, EventArgs e)
        {
            // Removed puzzle-specific logic
        }

        private void timerLearnMode_Tick(object sender, EventArgs e)
        {
            // Removed puzzle-specific logic
        }

        private void ChessBoardViewer_Move(object sender, EventArgs e)
        {
            // No need to save position for Opening Builder board
        }
    }
}
