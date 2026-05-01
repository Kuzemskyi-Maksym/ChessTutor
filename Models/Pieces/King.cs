using System.Collections.Generic;
using System.Drawing;

namespace ChessTutor.Models.Pieces
{


    /// <summary>
    /// Король: ходить на 1 клітину у будь-якому напрямку.
    /// Рокіровка перевіряється у MoveValidator окремо.
    /// </summary>
    public class King : Piece
    {
        public override PieceType Type => PieceType.King;
        public override int Value => 20000; // умовно велике значення

        public King(PieceColor color) : base(color) { }

        public override List<Move> GetPseudoLegalMoves(Position from, Board board)
        {
            var moves = new List<Move>();

            int[] deltas = { -1, 0, 1 };
            foreach (int dr in deltas)
                foreach (int dc in deltas)
                {
                    if (dr == 0 && dc == 0) continue;
                    var to = new Position(from.Row + dr, from.Col + dc);
                    if (CanMoveTo(to, board))
                        moves.Add(CreateMove(from, to, board));
                }

            return moves;
        }
    }
}