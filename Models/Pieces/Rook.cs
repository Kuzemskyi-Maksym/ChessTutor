using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChessTutor.Models.Pieces
{

    /// <summary>
    /// Тура: ковзає по горизонталях і вертикалях.
    /// </summary>
    public class Rook : Piece
    {
        public override PieceType Type => PieceType.Rook;
        public override int Value => 500;

        public Rook(PieceColor color) : base(color) { }

        public override List<Move> GetPseudoLegalMoves(Position from, Board board)
        {
            var moves = new List<Move>();
            int[][] dirs = { new[] { 1, 0 }, new[] { -1, 0 }, new[] { 0, 1 }, new[] { 0, -1 } };
            foreach (var d in dirs)
                AddSlidingMoves(from, d[0], d[1], board, moves);
            return moves;
        }
    }
}
