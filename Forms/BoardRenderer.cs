using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using ChessTutor.Models;

namespace ChessTutor.Forms
{
    /// <summary>
    /// Спільна логіка малювання шахової дошки та фігур.
    /// Винесено окремо щоб MainForm, EditorForm і ReplayForm не дублювали код.
    /// </summary>
    public static class BoardRenderer
    {
        public const int DefaultCellSize = 64;

        public static readonly Color ColorLight = Color.FromArgb(240, 217, 181);
        public static readonly Color ColorDark  = Color.FromArgb(181, 136, 99);
        public static readonly Color ColorBorder = Color.FromArgb(80, 80, 80);

        // Заповнені Unicode-символи — для всіх фігур, кольори через заливку
        private static readonly Dictionary<PieceType, string> PieceSymbol
            = new Dictionary<PieceType, string>
        {
            { PieceType.King,   "♚" },
            { PieceType.Queen,  "♛" },
            { PieceType.Rook,   "♜" },
            { PieceType.Bishop, "♝" },
            { PieceType.Knight, "♞" },
            { PieceType.Pawn,   "♟" },
        };

        /// <summary>
        /// Малює дошку у виділеній області.
        /// </summary>
        public static void Draw(Graphics g, Board board, Rectangle area, bool flipped, int cellSize)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;

            using (Brush bLight = new SolidBrush(ColorLight))
            using (Brush bDark  = new SolidBrush(ColorDark))
            using (Pen border   = new Pen(ColorBorder, 2f))
            {
                for (int row = 0; row < 8; row++)
                {
                    for (int col = 0; col < 8; col++)
                    {
                        int displayRow = flipped ? 7 - row : row;
                        int displayCol = flipped ? 7 - col : col;

                        int px = area.X + col * cellSize;
                        int py = area.Y + (7 - row) * cellSize;
                        var rect = new Rectangle(px, py, cellSize, cellSize);

                        bool isLight = (displayRow + displayCol) % 2 == 0;
                        g.FillRectangle(isLight ? bLight : bDark, rect);

                        Piece piece = board.GetPiece(new Position(displayRow, displayCol));
                        if (piece != null)
                            DrawPiece(g, piece, px, py, cellSize);
                    }
                }
                g.DrawRectangle(border, area.X, area.Y, cellSize * 8, cellSize * 8);
            }
        }

        /// <summary>Малює окрему фігуру з заливкою + контрастним обведенням.</summary>
        public static void DrawPiece(Graphics g, Piece piece, int px, int py, int cellSize)
        {
            string symbol = PieceSymbol[piece.Type];
            Color fill = piece.Color == PieceColor.White
                ? Color.FromArgb(248, 248, 248)
                : Color.FromArgb(25, 25, 25);
            Color outline = piece.Color == PieceColor.White
                ? Color.FromArgb(20, 20, 20)
                : Color.FromArgb(245, 245, 245);

            float emSize = cellSize * 0.62f;
            using (var fam = new FontFamily("Segoe UI Symbol"))
            using (var path = new GraphicsPath())
            using (var sf = new StringFormat(StringFormat.GenericTypographic))
            {
                sf.Alignment = StringAlignment.Center;
                sf.LineAlignment = StringAlignment.Center;
                var rect = new RectangleF(px, py, cellSize, cellSize);
                path.AddString(symbol, fam, (int)FontStyle.Regular, emSize, rect, sf);

                using (var pen = new Pen(outline, 3.0f) { LineJoin = LineJoin.Round })
                    g.DrawPath(pen, path);
                using (var brush = new SolidBrush(fill))
                    g.FillPath(brush, path);
            }
        }

        /// <summary>Конвертує піксельні координати в позицію на дошці (з урахуванням перевернутості).</summary>
        public static Position? PixelToPosition(int px, int py, Rectangle area, bool flipped, int cellSize)
        {
            int col = (px - area.X) / cellSize;
            int row = (py - area.Y) / cellSize;
            if (col < 0 || col >= 8 || row < 0 || row >= 8) return null;
            int boardRow = flipped ? row : 7 - row;
            int boardCol = flipped ? 7 - col : col;
            return new Position(boardRow, boardCol);
        }
    }

    /// <summary>Панель з подвійним буферуванням — без блимання при перерисовці.</summary>
    public sealed class DoubleBufferedPanelShared : System.Windows.Forms.Panel
    {
        public DoubleBufferedPanelShared()
        {
            SetStyle(System.Windows.Forms.ControlStyles.AllPaintingInWmPaint
                   | System.Windows.Forms.ControlStyles.OptimizedDoubleBuffer
                   | System.Windows.Forms.ControlStyles.UserPaint
                   | System.Windows.Forms.ControlStyles.ResizeRedraw, true);
            DoubleBuffered = true;
            UpdateStyles();
        }
    }
}
