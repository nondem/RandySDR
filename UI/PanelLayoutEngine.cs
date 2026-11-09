namespace RandySDR
{
    internal readonly record struct PanelLayoutItem<T>(T Card, int Height, Padding Margin, bool Wide, int RequestedSpan = 0) where T : class;

    internal readonly record struct PanelLayoutPlacement<T>(T Card, Rectangle Bounds) where T : class;

    internal static class PanelLayoutEngine
    {
        internal static List<PanelLayoutPlacement<T>> Calculate<T>(
            IReadOnlyList<PanelLayoutItem<T>> items, int available, int columns, int wideWidth) where T : class
        {
            int cellWidth = available / columns;
            var occupied = new List<Rectangle>();
            var placements = new List<PanelLayoutPlacement<T>>();
            foreach (var item in items)
            {
                int span = item.Wide ? Math.Min(columns, (wideWidth + 8 + cellWidth - 1) / cellWidth) : 1;
                if (item.RequestedSpan > 0) span = Math.Clamp(item.RequestedSpan, span, columns);
                var candidates = occupied.Select(rect => rect.Bottom).Append(0).Distinct().OrderBy(y => y);
                foreach (int top in candidates)
                {
                    bool placed = false;
                    for (int column = 0; column <= columns - span; column++)
                    {
                        var bounds = new Rectangle(column * cellWidth, top, span * cellWidth,
                            item.Height + item.Margin.Vertical);
                        if (occupied.Any(rect => rect.IntersectsWith(bounds))) continue;
                        occupied.Add(bounds);
                        placements.Add(new PanelLayoutPlacement<T>(item.Card, new Rectangle(
                            bounds.Left + item.Margin.Left, bounds.Top + item.Margin.Top,
                            bounds.Width - item.Margin.Horizontal, item.Height)));
                        placed = true;
                        break;
                    }
                    if (placed) break;
                }
            }
            return placements;
        }

        internal static (T Target, bool Before) FindInsertion<T>(
            IReadOnlyList<PanelLayoutPlacement<T>> placements, Point point) where T : class
        {
            var nearest = placements[0];
            long nearestDistance = long.MaxValue;
            foreach (var placement in placements)
            {
                Rectangle bounds = placement.Bounds;
                long dx = Math.Max(0, Math.Max((long)bounds.Left - point.X, (long)point.X - bounds.Right));
                long dy = Math.Max(0, Math.Max((long)bounds.Top - point.Y, (long)point.Y - bounds.Bottom));
                long distance = dx * dx + dy * dy;
                if (distance >= nearestDistance) continue;
                nearest = placement;
                nearestDistance = distance;
            }
            return (nearest.Card, point.Y < nearest.Bounds.Top + nearest.Bounds.Height / 2);
        }

        internal static List<T> Move<T>(IReadOnlyList<T> order, T card, T target, bool before) where T : class
        {
            var result = order.ToList();
            if (ReferenceEquals(card, target)) return result;
            result.Remove(card);
            int index = result.IndexOf(target);
            result.Insert(before ? index : index + 1, card);
            return result;
        }
    }
}
