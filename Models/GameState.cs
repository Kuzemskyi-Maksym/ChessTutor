using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ChessTutor.Logic;

namespace ChessTutor.Models
{
    public enum GameStatus { InProgress, Check, Checkmate, Stalemate, Draw }
    public enum GameMode { TwoPlayers, VsComputer }

    /// <summary>
    /// Зберігає поточний стан шахової партії:
    /// чий хід, статус гри, список ходів, кількість ходів без взяття/пішака (правило 50 ходів).
    /// </summary>
    public class GameState
    {
        // ── Залежності ──

        private readonly Board _board;
        private readonly MoveValidator _validator;

        // ─ Властивості ─

        public PieceColor CurrentTurn { get; private set; }
        public GameStatus Status { get; private set; }
        public GameMode Mode { get; set; }
        public List<Move> MoveHistory { get; } = new List<Move>();
        public int HalfMoveClock { get; private set; } // для правила 50 ходів

        // Стек значень HalfMoveClock перед кожним ходом — для коректного Undo
        private readonly Stack<int> _halfMoveClockStack = new Stack<int>();

        public bool CanUndo => MoveHistory.Count > 0;

        // ─ Конструктор ─

        public GameState(Board board, MoveValidator validator)
        {
            _board = board;
            _validator = validator;
            CurrentTurn = PieceColor.White;
            Status = GameStatus.InProgress;
        }

        // ─ Початок нової гри ────────────────────────────────────────────────────

        /// <summary>Скидає стан до початку партії.</summary>
        public void Reset()
        {
            _board.SetupStandardPosition();
            CurrentTurn = PieceColor.White;
            Status = GameStatus.InProgress;
            HalfMoveClock = 0;
            MoveHistory.Clear();
            _halfMoveClockStack.Clear();
        }

        /// <summary>
        /// Починає партію з вже розставленої позиції на дошці (для редактора).
        /// Викликати після того як Board вручну заповнено.
        /// </summary>
        public void StartFromCurrentBoard(PieceColor whoMovesFirst)
        {
            CurrentTurn = whoMovesFirst;
            Status = GameStatus.InProgress;
            HalfMoveClock = 0;
            MoveHistory.Clear();
            _halfMoveClockStack.Clear();
            UpdateStatus();
        }

        // ─ Виконання ходу ─

        /// <summary>
        /// Виконує хід, якщо він легальний.
        /// Оновлює статус гри та перемикає гравця.
        /// </summary>
        /// <returns>true якщо хід виконано успішно.</returns>
        public bool TryMakeMove(Move move)
        {
            // Зберігаємо попередній HalfMoveClock у стек для Undo
            _halfMoveClockStack.Push(HalfMoveClock);

            // Лічильник 50 ходів
            bool isCapture = move.CapturedPiece != null;
            bool isPawn = move.MovingPiece.Type == PieceType.Pawn;
            HalfMoveClock = (isCapture || isPawn) ? 0 : HalfMoveClock + 1;

            _board.ApplyMove(move);
            MoveHistory.Add(move);

            // Перемикаємо гравця і оновлюємо статус
            CurrentTurn = Opponent(CurrentTurn);
            UpdateStatus();

            return true;
        }

        /// <summary>
        /// Скасовує останній зроблений хід. Повертає true якщо вдалося.
        /// </summary>
        public bool UndoLastMove()
        {
            if (MoveHistory.Count == 0) return false;

            int lastIdx = MoveHistory.Count - 1;
            Move last = MoveHistory[lastIdx];
            MoveHistory.RemoveAt(lastIdx);

            _board.UndoMove(last);
            CurrentTurn = Opponent(CurrentTurn);
            HalfMoveClock = _halfMoveClockStack.Count > 0 ? _halfMoveClockStack.Pop() : 0;

            UpdateStatus();
            return true;
        }

        // ─ Оновлення статусу ─

        private void UpdateStatus()
        {
            if (HalfMoveClock >= 100)                         // 50 ходів без взяття
            {
                Status = GameStatus.Draw;
            }
            else if (_validator.IsCheckmate(CurrentTurn, _board))
            {
                Status = GameStatus.Checkmate;
            }
            else if (_validator.IsStalemate(CurrentTurn, _board))
            {
                Status = GameStatus.Stalemate;
            }
            else if (_validator.IsInCheck(CurrentTurn, _board))
            {
                Status = GameStatus.Check;
            }
            else
            {
                Status = GameStatus.InProgress;
            }
        }

        // ─ Збереження результатів ─

        /// <summary>
        /// Зберігає результат і список ходів у текстовий файл.
        /// Виконує вимогу ТЗ: «збереження результатів у текстовий файл».
        /// </summary>
        /// <param name="path">Шлях до файлу.</param>
        public void SaveToFile(string path)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== Шаховий тренажер — результат партії ===");
            sb.AppendLine($"Дата: {DateTime.Now:dd.MM.yyyy HH:mm}");
            sb.AppendLine($"Режим: {(Mode == GameMode.VsComputer ? "Проти комп'ютера" : "2 гравці")}");
            sb.AppendLine($"Статус: {StatusToUkrainian()}");
            sb.AppendLine($"Всього ходів: {MoveHistory.Count}");
            sb.AppendLine();
            sb.AppendLine("Запис партії:");

            for (int i = 0; i < MoveHistory.Count; i++)
            {
                if (i % 2 == 0)
                    sb.Append($"{i / 2 + 1}. ");
                sb.Append(MoveHistory[i].ToString() + "  ");
                if (i % 2 == 1)
                    sb.AppendLine();
            }

            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        }

        // ─ Допоміжні ─

        private string StatusToUkrainian()
        {
            switch (Status)
            {
                case GameStatus.Checkmate: return $"Шахмат ({Opponent(CurrentTurn)} виграв)";
                case GameStatus.Stalemate: return "Пат — нічия";
                case GameStatus.Draw: return "Нічия (правило 50 ходів)";
                default: return "Гра перервана";
            }
        }

        private static PieceColor Opponent(PieceColor c) =>
            c == PieceColor.White ? PieceColor.Black : PieceColor.White;
    }
}