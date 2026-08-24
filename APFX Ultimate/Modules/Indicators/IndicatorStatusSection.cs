namespace cAlgo.Robots;

/// <summary>
/// Displays the live values of the indicators calculated internally by the bot.
/// </summary>
public sealed class IndicatorStatusSection
{
    private readonly double _fontSize;
    private readonly int _atrPeriod;
    private TextBlock? _atrValue;
    private TextBlock? _adxValue;
    private TextBlock? _diPlusValue;
    private TextBlock? _diMinusValue;
    private readonly Dictionary<string, TextBlock> _rsiValues = new();
    private DateTime _nextRefreshAt = DateTime.MinValue;

    public IndicatorStatusSection(double fontSize, int atrPeriod)
    {
        _fontSize = fontSize;
        _atrPeriod = atrPeriod;
    }

    public ControlBase CreateControl(RobotContext context)
    {
        var section = new StackPanel { Orientation = Orientation.Vertical };
        section.AddChild(new TextBlock
        {
            Text = "Indicators",
            ForegroundColor = Color.FromHex("#FF6FE3FF"),
            FontWeight = FontWeight.ExtraBold,
            FontSize = _fontSize + 1,
            HorizontalAlignment = HorizontalAlignment.Center
        });

        var atrGrid = new Grid(1, 2) { Margin = new Thickness(0, 4, 0, 0) };
        AddLabel(atrGrid, $"ATR ({_atrPeriod})", 0);
        _atrValue = AddValue(atrGrid, 0);
        section.AddChild(atrGrid);
        var directionalMovementGrid = new Grid(2, 3) { Margin = new Thickness(0, 5, 0, 0) };
        AddDirectionalMovementColumn(directionalMovementGrid, "ADX (14)", 0, out _adxValue);
        AddDirectionalMovementColumn(directionalMovementGrid, "+DI", 1, out _diPlusValue);
        AddDirectionalMovementColumn(directionalMovementGrid, "-DI", 2, out _diMinusValue);
        section.AddChild(directionalMovementGrid);
        section.AddChild(new TextBlock
        {
            Text = "RSI (14)",
            ForegroundColor = Color.FromHex("#FFBDEBFF"),
            FontWeight = FontWeight.ExtraBold,
            FontSize = _fontSize,
            Margin = new Thickness(0, 5, 0, 1)
        });
        var rsiGrid = new Grid(2, 5);
        AddRsiColumn(rsiGrid, "M1", 0);
        AddRsiColumn(rsiGrid, "M5", 1);
        AddRsiColumn(rsiGrid, "M15", 2);
        AddRsiColumn(rsiGrid, "H1", 3);
        AddRsiColumn(rsiGrid, "H4", 4);
        section.AddChild(rsiGrid);

        Refresh(context);
        return section;
    }

    public void Refresh(RobotContext context)
    {
        if (_atrValue is null)
        {
            return;
        }

        var snapshot = context.GetMarketSnapshot();
        _atrValue.Text = $"{snapshot.AtrPips:N2} pips";
        SetDirectionalMovementValue(_adxValue!, snapshot.Adx, snapshot.Adx >= 25 ? Color.FromHex("#FF65F2A5") : Color.White);
        SetDirectionalMovementValue(_diPlusValue!, snapshot.DiPlus, Color.FromHex("#FF65F2A5"));
        SetDirectionalMovementValue(_diMinusValue!, snapshot.DiMinus, Color.FromHex("#FFFF8B98"));
        SetRsiValue("M1", snapshot.RsiM1);
        SetRsiValue("M5", snapshot.RsiM5);
        SetRsiValue("M15", snapshot.RsiM15);
        SetRsiValue("H1", snapshot.RsiH1);
        SetRsiValue("H4", snapshot.RsiH4);
        _nextRefreshAt = context.ServerTime.AddMilliseconds(250);
    }

    private void AddRsiColumn(Grid grid, string timeFrame, int column)
    {
        grid.AddChild(new TextBlock
        {
            Text = timeFrame,
            ForegroundColor = Color.FromHex("#FFBDEBFF"),
            FontWeight = FontWeight.ExtraBold,
            FontSize = Math.Max(9, _fontSize - 1),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(1)
        }, 0, column);
        var value = new TextBlock
        {
            Text = "-",
            ForegroundColor = Color.White,
            FontSize = Math.Max(9, _fontSize - 1),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(1)
        };
        grid.AddChild(value, 1, column);
        _rsiValues[timeFrame] = value;
    }

    private void AddDirectionalMovementColumn(Grid grid, string label, int column, out TextBlock value)
    {
        var alignment = column == 0 ? HorizontalAlignment.Left : HorizontalAlignment.Center;
        grid.AddChild(new TextBlock
        {
            Text = label,
            ForegroundColor = Color.FromHex("#FFBDEBFF"),
            FontWeight = FontWeight.ExtraBold,
            FontSize = Math.Max(9, _fontSize - 1),
            HorizontalAlignment = alignment,
            Margin = new Thickness(1)
        }, 0, column);
        value = new TextBlock
        {
            Text = "-",
            ForegroundColor = Color.White,
            FontSize = Math.Max(9, _fontSize - 1),
            HorizontalAlignment = alignment,
            Margin = new Thickness(1)
        };
        grid.AddChild(value, 1, column);
    }

    private static void SetDirectionalMovementValue(TextBlock value, double indicatorValue, Color color)
    {
        value.Text = indicatorValue.ToString("N2");
        value.ForegroundColor = color;
    }

    private void SetRsiValue(string label, double value)
    {
        var textBlock = _rsiValues[label];
        textBlock.Text = value.ToString("N2");
        textBlock.ForegroundColor = value switch
        {
            >= 70 => Color.FromHex("#FFFF8B98"),
            <= 30 => Color.FromHex("#FF65F2A5"),
            _ => Color.White
        };
    }

    public void RefreshIfDue(RobotContext context)
    {
        if (context.ServerTime >= _nextRefreshAt)
        {
            Refresh(context);
        }
    }

    private void AddLabel(Grid grid, string text, int row)
    {
        grid.AddChild(new TextBlock
        {
            Text = text,
            ForegroundColor = Color.FromHex("#FFBDEBFF"),
            FontSize = _fontSize,
            Margin = new Thickness(2)
        }, row, 0);
    }

    private TextBlock AddValue(Grid grid, int row)
    {
        var value = new TextBlock
        {
            Text = "-",
            ForegroundColor = Color.White,
            FontSize = _fontSize,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(2)
        };
        grid.AddChild(value, row, 1);
        return value;
    }
}
