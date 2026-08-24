namespace cAlgo.Robots;

/// <summary>
/// Shows the live account, position, and active basket status in one place.
/// </summary>
public sealed class OpenPositionStatisticsSection
{
    private readonly double _fontSize;
    private readonly GridStatus? _gridStatus;
    private readonly Dictionary<string, TextBlock> _valueCells = new();
    private DateTime _nextRefreshAt = DateTime.MinValue;

    public OpenPositionStatisticsSection(double fontSize, GridStatus? gridStatus)
    {
        _fontSize = fontSize;
        _gridStatus = gridStatus;
    }

    public ControlBase CreateControl(RobotContext context)
    {
        var section = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Margin = new Thickness(0)
        };
        section.AddChild(new TextBlock
        {
            Text = "Live Trading Status",
            ForegroundColor = Color.FromHex("#FF6FE3FF"),
            FontWeight = FontWeight.ExtraBold,
            FontSize = _fontSize + 1,
            HorizontalAlignment = HorizontalAlignment.Center
        });

        var rowCount = _gridStatus is null ? 7 : 13;
        var grid = new Grid(rowCount, 2)
        {
            Margin = new Thickness(0, 4, 0, 0)
        };
        AddRow(grid, "Volume (Lots)", 0);
        AddRow(grid, "Spread", 1);
        AddRow(grid, "Positions", 2);
        AddRow(grid, "Open P / L", 3);
        if (_gridStatus is not null)
        {
            AddRow(grid, "Target ($)", 4);
            AddRow(grid, "Trail Lock", 5);
            AddRow(grid, "Trail ($)", 6);
            AddRow(grid, "Grid Pending", 7);
            AddRow(grid, "Distance ($)", 8);
        }
        AddRow(grid, "Equity", _gridStatus is null ? 4 : 9);
        if (_gridStatus is not null)
        {
            AddRow(grid, "Drawdown Left", 10);
        }
        AddRow(grid, "Margin Used", _gridStatus is null ? 5 : 11);
        AddRow(grid, "Margin Free", _gridStatus is null ? 6 : 12);

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
        var snapshot = context.GetOpenPositionSnapshot();

        _valueCells["Volume (Lots)"].Text = context.GetTradeVolumeInfo().VolumeInLots.ToString("N2");
        var market = context.GetMarketSnapshot();
        _valueCells["Spread"].Text = $"{(market.Ask - market.Bid) / market.PipSize:N1} pips";
        _valueCells["Positions"].Text = snapshot.OpenPositionCount.ToString();
        SetProfitLoss("Open P / L", snapshot.UnrealizedNetProfit);
        _valueCells["Equity"].Text = FormatAmount(snapshot.Equity);
        _valueCells["Margin Used"].Text = FormatAmount(snapshot.UsedMargin);
        _valueCells["Margin Free"].Text = FormatAmount(snapshot.FreeMargin);

        if (_gridStatus is not null)
        {
            _valueCells["Target ($)"].Text = FormatAmount(_gridStatus.EffectiveTarget > 0 ? _gridStatus.EffectiveTarget : _gridStatus.BaseTarget);
            _valueCells["Grid Pending"].Text = _gridStatus.EntriesPlaced.ToString();
            _valueCells["Distance ($)"].Text = FormatAmount(_gridStatus.DistanceAmount);
            _valueCells["Trail Lock"].Text = _gridStatus.TrailingLockPercent.ToString("N0") + "%";
            var configuredTrailAmount = (_gridStatus.EffectiveTarget > 0 ? _gridStatus.EffectiveTarget : _gridStatus.BaseTarget)
                * _gridStatus.TrailingLockPercent / 100d;
            _valueCells["Trail ($)"].Text = FormatAmount(_gridStatus.TrailingFloor > 0 ? _gridStatus.TrailingFloor : configuredTrailAmount);
            _valueCells["Drawdown Left"].Text = $"{_gridStatus.DrawdownRemaining:N2} ({_gridStatus.MaxDrawdownPercent:N0}%)";
        }

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

    private void SetProfitLoss(string label, double amount)
    {
        var value = _valueCells[label];
        value.Text = amount >= 0 ? $"+{amount:N2}" : amount.ToString("N2");
        value.ForegroundColor = amount switch
        {
            > 0 => Color.FromHex("#FF65F2A5"),
            < 0 => Color.FromHex("#FFFF8B98"),
            _ => Color.FromHex("#FFBDEBFF")
        };
    }

    private static string FormatAmount(double amount) => amount.ToString("N2");
}
