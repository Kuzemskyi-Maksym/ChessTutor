using System;
using System.Drawing;
using System.Windows.Forms;
using ChessTutor.Models;

namespace ChessTutor.Forms
{
    /// <summary>
    /// Діалог вибору фігури при перетворенні пішака.
    /// З'являється коли пішак досягає останньої лінії.
    /// </summary>
    public class PromotionDialog : Form
    {
        public PieceType SelectedPiece { get; private set; } = PieceType.Queen;

        private readonly PieceColor _color;

        private static readonly (PieceType type, string symbol, string name)[] _options =
        {
            (PieceType.Queen,  "♕", "Ферзь"),
            (PieceType.Rook,   "♖", "Тура"),
            (PieceType.Bishop, "♗", "Слон"),
            (PieceType.Knight, "♘", "Кінь"),
        };

        public PromotionDialog(PieceColor color)
        {
            _color = color;
            BuildUI();
        }

        private void BuildUI()
        {
            Text            = "Перетворення пішака";
            Size            = new Size(360, 130);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox     = false;
            MinimizeBox     = false;
            StartPosition   = FormStartPosition.CenterParent;
            BackColor       = Color.FromArgb(50, 50, 50);

            var label = new Label
            {
                Text      = "Оберіть фігуру:",
                ForeColor = Color.White,
                Location  = new Point(10, 10),
                Size      = new Size(150, 20)
            };
            Controls.Add(label);

            for (int i = 0; i < _options.Length; i++)
            {
                var opt = _options[i];
                string sym = _color == PieceColor.Black
                    ? opt.symbol.Replace("♕","♛").Replace("♖","♜").Replace("♗","♝").Replace("♘","♞")
                    : opt.symbol;

                var btn = new Button
                {
                    Text      = $"{sym}\n{opt.name}",
                    Size      = new Size(78, 62),
                    Location  = new Point(10 + i * 82, 30),
                    BackColor = Color.FromArgb(70, 70, 70),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat,
                    Font      = new Font("Segoe UI Symbol", 14f),
                    Tag       = opt.type
                };
                btn.FlatAppearance.BorderColor = Color.FromArgb(120, 120, 120);
                btn.Click += (s, e) => {
                    SelectedPiece = (PieceType)((Button)s).Tag;
                    DialogResult  = DialogResult.OK;
                    Close();
                };
                Controls.Add(btn);
            }
        }
    }
}
