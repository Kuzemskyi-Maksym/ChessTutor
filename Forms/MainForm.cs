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
        private Label _capturedWhiteLabel;   // взяті білі фігури (з'їдені чорними)
        private Label _capturedBlackLabel;   // взяті чорні фігури (з'їдені білими)
        private Label _materialLabel;        // матеріальна перевага
        private Label _hintLabel;            // повідомлення про некор. дії
        private MenuStrip _menu;
        private ToolStrip _toolbar;
        private System.Windows.Forms.Timer _hintTimer; // таймер для авто-приховування підказки

        // ── Спільні графічні ресурси (звільняються при закритті форми) ────────────
        private static readonly Brush BrushBoardLight = new SolidBrush(ColorLight);
        private static readonly Brush BrushBoardDark  = new SolidBrush(ColorDark);
        private static readonly Brush BrushSelected   = new SolidBrush(Color.FromArgb(205, 210, 56));
        private static readonly Brush BrushKingCheck  = new SolidBrush(Color.FromArgb(220, 60, 60));
        private static readonly Brush BrushLegalDot   = new SolidBrush(ColorLegal);
        private static readonly Pen   PenLegalCapture = new Pen(ColorLegal, 4f);
        private static readonly Pen   PenBoardBorder  = new Pen(Color.FromArgb(80, 80, 80), 2f);
        private static readonly Brush BrushAIBg       = new SolidBrush(Color.FromArgb(160, 0, 0, 0));

        // ── Зображення фігур ─────────────────────────────────────────────────────

        // Словник Unicode-символів. Використовуємо ЗАЛИТІ варіанти для всіх фігур,
        // а кольори (фарбу і обведення) задаємо окремо в DrawPiece — це дає
        // чіткий силует без потреби в круглому фоні.
        private static readonly Dictionary<PieceType, string> _pieceSymbol
            = new Dictionary<PieceType, string>
        {
            { PieceType.King,   "♚" },  // ♚
            { PieceType.Queen,  "♛" },  // ♛
            { PieceType.Rook,   "♜" },  // ♜
            { PieceType.Bishop, "♝" },  // ♝
            { PieceType.Knight, "♞" },  // ♞
            { PieceType.Pawn,   "♟" },  // ♟
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
            _controller.BoardChanged += OnBoardChanged;
        }

        /// <summary>Перемальовує дошку та повністю переобчислює правий бічний UI.</summary>
        private void OnBoardChanged()
        {
            if (InvokeRequired) { Invoke(new Action(OnBoardChanged)); return; }
            RebuildMoveList();
            UpdateCapturedPanel();
            UpdateLabels();
            _boardPanel.Invalidate();
        }

        // ═════════════════════════════════════════════════════════════════════════
        //  ПОБУДОВА ІНТЕРФЕЙСУ
        // ═════════════════════════════════════════════════════════════════════════

        private void BuildUI()
        {
            // ── Форма ────────────────────────────────────────────────────────────
            Text = "Шаховий тренажер — КПІ ім. І. Сікорського";
            // Збільшено праву панель щоб уміщувати захоплені фігури і матеріальну перевагу
            Size = new Size(BoardSize + BoardOffset * 2 + 290, BoardSize + BoardOffset * 2 + 130);
            MinimumSize = Size;
            MaximumSize = Size;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(40, 40, 40);
            Font = new Font("Segoe UI", 9f);
            KeyPreview = true;
            KeyDown += OnFormKeyDown;
            FormClosed += OnFormClosed;

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
            gameMenu.DropDownItems.Add("Скасувати хід (Ctrl+Z)", null, (s, e) => DoUndo());
            gameMenu.DropDownItems.Add("Редактор позиції...", null, (s, e) => OpenEditor());
            gameMenu.DropDownItems.Add(new ToolStripSeparator());
            gameMenu.DropDownItems.Add("Зберегти результат (текст)...", null, OnSaveGame);
            gameMenu.DropDownItems.Add("Зберегти партію (PGN)...", null, OnSavePgn);
            gameMenu.DropDownItems.Add("Переглянути партію (PGN)...", null, (s, e) => ReplayForm.OpenFromFile(this));
            gameMenu.DropDownItems.Add(new ToolStripSeparator());
            gameMenu.DropDownItems.Add("Вийти", null, (s, e) => Application.Exit());

            var viewMenu = new ToolStripMenuItem("Вигляд");
            viewMenu.DropDownItems.Add("Перевернути дошку", null, (s, e) => { _flipped = !_flipped; _boardPanel.Invalidate(); });

            _menu.Items.Add(gameMenu);
            _menu.Items.Add(viewMenu);
            Controls.Add(_menu);
            MainMenuStrip = _menu;

            // ── Toolbar ──────────────────────────────────────────────────────────
            _toolbar = new ToolStrip
            {
                BackColor = Color.FromArgb(50, 50, 50),
                ForeColor = Color.White,
                GripStyle = ToolStripGripStyle.Hidden,
                ImageScalingSize = new Size(18, 18),
                Padding = new Padding(4)
            };
            _toolbar.Items.Add(MakeToolButton("⟳ Нова",   "Нова гра (2 гравці)", (s, e) => StartNewTwoPlayerGame()));
            _toolbar.Items.Add(MakeToolButton("♔ vs ШІ",  "Грати білими проти ШІ", (s, e) => StartVsAI(PieceColor.White)));
            _toolbar.Items.Add(MakeToolButton("♚ vs ШІ",  "Грати чорними проти ШІ", (s, e) => StartVsAI(PieceColor.Black)));
            _toolbar.Items.Add(new ToolStripSeparator());
            _toolbar.Items.Add(MakeToolButton("↶ Назад",  "Скасувати хід (Ctrl+Z)", (s, e) => DoUndo()));
            _toolbar.Items.Add(MakeToolButton("⇅ Перевернути", "Перевернути дошку", (s, e) => { _flipped = !_flipped; _boardPanel.Invalidate(); }));
            _toolbar.Items.Add(new ToolStripSeparator());
            _toolbar.Items.Add(MakeToolButton("✎ Редактор", "Редактор позиції", (s, e) => OpenEditor()));
            _toolbar.Items.Add(MakeToolButton("💾 Текст", "Зберегти результат у текст", OnSaveGame));
            _toolbar.Items.Add(MakeToolButton("📄 PGN", "Зберегти партію у PGN-форматі", OnSavePgn));
            _toolbar.Items.Add(MakeToolButton("📖 Переглянути", "Переглянути збережену партію", (s, e) => ReplayForm.OpenFromFile(this)));
            Controls.Add(_toolbar);

            int topOffset = _menu.Height + _toolbar.Height;

            // ── Дошка ────────────────────────────────────────────────────────────
            // Використовуємо панель з подвійним буферуванням — щоб не блимало при перерисовці
            _boardPanel = new DoubleBufferedPanel
            {
                Location = new Point(BoardOffset, topOffset + BoardOffset),
                Size = new Size(BoardSize + BoardOffset, BoardSize + BoardOffset),
                BackColor = Color.FromArgb(40, 40, 40)
            };
            _boardPanel.Paint += OnBoardPaint;
            _boardPanel.MouseClick += OnBoardClick;
            Controls.Add(_boardPanel);

            // ── Права панель ─────────────────────────────────────────────────────
            int rightX = BoardSize + BoardOffset * 2 + 10;
            int rightY = topOffset + BoardOffset;
            int rightW = 250;

            _turnLabel = new Label
            {
                Location = new Point(rightX, rightY),
                Size = new Size(rightW, 28),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                Text = "Хід: Білі"
            };
            Controls.Add(_turnLabel);

            _statusLabel = new Label
            {
                Location = new Point(rightX, rightY + 30),
                Size = new Size(rightW, 22),
                ForeColor = Color.FromArgb(200, 200, 200),
                Font = new Font("Segoe UI", 9f),
                Text = ""
            };
            Controls.Add(_statusLabel);

            // Підказка / повідомлення про некор. дії — оранжева, авто-зникає
            _hintLabel = new Label
            {
                Location = new Point(rightX, rightY + 54),
                Size = new Size(rightW, 22),
                ForeColor = Color.FromArgb(255, 180, 80),
                Font = new Font("Segoe UI", 9f, FontStyle.Italic),
                Text = ""
            };
            Controls.Add(_hintLabel);

            // ── Захоплені фігури ─────────────────────────────────────────────────
            int capY = rightY + 84;
            var capTitle = new Label
            {
                Location = new Point(rightX, capY),
                Size = new Size(rightW, 18),
                ForeColor = Color.FromArgb(180, 180, 180),
                Text = "Захоплені фігури:"
            };
            Controls.Add(capTitle);

            // Ряд для з'їдених білих (на темному фоні — світлі силуети)
            _capturedWhiteLabel = new Label
            {
                Location = new Point(rightX, capY + 20),
                Size = new Size(rightW, 28),
                ForeColor = Color.FromArgb(245, 245, 245),
                BackColor = Color.FromArgb(60, 60, 60),
                Font = new Font("Segoe UI Symbol", 16f),
                Text = " "
            };
            Controls.Add(_capturedWhiteLabel);

            // Ряд для з'їдених чорних (на світлому фоні — темні силуети)
            _capturedBlackLabel = new Label
            {
                Location = new Point(rightX, capY + 50),
                Size = new Size(rightW, 28),
                ForeColor = Color.FromArgb(25, 25, 25),
                BackColor = Color.FromArgb(220, 200, 165),
                Font = new Font("Segoe UI Symbol", 16f),
                Text = " "
            };
            Controls.Add(_capturedBlackLabel);

            _materialLabel = new Label
            {
                Location = new Point(rightX, capY + 82),
                Size = new Size(rightW, 22),
                ForeColor = Color.FromArgb(220, 220, 220),
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                Text = "Матеріал: 0"
            };
            Controls.Add(_materialLabel);

            // ── Список ходів ────────────────────────────────────────────────────
            int movesY = capY + 112;
            var movesTitle = new Label
            {
                Location = new Point(rightX, movesY),
                Size = new Size(rightW, 20),
                ForeColor = Color.FromArgb(180, 180, 180),
                Text = "Список ходів:"
            };
            Controls.Add(movesTitle);

            _moveList = new ListBox
            {
                Location = new Point(rightX, movesY + 22),
                Size = new Size(rightW, BoardSize - (movesY - rightY) - 22),
                BackColor = Color.FromArgb(55, 55, 55),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.None,
                Font = new Font("Consolas", 9f)
            };
            Controls.Add(_moveList);

            // Таймер для приховування підказки (явно WinForms — щоб не конфліктувало з System.Threading.Timer)
            _hintTimer = new System.Windows.Forms.Timer { Interval = 2500 };
            _hintTimer.Tick += (s, e) => { _hintTimer.Stop(); _hintLabel.Text = ""; };
        }

        /// <summary>Утиліта для створення кнопки тулбара з назвою і tooltip.</summary>
        private ToolStripButton MakeToolButton(string text, string tooltip, EventHandler click)
        {
            var btn = new ToolStripButton(text)
            {
                DisplayStyle = ToolStripItemDisplayStyle.Text,
                ToolTipText = tooltip,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Padding = new Padding(6, 2, 6, 2)
            };
            btn.Click += click;
            return btn;
        }

        /// <summary>Показує користувачу повідомлення (про некор. дію) на 2.5с.</summary>
        private void ShowHint(string text)
        {
            if (_hintLabel == null) return;
            _hintLabel.Text = text;
            if (_hintTimer != null)
            {
                _hintTimer.Stop();
                _hintTimer.Start();
            }
        }

        /// <summary>Викликає Undo з урахуванням режиму гри.</summary>
        private void DoUndo()
        {
            if (_aiThinking) { ShowHint("ШІ зараз думає, зачекайте"); return; }
            if (!_controller.State.CanUndo) { ShowHint("Немає ходів для скасування"); return; }
            // У режимі vs AI повертаємо одразу 2 півходи (хід AI + свій),
            // щоб після Undo знов хід був за людиною
            int count = _controller.State.Mode == GameMode.VsComputer ? 2 : 1;
            _controller.UndoMoves(count);
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
            UpdateCapturedPanel();
            if (_hintLabel != null) _hintLabel.Text = "";
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
                    Brush cellBrush;
                    if (kingInCheck.HasValue && pos == kingInCheck.Value)
                        cellBrush = BrushKingCheck;
                    else if (_selectedPos.HasValue && pos == _selectedPos.Value)
                        cellBrush = BrushSelected;
                    else
                        cellBrush = isLight ? BrushBoardLight : BrushBoardDark;

                    g.FillRectangle(cellBrush, rect);

                    // Підсвічування: легальні ходи
                    if (IsLegalTarget(pos))
                    {
                        Piece target = board.GetPiece(pos);
                        if (target != null)
                        {
                            // Кутові маркери для взяття
                            g.DrawRectangle(PenLegalCapture, px + 3, py + 3, CellSize - 6, CellSize - 6);
                        }
                        else
                        {
                            // Крапка для переміщення
                            int dotSize = CellSize / 4;
                            int dotX = px + (CellSize - dotSize) / 2;
                            int dotY = py + (CellSize - dotSize) / 2;
                            g.FillEllipse(BrushLegalDot, dotX, dotY, dotSize, dotSize);
                        }
                    }

                    // ── Фігура ───────────────────────────────────────────────
                    Piece piece = board.GetPiece(pos);
                    if (piece != null)
                        DrawPiece(g, piece, px, py);
                }
            }

            // ── Рамка дошки ──────────────────────────────────────────────────
            g.DrawRectangle(PenBoardBorder, BoardOffset, BoardOffset, BoardSize, BoardSize);

            // ── Індикатор «ШІ думає» ─────────────────────────────────────────
            if (_aiThinking)
            {
                string text = "ШІ думає...";
                using (var f = new Font("Segoe UI", 12f, FontStyle.Bold))
                {
                    SizeF sz = g.MeasureString(text, f);
                    float tx = BoardOffset + (BoardSize - sz.Width) / 2;
                    float ty = BoardOffset + (BoardSize - sz.Height) / 2;
                    g.FillRectangle(BrushAIBg, tx - 10, ty - 6, sz.Width + 20, sz.Height + 12);
                    g.DrawString(text, f, Brushes.White, tx, ty);
                }
            }
        }

        /// <summary>
        /// Малює фігуру у клітині через Unicode-символ із заливкою та обведенням
        /// (без жодного фонового кола).
        /// </summary>
        private void DrawPiece(Graphics g, Piece piece, int px, int py)
        {
            string symbol = _pieceSymbol[piece.Type];

            // Кольори фарби та обведення залежать від кольору фігури
            Color fillColor = piece.Color == PieceColor.White
                ? Color.FromArgb(248, 248, 248)
                : Color.FromArgb(25, 25, 25);
            Color outlineColor = piece.Color == PieceColor.White
                ? Color.FromArgb(20, 20, 20)
                : Color.FromArgb(245, 245, 245);

            float emSize = CellSize * 0.62f;
            using (var fam = new FontFamily("Segoe UI Symbol"))
            using (var path = new GraphicsPath())
            {
                // Будуємо контур символу
                using (var sf = new StringFormat(StringFormat.GenericTypographic))
                {
                    sf.Alignment = StringAlignment.Center;
                    sf.LineAlignment = StringAlignment.Center;
                    var rect = new RectangleF(px, py, CellSize, CellSize);
                    path.AddString(symbol, fam, (int)FontStyle.Regular, emSize, rect, sf);
                }

                // Спершу малюємо обведення, потім заливку — щоб контур був за фігурою
                using (var pen = new Pen(outlineColor, 3.2f) { LineJoin = LineJoin.Round })
                    g.DrawPath(pen, path);
                using (var brush = new SolidBrush(fillColor))
                    g.FillPath(brush, path);
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
                if (piece == null) return;             // клік по порожній клітині
                if (piece.Color != _controller.State.CurrentTurn)
                {
                    ShowHint("Це фігура суперника");
                    return;
                }
                _selectedPos = pos;
                _legalMoves = _controller.GetLegalMovesFor(pos);
                if (_legalMoves.Count == 0)
                    ShowHint("Ця фігура не має ходів");
                _boardPanel.Invalidate();
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
            bool ok = _controller.TryHumanMove(_selectedPos.Value, pos);
            if (!ok)
            {
                // Хід неможливий — пояснюємо чому
                if (_controller.IsCurrentPlayerInCheck())
                    ShowHint("Хід неможливий — потрібно захистити короля");
                else
                    ShowHint("Хід неможливий за правилами");
            }

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
            UpdateCapturedPanel();
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
                _moveList.Items.Add($"{(moveNum + 1) / 2,2}. {move}");
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
            _moveList.TopIndex = Math.Max(0, _moveList.Items.Count - 1);
        }

        /// <summary>Повністю переобчислює список ходів з MoveHistory (після Undo чи перегляду).</summary>
        private void RebuildMoveList()
        {
            _moveList.Items.Clear();
            var hist = _controller.State.MoveHistory;
            for (int i = 0; i < hist.Count; i++)
            {
                if (i % 2 == 0)
                    _moveList.Items.Add($"{i / 2 + 1,2}. {hist[i]}");
                else
                {
                    string last = _moveList.Items[_moveList.Items.Count - 1].ToString();
                    _moveList.Items[_moveList.Items.Count - 1] = $"{last}  {hist[i]}";
                }
            }
            _moveList.TopIndex = Math.Max(0, _moveList.Items.Count - 1);
        }

        /// <summary>
        /// Підраховує захоплені фігури з MoveHistory і виводить їх символами,
        /// плюс матеріальну перевагу у пунктах (P=1, N=B=3, R=5, Q=9).
        /// </summary>
        private void UpdateCapturedPanel()
        {
            if (_capturedWhiteLabel == null) return;

            // Стандартна вартість фігур (без короля)
            int Value(PieceType t)
            {
                switch (t)
                {
                    case PieceType.Pawn: return 1;
                    case PieceType.Knight:
                    case PieceType.Bishop: return 3;
                    case PieceType.Rook: return 5;
                    case PieceType.Queen: return 9;
                    default: return 0;
                }
            }

            var capturedWhite = new List<PieceType>(); // знятих білих фігур
            var capturedBlack = new List<PieceType>();
            int materialDiff = 0; // позитивне = перевага білих

            foreach (var m in _controller.State.MoveHistory)
            {
                if (m.CapturedPiece == null) continue;
                int v = Value(m.CapturedPiece.Type);
                if (m.CapturedPiece.Color == PieceColor.White)
                {
                    capturedWhite.Add(m.CapturedPiece.Type);
                    materialDiff -= v;
                }
                else
                {
                    capturedBlack.Add(m.CapturedPiece.Type);
                    materialDiff += v;
                }
            }

            // Сортуємо за вартістю (старші — спочатку), щоб краще читалося
            capturedWhite.Sort((a, b) => Value(b).CompareTo(Value(a)));
            capturedBlack.Sort((a, b) => Value(b).CompareTo(Value(a)));

            _capturedWhiteLabel.Text = string.Concat(capturedWhite.ConvertAll(t => _pieceSymbol[t] + " "));
            _capturedBlackLabel.Text = string.Concat(capturedBlack.ConvertAll(t => _pieceSymbol[t] + " "));

            string sign = materialDiff > 0 ? "+" : "";
            _materialLabel.Text = materialDiff == 0
                ? "Матеріал: рівно"
                : $"Матеріал: {sign}{materialDiff} ({(materialDiff > 0 ? "білі" : "чорні")} попереду)";
        }

        /// <summary>Глобальна обробка клавіатури (Ctrl+Z = Undo).</summary>
        private void OnFormKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.Z)
            {
                DoUndo();
                e.Handled = true;
            }
        }

        /// <summary>Звільнення ресурсів при закритті форми.</summary>
        private void OnFormClosed(object sender, FormClosedEventArgs e)
        {
            _hintTimer?.Dispose();
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

        private void OnSavePgn(object sender, EventArgs e)
        {
            if (_controller.State.MoveHistory.Count == 0)
            {
                ShowHint("Немає ходів для збереження");
                return;
            }
            using (var dlg = new SaveFileDialog
            {
                Filter = "PGN партія (*.pgn)|*.pgn|Усі файли (*.*)|*.*",
                FileName = $"chess_{DateTime.Now:yyyyMMdd_HHmm}.pgn"
            })
            {
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        _controller.SavePgn(dlg.FileName);
                        MessageBox.Show("Партію збережено у форматі PGN!", "Збереження",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Помилка збереження PGN: " + ex.Message, "Помилка",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        /// <summary>
        /// Відкриває редактор позиції. При успішному завершенні копіює стан
        /// з редактора у головну дошку та починає партію в режимі 2 гравців.
        /// </summary>
        private void OpenEditor()
        {
            if (_aiThinking) { ShowHint("ШІ зараз думає, зачекайте"); return; }

            using (var dlg = new EditorForm(_controller.Board))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;

                // Копіюємо стан із редактора у головну дошку
                _controller.Board.Clear();
                for (int r = 0; r < 8; r++)
                    for (int c = 0; c < 8; c++)
                    {
                        var p = dlg.EditedBoard.GetPiece(new Position(r, c));
                        if (p != null) _controller.Board.SetPiece(new Position(r, c), p);
                    }
                // Запускаємо нову партію 2 гравці з цієї позиції
                _controller.StartFromCustomPosition(dlg.WhoMovesFirst, GameMode.TwoPlayers);
                ResetUIState();
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

        // ── Панель з подвійним буферуванням ─────────────────────────────────────
        // Звичайна Panel у WinForms не вмикає подвійне буферування,
        // тому при перерисовці (вибір клітини, підсвічування) видно блимання.
        // Цей нащадок вмикає всі потрібні стилі.
        private sealed class DoubleBufferedPanel : Panel
        {
            public DoubleBufferedPanel()
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint
                       | ControlStyles.OptimizedDoubleBuffer
                       | ControlStyles.UserPaint
                       | ControlStyles.ResizeRedraw, true);
                DoubleBuffered = true;
                UpdateStyles();
            }
        }
    }
}