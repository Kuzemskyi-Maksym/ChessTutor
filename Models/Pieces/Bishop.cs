using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChessTutor.Models.Pieces
{


    /// <summary>
    /// Слон: ковзає по діагоналях.
    /// </summary>
    public class Bishop : Piece
    {
        public override PieceType Type => PieceType.Bishop;
        public override int Value => 330;

        public Bishop(PieceColor color) : base(color) { }

        public override List<Move> GetPseudoLegalMoves(Position from, Board board)
        {
            var moves = new List<Move>();
            int[][] dirs = { new[] { 1, 1 }, new[] { 1, -1 }, new[] { -1, 1 }, new[] { -1, -1 } };
            foreach (var d in dirs)
                AddSlidingMoves(from, d[0], d[1], board, moves);
            return moves;
        }
    }
}
