// Form1 - folding of cards. A folded card shrinks to its header; each column then re-balances so no space is wasted.
namespace RandySDR
{
    public partial class Form1
    {
        private readonly Dictionary<Control, RowStyle> _rowOriginal = new();
        private readonly HashSet<CardGroupBox> _autoCards = new();

        private void HookCardCollapse(params CardGroupBox[] cards)
        {
            foreach (var card in cards)
            {
                var c = card;
                c.CollapsedChanged += (_, _) => OnCardCollapsed(c);
            }
        }

        private void OnCardCollapsed(CardGroupBox card)
        {
            if (_autoCards.Contains(card))
            {
                card.AutoSize = !card.Collapsed;
                if (card.Collapsed) card.Height = card.HeaderHeight;
            }

            // Demod and Audio share one row inside the modes table.
            if (card.Parent == _modesTable && _modesTable.Parent is TableLayoutPanel outer)
                Rebalance(outer);
            else if (card.Parent is TableLayoutPanel table)
            {
                Rebalance(table);
                if (table == _scanStack) UpdateScannerMinimumHeight();
            }
        }

        private static bool IsFolded(Control c) => c switch
        {
            CardGroupBox card => card.Collapsed,
            TableLayoutPanel t => t.Controls.OfType<CardGroupBox>().All(x => x.Collapsed),
            _ => false
        };

        private static int FoldedHeight(Control c) =>
            c is CardGroupBox card ? card.HeaderHeight + card.Margin.Bottom
                : c.Controls.OfType<CardGroupBox>().Max(x => x.HeaderHeight + x.Margin.Bottom);

        /// <summary>
        /// Folded rows shrink to their header; open rows keep their designed size. If that leaves no row able to
        /// absorb spare height, the last open card is stretched so the column still fills its panel.
        /// </summary>
        private void Rebalance(TableLayoutPanel table)
        {
            table.SuspendLayout();
            var flexCandidates = new List<int>();
            bool hasPercent = false;

            foreach (Control c in table.Controls)
            {
                if (c is not (CardGroupBox or TableLayoutPanel)) continue;
                int row = table.GetRow(c);
                if (row < 0 || row >= table.RowStyles.Count) continue;
                var style = table.RowStyles[row];
                if (!_rowOriginal.TryGetValue(c, out var orig))
                    _rowOriginal[c] = orig = new RowStyle(style.SizeType, style.Height);

                if (IsFolded(c))
                {
                    style.SizeType = SizeType.Absolute;
                    style.Height = FoldedHeight(c);
                }
                else
                {
                    style.SizeType = orig.SizeType;
                    style.Height = orig.Height;
                    if (orig.SizeType == SizeType.Percent) hasPercent = true;
                    else if (c is not CardGroupBox g || !_autoCards.Contains(g)) flexCandidates.Add(row);
                }
            }

            if (!hasPercent && flexCandidates.Count > 0)
            {
                var style = table.RowStyles[flexCandidates.Max()];
                style.SizeType = SizeType.Percent;
                style.Height = 100;
            }

            table.ResumeLayout(true);
        }
    }
}
