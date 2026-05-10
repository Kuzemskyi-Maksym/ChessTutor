using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using ChessTutor.Logic;
using ChessTutor.Models;

namespace ChessTutor.Forms
{
    /// <summary>
    /// Діалог перегляду збереженої партії.
    /// Завантажує PGN-файл, парсить ходи у Move'и та дозволяє
    /// перегортати партію кнопками |&lt;, &lt;, &gt;, &gt;|.
    /// </summary>
    public class ReplayForm : Form
    {
        private const int CellSize = 60;
        private const int BoardSizePx = CellSize * 8;
        private const int BoardLeft = 16;
        private const int BoardTop = 16;

        // Початкова позиція + список ходів та поточний індекс (0 = початкова)
        private readonly Board _board = new Board();
        private readonly MoveValidator _validator = new MoveValidator();
        private List<Move> _moves = new List<Move>();
        private int _currentPly = 0; // 0 = до 1-го ходу; N = після N-го півходу
        private PgnNotation.ParsedGame _parsed;

        private DoubleBufferedPanelShared _boardPanel;
        private Label _infoLabel;
        private Label _tagsLabel;
        private ListBox _movesList;
        private Button _btnFirst, _btnPrev, _btnNext, _btnLast;

        public ReplayForm(string pgnText)
        {
            BuildUI();
            try
            {
                LoadPgn(pgnText);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не вдалося прочитати файл партії: " + ex.Message,
                    "Помилка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ─ UI ─
        private void BuildUI()
        {
            Text = "Перегляд партії";
            ClientSize = new Size(BoardLeft + BoardSizePx + 280, BoardTop + BoardSizePx + 70);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.FromArgb(40, 40, 40);
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 9f);
            KeyPreview = true;
            KeyDown += OnKeyDownNav;

            _boardPanel = new DoubleBufferedPanelShared
            {
                Location = new Point(BoardLeft, BoardTop),
                Size = new Size(BoardSizePx, BoardSizePx),
                BackColor = Color.FromArgb(40, 40, 40)
            };
            _boardPanel.Paint += (s, e) =>
                BoardRenderer.Draw(e.Graphics, _board,
                    new Rectangle(0, 0, BoardSizePx, BoardSizePx), false, CellSize);
            Controls.Add(_boardPanel);

            int rightX = BoardLeft + BoardSizePx + 16;
            int rightW = 250;

            _tagsLabel = new Label
            {
                Location = new Point(rightX, BoardTop),
                Size = new Size(rightW, 100),
                Font = new Font("Consolas", 8.5f),
                ForeColor = Color.FromArgb(200, 200, 200),
                Text = "(метадані партії)"
            };
            Controls.Add(_tagsLabel);

            _movesList = new ListBox
            {
                Location = new Point(rightX, BoardTop + 110),
                Size = new Size(rightW, BoardSizePx - 110),
                BackColor = Color.FromArgb(55, 55, 55),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.None,
                Font = new Font("Consolas", 9.5f)
            };
            _movesList.SelectedIndexChanged += (s, e) =>
            {
                if (_movesList.SelectedIndex >= 0)
                    GoTo(_movesList.SelectedIndex + 1);
            };
            Controls.Add(_movesList);

            // Панель навігації знизу
            int navY = BoardTop + BoardSizePx + 16;
            _btnFirst = NavBtn("|◀", BoardLeft, navY, () => GoTo(0));
            _btnPrev  = NavBtn("◀",  BoardLeft + 60, navY, () => GoTo(_currentPly - 1));
            _btnNext  = NavBtn("▶",  BoardLeft + 120, navY, () => GoTo(_currentPly + 1));
            _btnLast  = NavBtn("▶|", BoardLeft + 180, navY, () => GoTo(_moves.Count));
            Controls.Add(_btnFirst);
            Controls.Add(_btnPrev);
            Controls.Add(_btnNext);
            Controls.Add(_btnLast);

            _infoLabel = new Label
            {
                Location = new Point(BoardLeft + 250, navY + 6),
                Size = new Size(BoardSizePx - 250, 24),
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Text = "Хід 0 з 0"
            };
            Controls.Add(_infoLabel);
        }

        private Button NavBtn(string text, int x, int y, Action click)
        {
            var b = new Button
            {
                Location = new Point(x, y),
                Size = new Size(50, 30),
                Text = text,
                BackColor = Color.FromArgb(70, 70, 70),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 11f, FontStyle.Bold)
            };
            b.Click += (s, e) => click();
            return b;
        }

        private void OnKeyDownNav(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Right) { GoTo(_currentPly + 1); e.Handled = true; }
            else if (e.KeyCode == Keys.Left) { GoTo(_currentPly - 1); e.Handled = true; }
            else if (e.KeyCode == Keys.Home) { GoTo(0); e.Handled = true; }
            else if (e.KeyCode == Keys.End) { GoTo(_moves.Count); e.Handled = true; }
        }

        // ─ Завантаження PGN ─
        private void LoadPgn(string text)
        {
            _parsed = PgnNotation.ParseGame(text);

            // Парсимо SAN ходи в Move'и, переграваючи на тимчасовій дошці
            var work = new Board();
            work.SetupStandardPosition();
            var validator = new MoveValidator();
            var color = PieceColor.White;
            var movesList = new List<Move>();
            var sanList = new List<string>();

            foreach (var san in _parsed.SanMoves)
            {
                Move m = PgnNotation.ParseSan(san, work, validator, color);
                if (m == null)
                {
                    MessageBox.Show($"Не вдалося розпізнати хід: {san}\n" +
                        "Подальші ходи в партії не будуть показані.",
                        "Парсинг PGN", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    break;
                }
                movesList.Add(m);
                sanList.Add(san);
                work.ApplyMove(m);
                color = (color == PieceColor.White) ? PieceColor.Black : PieceColor.White;
            }

            _moves = movesList;
            // Заповнюємо список ходів як 1. e4 e5 …
            _movesList.Items.Clear();
            for (int i = 0; i < sanList.Count; i++)
            {
                if (i % 2 == 0)
                    _movesList.Items.Add($"{i / 2 + 1,2}. {sanList[i]}");
                else
                {
                    string last = _movesList.Items[_movesList.Items.Count - 1].ToString();
                    _movesList.Items[_movesList.Items.Count - 1] = $"{last}  {sanList[i]}";
                }
            }

            // Заповнюємо теги
            var tg = new System.Text.StringBuilder();
            foreach (var key in new[] { "Event", "Site", "Date", "White", "Black", "Result" })
                if (_parsed.Tags.TryGetValue(key, out string v))
                    tg.AppendLine($"{key}: {v}");
            _tagsLabel.Text = tg.Length > 0 ? tg.ToString() : "(без метаданих)";

            // Початкова позиція
            _board.SetupStandardPosition();
            _currentPly = 0;
            UpdateInfo();
            _boardPanel.Invalidate();
        }

        /// <summary>Перегортає партію до позиції після ply-го півходу.</summary>
        private void GoTo(int ply)
        {
            ply = Math.Max(0, Math.Min(_moves.Count, ply));
            // Простіше — перебудовуємо з нуля до ply
            _board.SetupStandardPosition();
            for (int i = 0; i < ply; i++)
                _board.ApplyMove(_moves[i]);
            _currentPly = ply;
            UpdateInfo();
            _boardPanel.Invalidate();
        }

        private void UpdateInfo()
        {
            _infoLabel.Text = _moves.Count == 0
                ? "(немає ходів)"
                : $"Півхід {_currentPly} з {_moves.Count}";
        }

        // ─ Статичний хелпер для відкриття файлу з MainForm ─
        public static void OpenFromFile(IWin32Window owner)
        {
            using (var dlg = new OpenFileDialog
            {
                Filter = "PGN партія (*.pgn)|*.pgn|Усі файли (*.*)|*.*",
                Title = "Відкрити партію для перегляду"
            })
            {
                if (dlg.ShowDialog(owner) != DialogResult.OK) return;
                string text;
                try { text = File.ReadAllText(dlg.FileName); }
                catch (Exception ex)
                {
                    MessageBox.Show("Не вдалося відкрити файл: " + ex.Message,
                        "Помилка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                using (var rep = new ReplayForm(text))
                    rep.ShowDialog(owner);
            }
        }
    }
}
