using System.Collections.Generic;

namespace ChessTutor.Models
{
    /// <summary>
    /// Абстрактний базовий клас для всіх шахових фігур.
    /// Демонструє принципи ООП: інкапсуляція (властивості), поліморфізм (GetValidMoves).
    /// </summary>
    public abstract class Piece
    {

        /// <summary>Колір фігури.</summary>
        public PieceColor Color { get; }

        /// <summary>Тип фігури (для ШІ та UI).</summary>
        public abstract PieceType Type { get; }

        /// <summary>
        /// Вага фігури для оцінювальної функції ШІ.
        /// Перевизначається у кожному нащадку.
        /// </summary>
        public abstract int Value { get; }

        /// <summary>Чи ходила ця фігура хоча б раз (потрібно для рокіровки та пішака).</summary>
        public bool HasMoved { get; set; }


        protected Piece(PieceColor color)
        {
            Color = color;
            HasMoved = false;
        }


        /// <summary>
        /// Повертає список усіх можливих ходів фігури з позиції <paramref name="from"/>
        /// на поточній дошці <paramref name="board"/>
        /// (без перевірки на шах власному королю — це робить MoveValidator).
        /// </summary>
        /// <param name="from">Поточна позиція фігури.</param>
        /// <param name="board">Поточний стан дошки.</param>
        /// <returns>Список псевдолегальних ходів.</returns>
        public abstract List<Move> GetPseudoLegalMoves(Position from, Board board);


        /// <summary>
        /// Перевіряє чи клітина <paramref name="pos"/> є ворожою або порожньою —
        /// тобто чи туди можна піти.
        /// </summary>
        protected bool CanMoveTo(Position pos, Board board)
        {
            if (!pos.IsValid()) return false;
            Piece target = board.GetPiece(pos);
            return target == null || target.Color != Color;
        }

        /// <summary>
        /// Будує хід і автоматично заповнює CapturedPiece.
        /// </summary>
        protected Move CreateMove(Position from, Position to, Board board)
        {
            var move = new Move(this, from, to)
            {
                CapturedPiece = board.GetPiece(to)
            };
            return move;
        }

        /// <summary>
        /// Допомагає ковзаючим фігурам (Ферзь, Тура, Слон) сканувати промінь
        /// у напрямку (dr, dc), зупиняючись на перешкодах.
        /// </summary>
        protected void AddSlidingMoves(Position from, int dr, int dc,
                                       Board board, List<Move> moves)
        {
            int r = from.Row + dr;
            int c = from.Col + dc;

            while (new Position(r, c).IsValid())
            {
                var pos = new Position(r, c);
                Piece here = board.GetPiece(pos);

                if (here == null)
                {
                    moves.Add(CreateMove(from, pos, board));
                }
                else
                {
                    if (here.Color != Color)
                        moves.Add(CreateMove(from, pos, board)); // взяття
                    break;
                }

                r += dr;
                c += dc;
            }
        }
    }

    public enum PieceColor { White, Black }

    public enum PieceType { King, Queen, Rook, Bishop, Knight, Pawn }
}