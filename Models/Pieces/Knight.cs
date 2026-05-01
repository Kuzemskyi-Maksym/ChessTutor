using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChessTutor.Models.Pieces
{

    /// <summary>
    /// Кінь: стрибає буквою «Г», єдина фігура що перестрибує інші.
    /// </summary>
    public class Knight : Piece
    {
        public override PieceType Type => PieceType.Knight;
        public override int Value => 320;

        public Knight(PieceColor color) : base(color) { }

        private static readonly int[,] _jumps =
        {
            { 2, 1 }, { 2,-1 }, {-2, 1 }, {-2,-1 },
            { 1, 2 }, { 1,-2 }, {-1, 2 }, {-1,-2 }
        };

        public override List<Move> GetPseudoLegalMoves(Position from, Board board)
        {
            var moves = new List<Move>();
            for (int i = 0; i < 8; i++)
            {
                var to = new Position(from.Row + _jumps[i, 0], from.Col + _jumps[i, 1]);
                if (CanMoveTo(to, board))
                    moves.Add(CreateMove(from, to, board));
            }
            return moves;
        }
    }

}
