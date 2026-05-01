using ChessTutor.Logic;
using ChessTutor.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChessTutor.AI
{

    /// <summary>
    /// Комп'ютерний опонент.
    /// Реалізує алгоритм мінімаксу з відсіканням альфа-бета (глибина за замовчуванням 3).
    ///
    /// Оцінювальна функція враховує:
    ///   - матеріальну вагу фігур;
    ///   - бонуси позиції (центральні клітини, розвиток коня/слона).
    /// </summary>
    public class AIPlayer : IPlayer
    {
        public PieceColor Color { get; }

        /// <summary>Глибина пошуку (1 = 1 хід вперед, 3 = стандарт).</summary>
        public int Depth { get; set; }

        private readonly MoveValidator _validator;
        private readonly Random _rng = new Random();

        public AIPlayer(PieceColor color, int depth = 3)
        {
            Color = color;
            Depth = depth;
            _validator = new MoveValidator();
        }

        // ── Головний метод ────────────────────────────────────────────────────

        /// <summary>
        /// Вибирає найкращий хід за допомогою мінімаксу з альфа-бета відсіканням.
        /// </summary>
        public Move GetMove(Board board, MoveValidator validator)
        {
            var moves = validator.GetAllLegalMoves(Color, board);
            if (moves.Count == 0) return null;

            Move bestMove = null;
            int bestScore = int.MinValue;
            int alpha = int.MinValue;
            int beta = int.MaxValue;

            // Перемішуємо ходи щоб уникнути однотипної гри при рівних оцінках
            Shuffle(moves);

            foreach (var move in moves)
            {
                board.ApplyMove(move, out Position? prevEP);

                int score = -Minimax(board, Depth - 1, -beta, -alpha, Opponent(Color));

                board.UndoMove(move, prevEP);

                if (score > bestScore)
                {
                    bestScore = score;
                    bestMove = move;
                }
                alpha = Math.Max(alpha, score);
            }

            return bestMove;
        }

        // ── Мінімакс з альфа-бета відсіканням ───────────────────────────────

        /// <summary>
        /// Рекурсивний мінімакс. Повертає оцінку позиції з точки зору <paramref name="color"/>.
        /// </summary>
        /// <param name="depth">Залишкова глибина пошуку.</param>
        /// <param name="alpha">Нижня межа (найкращий результат для поточного гравця).</param>
        /// <param name="beta">Верхня межа (найкращий результат для суперника).</param>
        private int Minimax(Board board, int depth, int alpha, int beta, PieceColor color)
        {
            // Термінальні вузли
            if (_validator.IsCheckmate(color, board))
                return -30000 - depth; // чим швидший мат, тим краще

            if (_validator.IsStalemate(color, board))
                return 0; // пат — нічия

            if (depth == 0)
                return Evaluate(board, color);

            var moves = _validator.GetAllLegalMoves(color, board);

            int best = int.MinValue;
            foreach (var move in moves)
            {
                board.ApplyMove(move, out Position? prevEP);

                int score = -Minimax(board, depth - 1, -beta, -alpha, Opponent(color));

                board.UndoMove(move, prevEP);

                best = Math.Max(best, score);
                alpha = Math.Max(alpha, score);

                if (alpha >= beta) break; // відсікання
            }

            return best;
        }

        // ── Оцінювальна функція ──────────────────────────────────────────────

        /// <summary>
        /// Оцінює позицію з точки зору гравця <paramref name="color"/>.
        /// Позитивне значення = краще для color, від'ємне = краще для суперника.
        /// </summary>
        private int Evaluate(Board board, PieceColor color)
        {
            int score = 0;

            for (int r = 0; r < 8; r++)
                for (int c = 0; c < 8; c++)
                {
                    Piece p = board.GetPiece(new Position(r, c));
                    if (p == null) continue;

                    int pieceScore = p.Value + GetPositionBonus(p, r, c);
                    score += p.Color == color ? pieceScore : -pieceScore;
                }

            return score;
        }

        /// <summary>
        /// Бонус за позицію: центральні клітини цінніші, пішаки просунуті вперед.
        /// Таблиці piece-square tables (спрощені).
        /// </summary>
        private int GetPositionBonus(Piece piece, int row, int col)
        {
            // Відзеркалюємо рядок для чорних
            int r = piece.Color == PieceColor.White ? row : 7 - row;

            switch (piece.Type)
            {
                case PieceType.Pawn:
                    // Пішаки отримують бонус за просування
                    return r * 5;

                case PieceType.Knight:
                case PieceType.Bishop:
                    // Фігури в центрі (3-4 ряд, 2-5 стовпець) отримують бонус
                    int centerDist = Math.Abs(col - 3) + Math.Abs(col - 4)
                                   + Math.Abs(row - 3) + Math.Abs(row - 4);
                    return Math.Max(0, 20 - centerDist * 5);

                default:
                    return 0;
            }
        }

        // ── Допоміжні ────────────────────────────────────────────────────────

        private static PieceColor Opponent(PieceColor c) =>
            c == PieceColor.White ? PieceColor.Black : PieceColor.White;

        private void Shuffle(List<Move> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = _rng.Next(i + 1);
                var tmp = list[i]; list[i] = list[j]; list[j] = tmp;
            }
        }
    }
}
