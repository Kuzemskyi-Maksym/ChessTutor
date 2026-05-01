using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using ChessTutor.AI;
using ChessTutor.Logic;
using ChessTutor.Models;

namespace ChessTutor.Forms
{
    /// <summary>
    /// Головна форма програми.
    /// Містить шахову дошку (BoardPanel), панель статусу, меню та список ходів.
    /// </summary>
    public partial class MainForm : Form
    {
        // ── Константи відображення ────────────────────────────────────────────────

        private const int CellSize = 72;   // розмір клітини у пікселях
        private const int BoardOffset = 30;   // відступ для підписів A-H / 1-8
        private const int BoardSize = CellSize * 8;

        private static readonly Color ColorLight = Color.FromArgb(240, 217, 181);
        private static readonly Color ColorDark = Color.FromArgb(181, 136, 99);
        private static readonly Color ColorSelected = Color.FromArgb(180, 255, 255, 0);  // жовтий напівпрозорий
        private static readonly Color ColorLegal = Color.FromArgb(120, 0, 200, 0);  // зелений напівпрозорий
        private static readonly Color ColorCheck = Color.FromArgb(180, 220, 0, 0);  // червоний

        // ── Гра ──────────────────────────────────────────────────────────────────

        private GameController _controller;

        // ── Стан UI ──────────────────────────────────────────────────────────────

        private Position? _selectedPos = null;   // клітина, яку натиснув гравець
        private List<Move> _legalMoves = new List<Move>();
        private bool _flipped = false;  // чи перевернута дошка (чорні знизу)
        private bool _aiThinking = false;

        // ── Контроли ─────────────────────────────────────────────────────────────

        private Panel _boardPanel;
        private ListBox _moveList;
        private Label _statusLabel;
        private Label _turnLabel;
        private MenuStrip _menu;

        // ── Зображення фігур ─────────────────────────────────────────────────────

        // Словник: (PieceType, PieceColor) -> Image
        // Фігури малюються через GDI+ Unicode-символи (не потребує зовнішніх ресурсів)
        private static readonly Dictionary<(PieceType, PieceColor), string> _pieceSymbols
            = new Dictionary<(PieceType, PieceColor), string>
        {
            { (PieceType.King,   PieceColor.White), "♔" },
            { (PieceType.Queen,  PieceColor.White), "♕" },
            { (PieceType.Rook,   PieceColor.White), "♖" },
            { (PieceType.Bishop, PieceColor.White), "♗" },
            { (PieceType.Knight, PieceColor.White), "♘" },
            { (PieceType.Pawn,   PieceColor.White), "♙" },
            { (PieceType.King,   PieceColor.Black), "♚" },
            { (PieceType.Queen,  PieceColor.Black), "♛" },
            { (PieceType.Rook,   PieceColor.Black), "♜" },
            { (PieceType.Bishop, PieceColor.Black), "♝" },
            { (PieceType.Knight, PieceColor.Black), "♞" },
            { (PieceType.Pawn,   PieceColor.Black), "♟" },
        };

        // ── Конструктор ──────────────────────────────────────────────────────────

        public MainForm()
        {
            InitializeController();
            BuildUI();
            StartNewTwoPlayerGame();
        }

        // ── Ініціалізація контролера ─────────────────────────────────────────────

        private void InitializeController()
        {
            _controller = new GameController();

            // Підписуємось на події контролера
            _controller.MoveMade += OnMoveMade;
            _controller.StatusChanged += OnStatusChanged;
            _controller.PromotionRequested += OnPromotionRequested;
        }

        // ═════════════════════════════════════════════════════════════════════════
        //  ПОБУДОВА ІНТЕРФЕЙСУ
        // ═════════════════════════════════════════════════════════════════════════

        private void BuildUI()
        {
            // ── Форма ────────────────────────────────────────────────────────────
            Text = "Шаховий тренажер — КПІ ім. І. Сікорського";
            Size = new Size(BoardSize + BoardOffset * 2 + 260, BoardSize + BoardOffset * 2 + 80);
            MinimumSize = Size;
            MaximumSize = Size;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(40, 40, 40);
            Font = new Font("Segoe UI", 9f);

            // ── Меню ─────────────────────────────────────────────────────────────
            _menu = new MenuStrip { BackColor = Color.FromArgb(55, 55, 55), ForeColor = Color.White };

            var gameMenu = new ToolStripMenuItem("Гра");
            gameMenu.DropDownItems.Add("Нова гра (2 гравці)", null, (s, e) => StartNewTwoPlayerGame());
            gameMenu.DropDownItems.Add("Нова гра (vs ШІ — білі)", null, (s, e) => StartVsAI(PieceColor.White));
            gameMenu.DropDownItems.Add("Нова гра (vs ШІ — чорні)", null, (s, e) => StartVsAI(PieceColor.Black));
            gameMenu.DropDownItems.Add(new ToolStripSeparator());

            var depthMenu = new ToolStripMenuItem("Рівень ШІ");
            for (int d = 1; d <= 5; d++)
            {
                int depth = d;
                string label = d == 1 ? "1 — легкий" : d == 3 ? "3 — середній" : d == 5 ? "5 — складний" : d.ToString();
                var item = new ToolStripMenuItem(label) { Tag = depth };
                item.Click += (s, e) => {
                    _aiDepth = depth;
                    foreach (ToolStripMenuItem i in depthMenu.DropDownItems)
                        i.Checked = (int)i.Tag == depth;
                };
                if (d == 3) item.Checked = true;
                depthMenu.DropDownItems.Add(item);
            }
            gameMenu.DropDownItems.Add(depthMenu);
            gameMenu.DropDownItems.Add(new ToolStripSeparator());
            gameMenu.DropDownItems.Add("Зберегти результат...", null, OnSaveGame);
            gameMenu.DropDownItems.Add(new ToolStripSeparator());
            gameMenu.DropDownItems.Add("Вийти", null, (s, e) => Application.Exit());

            var viewMenu = new ToolStripMenuItem("Вигляд");
            viewMenu.DropDownItems.Add("Перевернути дошку", null, (s, e) => { _flipped = !_flipped; _boardPanel.Invalidate(); });

            _menu.Items.Add(gameMenu);
            _menu.Items.Add(viewMenu);
            Controls.Add(_menu);
            MainMenuStrip = _menu;

            // ── Дошка ────────────────────────────────────────────────────────────
            _boardPanel = new Panel
            {
                Location = new Point(BoardOffset, _menu.Height + BoardOffset),
                Size = new Size(BoardSize + BoardOffset, BoardSize + BoardOffset),
                BackColor = Color.Transparent
            };
            _boardPanel.Paint += OnBoardPaint;
            _boardPanel.MouseClick += OnBoardClick;
            Controls.Add(_boardPanel);

            // ── Права панель ─────────────────────────────────────────────────────
            int rightX = BoardSize + BoardOffset * 2 + 10;
            int rightY = _menu.Height + BoardOffset;

            _turnLabel = new Label
            {
                Location = new Point(rightX, rightY),
                Size = new Size(220, 30),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                Text = "Хід: Білі"
            };
            Controls.Add(_turnLabel);

            _statusLabel = new Label
            {
                Location = new Point(rightX, rightY + 35),
                Size = new Size(220, 30),
                ForeColor = Color.FromArgb(200, 200, 200),
                Font = new Font("Segoe UI", 9f),
                Text = ""
            };
            Controls.Add(_statusLabel);

            var movesTitle = new Label
            {
                Location = new Point(rightX, rightY + 75),
                Size = new Size(220, 20),
                ForeColor = Color.FromArgb(180, 180, 180),
                Text = "Список ходів:"
            };
            Controls.Add(movesTitle);

            _moveList = new ListBox
            {
                Location = new Point(rightX, rightY + 98),
                Size = new Size(220, BoardSize - 98),
                BackColor = Color.FromArgb(55, 55, 55),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.None,
                Font = new Font("Consolas", 9f)
            };
            Controls.Add(_moveList);
        }

        private int _aiDepth = 3;

        // ═════════════════════════════════════════════════════════════════════════
        //  ПОЧАТОК НОВОЇ ГРИ
        // ═════════════════════════════════════════════════════════════════════════

        private void StartNewTwoPlayerGame()
        {
            _controller.StartTwoPlayerGame();
            ResetUIState();
        }

        private void StartVsAI(PieceColor playerColor)
        {
            _controller.StartVsComputerGame(playerColor, _aiDepth);
            _flipped = (playerColor == PieceColor.Black);
            ResetUIState();

            // Якщо гравець грає чорними — ШІ ходить першим
            if (playerColor == PieceColor.Black)
                TriggerAIMove();
        }

        private void ResetUIState()
        {
            _selectedPos = null;
            _legalMoves.Clear();
            _moveList.Items.Clear();
            _aiThinking = false;
            UpdateLabels();
            _boardPanel.Invalidate();
        }

        // ═════════════════════════════════════════════════════════════════════════
        //  МАЛЮВАННЯ ДОШКИ
        // ═════════════════════════════════════════════════════════════════════════

        private void OnBoardPaint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;

            Board board = _controller.Board;
            Position? kingInCheck = null;

            // Знаходимо позицію короля під шахом для підсвічування
            if (_controller.State.Status == GameStatus.Check ||
                _controller.State.Status == GameStatus.Checkmate)
            {
                try { kingInCheck = board.FindKing(_controller.State.CurrentTurn); }
                catch { }
            }

            // ── Підписи стовпців (A-H) та рядків (1-8) ───────────────────────
            using (var labelFont = new Font("Segoe UI", 8f, FontStyle.Bold))
                for (int i = 0; i < 8; i++)
                {
                    // Файли (A-H) — внизу
                    char file = (char)('A' + (_flipped ? 7 - i : i));
                    g.DrawString(file.ToString(), labelFont, Brushes.LightGray,
                        BoardOffset + i * CellSize + CellSize / 2 - 5, BoardSize + BoardOffset + 4);

                    // Ранги (1-8) — зліва
                    int rank = _flipped ? i + 1 : 8 - i;
                    g.DrawString(rank.ToString(), labelFont, Brushes.LightGray,
                        4, BoardOffset + i * CellSize + CellSize / 2 - 8);
                }

            // ── Клітини і фігури ─────────────────────────────────────────────
            for (int row = 0; row < 8; row++)
            {
                for (int col = 0; col < 8; col++)
                {
                    int displayRow = _flipped ? 7 - row : row;
                    int displayCol = _flipped ? 7 - col : col;

                    var pos = new Position(displayRow, displayCol);
                    int px = BoardOffset + col * CellSize;
                    int py = BoardOffset + (7 - row) * CellSize;   // row 0 = знизу
                    var rect = new Rectangle(px, py, CellSize, CellSize);

                    // Колір клітини
                    bool isLight = (displayRow + displayCol) % 2 == 0;
                    Color cellColor = isLight ? ColorLight : ColorDark;

                    // Підсвічування: шах
                    if (kingInCheck.HasValue && pos == kingInCheck.Value)
                        cellColor = Color.FromArgb(220, 60, 60);
                    // Підсвічування: вибрана клітина
                    else if (_selectedPos.HasValue && pos == _selectedPos.Value)
                        cellColor = Color.FromArgb(205, 210, 56);

                    g.FillRectangle(new SolidBrush(cellColor), rect);

                    // Підсвічування: легальні ходи
                    if (IsLegalTarget(pos))
                    {
                        Piece target = board.GetPiece(pos);
                        if (target != null)
                        {
                            // Кутові маркери для взяття
                            using (var p = new Pen(ColorLegal, 4f))
                                g.DrawRectangle(p, px + 3, py + 3, CellSize - 6, CellSize - 6);
                        }
                        else
                        {
                            // Крапка для переміщення
                            int dotSize = CellSize / 4;
                            int dotX = px + (CellSize - dotSize) / 2;
                            int dotY = py + (CellSize - dotSize) / 2;
                            using (var b = new SolidBrush(ColorLegal))
                                g.FillEllipse(b, dotX, dotY, dotSize, dotSize);
                        }
                    }

                    // ── Фігура ───────────────────────────────────────────────
                    Piece piece = board.GetPiece(pos);
                    if (piece != null)
                        DrawPiece(g, piece, px, py);
                }
            }

            // ── Рамка дошки ──────────────────────────────────────────────────
            using (var borderPen = new Pen(Color.FromArgb(80, 80, 80), 2f))
                g.DrawRectangle(borderPen, BoardOffset, BoardOffset, BoardSize, BoardSize);

            // ── Індикатор «ШІ думає» ─────────────────────────────────────────
            if (_aiThinking)
            {
                string text = "ШІ думає...";
                using (var f = new Font("Segoe UI", 12f, FontStyle.Bold))
                {
                    SizeF sz = g.MeasureString(text, f);
                    float tx = BoardOffset + (BoardSize - sz.Width) / 2;
                    float ty = BoardOffset + (BoardSize - sz.Height) / 2;
                    g.FillRectangle(new SolidBrush(Color.FromArgb(160, 0, 0, 0)),
                        tx - 10, ty - 6, sz.Width + 20, sz.Height + 12);
                    g.DrawString(text, f, Brushes.White, tx, ty);
                }
            }
        }

        /// <summary>Малює фігуру у клітині через Unicode-символ.</summary>
        private void DrawPiece(Graphics g, Piece piece, int px, int py)
        {
            string symbol = _pieceSymbols[(piece.Type, piece.Color)];
            int margin = 4;
            var bgRect = new RectangleF(px + margin, py + margin, CellSize - margin * 2, CellSize - margin * 2);

            // Фон фігури — коло для розрізнення білих і чорних
            if (piece.Color == PieceColor.White)
                g.FillEllipse(new SolidBrush(Color.FromArgb(220, 255, 255, 255)), bgRect);
            else
                g.FillEllipse(new SolidBrush(Color.FromArgb(200, 30, 30, 30)), bgRect);

            using (var font = new Font("Segoe UI Symbol", CellSize * 0.60f, FontStyle.Regular, GraphicsUnit.Pixel))
            {
                SizeF size = g.MeasureString(symbol, font);
                float x = px + (CellSize - size.Width) / 2f;
                float y = py + (CellSize - size.Height) / 2f;

                // Символ чорного кольору для білих фігур, білого — для чорних
                Brush brush = piece.Color == PieceColor.White
                    ? new SolidBrush(Color.FromArgb(20, 20, 20))
                    : new SolidBrush(Color.FromArgb(240, 240, 240));

                g.DrawString(symbol, font, brush, x, y);
            }
        }

        // ═════════════════════════════════════════════════════════════════════════
        //  ОБРОБКА КЛІКІВ
        // ═════════════════════════════════════════════════════════════════════════

        private void OnBoardClick(object sender, MouseEventArgs e)
        {
            if (_aiThinking) return;
            if (_controller.State.Status == GameStatus.Checkmate ||
                _controller.State.Status == GameStatus.Stalemate ||
                _controller.State.Status == GameStatus.Draw) return;

            // Конвертуємо піксельні координати → позицію на дошці
            Position? clickedPos = PixelToPosition(e.X, e.Y);
            if (!clickedPos.HasValue) return;

            Position pos = clickedPos.Value;

            // ── Немає вибраної фігури ─────────────────────────────────────────
            if (!_selectedPos.HasValue)
            {
                Piece piece = _controller.Board.GetPiece(pos);
                if (piece != null && piece.Color == _controller.State.CurrentTurn)
                {
                    _selectedPos = pos;
                    _legalMoves = _controller.GetLegalMovesFor(pos);
                    _boardPanel.Invalidate();
                }
                return;
            }

            // ── Є вибрана фігура ──────────────────────────────────────────────

            // Клік на ту ж клітину — скасовуємо вибір
            if (pos == _selectedPos.Value)
            {
                _selectedPos = null;
                _legalMoves.Clear();
                _boardPanel.Invalidate();
                return;
            }

            // Клік на іншу власну фігуру — перевибираємо
            Piece target = _controller.Board.GetPiece(pos);
            if (target != null && target.Color == _controller.State.CurrentTurn)
            {
                _selectedPos = pos;
                _legalMoves = _controller.GetLegalMovesFor(pos);
                _boardPanel.Invalidate();
                return;
            }

            // Спроба зробити хід
            _controller.TryHumanMove(_selectedPos.Value, pos);

            _selectedPos = null;
            _legalMoves.Clear();
            _boardPanel.Invalidate();
        }

        // ═════════════════════════════════════════════════════════════════════════
        //  ПОДІЇ КОНТРОЛЕРА
        // ═════════════════════════════════════════════════════════════════════════

        private void OnMoveMade(Move move)
        {
            // Може викликатись з потоку ШІ → безпечний Invoke
            if (InvokeRequired)
            {
                Invoke(new Action<Move>(OnMoveMade), move);
                return;
            }

            AddMoveToList(move);
            UpdateLabels();
            _boardPanel.Invalidate();

            // Якщо тепер хід AI і гра ще триває — запускаємо AI
            if (!_aiThinking
                && _controller.State.Mode == GameMode.VsComputer
                && _controller.State.Status != GameStatus.Checkmate
                && _controller.State.Status != GameStatus.Stalemate
                && _controller.State.Status != GameStatus.Draw
                && _controller.IsCurrentPlayerAI())
            {
                TriggerAIMove();
            }
        }

        private void OnStatusChanged(GameStatus status)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<GameStatus>(OnStatusChanged), status);
                return;
            }

            UpdateLabels();

            switch (status)
            {
                case GameStatus.Checkmate:
                    PieceColor winner = _controller.State.CurrentTurn == PieceColor.White
                        ? PieceColor.Black : PieceColor.White;
                    MessageBox.Show(
                        $"Шахмат! {ColorName(winner)} виграли!",
                        "Кінець гри", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    break;

                case GameStatus.Stalemate:
                    MessageBox.Show("Пат — нічия!", "Кінець гри",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    break;

                case GameStatus.Draw:
                    MessageBox.Show("Нічия (правило 50 ходів).", "Кінець гри",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    break;
            }
        }

        private PieceType OnPromotionRequested()
        {
            // Якщо викликається з іншого потоку
            if (InvokeRequired)
                return (PieceType)Invoke(new Func<PieceType>(OnPromotionRequested));

            using (var dlg = new PromotionDialog(_controller.State.CurrentTurn))
            {
                dlg.ShowDialog(this);
                return dlg.SelectedPiece;
            }
        }

        // ═════════════════════════════════════════════════════════════════════════
        //  ДОПОМІЖНІ МЕТОДИ UI
        // ═════════════════════════════════════════════════════════════════════════

        /// <summary>Перетворює координати кліку на позицію дошки.</summary>
        private Position? PixelToPosition(int px, int py)
        {
            int col = (px - BoardOffset) / CellSize;
            int row = (py - BoardOffset) / CellSize;

            if (col < 0 || col >= 8 || row < 0 || row >= 8) return null;

            // row=0 у пікселях = верхній рядок, але дошка: row 7 = верх (якщо не перевернута)
            int boardRow = _flipped ? row : 7 - row;
            int boardCol = _flipped ? 7 - col : col;

            return new Position(boardRow, boardCol);
        }

        /// <summary>Чи є pos серед легальних цілей поточного вибору?</summary>
        private bool IsLegalTarget(Position pos)
        {
            foreach (var m in _legalMoves)
                if (m.To == pos) return true;
            return false;
        }

        private void UpdateLabels()
        {
            string turn = ColorName(_controller.State.CurrentTurn);
            _turnLabel.Text = $"Хід: {turn}";

            switch (_controller.State.Status)
            {
                case GameStatus.Check: _statusLabel.Text = "ШАХ!"; _statusLabel.ForeColor = Color.OrangeRed; break;
                case GameStatus.Checkmate: _statusLabel.Text = "ШАХМАТ"; _statusLabel.ForeColor = Color.Red; break;
                case GameStatus.Stalemate: _statusLabel.Text = "ПАТ"; _statusLabel.ForeColor = Color.Gold; break;
                case GameStatus.Draw: _statusLabel.Text = "НІЧИЯ"; _statusLabel.ForeColor = Color.Gold; break;
                default: _statusLabel.Text = ""; _statusLabel.ForeColor = Color.White; break;
            }

            // Показуємо "ШІ думає" у turn label
            if (_aiThinking)
            {
                _turnLabel.Text = "ШІ думає...";
                _turnLabel.ForeColor = Color.LightSkyBlue;
            }
            else
            {
                _turnLabel.ForeColor = _controller.State.CurrentTurn == PieceColor.White
                    ? Color.White : Color.LightGray;
            }
        }

        private void AddMoveToList(Move move)
        {
            int moveNum = _controller.State.MoveHistory.Count;
            if (moveNum % 2 == 1)
            {
                // Хід білих — новий рядок
                _moveList.Items.Add($"{(moveNum + 1) / 2 + 1,2}. {move}");
            }
            else
            {
                // Хід чорних — дописуємо до останнього рядка
                if (_moveList.Items.Count > 0)
                {
                    string last = _moveList.Items[_moveList.Items.Count - 1].ToString();
                    _moveList.Items[_moveList.Items.Count - 1] = $"{last}  {move}";
                }
            }
            _moveList.TopIndex = _moveList.Items.Count - 1;
        }

        private void TriggerAIMove()
        {
            _aiThinking = true;
            UpdateLabels();
            _boardPanel.Invalidate();

            ThreadPool.QueueUserWorkItem(_ =>
            {
                Move mov = null;
                try
                {
                    mov = _controller.ComputeAIMove();
                }
                catch (Exception ex)
                {
                    Invoke(new Action(() =>
                    {
                        _aiThinking = false;
                        UpdateLabels();
                        _boardPanel.Invalidate();
                        MessageBox.Show("Помилка ШІ: " + ex.Message, "ШІ",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }));
                    return;
                }

                Invoke(new Action(() =>
                {
                    _aiThinking = false;
                    if (mov == null)
                    {
                        UpdateLabels();
                        _boardPanel.Invalidate();
                        return;
                    }
                    _controller.ApplyComputedMove(mov);
                    // OnMoveMade/OnStatusChanged вже викликаються через події
                }));
            });
        }

        private void OnSaveGame(object sender, EventArgs e)
        {
            using (var dlg = new SaveFileDialog
            {
                Filter = "Текстовий файл (*.txt)|*.txt",
                FileName = $"chess_{DateTime.Now:yyyyMMdd_HHmm}.txt"
            })
            {
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    _controller.SaveGame(dlg.FileName);
                    MessageBox.Show("Результат збережено!", "Збереження",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        private static string ColorName(PieceColor c) =>
            c == PieceColor.White ? "Білі" : "Чорні";

        // ── Обов'язковий InitializeComponent (мінімальний, без Designer) ─────────
        private void InitializeComponent()
        {
            SuspendLayout();
            AutoScaleDimensions = new SizeF(6F, 13F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(900, 650);
            Name = "MainForm";
            ResumeLayout(false);
            PerformLayout();
        }
    }
}