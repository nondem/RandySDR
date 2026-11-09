namespace RandySDR
{
    public partial class Form1
    {
        private void HookCardCollapse(params CardGroupBox[] cards)
        {
            foreach (var card in cards)
                card.CollapsedChanged += (_, _) => OnCardCollapsed(card);
        }

        private void OnCardCollapsed(CardGroupBox card)
        {
            if (_dashboardReady)
            {
                DashboardCollapsed(card);
                return;
            }
            if (_cardHeights.TryGetValue(card, out int height))
                card.Height = card.Collapsed ? card.HeaderHeight : height;
            if (_resizeGrips.TryGetValue(card, out var grip))
                grip.Visible = _resizeModeEnabled && !card.Collapsed;
            SizeWorkspace();
        }
    }
}
