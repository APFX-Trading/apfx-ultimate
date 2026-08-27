namespace cAlgo.Robots;

/// <summary>
/// Summarises the current chart's price location, volatility, nearby levels, and bar state.
/// </summary>
public sealed class ChartContextSection
{
    private readonly double _fontSize;
    private readonly MarketHoursOverlayModule _marketHoursOverlay;
    private readonly MarketPulseLevelsOverlayModule _marketPulseLevels;
    private readonly Dictionary<string, TextBlock> _valueCells = new();
    private DateTime _nextRefreshAt = DateTime.MinValue;

    public ChartContextSection(double fontSize, MarketHoursOverlayModule marketHoursOverlay, MarketPulseLevelsOverlayModule marketPulseLevels)
    {
        _fontSize = fontSize;
        _marketHoursOverlay = marketHoursOverlay ?? throw new ArgumentNullException(nameof(marketHoursOverlay));
        _marketPulseLevels = marketPulseLevels ?? throw new ArgumentNullException(nameof(marketPulseLevels));
    }

    public ControlBase CreateControl(RobotContext context)
    {
        var section = new StackPanel { Orientation = Orientation.Vertical };
        section.AddChild(new TextBlock
        {
            Text = "Chart Context",
            ForegroundColor = Color.FromHex("#FF6FE3FF"),
            FontWeight = FontWeight.ExtraBold,
            FontSize = _fontSize + 1,
            HorizontalAlignment = HorizontalAlignment.Center
        });

        var grid = new Grid(10, 2) { Margin = new Thickness(0, 4, 0, 0) };
        AddRow(grid, "Symbol / TF", 0);
        AddRow(grid, "Price / Day", 1);
        AddRow(grid, "Today H / L", 2);
        AddRow(grid, "To High / Low", 3);
        AddRow(grid, "Range / ATR", 4);
        AddRow(grid, "Session H / L", 5);
        AddRow(grid, "Session Range", 6);
        AddRow(grid, "Levels ↓ / ↑", 7);
        AddRow(grid, "Strong S / R", 8);
        AddRow(grid, "Trend / Close", 9);
        section.AddChild(grid);
        Refresh(context);
        return section;
    }

    public void RefreshIfDue(RobotContext context)
    {
        if (context.ServerTime >= _nextRefreshAt)
        {
            Refresh(context);
        }
    }

    public void Refresh(RobotContext context)
    {
        var chart = context.GetChartContextSnapshot();
        var market = context.GetMarketSnapshot();
        if (!chart.IsAvailable || market.PipSize <= 0)
        {
            SetAllUnavailable();
            _nextRefreshAt = context.ServerTime.AddSeconds(1);
            return;
        }

        var decimals = GetPriceDecimals(market.PipSize);
        var priceFormat = "N" + decimals;
        var dailyChange = market.Bid - chart.DailyOpen;
        var dailyChangePercent = chart.DailyOpen == 0 ? 0 : dailyChange / chart.DailyOpen * 100;
        var dailyRange = chart.DailyHigh - chart.DailyLow;
        var rangePips = dailyRange / market.PipSize;
        var atrMultiple = market.AtrPips <= 0 ? 0 : rangePips / market.AtrPips;

        _valueCells["Symbol / TF"].Text = $"{chart.SymbolName} · {chart.TimeFrameName}";
        SetSignedValue("Price / Day", $"{market.Bid.ToString(priceFormat)}  {dailyChange:+0.##;-0.##;0} ({dailyChangePercent:+0.00;-0.00;0.00}%)", dailyChange);
        _valueCells["Today H / L"].Text = $"{chart.DailyHigh.ToString(priceFormat)} / {chart.DailyLow.ToString(priceFormat)}";
        _valueCells["To High / Low"].Text = $"{FormatDistance(chart.DailyHigh - market.Bid, market.PipSize)} / {FormatDistance(market.Bid - chart.DailyLow, market.PipSize)}";
        _valueCells["Range / ATR"].Text = $"{rangePips:N0} pips · {atrMultiple:N2}×";

        var session = _marketHoursOverlay.GetCurrentSession(context);
        _valueCells["Session H / L"].Text = session is null
            ? "-"
            : $"{session.Value.Name}  {session.Value.High.ToString(priceFormat)} / {session.Value.Low.ToString(priceFormat)}";
        _valueCells["Session Range"].Text = session is null
            ? "-"
            : FormatDistance(session.Value.High - session.Value.Low, market.PipSize);

        var psychologicalLevels = _marketHoursOverlay.GetNearestPsychologicalLevels(market.Bid);
        _valueCells["Levels ↓ / ↑"].Text = psychologicalLevels is null
            ? "-"
            : $"{psychologicalLevels.Value.Below.ToString(priceFormat)} / {psychologicalLevels.Value.Above.ToString(priceFormat)}";
        var strongLevels = _marketPulseLevels.GetNearestLevels(market.Bid);
        _valueCells["Strong S / R"].Text = $"{FormatOptionalPrice(strongLevels.Support, priceFormat)} / {FormatOptionalPrice(strongLevels.Resistance, priceFormat)}";

        var (trendLabel, trendColor) = GetTrend(market);
        var closeIn = GetCandleCountdown(chart, context.ServerTime);
        _valueCells["Trend / Close"].Text = $"{trendLabel} · {closeIn}";
        _valueCells["Trend / Close"].ForegroundColor = trendColor;
        _nextRefreshAt = context.ServerTime.AddMilliseconds(250);
    }

    private void AddRow(Grid grid, string label, int row)
    {
        grid.AddChild(new TextBlock
        {
            Text = label,
            ForegroundColor = Color.FromHex("#FFBDEBFF"),
            FontSize = _fontSize,
            Margin = new Thickness(2)
        }, row, 0);

        var value = new TextBlock
        {
            Text = "-",
            ForegroundColor = Color.White,
            FontSize = _fontSize,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(2)
        };
        grid.AddChild(value, row, 1);
        _valueCells[label] = value;
    }

    private void SetAllUnavailable()
    {
        foreach (var value in _valueCells.Values)
        {
            value.Text = "-";
            value.ForegroundColor = Color.White;
        }
    }

    private void SetSignedValue(string key, string text, double value)
    {
        _valueCells[key].Text = text;
        _valueCells[key].ForegroundColor = value switch
        {
            > 0 => Color.FromHex("#FF65F2A5"),
            < 0 => Color.FromHex("#FFFF8B98"),
            _ => Color.FromHex("#FFBDEBFF")
        };
    }

    private static string FormatDistance(double distance, double pipSize) => $"{distance / pipSize:N0} pips";

    private static string FormatOptionalPrice(double? price, string format) => price?.ToString(format) ?? "-";

    private static int GetPriceDecimals(double pipSize)
    {
        var decimals = pipSize >= 1 ? 0 : (int)Math.Ceiling(-Math.Log10(pipSize));
        return Math.Clamp(decimals, 0, 5);
    }

    private static (string Label, Color Color) GetTrend(MarketSnapshot market)
    {
        if (market.FastEma > market.SlowEma && market.Bid >= market.FastEma)
        {
            return ($"Bullish ADX {market.Adx:N0}", Color.FromHex("#FF65F2A5"));
        }

        if (market.FastEma < market.SlowEma && market.Bid <= market.FastEma)
        {
            return ($"Bearish ADX {market.Adx:N0}", Color.FromHex("#FFFF8B98"));
        }

        return ($"Neutral ADX {market.Adx:N0}", Color.FromHex("#FFBDEBFF"));
    }

    private static string GetCandleCountdown(ChartContextSnapshot chart, DateTime now)
    {
        if (chart.CurrentBarDuration <= TimeSpan.Zero)
        {
            return "-";
        }

        var remaining = chart.CurrentBarOpenTime.Add(chart.CurrentBarDuration) - now;
        return remaining <= TimeSpan.Zero ? "Closing" : $"{remaining:mm\\:ss}";
    }
}
