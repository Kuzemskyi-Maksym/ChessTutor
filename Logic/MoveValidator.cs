using System.Collections.Generic;
using ChessTutor.Models;

namespace ChessTutor.Logic
{
    /// <summary>
    /// Перевіряє легальність ходів:
    /// - відфільтровує ходи, що залишають власного короля під шахом;
    /// - додає рокіровку;
    /// - визначає стан шаху, шахмату та пату.
    /// </summary>
    public class MoveValidator
    {
        // ─ Публічний API ─

        /// <summary>
        /// Повертає список усіх легальних ходів для фігури на позиції <paramref name="from"/>.
        /// </summary>
        public List<Move> GetLegalMoves(Position from, Board board)
        {
            Piece piece = board.GetPiece(from);
            if (piece == null) return new List<Move>();

            var pseudo = piece.GetPseudoLegalMoves(from, board);
            var legal = new List<Move>();

            foreach (var move in pseudo)
            {
                if (!WouldLeaveKingInCheck(move, board, piece.Color))
                    legal.Add(move);
            }

            // Додаємо рокіровку (перевіряємо окремо)
            legal.AddRange(GetCastlingMoves(from, board, piece));

            return legal;
        }

        /// <summary>
        /// Повертає всі легальні ходи для заданого кольору (потрібно для шахмату/пату).
        /// </summary>
        public List<Move> GetAllLegalMoves(PieceColor color, Board board)
        {
            var all = new List<Move>();
            for (int r = 0; r < 8; r++)
                for (int c = 0; c < 8; c++)
                {
                    Piece p = board.GetPiece(new Position(r, c));
                    if (p != null && p.Color == color)
                        all.AddRange(GetLegalMoves(new Position(r, c), board));
                }
            return all;
        }

        /// <summary>Чи знаходиться король заданого кольору під шахом?</summary>
        public bool IsInCheck(PieceColor color, Board board)
        {
            Position kingPos = board.FindKing(color);
            return IsSquareAttackedBy(kingPos, Opponent(color), board);
        }

        /// <summary>Шахмат: шах + немає легальних ходів.</summary>
        public bool IsCheckmate(PieceColor color, Board board) =>
            IsInCheck(color, board) && GetAllLegalMoves(color, board).Count == 0;

        /// <summary>Пат: немає шаху, але немає легальних ходів.</summary>
        public bool IsStalemate(PieceColor color, Board board) =>
            !IsInCheck(color, board) && GetAllLegalMoves(color, board).Count == 0;

        // ─ Внутрішня логіка ─

        /// <summary>
        /// Перевіряє, чи залишив би хід власного короля під шахом.
        /// Використовує ApplyMove/UndoMove для тимчасового виконання ходу.
        /// </summary>
        private bool WouldLeaveKingInCheck(Move move, Board board, PieceColor color)
        {
            board.ApplyMove(move, out Position? prevEP);
            bool inCheck = IsInCheck(color, board);
            board.UndoMove(move, prevEP);
            return inCheck;
        }

        /// <summary>
        /// Чи атакована клітина <paramref name="pos"/> фігурами кольору <paramref name="attacker"/>?
        /// </summary>
        public bool IsSquareAttackedBy(Position pos, PieceColor attacker, Board board)
        {
            for (int r = 0; r < 8; r++)
                for (int c = 0; c < 8; c++)
                {
                    Piece p = board.GetPiece(new Position(r, c));
                    if (p == null || p.Color != attacker) continue;

                    var pseudo = p.GetPseudoLegalMoves(new Position(r, c), board);
                    foreach (var m in pseudo)
                        if (m.To == pos) return true;
                }
            return false;
        }

        // ─ Рокіровка ─

        private List<Move> GetCastlingMoves(Position from, Board board, Piece piece)
        {
            var moves = new List<Move>();
            if (piece.Type != PieceType.King || piece.HasMoved) return moves;
            if (IsInCheck(piece.Color, board)) return moves;

            int row = from.Row;

            // Коротка рокіровка (O-O)
            if (CanCastle(board, row, 5, 6, 7, piece.Color, MoveType.CastlingKingside))
            {
                var m = new Move(piece, from, new Position(row, 6)) { Type = MoveType.CastlingKingside };
                moves.Add(m);
            }

            // Довга рокіровка (O-O-O)
            if (CanCastle(board, row, 3, 2, 0, piece.Color, MoveType.CastlingQueenside))
            {
                var m = new Move(piece, from, new Position(row, 2)) { Type = MoveType.CastlingQueenside };
                moves.Add(m);
            }

            return moves;
        }

        /// <summary>
        /// Перевіряє умови рокіровки: шлях вільний, клітини не під атакою, тура не ходила.
        /// </summary>
        private bool CanCastle(Board board, int row,
                                int passCol1, int passCol2, int rookCol,
                                PieceColor color, MoveType type)
        {
            Piece rook = board.GetPiece(new Position(row, rookCol));
            if (rook == null || rook.Type != PieceType.Rook || rook.HasMoved) return false;

            // Перевіряємо що всі клітини між королем і турою порожні
            int kingCol = 4;
            int minCol = System.Math.Min(kingCol, rookCol) + 1;
            int maxCol = System.Math.Max(kingCol, rookCol) - 1;
            for (int c = minCol; c <= maxCol; c++)
                if (board.GetPiece(new Position(row, c)) != null) return false;

            // Клітини через які проходить король не повинні бути під атакою
            PieceColor opp = Opponent(color);
            if (IsSquareAttackedBy(new Position(row, passCol1), opp, board)) return false;
            if (IsSquareAttackedBy(new Position(row, passCol2), opp, board)) return false;

            return true;
        }

        private static PieceColor Opponent(PieceColor c) =>
            c == PieceColor.White ? PieceColor.Black : PieceColor.White;
    }
}