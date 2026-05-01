using System;
using System.Collections.Generic;
using ChessTutor.Logic;
using ChessTutor.Models;

namespace ChessTutor.AI
{
    // ═══════════════════════════════════════════════════════════════════
    //  INTERFACE IPlayer
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Інтерфейс гравця. Демонструє поліморфізм:
    /// HumanPlayer і AIPlayer мають різну реалізацію GetMove().
    /// </summary>
    public interface IPlayer
    {
        PieceColor Color { get; }

        /// <summary>
        /// Повертає вибраний гравцем хід або null (для HumanPlayer — хід передається через UI).
        /// </summary>
        Move GetMove(Board board, MoveValidator validator);
    }

}