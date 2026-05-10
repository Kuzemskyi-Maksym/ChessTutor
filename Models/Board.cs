using System;
using ChessTutor.Models.Pieces;

namespace ChessTutor.Models
{
    /// <summary>
    /// Шахова дошка 8×8.
    /// Зберігає розташування фігур і забезпечує базові операції переміщення.
    /// </summary>
    public class Board
    {
        // ─ Поля ─

        /// <summary>Сітка фігур [рядок, стовпець], 0-based (рядок 0 = лінія 1 для білих).</summary>
        private readonly Piece[,] _grid = new Piece[8, 8];

        /// <summary>
        /// Клітина, на якій можливе взяття на проході у поточному ході.
        /// Null якщо таке взяття неможливе.
        /// </summary>
        public Position? EnPassantTarget { get; private set; }

        // ─ Доступ до клітин ─

        /// <summary>Повертає фігуру на позиції (або null якщо порожньо).</summary>
        public Piece GetPiece(Position pos) => _grid[pos.Row, pos.Col];

        /// <summary>Встановлює фігуру на позицію (null = очистити клітину).</summary>
        public void SetPiece(Position pos, Piece piece) => _grid[pos.Row, pos.Col] = piece;

        // ─ Розміщення фігур ─

        /// <summary>
        /// Розміщує всі фігури у стандартну початкову позицію.
        /// </summary>
        public void SetupStandardPosition()
        {
            Clear();

            PieceColor[] colors = { PieceColor.White, PieceColor.Black };
            int[] backRanks = { 0, 7 };
            int[] pawnRanks = { 1, 6 };

            for (int i = 0; i < 2; i++)
            {
                PieceColor c = colors[i];
                int br = backRanks[i];
                int pr = pawnRanks[i];

                // Задня лінія
                SetPiece(new Position(br, 0), new Rook(c));
                SetPiece(new Position(br, 1), new Knight(c));
                SetPiece(new Position(br, 2), new Bishop(c));
                SetPiece(new Position(br, 3), new Queen(c));
                SetPiece(new Position(br, 4), new King(c));
                SetPiece(new Position(br, 5), new Bishop(c));
                SetPiece(new Position(br, 6), new Knight(c));
                SetPiece(new Position(br, 7), new Rook(c));

                // Пішаки
                for (int col = 0; col < 8; col++)
                    SetPiece(new Position(pr, col), new Pawn(c));
            }
        }

        /// <summary>Очищує всю дошку.</summary>
        public void Clear()
        {
            for (int r = 0; r < 8; r++)
                for (int c = 0; c < 8; c++)
                    _grid[r, c] = null;
            EnPassantTarget = null;
        }

        // ─ Виконання ходу ─

        /// <summary>
        /// Виконує хід на дошці: переміщує фігуру, оновлює стан рокіровки,
        /// взяття на проході та перетворення пішака.
        /// Не перевіряє легальність — це завдання MoveValidator.
        /// Усі дані для UndoMove зберігаються у самому move.
        /// </summary>
        public void ApplyMove(Move move)
        {
            Piece piece = GetPiece(move.From);

            // Зберігаємо поточний EnPassantTarget щоб UndoMove міг відновити
            move.PrevEnPassantTarget = EnPassantTarget;
            EnPassantTarget = null;

            // Зберігаємо попередній стан HasMoved для коректного UndoMove
            move.PrevMovingPieceHasMoved = piece.HasMoved;

            switch (move.Type)
            {
                // ─ Звичайний хід / взяття ─
                case MoveType.Normal:
                default:
                    SetPiece(move.To, piece);
                    SetPiece(move.From, null);

                    // Пішак рухається на 2 — встановлюємо поле для взяття на проході
                    if (piece.Type == PieceType.Pawn && Math.Abs(move.To.Row - move.From.Row) == 2)
                    {
                        int epRow = (move.From.Row + move.To.Row) / 2;
                        EnPassantTarget = new Position(epRow, move.From.Col);
                    }
                    break;

                // ─ Рокіровка ─
                case MoveType.CastlingKingside:
                case MoveType.CastlingQueenside:
                    int rookFromCol = move.Type == MoveType.CastlingKingside ? 7 : 0;
                    int rookToCol = move.Type == MoveType.CastlingKingside ? 5 : 3;
                    int row = move.From.Row;

                    Piece rook = GetPiece(new Position(row, rookFromCol));
                    if (rook == null) break; // захист від некоректного стану

                    move.PrevRookHasMoved = rook.HasMoved;

                    // Спочатку прибираємо обидві фігури, потім ставимо на нові місця
                    SetPiece(move.From, null);
                    SetPiece(new Position(row, rookFromCol), null);
                    SetPiece(move.To, piece);
                    SetPiece(new Position(row, rookToCol), rook);
                    rook.HasMoved = true;
                    break;

                // ─ Взяття на проході ─
                case MoveType.EnPassant:
                    var epCapture = new Position(move.From.Row, move.To.Col);
                    // Зберігаємо взятого пішака якщо він ще не записаний
                    if (move.CapturedPiece == null)
                        move.CapturedPiece = GetPiece(epCapture);
                    SetPiece(epCapture, null);
                    SetPiece(move.To, piece);
                    SetPiece(move.From, null);
                    break;

                // ─ Перетворення пішака ─
                case MoveType.Promotion:
                    SetPiece(move.From, null);
                    SetPiece(move.To, CreatePromotedPiece(move.PromotionPieceType, piece.Color));
                    break;
            }

            piece.HasMoved = true;
        }

        /// <summary>
        /// Скасовує хід (використовується ШІ для перебору варіантів та UI Undo).
        /// </summary>
        public void UndoMove(Move move)
        {
            EnPassantTarget = move.PrevEnPassantTarget;
            Piece piece = GetPiece(move.To);

            switch (move.Type)
            {
                case MoveType.Normal:
                default:
                    SetPiece(move.From, move.MovingPiece);
                    SetPiece(move.To, move.CapturedPiece);
                    break;

                case MoveType.CastlingKingside:
                case MoveType.CastlingQueenside:
                    int rookFromCol = move.Type == MoveType.CastlingKingside ? 7 : 0;
                    int rookToCol = move.Type == MoveType.CastlingKingside ? 5 : 3;
                    int row = move.From.Row;

                    Piece rook = GetPiece(new Position(row, rookToCol));
                    SetPiece(move.From, move.MovingPiece);
                    SetPiece(move.To, null);
                    SetPiece(new Position(row, rookFromCol), rook);
                    SetPiece(new Position(row, rookToCol), null);
                    rook.HasMoved = move.PrevRookHasMoved; // відновлюємо попередній стан
                    break;

                case MoveType.EnPassant:
                    SetPiece(move.From, move.MovingPiece);
                    SetPiece(move.To, null);
                    SetPiece(new Position(move.From.Row, move.To.Col), move.CapturedPiece);
                    break;

                case MoveType.Promotion:
                    SetPiece(move.From, move.MovingPiece);
                    SetPiece(move.To, move.CapturedPiece);
                    break;
            }

            move.MovingPiece.HasMoved = move.PrevMovingPieceHasMoved;
        }

        // ─ Пошук короля ─

        /// <summary>Знаходить позицію короля заданого кольору.</summary>
        public Position FindKing(PieceColor color)
        {
            for (int r = 0; r < 8; r++)
                for (int c = 0; c < 8; c++)
                {
                    var p = _grid[r, c];
                    if (p != null && p.Type == PieceType.King && p.Color == color)
                        return new Position(r, c);
                }
            throw new InvalidOperationException($"Короля кольору {color} не знайдено на дошці.");
        }

        // ─ Глибоке копіювання ─

        /// <summary>
        /// Повертає ГЛИБОКУ копію дошки: і клітинна сітка, і всі фігури — нові об'єкти.
        /// Це потрібно для ШІ, який працює у фоновому потоці на знімку дошки —
        /// щоб UI міг безпечно малювати оригінальну позицію без race condition.
        /// </summary>
        public Board Clone()
        {
            var clone = new Board { EnPassantTarget = EnPassantTarget };
            for (int r = 0; r < 8; r++)
                for (int c = 0; c < 8; c++)
                {
                    Piece p = _grid[r, c];
                    if (p == null) continue;
                    Piece copy = ClonePiece(p);
                    copy.HasMoved = p.HasMoved;
                    clone._grid[r, c] = copy;
                }
            return clone;
        }

        // ─ Допоміжні методи ─

        /// <summary>Створює нову фігуру того ж типу та кольору (без копіювання HasMoved).</summary>
        private static Piece ClonePiece(Piece p)
        {
            switch (p.Type)
            {
                case PieceType.King:   return new King(p.Color);
                case PieceType.Queen:  return new Queen(p.Color);
                case PieceType.Rook:   return new Rook(p.Color);
                case PieceType.Bishop: return new Bishop(p.Color);
                case PieceType.Knight: return new Knight(p.Color);
                case PieceType.Pawn:   return new Pawn(p.Color);
            }
            throw new ArgumentException("Unknown piece type");
        }

        private Piece CreatePromotedPiece(PieceType type, PieceColor color)
        {
            switch (type)
            {
                case PieceType.Queen: return new Queen(color);
                case PieceType.Rook: return new Rook(color);
                case PieceType.Bishop: return new Bishop(color);
                case PieceType.Knight: return new Knight(color);
                default: return new Queen(color);
            }
        }
    }
}