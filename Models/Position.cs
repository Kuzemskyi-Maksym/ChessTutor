namespace ChessTutor.Models
{
    /// <summary>
    /// Позиція клітини на дошці (рядок і стовпець, 0-7).
    /// </summary>
    public readonly struct Position
    {
        public int Row { get; }
        public int Col { get; }

        public Position(int row, int col)
        {
            Row = row;
            Col = col;
        }

        /// <summary>Перевіряє, чи позиція знаходиться у межах дошки 8×8.</summary>
        public bool IsValid() => Row >= 0 && Row < 8 && Col >= 0 && Col < 8;

        public override string ToString()
        {
            char file = (char)('A' + Col);
            int rank = Row + 1;
            return $"{file}{rank}";
        }

        public override bool Equals(object obj) =>
            obj is Position p && p.Row == Row && p.Col == Col;

        public override int GetHashCode() => Row * 8 + Col;

        public static bool operator ==(Position a, Position b) => a.Equals(b);
        public static bool operator !=(Position a, Position b) => !a.Equals(b);
    }
}