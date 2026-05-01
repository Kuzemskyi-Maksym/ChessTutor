using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChessTutor.Models.Pieces
{
    /// <summary>
    /// Пішак: рухається вперед 1 (або 2 з початкової позиції),
    /// б'є по діагоналі, може перетворитися та бити на проході.
    /// </summary>
    public class Pawn : Piece
    {
        public override PieceType Type => PieceType.Pawn;
        public override int Value => 100;

        public Pawn(PieceColor color) : base(color) { }

        public override List<Move> GetPseudoLegalMoves(Position from, Board board)
        {
            var moves = new List<Move>();
            int dir = Color == PieceColor.White ? 1 : -1;
            int startRow = Color == PieceColor.White ? 1 : 6;
            int promRow = Color == PieceColor.White ? 7 : 0;

            // Хід вперед на 1
            var oneAhead = new Position(from.Row + dir, from.Col);
            if (oneAhead.IsValid() && board.GetPiece(oneAhead) == null)
            {
                AddPawnMove(from, oneAhead, promRow, board, moves);

                // Хід вперед на 2 з початкової позиції
                if (from.Row == startRow)
                {
                    var twoAhead = new Position(from.Row + 2 * dir, from.Col);
                    if (board.GetPiece(twoAhead) == null)
                        moves.Add(CreateMove(from, twoAhead, board));
                }
            }

            // Взяття по діагоналі
            foreach (int dc in new[] { -1, 1 })
            {
                var diag = new Position(from.Row + dir, from.Col + dc);
                if (!diag.IsValid()) continue;

                Piece target = board.GetPiece(diag);

                // Звичайне взяття
                if (target != null && target.Color != Color)
                    AddPawnMove(from, diag, promRow, board, moves);

                // Взяття на проході
                if (diag == board.EnPassantTarget)
                {
                    var epMove = new Move(this, from, diag)
                    {
                        Type = MoveType.EnPassant,
                        CapturedPiece = board.GetPiece(new Position(from.Row, diag.Col))
                    };
                    moves.Add(epMove);
                }
            }

            return moves;
        }

        /// <summary>
        /// Додає хід або кілька ходів перетворення, якщо пішак досягає останньої лінії.
        /// </summary>
        private void AddPawnMove(Position from, Position to, int promRow, Board board, List<Move> moves)
        {
            if (to.Row == promRow)
            {
                foreach (PieceType pt in new[] { PieceType.Queen, PieceType.Rook,
                                                  PieceType.Bishop, PieceType.Knight })
                {
                    var m = CreateMove(from, to, board);
                    m.Type = MoveType.Promotion;
                    m.PromotionPieceType = pt;
                    moves.Add(m);
                }
            }
            else
            {
                moves.Add(CreateMove(from, to, board));
            }
        }
    }
}
