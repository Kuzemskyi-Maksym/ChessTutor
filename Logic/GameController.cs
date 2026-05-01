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
        /// Після вдалого ходу автоматично запускає хід ШІ (якщо режим VsComputer).
        /// </summary>
        /// <param name="from">Початкова позиція.</param>
        /// <param name="to">Кінцева позиція.</param>
        /// <returns>true якщо хід виконано.</returns>
        public bool TryHumanMove(Position from, Position to)
        {
            if (State.Status == GameStatus.Checkmate || State.Status == GameStatus.Stalemate)
                return false;

            Piece piece = Board.GetPiece(from);
            if (piece == null || piece.Color != State.CurrentTurn) return false;

            // Перетворення пішака — запитуємо UI
            MoveType type = MoveType.Normal;
            PieceType promotion = PieceType.Queen;

            bool isPromotion = piece is Models.Pieces.Pawn
                && ((piece.Color == PieceColor.White && to.Row == 7)
                 || (piece.Color == PieceColor.Black && to.Row == 0));

            if (isPromotion)
            {
                type = MoveType.Promotion;
                promotion = PromotionRequested?.Invoke() ?? PieceType.Queen;
            }

            var move = new Move(piece, from, to)
            {
                Type = type,
                PromotionPieceType = promotion
            };

            bool success = State.TryMakeMove(move);
            if (!success) return false;

            MoveMade?.Invoke(move);
            StatusChanged?.Invoke(State.Status);

            // Якщо ШІ ходить — запускаємо асинхронно
            if (State.Mode == GameMode.VsComputer && State.Status == GameStatus.InProgress)
                System.Threading.ThreadPool.QueueUserWorkItem(_ => MakeAIMove());

            return true;
        }

        /// <summary>
        /// Повертає список легальних ходів для фігури — для підсвічування на UI.
        /// </summary>
        public List<Move> GetLegalMovesFor(Position pos) =>
            Validator.GetLegalMoves(pos, Board);


        private void MakeAIMove()
        {
            IPlayer ai = State.CurrentTurn == PieceColor.White ? _whitePlayer : _blackPlayer;
            if (!(ai is AIPlayer)) return;

            Move move = ai.GetMove(Board, Validator);
            if (move == null) return;

            // Виконуємо хід у потоці UI через Invoke (WinForms)
            // MainForm підписується на MoveMade і викликає Invoke
            State.TryMakeMove(move);
            MoveMade?.Invoke(move);
            StatusChanged?.Invoke(State.Status);
        }


        /// <summary>Зберігає результат партії у файл.</summary>
        public void SaveGame(string path) => State.SaveToFile(path);

        /// <summary>Чи є поточна позиція під шахом для поточного гравця?</summary>
        public bool IsCurrentPlayerInCheck() =>
            Validator.IsInCheck(State.CurrentTurn, Board);
    }
}