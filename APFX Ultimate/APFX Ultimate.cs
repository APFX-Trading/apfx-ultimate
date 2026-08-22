namespace cAlgo.Robots;

[Robot(AccessRights = AccessRights.None, AddIndicators = false)]
public partial class APFXUltimate : Robot
{
    private RobotModuleRunner? _modules;

    [Parameter("Position", DefaultValue = PanelPosition.TopRight, Group = "Chart Panel")]
    public PanelPosition ChartPanelPosition { get; set; }

    [Parameter("Font Size", DefaultValue = 13.0, MinValue = 10, MaxValue = 24, Step = 1, Group = "Chart Panel")]
    public double ChartPanelFontSize { get; set; }

    [Parameter("Volume (Lots)", DefaultValue = 1.0, MinValue = 0.01, MaxValue = 1000, Step = 0.01, Group = "Trading")]
    public double TradingVolumeLots { get; set; }

    [Parameter("Enable", DefaultValue = false, Group = "Grid")]
    public bool GridEnabled { get; set; }
    [Parameter("Direction", DefaultValue = GridDirection.Both, Group = "Grid")]
    public GridDirection GridDirection { get; set; }
    [Parameter("Additional Entries", DefaultValue = 3, MinValue = 0, MaxValue = 20, Group = "Grid")]
    public int GridAdditionalEntries { get; set; }
    [Parameter("Target Profit", DefaultValue = 10.0, MinValue = 0.01, Group = "Grid")]
    public double GridTargetProfit { get; set; }
    [Parameter("Trailing Lock (%)", DefaultValue = 80.0, MinValue = 1, MaxValue = 100, Group = "Grid")]
    public double GridTrailingLockPercent { get; set; }
    [Parameter("Distance", DefaultValue = 15.0, MinValue = 0.01, Group = "Grid")]
    public double GridDistanceAmount { get; set; }
    [Parameter("Use ATR Scaling", DefaultValue = false, Group = "Grid")]
    public bool GridUseAtr { get; set; }
    [Parameter("ATR Period", DefaultValue = 14, MinValue = 1, Group = "Grid")]
    public int GridAtrPeriod { get; set; }
    [Parameter("Max Drawdown (%)", DefaultValue = 10.0, MinValue = 0.1, MaxValue = 100, Group = "Grid")]
    public double GridMaxDrawdownPercent { get; set; }

    [Parameter("API Key", DefaultValue = "", Group = "Calendar News")]
    public string CalendarApiKey { get; set; } = string.Empty;

    [Parameter("Hours Ahead", DefaultValue = 168, MinValue = 1, MaxValue = 720, Group = "Calendar News")]
    public int CalendarHoursAhead { get; set; }

    [Parameter("Refresh (Minutes)", DefaultValue = 5, MinValue = 1, MaxValue = 60, Group = "Calendar News")]
    public int CalendarRefreshMinutes { get; set; }

    protected override void OnStart()
    {
        var gridAtr = Indicators.AverageTrueRange(GridAtrPeriod, MovingAverageType.Simple);
        var rsiM1 = Indicators.RelativeStrengthIndex(MarketData.GetBars(TimeFrame.Minute, SymbolName).ClosePrices, 14);
        var rsiM5 = Indicators.RelativeStrengthIndex(MarketData.GetBars(TimeFrame.Minute5, SymbolName).ClosePrices, 14);
        var rsiM15 = Indicators.RelativeStrengthIndex(MarketData.GetBars(TimeFrame.Minute15, SymbolName).ClosePrices, 14);
        var rsiH1 = Indicators.RelativeStrengthIndex(MarketData.GetBars(TimeFrame.Hour, SymbolName).ClosePrices, 14);
        var rsiH4 = Indicators.RelativeStrengthIndex(MarketData.GetBars(TimeFrame.Hour4, SymbolName).ClosePrices, 14);
        var directionalMovement = Indicators.DirectionalMovementSystem(14);
        var gridStatus = new GridStatus { MaxEntries = GridAdditionalEntries };
        var calendarNewsState = new CalendarNewsState();
        var context = new RobotContext(
            log: message => Print(message),
            getServerTime: () => Server.Time,
            addChartControl: control => Chart.AddControl(control),
            removeChartControl: control => Chart.RemoveControl(control),
            openMarketOrder: tradeType => ExecuteMarketOrder(
                tradeType,
                SymbolName,
                GetTradeVolumeInUnits(),
                "APFXUltimate.Panel"),
            closeChartPositions: () => Positions
                .Where(position => string.Equals(position.SymbolName, SymbolName, StringComparison.Ordinal))
                .ToArray()
                .Select(position => ClosePosition(position))
                .ToArray(),
            cancelChartPendingOrders: () => PendingOrders
                .Where(order => string.Equals(order.SymbolName, SymbolName, StringComparison.Ordinal))
                .ToArray()
                .Select(order => CancelPendingOrder(order))
                .ToArray(),
            getHistoricalTrades: () => History.ToArray(),
            getHistoricalTradeCount: () => History.Count,
            getOpenPositionSnapshot: () =>
            {
                var positions = Positions.ToArray();
                return new OpenPositionSnapshot(
                    positions.Length,
                    positions.Sum(position => position.NetProfit),
                    Account.Equity,
                    Account.Margin,
                    Account.FreeMargin);
            },
            getTradeVolumeInfo: () =>
            {
                var volumeInUnits = GetTradeVolumeInUnits();
                return new TradeVolumeInfo(
                    Symbol.VolumeInUnitsToQuantity(volumeInUnits),
                    volumeInUnits,
                    Symbol.GetEstimatedMargin(TradeType.Buy, volumeInUnits),
                    Symbol.GetEstimatedMargin(TradeType.Sell, volumeInUnits));
            },
            getChartPositions: direction => Positions.Where(p => p.SymbolName == SymbolName && p.TradeType == direction).ToArray(),
            getGridPendingOrders: labelPrefix => PendingOrders.Where(o => o.SymbolName == SymbolName && o.Label?.StartsWith(labelPrefix, StringComparison.Ordinal) == true).ToArray(),
            placeGridLimit: (direction, price, label) => PlaceLimitOrder(direction, SymbolName, GetTradeVolumeInUnits(), price, label),
            closePosition: position => ClosePosition(position),
            cancelPendingOrder: order => CancelPendingOrder(order),
            getMarketSnapshot: () => new MarketSnapshot(SymbolName, Symbol.Bid, Symbol.Ask, Symbol.PipSize, Symbol.PipValue, Account.Equity, Account.FreeMargin, gridAtr.Result.LastValue / Symbol.PipSize, directionalMovement.ADX.LastValue, directionalMovement.DIPlus.LastValue, directionalMovement.DIMinus.LastValue, rsiM1.Result.LastValue, rsiM5.Result.LastValue, rsiM15.Result.LastValue, rsiH1.Result.LastValue, rsiH4.Result.LastValue),
            drawHorizontalLine: (name, price, color) => Chart.DrawHorizontalLine(name, price, color, 2, LineStyle.Dots),
            removeChartObject: name => Chart.RemoveObject(name),
            sendHttpRequest: (request, callback) => Http.SendAsync(request, callback),
            beginInvokeOnMainThread: action => BeginInvokeOnMainThread(action));

        _modules = new RobotModuleRunner(new IRobotModule[]
        {
            new GridTradingModule(new GridConfiguration(GridEnabled, GridDirection, GridAdditionalEntries, GridTargetProfit, GridTrailingLockPercent, GridDistanceAmount, GridUseAtr, GridAtrPeriod, GridMaxDrawdownPercent), gridStatus),
            new CalendarNewsModule(new CalendarNewsConfiguration(CalendarApiKey, CalendarHoursAhead, CalendarRefreshMinutes), calendarNewsState),
            new ChartPanelModule(ChartPanelPosition, ChartPanelFontSize, gridStatus, GridAtrPeriod, GridUseAtr, calendarNewsState)
        });

        _modules.Start(context);
        Timer.Start(TimeSpan.FromSeconds(1));
    }

    protected override void OnTick()
    {
        _modules?.Tick();
    }

    protected override void OnTimer()
    {
        _modules?.Timer();
    }

    protected override void OnStop()
    {
        Timer.Stop();
        _modules?.Stop();
    }

    private double GetTradeVolumeInUnits()
    {
        var requestedVolumeInUnits = Symbol.QuantityToVolumeInUnits(TradingVolumeLots);
        return Symbol.NormalizeVolumeInUnits(requestedVolumeInUnits, RoundingMode.Down);
    }
}
