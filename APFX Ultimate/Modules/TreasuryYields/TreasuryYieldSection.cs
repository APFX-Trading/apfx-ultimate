namespace cAlgo.Robots;

/// <summary>Displays daily FRED Treasury yields and day-over-day changes in basis points.</summary>
public sealed class TreasuryYieldSection
{
    private static readonly string[] Maturities = { "2Y", "10Y", "30Y" };
    private readonly double _fontSize;
    private readonly TreasuryYieldState _state;
    private readonly Dictionary<string, TextBlock> _yieldCells = new();
    private readonly Dictionary<string, TextBlock> _changeCells = new();
    private TextBlock? _status;
    private int _renderedVersion = -1;

    public TreasuryYieldSection(double fontSize, TreasuryYieldState state)
    {
        _fontSize = fontSize;
        _state = state;
    }

    public ControlBase CreateControl(RobotContext context)
    {
        var section = new StackPanel { Orientation = Orientation.Vertical };
        section.AddChild(new TextBlock
        {
            Text = "US Treasury Yields (FRED)",
            ForegroundColor = Color.FromHex("#FF6FE3FF"),
            FontWeight = FontWeight.ExtraBold,
            FontSize = _fontSize + 1,
            HorizontalAlignment = HorizontalAlignment.Center
        });
        _status = new TextBlock { ForegroundColor = Color.FromHex("#FFBDEBFF"), FontSize = Math.Max(8, _fontSize - 2), Margin = new Thickness(0, 1, 0, 3) };
        section.AddChild(_status);

        var grid = new Grid(4, 3);
        AddCell(grid, "Term", 0, 0, true);
        AddCell(grid, "Yield", 0, 1, true);
        AddCell(grid, "Daily", 0, 2, true);
        for (var row = 0; row < Maturities.Length; row++)
        {
            var maturity = Maturities[row];
            AddCell(grid, maturity, row + 1, 0, true);
            _yieldCells[maturity] = AddCell(grid, "-", row + 1, 1, false);
            _changeCells[maturity] = AddCell(grid, "-", row + 1, 2, false);
        }
        section.AddChild(grid);
        Refresh(context);
        return section;
    }

    public void Refresh(RobotContext context)
    {
        if (_status is null || _renderedVersion == _state.Version) return;
        _status.Text = _state.Status;
        foreach (var maturity in Maturities)
        {
            if (!_state.Yields.TryGetValue(maturity, out var yield)) continue;
            _yieldCells[maturity].Text = yield.Value.ToString("N2") + "%";
            var change = yield.PreviousValue is { } previous ? (yield.Value - previous) * 100d : (double?)null;
            _changeCells[maturity].Text = change is { } basisPoints ? $"{basisPoints:+0.0;-0.0;0.0} bp" : "-";
            _changeCells[maturity].ForegroundColor = change switch
            {
                > 0 => Color.FromHex("#FFFF8B98"),
                < 0 => Color.FromHex("#FF65F2A5"),
                _ => Color.White
            };
        }
        _renderedVersion = _state.Version;
    }

    private TextBlock AddCell(Grid grid, string text, int row, int column, bool isHeader)
    {
        var cell = new TextBlock
        {
            Text = text,
            ForegroundColor = isHeader ? Color.FromHex("#FFBDEBFF") : Color.White,
            FontWeight = isHeader ? FontWeight.ExtraBold : FontWeight.Normal,
            FontSize = Math.Max(8, _fontSize - 2),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(1)
        };
        grid.AddChild(cell, row, column);
        return cell;
    }
}
