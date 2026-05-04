using ChessTutor.AI;
using ChessTutor.Logic;
using ChessTutor.Models;
using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace ChessTutor.Logic
{
    /// <summary>
    /// Головний контролер гри. Поєднує Board, GameState, MoveValidator і Players.
    /// Є єдиною точкою взаємодії між GUI (MainForm) і логікою гри.
    /// </summary>
    public class GameController
    {

        public Board Board { get; }
        public GameState State { get; }
        public MoveValidator Validator { get; }

        private IPlayer _whitePlayer;
        private IPlayer _blackPlayer;


        /// <summary>Виникає після кожного ходу — GUI оновлює дошку.</summary>
        public event Action<Move> MoveMade;

        /// <summary>Виникає при зміні статусу (шах, мат, пат).</summary>
        public event Action<GameStatus> StatusChanged;

        /// <summary>Виникає коли потрібно вибрати фігуру для перетворення пішака.</summary>
        public event Func<PieceType> PromotionRequested;

        /// <summary>Виникає при будь-якій зміні дошки (Reset, Undo, нова партія).</summary>
        public event Action BoardChanged;


        public GameController()
        {
            Board = new Board();
            Validator = new MoveValidator();
            State = new GameState(Board, Validator);
        }


        /// <summary>Починає нову партію у режимі двох гравців.</summary>
        public void StartTwoPlayerGame()
        {
            State.Mode = GameMode.TwoPlayers;
            _whitePlayer = new HumanPlayer(PieceColor.White);
            _blackPlayer = new HumanPlayer(PieceColor.Black);
            State.Reset();
        }

        /// <summary>Починає нову партію проти комп'ютера.</summary>
        /// <param name="playerColor">Колір людини-гравця.</param>
        /// <param name="aiDepth">Глибина пошуку ШІ (1-5).</param>
        public void StartVsComputerGame(PieceColor playerColor, int aiDepth = 3)
        {
            State.Mode = GameMode.VsComputer;
            PieceColor aiColor = playerColor == PieceColor.White ? PieceColor.Black : PieceColor.White;

            _whitePlayer = playerColor == PieceColor.White
                           ? (IPlayer)new HumanPlayer(PieceColor.White)
                           : new AIPlayer(PieceColor.White, aiDepth);

            _blackPlayer = playerColor == PieceColor.Black
                           ? (IPlayer)new HumanPlayer(PieceColor.Black)
                           : new AIPlayer(PieceColor.Black, aiDepth);

            State.Reset();
        }


        /// <summary>
        /// Виконує хід людини-гравця.
        /// Шукає легальний хід серед списку валідатора, тому правильно
        /// обробляє рокіровку, взяття на проході та перетворення пішака.
        /// </summary>
        /// <param name="from">Початкова позиція.</param>
        /// <param name="to">Кінцева позиція.</param>
        /// <returns>true якщо хід виконано.</returns>
        public bool TryHumanMove(Position from, Position to)
        {
            if (State.Status == GameStatus.Checkmate
                || State.Status == GameStatus.Stalemate
                || State.Status == GameStatus.Draw)
                return false;

            Piece piece = Board.GetPiece(from);
            if (piece == null || piece.Color != State.CurrentTurn) return false;

            // Беремо реальні легальні ходи — у них вже встановлено правильний Type
            var legalMoves = Validator.GetLegalMoves(from, Board);

            // Шукаємо хід з потрібним призначенням
            Move move = null;
            foreach (var m in legalMoves)
                if (m.To == to) { move = m; break; }

            if (move == null) return false; // ход неможливий

            // Якщо це перетворення пішака — запитуємо UI який саме варіант
            if (move.Type == MoveType.Promotion)
            {
                PieceType promotion = PromotionRequested?.Invoke() ?? PieceType.Queen;
                // У GetLegalMoves для промоції є 4 ходи (Queen/Rook/Bishop/Knight)
                Move chosen = null;
                foreach (var m in legalMoves)
                    if (m.To == to && m.Type == MoveType.Promotion && m.PromotionPieceType == promotion)
                    { chosen = m; break; }
                if (chosen != null) move = chosen;
            }

            bool success = State.TryMakeMove(move);
            if (!success) return false;

            MoveMade?.Invoke(move);
            StatusChanged?.Invoke(State.Status);

            return true;
        }

        /// <summary>Чи є поточний гравець комп'ютером?</summary>
        public bool IsCurrentPlayerAI()
        {
            IPlayer cur = State.CurrentTurn == PieceColor.White ? _whitePlayer : _blackPlayer;
            return cur is AIPlayer;
        }

        /// <summary>Глибина пошуку поточного AI (для відображення/налаштування).</summary>
        public int GetCurrentAIDepth()
        {
            IPlayer cur = State.CurrentTurn == PieceColor.White ? _whitePlayer : _blackPlayer;
            AIPlayer ai = cur as AIPlayer;
            return ai != null ? ai.Depth : 0;
        }

        /// <summary>
        /// Обчислює та виконує хід AI для поточного гравця.
        /// Цей метод НЕ запускає потік сам — викликати з фонового потоку.
        /// Не викликає подію MoveMade, бо UI робить це сам після Invoke.
        /// </summary>
        public Move ComputeAIMove()
        {
            IPlayer ai = State.CurrentTurn == PieceColor.White ? _whitePlayer : _blackPlayer;
            if (!(ai is AIPlayer)) return null;
            return ai.GetMove(Board, Validator);
        }

        /// <summary>Виконує хід (як гравець, так і AI), кидає події UI.</summary>
        public bool ApplyComputedMove(Move move)
        {
            if (move == null) return false;
            bool ok = State.TryMakeMove(move);
            if (!ok) return false;
            MoveMade?.Invoke(move);
            StatusChanged?.Invoke(State.Status);
            return true;
        }

        /// <summary>
        /// Скасовує N останніх ходів. У режимі vs AI зазвичай 2 (хід AI + свій),
        /// у режимі 2 гравців — 1.
        /// </summary>
        public bool UndoMoves(int count)
        {
            bool any = false;
            for (int i = 0; i < count; i++)
            {
                if (!State.UndoLastMove()) break;
                any = true;
            }
            if (any)
            {
                BoardChanged?.Invoke();
                StatusChanged?.Invoke(State.Status);
            }
            return any;
        }

        /// <summary>
        /// Повертає список легальних ходів для фігури — для підсвічування на UI.
        /// </summary>
        public List<Move> GetLegalMovesFor(Position pos) =>
            Validator.GetLegalMoves(pos, Board);


        /// <summary>Зберігає результат партії у файл.</summary>
        public void SaveGame(string path) => State.SaveToFile(path);

        /// <summary>Чи є поточна позиція під шахом для поточного гравця?</summary>
        public bool IsCurrentPlayerInCheck() =>
            Validator.IsInCheck(State.CurrentTurn, Board);
    }
}