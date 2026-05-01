using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChessTutor.Models.Pieces
{

    /// <summary>
    /// Ферзь: комбінує рух Тури і Слона — ковзає по 8 напрямках.
    /// </summary>
    public class Queen : Piece
    {
        public override PieceType Type => PieceType.Queen;
        public override int Value => 900;

        public Queen(PieceColor color) : base(color) { }

        public override List<Move> GetPseudoLegalMoves(Position from, Board board)
        {
            var moves = new List<Move>();

            int[][] dirs = { new[]{1,0},new[]{-1,0},new[]{0,1},new[]{0,-1},
                             new[]{1,1},new[]{1,-1},new[]{-1,1},new[]{-1,-1} };
            foreach (var d in dirs)
                AddSlidingMoves(from, d[0], d[1], board, moves);

            return moves;
        }
    }
}
