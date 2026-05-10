using System;
using System.Drawing;
using System.Windows.Forms;
using ChessTutor.Models;
using ChessTutor.Models.Pieces;

namespace ChessTutor.Forms
{
    /// <summary>
    /// Діалог редактора позиції.
    /// Дозволяє очистити дошку, розставити фігури, видалити їх,
    /// вибрати хто ходить першим та запустити партію з цієї позиції.
    /// </summary>
    public class EditorForm : Form
    {
        // ─ Константи розмітки ────────────────────────────────────────────
        private const int CellSize = 56;
        private const int BoardSizePx = CellSize * 8;     // 448
        private const int BoardLeft = 16;
        private const int BoardTop = 50;
        private const int RightX = BoardLeft + BoardSizePx + 16;  // 480
        private const int RightWidth = 220;
        private const int PaletteHeight = 290;            // достатньо для 5 рядів × 54
        private const int FormPad = 16;

        // ─ Стан ─
        public Board EditedBoard { get; private set; } = new Board();
        public PieceColor WhoMovesFirst { get; private set; } = PieceColor.White;

        // Поточний інструмент палітри: тип фігури або null = "видалити"
        private (PieceType type, PieceColor color)? _selectedTool = (PieceType.Pawn, PieceColor.White);
        private bool _eraseTool = false;

        private DoubleBufferedPanelShared _boardPanel;
        private Panel _palettePanel;
        private Label _toolLabel;
        private RadioButton _whiteFirst;
        private RadioButton _blackFirst;

        public EditorForm(Board startFrom = null)
        {
            // Ініціалізуємо EditedBoard з переданої або стандартної позиції
            if (startFrom != null)
            {
                for (int r = 0; r < 8; r++)
                    for (int c = 0; c < 8; c++)
                        EditedBoard.SetPiece(new Position(r, c), startFrom.GetPiece(new Position(r, c)));
            }
            else
            {
                EditedBoard.SetupStandardPosition();
            }

            BuildUI();
        }

        // ─ Побудова UI ────────────────────────────────────────────────────
        private void BuildUI()
        {
            Text = "Редактор позиції";
            // Висота гарантовано вміщає дошку (низ ≈ 498) — додаємо запас
            ClientSize = new Size(RightX + RightWidth + FormPad,
                                  BoardTop + BoardSizePx + FormPad);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.FromArgb(40, 40, 40);
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 9f);

            _toolLabel = new Label
            {
                Location = new Point(BoardLeft, 14),
                Size = new Size(ClientSize.Width - BoardLeft - FormPad, 24),
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                Text = "Інструмент: білий пішак"
            };
            Controls.Add(_toolLabel);

            // ─ Дошка ─
            _boardPanel = new DoubleBufferedPanelShared
            {
                Location = new Point(BoardLeft, BoardTop),
                Size = new Size(BoardSizePx, BoardSizePx),
                BackColor = Color.FromArgb(40, 40, 40)
            };
            _boardPanel.Paint += OnBoardPaint;
            _boardPanel.MouseClick += OnBoardClick;
            Controls.Add(_boardPanel);

            // ─ Права колонка: палітра + радіо + кнопки (всі один під одним) ─
            // Палітра обмежена по висоті — не чіпає область керування під собою.
            _palettePanel = new Panel
            {
                Location = new Point(RightX, BoardTop),
                Size = new Size(RightWidth, PaletteHeight),
                BackColor = Color.FromArgb(50, 50, 50)
            };
            Controls.Add(_palettePanel);
            BuildPalette();

            // Y-координати елементів керування під палітрою
            int yWhoLabel = BoardTop + PaletteHeight + 8;       // ~348
            int yRadio    = yWhoLabel + 22;                      // ~370
            int yRow1     = yRadio + 30;                         // ~400  Очистити / Стандартна
            int yRow2     = yRow1 + 38;                          // ~438  Скасувати / Почати

            var lbl = new Label
            {
                Location = new Point(RightX, yWhoLabel),
                Size = new Size(RightWidth, 20),
                Text = "Хто ходить першим:",
                Font = new Font("Segoe UI", 9f, FontStyle.Bold)
            };
            Controls.Add(lbl);

            _whiteFirst = new RadioButton
            {
                Location = new Point(RightX, yRadio),
                Size = new Size(105, 22),
                Text = "Білі",
                Checked = true,
                ForeColor = Color.White
            };
            _blackFirst = new RadioButton
            {
                Location = new Point(RightX + 110, yRadio),
                Size = new Size(105, 22),
                Text = "Чорні",
                ForeColor = Color.White
            };
            Controls.Add(_whiteFirst);
            Controls.Add(_blackFirst);

            var btnClear = new Button
            {
                Location = new Point(RightX, yRow1),
                Size = new Size(105, 32),
                Text = "Очистити",
                BackColor = Color.FromArgb(75, 75, 75),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnClear.Click += (s, e) => { EditedBoard.Clear(); _boardPanel.Invalidate(); };
            Controls.Add(btnClear);

            var btnStandard = new Button
            {
                Location = new Point(RightX + 110, yRow1),
                Size = new Size(105, 32),
                Text = "Стандартна",
                BackColor = Color.FromArgb(75, 75, 75),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnStandard.Click += (s, e) => { EditedBoard.SetupStandardPosition(); _boardPanel.Invalidate(); };
            Controls.Add(btnStandard);

            var btnCancel = new Button
            {
                Location = new Point(RightX, yRow2),
                Size = new Size(105, 36),
                Text = "Скасувати",
                BackColor = Color.FromArgb(85, 60, 60),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                DialogResult = DialogResult.Cancel
            };
            Controls.Add(btnCancel);
            CancelButton = btnCancel;

            var btnStart = new Button
            {
                Location = new Point(RightX + 110, yRow2),
                Size = new Size(105, 36),
                Text = "Почати",
                BackColor = Color.FromArgb(60, 100, 60),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold)
            };
            btnStart.Click += OnStartClick;
            Controls.Add(btnStart);
            AcceptButton = btnStart;
        }

        private void BuildPalette()
        {
            // 6 фігур × 2 кольори + 1 ластик = 13 кнопок, 4 в ряд
            PieceType[] types = { PieceType.King, PieceType.Queen, PieceType.Rook,
                                   PieceType.Bishop, PieceType.Knight, PieceType.Pawn };

            int btnSize = 50, gap = 4;

            // Білі
            for (int i = 0; i < 6; i++)
            {
                int row = i / 4;
                int col = i % 4;
                AddPaletteButton(types[i], PieceColor.White,
                    new Point(8 + col * (btnSize + gap), 8 + row * (btnSize + gap)),
                    btnSize);
            }

            // Чорні
            for (int i = 0; i < 6; i++)
            {
                int row = (i / 4) + 2;
                int col = i % 4;
                AddPaletteButton(types[i], PieceColor.Black,
                    new Point(8 + col * (btnSize + gap), 8 + row * (btnSize + gap)),
                    btnSize);
            }

            // Ластик
            var erase = new Button
            {
                Location = new Point(8, 8 + 4 * (btnSize + gap)),
                Size = new Size(btnSize * 2 + gap, btnSize),
                Text = "✕  Видалити",
                BackColor = Color.FromArgb(90, 60, 60),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10f, FontStyle.Bold)
            };
            erase.Click += (s, e) => { _eraseTool = true; _selectedTool = null; UpdateToolLabel(); };
            _palettePanel.Controls.Add(erase);
        }

        private void AddPaletteButton(PieceType type, PieceColor color, Point loc, int size)
        {
            var btn = new DoubleBufferedPanelShared
            {
                Location = loc,
                Size = new Size(size, size),
                BackColor = color == PieceColor.White
                    ? Color.FromArgb(120, 120, 120)
                    : Color.FromArgb(30, 30, 30),
                Cursor = Cursors.Hand
            };
            // Малюємо фігуру
            btn.Paint += (s, e) =>
            {
                var dummyPiece = MakePiece(type, color);
                BoardRenderer.DrawPiece(e.Graphics, dummyPiece, 0, 0, size);
            };
            btn.MouseClick += (s, e) =>
            {
                _eraseTool = false;
                _selectedTool = (type, color);
                UpdateToolLabel();
            };
            _palettePanel.Controls.Add(btn);
        }

        private Piece MakePiece(PieceType type, PieceColor color)
        {
            switch (type)
            {
                case PieceType.King:   return new King(color);
                case PieceType.Queen:  return new Queen(color);
                case PieceType.Rook:   return new Rook(color);
                case PieceType.Bishop: return new Bishop(color);
                case PieceType.Knight: return new Knight(color);
                case PieceType.Pawn:   return new Pawn(color);
            }
            return null;
        }

        private void UpdateToolLabel()
        {
            if (_eraseTool) { _toolLabel.Text = "Інструмент: видалити фігуру (правий клік на дошці теж стирає)"; return; }
            if (_selectedTool.HasValue)
            {
                var (t, c) = _selectedTool.Value;
                string colorName = c == PieceColor.White ? "білий" : "чорна";
                string typeName = TypeName(t);
                _toolLabel.Text = $"Інструмент: {colorName} {typeName}";
            }
        }

        private static string TypeName(PieceType t)
        {
            switch (t)
            {
                case PieceType.King:   return "король";
                case PieceType.Queen:  return "ферзь";
                case PieceType.Rook:   return "тура";
                case PieceType.Bishop: return "слон";
                case PieceType.Knight: return "кінь";
                case PieceType.Pawn:   return "пішак";
            }
            return "?";
        }

        // ─ Малювання дошки ─
        private void OnBoardPaint(object sender, PaintEventArgs e)
        {
            BoardRenderer.Draw(e.Graphics, EditedBoard,
                new Rectangle(0, 0, BoardSizePx, BoardSizePx),
                false, CellSize);
        }

        private void OnBoardClick(object sender, MouseEventArgs e)
        {
            var pos = BoardRenderer.PixelToPosition(e.X, e.Y,
                new Rectangle(0, 0, BoardSizePx, BoardSizePx), false, CellSize);
            if (!pos.HasValue) return;

            // Правий клік — завжди стирає
            if (e.Button == MouseButtons.Right)
            {
                EditedBoard.SetPiece(pos.Value, null);
                _boardPanel.Invalidate();
                return;
            }

            if (_eraseTool)
            {
                EditedBoard.SetPiece(pos.Value, null);
            }
            else if (_selectedTool.HasValue)
            {
                var (t, c) = _selectedTool.Value;
                EditedBoard.SetPiece(pos.Value, MakePiece(t, c));
            }
            _boardPanel.Invalidate();
        }

        // ─ Старт партії з валідацією ─
        private void OnStartClick(object sender, EventArgs e)
        {
            string err = ValidatePosition();
            if (err != null)
            {
                MessageBox.Show(this, err, "Некоректна позиція",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            WhoMovesFirst = _whiteFirst.Checked ? PieceColor.White : PieceColor.Black;
            DialogResult = DialogResult.OK;
            Close();
        }

        /// <summary>Перевіряє правила розстановки. Повертає опис помилки або null якщо OK.</summary>
        private string ValidatePosition()
        {
            int whiteKing = 0, blackKing = 0;
            for (int r = 0; r < 8; r++)
                for (int c = 0; c < 8; c++)
                {
                    var p = EditedBoard.GetPiece(new Position(r, c));
                    if (p == null) continue;
                    if (p.Type == PieceType.King)
                    {
                        if (p.Color == PieceColor.White) whiteKing++; else blackKing++;
                    }
                    if (p.Type == PieceType.Pawn && (r == 0 || r == 7))
                        return $"Пішак не може стояти на лінії {r + 1}.";
                }
            if (whiteKing != 1) return $"На дошці має бути рівно 1 білий король (зараз {whiteKing}).";
            if (blackKing != 1) return $"На дошці має бути рівно 1 чорний король (зараз {blackKing}).";
            return null;
        }
    }
}
