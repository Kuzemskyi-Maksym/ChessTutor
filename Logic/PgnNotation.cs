using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ChessTutor.Models;

namespace ChessTutor.Logic
{
    /// <summary>
    /// Конвертація між внутрішнім представленням ходів і
    /// стандартним форматом PGN (Portable Game Notation).
    ///
    /// PGN — стандарт ФІДЕ для запису шахових партій. Хід записується у
    /// SAN (Standard Algebraic Notation), наприклад: e4, Nf3, Bxe5+, O-O, e8=Q#.
    /// </summary>
    public static class PgnNotation
    {
        // ─ Базові утиліти ────────────────────────────────────────────────────

        public static char PieceLetter(PieceType t)
        {
            switch (t)
            {
                case PieceType.King:   return 'K';
                case PieceType.Queen:  return 'Q';
                case PieceType.Rook:   return 'R';
                case PieceType.Bishop: return 'B';
                case PieceType.Knight: return 'N';
                default:               return ' ';
            }
        }

        public static PieceType? LetterToPiece(char c)
        {
            switch (char.ToUpper(c))
            {
                case 'K': return PieceType.King;
                case 'Q': return PieceType.Queen;
                case 'R': return PieceType.Rook;
                case 'B': return PieceType.Bishop;
                case 'N': return PieceType.Knight;
                case 'P': return PieceType.Pawn;
                default:  return null;
            }
        }

        public static string SquareName(Position p)
            => $"{(char)('a' + p.Col)}{p.Row + 1}";

        public static bool TryParseSquare(string s, out Position pos)
        {
            pos = new Position(0, 0);
            if (s == null || s.Length != 2) return false;
            char file = char.ToLower(s[0]);
            char rank = s[1];
            if (file < 'a' || file > 'h') return false;
            if (rank < '1' || rank > '8') return false;
            pos = new Position(rank - '1', file - 'a');
            return true;
        }

        // ─ Move → SAN ────────────────────────────────────────────────────────

        /// <summary>
        /// Конвертує хід у SAN-нотацію. Дошка <paramref name="board"/>
        /// повинна бути У СТАНІ ПЕРЕД ходом.
        /// </summary>
        public static string ToSan(Move move, Board board, MoveValidator validator)
        {
            // Рокіровка
            if (move.Type == MoveType.CastlingKingside)
                return AppendCheckSuffix("O-O", move, board, validator);
            if (move.Type == MoveType.CastlingQueenside)
                return AppendCheckSuffix("O-O-O", move, board, validator);

            var sb = new StringBuilder();
            PieceType type = move.MovingPiece.Type;

            if (type == PieceType.Pawn)
            {
                // Хід пішака
                if (move.CapturedPiece != null || move.Type == MoveType.EnPassant)
                {
                    sb.Append((char)('a' + move.From.Col));
                    sb.Append('x');
                }
                sb.Append(SquareName(move.To));
                if (move.Type == MoveType.Promotion)
                {
                    sb.Append('=');
                    sb.Append(PieceLetter(move.PromotionPieceType));
                }
            }
            else
            {
                // Хід фігури (не пішака)
                sb.Append(PieceLetter(type));

                // Розв'язання неоднозначності — чи інша фігура того ж типу
                // та кольору може теж зробити цей хід?
                var ambig = new List<Position>();
                for (int r = 0; r < 8; r++)
                    for (int c = 0; c < 8; c++)
                    {
                        if (r == move.From.Row && c == move.From.Col) continue;
                        var p = board.GetPiece(new Position(r, c));
                        if (p == null || p.Type != type || p.Color != move.MovingPiece.Color) continue;
                        var legal = validator.GetLegalMoves(new Position(r, c), board);
                        foreach (var m in legal)
                            if (m.To == move.To) { ambig.Add(new Position(r, c)); break; }
                    }

                if (ambig.Count > 0)
                {
                    bool sameFile = false, sameRank = false;
                    foreach (var p in ambig)
                    {
                        if (p.Col == move.From.Col) sameFile = true;
                        if (p.Row == move.From.Row) sameRank = true;
                    }
                    // Стандартні правила розв'язання: спочатку файл, потім ранг,
                    // в крайньому випадку — обидва.
                    if (!sameFile)
                        sb.Append((char)('a' + move.From.Col));
                    else if (!sameRank)
                        sb.Append((char)('1' + move.From.Row));
                    else
                    {
                        sb.Append((char)('a' + move.From.Col));
                        sb.Append((char)('1' + move.From.Row));
                    }
                }

                if (move.CapturedPiece != null) sb.Append('x');
                sb.Append(SquareName(move.To));
            }

            return AppendCheckSuffix(sb.ToString(), move, board, validator);
        }

        /// <summary>Додає '+' (шах) або '#' (мат) до SAN, якщо хід їх дає.</summary>
        private static string AppendCheckSuffix(string san, Move move, Board boardBefore, MoveValidator validator)
        {
            boardBefore.ApplyMove(move);
            PieceColor opp = move.MovingPiece.Color == PieceColor.White
                ? PieceColor.Black : PieceColor.White;
            bool inCheck = validator.IsInCheck(opp, boardBefore);
            bool isMate = inCheck && validator.IsCheckmate(opp, boardBefore);
            boardBefore.UndoMove(move);

            if (isMate) return san + "#";
            if (inCheck) return san + "+";
            return san;
        }

        // ─ Серіалізація всієї партії в PGN ────────────────────────────────

        /// <summary>Повертає PGN-результат партії: 1-0, 0-1, 1/2-1/2 або *.</summary>
        public static string ResultString(GameState state)
        {
            if (state.Status == GameStatus.Checkmate)
                return state.CurrentTurn == PieceColor.White ? "0-1" : "1-0";
            if (state.Status == GameStatus.Stalemate || state.Status == GameStatus.Draw)
                return "1/2-1/2";
            return "*";
        }

        /// <summary>
        /// Серіалізує всю партію у формат PGN. Переграє історію на чистій дошці,
        /// генеруючи SAN на льоту (бо для disambiguation потрібен стан до ходу).
        /// </summary>
        public static string SerializeGame(GameState state)
        {
            var sb = new StringBuilder();
            string result = ResultString(state);

            // Заголовки PGN
            sb.AppendLine("[Event \"Casual Game\"]");
            sb.AppendLine("[Site \"Chess Tutor\"]");
            sb.AppendLine($"[Date \"{DateTime.Now:yyyy.MM.dd}\"]");
            sb.AppendLine("[Round \"-\"]");
            sb.AppendLine($"[White \"{(state.Mode == GameMode.VsComputer ? "Player" : "Player1")}\"]");
            sb.AppendLine($"[Black \"{(state.Mode == GameMode.VsComputer ? "Computer" : "Player2")}\"]");
            sb.AppendLine($"[Result \"{result}\"]");
            sb.AppendLine();

            // Переграємо ходи
            var board = new Board();
            board.SetupStandardPosition();
            var validator = new MoveValidator();

            var line = new StringBuilder();
            for (int i = 0; i < state.MoveHistory.Count; i++)
            {
                var origMove = state.MoveHistory[i];
                Piece localPiece = board.GetPiece(origMove.From);
                if (localPiece == null) break; // несподівана розсинхронізація

                var local = new Move(localPiece, origMove.From, origMove.To)
                {
                    Type = origMove.Type,
                    PromotionPieceType = origMove.PromotionPieceType,
                    CapturedPiece = origMove.Type == MoveType.EnPassant
                        ? board.GetPiece(new Position(origMove.From.Row, origMove.To.Col))
                        : board.GetPiece(origMove.To)
                };

                if (i % 2 == 0) line.Append($"{i / 2 + 1}. ");
                line.Append(ToSan(local, board, validator));
                line.Append(' ');

                board.ApplyMove(local);

                // Перенесення на новий рядок щоб не виходило надто довго
                if (line.Length > 75)
                {
                    sb.AppendLine(line.ToString().TrimEnd());
                    line.Clear();
                }
            }

            if (line.Length > 0) sb.Append(line);
            sb.AppendLine(result);
            return sb.ToString();
        }

        public static void SaveGameToFile(GameState state, string path)
            => File.WriteAllText(path, SerializeGame(state), Encoding.UTF8);

        // ─ Парсинг PGN ────────────────────────────────────────────────────

        public class ParsedGame
        {
            public Dictionary<string, string> Tags { get; } = new Dictionary<string, string>();
            public List<string> SanMoves { get; } = new List<string>();
            public string Result { get; set; } = "*";
        }

        /// <summary>Парсить PGN текст: повертає теги і список SAN-ходів.</summary>
        public static ParsedGame ParseGame(string text)
        {
            var pg = new ParsedGame();
            var body = new StringBuilder();

            using (var reader = new StringReader(text))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    string trimmed = line.Trim();
                    if (trimmed.Length == 0) continue;

                    if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
                    {
                        ParseTagLine(trimmed, pg.Tags);
                    }
                    else
                    {
                        body.Append(trimmed);
                        body.Append(' ');
                    }
                }
            }

            string raw = body.ToString();
            // Видаляємо коментарі {…}
            raw = StripBetween(raw, '{', '}');
            // Видаляємо варіанти (…)
            raw = StripBetween(raw, '(', ')');
            // Видаляємо NAG-коментарі типу $1, $2 тощо
            raw = System.Text.RegularExpressions.Regex.Replace(raw, @"\$\d+", "");

            var tokens = raw.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var raw_t in tokens)
            {
                string t = raw_t.Trim();
                if (t.Length == 0) continue;
                // Результат
                if (t == "1-0" || t == "0-1" || t == "1/2-1/2" || t == "*")
                {
                    pg.Result = t;
                    continue;
                }
                // Номер ходу: "1." або "1..." або "12."
                if (char.IsDigit(t[0]))
                {
                    // Можливо токен типу "1.e4" — розіб'ємо
                    int dotIdx = t.IndexOf('.');
                    if (dotIdx >= 0)
                    {
                        string after = t.Substring(dotIdx + 1).TrimStart('.');
                        if (after.Length > 0) pg.SanMoves.Add(after);
                        continue;
                    }
                }
                pg.SanMoves.Add(t);
            }

            return pg;
        }

        private static void ParseTagLine(string line, Dictionary<string, string> tags)
        {
            // [Key "Value"]
            int firstSpace = line.IndexOf(' ');
            if (firstSpace < 0) return;
            string key = line.Substring(1, firstSpace - 1);
            int q1 = line.IndexOf('"', firstSpace);
            int q2 = line.LastIndexOf('"');
            if (q1 < 0 || q2 <= q1) return;
            tags[key] = line.Substring(q1 + 1, q2 - q1 - 1);
        }

        private static string StripBetween(string s, char open, char close)
        {
            while (true)
            {
                int o = s.IndexOf(open);
                if (o < 0) break;
                int c = s.IndexOf(close, o);
                if (c < 0) break;
                s = s.Substring(0, o) + s.Substring(c + 1);
            }
            return s;
        }

        // ─ SAN → Move (для перегляду партії) ───────────────────────────────

        /// <summary>
        /// Спроба знайти Move у поточній позиції за SAN-нотацією.
        /// Повертає null якщо хід не вдалося ідентифікувати.
        /// </summary>
        public static Move ParseSan(string san, Board board, MoveValidator validator, PieceColor color)
        {
            if (string.IsNullOrEmpty(san)) return null;

            // Прибираємо суфікси шаху/мату/анотацій
            san = san.TrimEnd('+', '#', '!', '?');

            // Рокіровка
            if (san == "O-O" || san == "0-0")
                return FindCastling(board, validator, color, MoveType.CastlingKingside);
            if (san == "O-O-O" || san == "0-0-0")
                return FindCastling(board, validator, color, MoveType.CastlingQueenside);

            // Промоція: "e8=Q" або "e8Q" — відокремлюємо тип
            PieceType? promotion = null;
            int eq = san.IndexOf('=');
            if (eq > 0 && eq + 1 < san.Length)
            {
                promotion = LetterToPiece(san[eq + 1]);
                san = san.Substring(0, eq);
            }

            // Останні два символи — клітина призначення
            if (san.Length < 2) return null;
            string toStr = san.Substring(san.Length - 2);
            if (!TryParseSquare(toStr, out Position to)) return null;
            string rest = san.Substring(0, san.Length - 2).Replace("x", "");

            PieceType pieceType = PieceType.Pawn;
            int idx = 0;
            if (rest.Length > 0 && char.IsUpper(rest[0]))
            {
                pieceType = LetterToPiece(rest[0]) ?? PieceType.Pawn;
                idx++;
            }

            // Залишок — disambiguation: 0..2 символів (file/rank/обидва)
            int fromCol = -1, fromRow = -1;
            for (int i = idx; i < rest.Length; i++)
            {
                char ch = rest[i];
                if (ch >= 'a' && ch <= 'h') fromCol = ch - 'a';
                else if (ch >= '1' && ch <= '8') fromRow = ch - '1';
            }

            // Шукаємо легальний хід що відповідає
            for (int r = 0; r < 8; r++)
                for (int c = 0; c < 8; c++)
                {
                    if (fromRow >= 0 && r != fromRow) continue;
                    if (fromCol >= 0 && c != fromCol) continue;
                    var pos = new Position(r, c);
                    var p = board.GetPiece(pos);
                    if (p == null || p.Type != pieceType || p.Color != color) continue;
                    var legal = validator.GetLegalMoves(pos, board);
                    foreach (var m in legal)
                    {
                        if (m.To != to) continue;
                        if (promotion.HasValue)
                        {
                            if (m.Type == MoveType.Promotion && m.PromotionPieceType == promotion.Value)
                                return m;
                        }
                        else
                        {
                            // Якщо це пішак на останній лінії і ми не вказали промоцію —
                            // беремо за замовчуванням ферзя (типовий промоушн)
                            if (m.Type == MoveType.Promotion && m.PromotionPieceType != PieceType.Queen)
                                continue;
                            return m;
                        }
                    }
                }

            return null;
        }

        private static Move FindCastling(Board board, MoveValidator v, PieceColor color, MoveType castling)
        {
            int row = color == PieceColor.White ? 0 : 7;
            var legal = v.GetLegalMoves(new Position(row, 4), board);
            foreach (var m in legal)
                if (m.Type == castling) return m;
            return null;
        }
    }
}
