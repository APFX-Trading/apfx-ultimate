namespace cAlgo.Robots;

/// <summary>Shows short-horizon oil and gold price changes as market context for index trading.</summary>
public sealed class OilVariationSection
{
    private readonly double _fontSize;
    private readonly OilInstrumentBars _wti;
    private readonly OilInstrumentBars _brent;
    private readonly OilInstrumentBars _gold;
    private readonly Dictionary<string, TextBlock> _values = new();
    private DateTime _nextRefreshAt = DateTime.MinValue;

    public OilVariationSection(double fontSize, OilInstrumentBars wti, OilInstrumentBars brent, OilInstrumentBars gold)
    {
        _fontSize = fontSize;
        _wti = wti;
        _brent = brent;
        _gold = gold;
    }

    public ControlBase CreateControl(RobotContext context)
    {
        var section = new StackPanel { Orientation = Orientation.Vertical };
        section.AddChild(new TextBlock
        {
            Text = "Commodity Variation",
            ForegroundColor = Color.FromHex("#FF6FE3FF"),
            FontWeight = FontWeight.ExtraBold,
            FontSize = _fontSize + 1,
            HorizontalAlignment = HorizontalAlignment.Center
        });

        var grid = new Grid(4, 6) { Margin = new Thickness(0, 4, 0, 0) };
        AddCell(grid, "", 0, 0, true);
        AddCell(grid, "Price", 0, 1, true);
        AddCell(grid, "5m", 0, 2, true);
        AddCell(grid, "15m", 0, 3, true);
        AddCell(grid, "1h", 0, 4, true);
        AddCell(grid, "Today", 0, 5, true);
        AddInstrumentRow(grid, 1, _wti);
        AddInstrumentRow(grid, 2, _brent);
        AddInstrumentRow(grid, 3, _gold);
        section.AddChild(grid);
        Refresh(context);
        return section;
    }

    public void RefreshIfDue(RobotContext context)
    {
        if (context.ServerTime >= _nextRefreshAt) Refresh(context);
    }

    private void Refresh(RobotContext context)
    {
        RefreshInstrument(_wti);
        RefreshInstrument(_brent);
        RefreshInstrument(_gold);
        _nextRefreshAt = context.ServerTime.AddSeconds(15);
    }

    private void AddInstrumentRow(Grid grid, int row, OilInstrumentBars instrument)
    {
        AddCell(grid, instrument.Label, row, 0, true);
        AddValue(grid, row, 1, instrument.Label + ".Price");
        AddValue(grid, row, 2, instrument.Label + ".5m");
        AddValue(grid, row, 3, instrument.Label + ".15m");
        AddValue(grid, row, 4, instrument.Label + ".1h");
        AddValue(grid, row, 5, instrument.Label + ".Today");
    }

    private void RefreshInstrument(OilInstrumentBars instrument)
    {
        SetPrice(instrument.Label + ".Price", GetCurrentPrice(instrument.Minute5));
        SetVariation(instrument.Label + ".5m", GetBarVariation(instrument.Minute5));
        SetVariation(instrument.Label + ".15m", GetBarVariation(instrument.Minute15));
        SetVariation(instrument.Label + ".1h", GetBarVariation(instrument.Hour));
        SetVariation(instrument.Label + ".Today", GetTodayVariation(instrument.Daily));
    }

    private void SetPrice(string key, double? price)
    {
        var value = _values[key];
        value.Text = price is { } current ? current.ToString("N2") : "-";
        value.ForegroundColor = Color.White;
    }

    private void SetVariation(string key, double? variation)
    {
        var value = _values[key];
        if (variation is not { } percent)
        {
            value.Text = "-";
            value.ForegroundColor = Color.FromHex("#FFBDEBFF");
            return;
        }

        value.Text = $"{percent:+0.00;-0.00;0.00}%";
        value.ForegroundColor = percent switch
        {
            > 0 => Color.FromHex("#FF65F2A5"),
            < 0 => Color.FromHex("#FFFF8B98"),
            _ => Color.White
        };
    }

    private static double? GetBarVariation(Bars? bars)
    {
        if (bars is null || bars.Count < 2) return null;
        var previousClose = bars.ClosePrices[bars.Count - 2];
        var currentClose = bars.ClosePrices[bars.Count - 1];
        return previousClose > 0 ? 100d * (currentClose - previousClose) / previousClose : null;
    }

    private static double? GetCurrentPrice(Bars? bars) => bars is { Count: > 0 } ? bars.ClosePrices[bars.Count - 1] : null;

    private static double? GetTodayVariation(Bars? dailyBars)
    {
        if (dailyBars is null || dailyBars.Count == 0) return null;
        var index = dailyBars.Count - 1;
        var open = dailyBars.OpenPrices[index];
        var current = dailyBars.ClosePrices[index];
        return open > 0 ? 100d * (current - open) / open : null;
    }

    private void AddCell(Grid grid, string text, int row, int column, bool isHeader)
    {
        grid.AddChild(new TextBlock
        {
            Text = text,
            ForegroundColor = Color.FromHex("#FFBDEBFF"),
            FontWeight = isHeader ? FontWeight.ExtraBold : FontWeight.Normal,
            FontSize = Math.Max(8, _fontSize - 2),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(1)
        }, row, column);
    }

    private void AddValue(Grid grid, int row, int column, string key)
    {
        var value = new TextBlock
        {
            Text = "-",
            ForegroundColor = Color.FromHex("#FFBDEBFF"),
            FontSize = Math.Max(8, _fontSize - 2),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(1)
        };
        grid.AddChild(value, row, column);
        _values[key] = value;
    }
}

public readonly record struct OilInstrumentBars(string Label, Bars? Minute5, Bars? Minute15, Bars? Hour, Bars? Daily);
