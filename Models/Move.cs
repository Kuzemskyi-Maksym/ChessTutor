namespace ChessTutor.Models
{
    /// <summary>
    /// Описує один хід: звідки, куди, тип ходу, взята фігура.
    /// </summary>
    public class Move
    {
        public Position From { get; }
        public Position To { get; }

        /// <summary>Фігура, яка виконує хід.</summary>
        public Piece MovingPiece { get; }

        /// <summary>Фігура, яку знято з дошки (null якщо клітина порожня).</summary>
        public Piece CapturedPiece { get; set; }

        /// <summary>Тип ходу (звичайний, рокіровка, взяття на проході, перетворення пішака).</summary>
        public MoveType Type { get; set; }

        /// <summary>Тип фігури, на яку перетворюється пішак (якщо Type == Promotion).</summary>
        public PieceType PromotionPieceType { get; set; }

        /// <summary>Збереження HasMoved перед ходом для коректного UndoMove.</summary>
        public bool PrevMovingPieceHasMoved { get; set; }

        /// <summary>Збереження HasMoved тури перед рокіровкою.</summary>
        public bool PrevRookHasMoved { get; set; }

        public Move(Piece piece, Position from, Position to)
        {
            MovingPiece = piece;
            From = from;
            To = to;
            Type = MoveType.Normal;
        }

        public override string ToString()
        {
            string capture = CapturedPiece != null ? "x" : "-";
            return $"{MovingPiece.GetType().Name} {From}{capture}{To}";
        }
    }

    public enum MoveType
    {
        Normal,
        CastlingKingside,
        CastlingQueenside,
        EnPassant,
        Promotion
    }
}