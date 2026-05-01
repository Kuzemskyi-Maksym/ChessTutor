using ChessTutor.Logic;
using ChessTutor.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChessTutor.AI
{

    // ═══════════════════════════════════════════════════════════════════
    //  HUMAN PLAYER
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Гравець-людина. GetMove() не використовується напряму —
    /// хід надходить з MainForm через подію кліку.
    /// Клас існує для єдиного інтерфейсу з AIPlayer.
    /// </summary>
    public class HumanPlayer : IPlayer
    {
        public PieceColor Color { get; }

        public HumanPlayer(PieceColor color) { Color = color; }

        /// <summary>Людина ходить через UI — цей метод не викликається.</summary>
        public Move GetMove(Board board, MoveValidator validator) => null;
    }

    // ═══════════════════════════════════════════════════════════════════
    //  AI PLAYER  (алгоритм Мінімакс з відсіканням Альфа-Бета)
    // ═══════════════════════════════════════════════════════════════════

    
}
