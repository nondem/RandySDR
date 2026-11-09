using RandySDR;

static void Check(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

static List<Rectangle> OriginalPacking(IReadOnlyList<PanelLayoutItem<object>> items, int available, int columns, int wideWidth)
{
    int cellWidth = available / columns;
    var occupied = new List<Rectangle>();
    var result = new List<Rectangle>();
    foreach (var item in items)
    {
        int span = item.Wide ? Math.Min(columns, (wideWidth + 8 + cellWidth - 1) / cellWidth) : 1;
        var candidates = occupied.Select(rect => rect.Bottom).Append(0).Distinct().OrderBy(y => y).ToArray();
        bool placed = false;
        foreach (int top in candidates)
        {
            for (int column = 0; column <= columns - span; column++)
            {
                var cell = new Rectangle(column * cellWidth, top, span * cellWidth, item.Height + item.Margin.Vertical);
                if (occupied.Any(rect => rect.IntersectsWith(cell))) continue;
                occupied.Add(cell);
                result.Add(new Rectangle(cell.Left + item.Margin.Left, cell.Top + item.Margin.Top,
                    cell.Width - item.Margin.Horizontal, item.Height));
                placed = true;
                break;
            }
            if (placed) break;
        }
    }
    return result;
}

var a = new object();
var b = new object();
var c = new object();
var d = new object();
var order = new[] { a, b, c, d };
var margin = new Padding(4);
var items = new[]
{
    new PanelLayoutItem<object>(a, 100, margin, false),
    new PanelLayoutItem<object>(b, 200, margin, false),
    new PanelLayoutItem<object>(c, 60, margin, false),
    new PanelLayoutItem<object>(d, 70, margin, true)
};
var layout = PanelLayoutEngine.Calculate(items, 1000, 2, 620);
Check(layout.Count == 4, "Every panel must be placed.");
Check(layout[0].Bounds == new Rectangle(4, 4, 492, 100), "First panel position.");
Check(layout[1].Bounds == new Rectangle(504, 4, 492, 200), "Second panel position.");
Check(layout[2].Bounds == new Rectangle(4, 112, 492, 60), "Short panel fills the gap.");
Check(layout[3].Bounds == new Rectangle(4, 212, 992, 70), "Wide panel spans both columns.");
Check(PanelLayoutEngine.FindInsertion(layout, new Point(100, 25)) == (a, true), "Upper half inserts before.");
Check(PanelLayoutEngine.FindInsertion(layout, new Point(100, 90)) == (a, false), "Lower half inserts after.");
Check(PanelLayoutEngine.FindInsertion(layout, new Point(900, 600)) == (d, false), "Blank space targets the nearest panel.");
Check(PanelLayoutEngine.FindInsertion(layout, new Point(50, -20)) == (a, true), "Space above targets the first panel.");
Check(PanelLayoutEngine.Move(order, d, b, true).SequenceEqual(new[] { a, d, b, c }), "Move before.");
Check(PanelLayoutEngine.Move(order, a, c, false).SequenceEqual(new[] { b, c, a, d }), "Move after.");
Check(PanelLayoutEngine.Move(order, b, b, true).SequenceEqual(order), "Dropping on itself is unchanged.");
Check(PanelLayoutEngine.Move(order, b, c, true).SequenceEqual(order), "Adjacent before is unchanged.");
var resized = PanelLayoutEngine.Calculate(new[]
{
    new PanelLayoutItem<object>(a, 100, margin, false, 2),
    new PanelLayoutItem<object>(b, 100, margin, false)
}, 1500, 3, 620);
Check(resized[0].Bounds.Width == 992, "Requested width must span two columns.");
Check(resized[1].Bounds.Left == 1004, "Other panels must reflow around resized panels.");
var narrowScreen = PanelLayoutEngine.Calculate(new[] { new PanelLayoutItem<object>(a, 100, margin, false, 2) }, 500, 1, 620);
Check(narrowScreen[0].Bounds.Width == 492, "Saved span must fit on a narrow screen.");

foreach (int columns in new[] { 1, 2, 3 })
{
    int available = columns * 500;
    foreach (var source in order)
    foreach (var target in order)
    foreach (bool before in new[] { true, false })
    {
        var proposed = PanelLayoutEngine.Move(order, source, target, before);
        Check(proposed.Count == order.Length && proposed.Distinct().Count() == order.Length,
            "Moving must preserve each panel exactly once.");
        var proposedItems = proposed.Select(card => items.Single(item => item.Card == card)).ToList();
        var preview = PanelLayoutEngine.Calculate(proposedItems, available, columns, 620);
        var original = OriginalPacking(proposedItems, available, columns, 620);
        Check(preview.Select(p => p.Bounds).SequenceEqual(original), "Existing compact packing must be preserved.");
        for (int i = 0; i < preview.Count; i++)
        {
            Check(preview[i].Bounds.Left >= 0 && preview[i].Bounds.Right <= available,
                "Panel must fit within the workspace.");
            for (int j = i + 1; j < preview.Count; j++)
                Check(!preview[i].Bounds.IntersectsWith(preview[j].Bounds), "Panels must not overlap.");
        }
    }
}

Console.WriteLine("Panel layout checks passed.");
