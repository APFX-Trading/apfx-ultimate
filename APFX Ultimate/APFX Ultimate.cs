namespace cAlgo.Robots;

[Robot(AccessRights = AccessRights.None, AddIndicators = false)]
public partial class APFXUltimate : Robot
{
    private RobotModuleRunner? _modules;
    private ChartPanelModule? _chartPanelModule;
    private Action<UserTimeOffsetChangedEventArgs>? _userTimeOffsetChangedHandler;

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

    [Parameter("Enable", DefaultValue = false, Group = "Telegram Reporting")]
    public bool TelegramReportingEnabled { get; set; }

    [Parameter("Bot Token", DefaultValue = "", Group = "Telegram Reporting")]
    public string TelegramBotToken { get; set; } = string.Empty;

    [Parameter("Chat ID", DefaultValue = "", Group = "Telegram Reporting")]
    public string TelegramChatId { get; set; } = string.Empty;

    [Parameter("UTC Offset Minutes", DefaultValue = 0, MinValue = -720, MaxValue = 840, Group = "Telegram Reporting")]
    public int TelegramUtcOffsetMinutes { get; set; }

    [Parameter("Enable Daily", DefaultValue = true, Group = "Telegram Daily")]
    public bool TelegramEnableDaily { get; set; }

    [Parameter("Hour", DefaultValue = 0, MinValue = 0, MaxValue = 23, Group = "Telegram Daily")]
    public int TelegramDailyHour { get; set; }

    [Parameter("Minute", DefaultValue = 5, MinValue = 0, MaxValue = 59, Group = "Telegram Daily")]
    public int TelegramDailyMinute { get; set; }

    [Parameter("Enable Weekly", DefaultValue = true, Group = "Telegram Weekly")]
    public bool TelegramEnableWeekly { get; set; }

    [Parameter("Day", DefaultValue = DayOfWeek.Monday, Group = "Telegram Weekly")]
    public DayOfWeek TelegramWeeklyDay { get; set; }

    [Parameter("Hour", DefaultValue = 0, MinValue = 0, MaxValue = 23, Group = "Telegram Weekly")]
    public int TelegramWeeklyHour { get; set; }

    [Parameter("Minute", DefaultValue = 10, MinValue = 0, MaxValue = 59, Group = "Telegram Weekly")]
    public int TelegramWeeklyMinute { get; set; }

    [Parameter("Enable Monthly", DefaultValue = true, Group = "Telegram Monthly")]
    public bool TelegramEnableMonthly { get; set; }

    [Parameter("Day", DefaultValue = 1, MinValue = 1, MaxValue = 28, Group = "Telegram Monthly")]
    public int TelegramMonthlyDay { get; set; }

    [Parameter("Hour", DefaultValue = 0, MinValue = 0, MaxValue = 23, Group = "Telegram Monthly")]
    public int TelegramMonthlyHour { get; set; }

    [Parameter("Minute", DefaultValue = 15, MinValue = 0, MaxValue = 59, Group = "Telegram Monthly")]
    public int TelegramMonthlyMinute { get; set; }

    [Parameter("Alert Closed Trades", DefaultValue = false, Group = "Telegram Alerts")]
    public bool TelegramAlertClosedTrades { get; set; }

    [Parameter("Symbol Filter", DefaultValue = "", Group = "Telegram Filters")]
    public string TelegramSymbolFilter { get; set; } = string.Empty;

    [Parameter("Label Filter", DefaultValue = "", Group = "Telegram Filters")]
    public string TelegramLabelFilter { get; set; } = string.Empty;

    [Parameter("Report Title", DefaultValue = "APFX Trading Statistics", Group = "Telegram Reporting")]
    public string TelegramReportTitle { get; set; } = "APFX Trading Statistics";

    [Parameter("Show Asia", DefaultValue = true, Group = "Graph")]
    public bool ShowAsiaSession { get; set; }

    [Parameter("Show Europe", DefaultValue = true, Group = "Graph")]
    public bool ShowEuropeSession { get; set; }

    [Parameter("Show US", DefaultValue = true, Group = "Graph")]
    public bool ShowUsSession { get; set; }

    [Parameter("Show Price Levels", DefaultValue = true, Group = "Graph")]
    public bool ShowPsychologicalLevels { get; set; }

    [Parameter("EMA Fast", DefaultValue = 20, MinValue = 1, Group = "Graph")]
    public int EmaFastPeriod { get; set; }

    [Parameter("EMA Slow", DefaultValue = 50, MinValue = 2, Group = "Graph")]
    public int EmaSlowPeriod { get; set; }

    [Parameter("Show EMAs", DefaultValue = false, Group = "Graph")]
    public bool ShowEma { get; set; }

    [Parameter("Bollinger Period", DefaultValue = 20, MinValue = 1, Group = "Graph")]
    public int BollingerPeriod { get; set; }

    [Parameter("Bollinger Std Dev", DefaultValue = 2.0, MinValue = 0.1, Step = 0.1, Group = "Graph")]
    public double BollingerStandardDeviations { get; set; }

    [Parameter("Show Bollinger Bands", DefaultValue = false, Group = "Graph")]
    public bool ShowBollingerBands { get; set; }

    protected override void OnStart()
    {
        var gridAtr = Indicators.AverageTrueRange(GridAtrPeriod, MovingAverageType.Simple);
        var fastEma = Indicators.MovingAverage(Bars.ClosePrices, EmaFastPeriod, MovingAverageType.Exponential);
        var slowEma = Indicators.MovingAverage(Bars.ClosePrices, EmaSlowPeriod, MovingAverageType.Exponential);
        var bollingerBands = Indicators.BollingerBands(Bars.ClosePrices, BollingerPeriod, BollingerStandardDeviations, MovingAverageType.Simple);
        var rsiM1 = Indicators.RelativeStrengthIndex(MarketData.GetBars(TimeFrame.Minute, SymbolName).ClosePrices, 14);
        var rsiM5 = Indicators.RelativeStrengthIndex(MarketData.GetBars(TimeFrame.Minute5, SymbolName).ClosePrices, 14);
        var rsiM15 = Indicators.RelativeStrengthIndex(MarketData.GetBars(TimeFrame.Minute15, SymbolName).ClosePrices, 14);
        var rsiH1 = Indicators.RelativeStrengthIndex(MarketData.GetBars(TimeFrame.Hour, SymbolName).ClosePrices, 14);
        var rsiH4 = Indicators.RelativeStrengthIndex(MarketData.GetBars(TimeFrame.Hour4, SymbolName).ClosePrices, 14);
        var directionalMovement = Indicators.DirectionalMovementSystem(14);
        var gridStatus = new GridStatus();
        var calendarNewsState = new CalendarNewsState();
        var marketHoursOverlay = new MarketHoursOverlayModule(ShowAsiaSession, ShowEuropeSession, ShowUsSession, ShowPsychologicalLevels, SymbolName);
        var emaOverlay = new EmaOverlayModule(EmaFastPeriod, EmaSlowPeriod, ShowEma);
        var bollingerBandsOverlay = new BollingerBandsOverlayModule(BollingerPeriod, BollingerStandardDeviations, ShowBollingerBands);
        _userTimeOffsetChangedHandler = args => calendarNewsState.RefreshDisplayedTimes(args.UserTimeOffset);
        Application.UserTimeOffsetChanged += _userTimeOffsetChangedHandler;
        var context = new RobotContext(
            log: message => Print(message),
            getServerTime: () => Server.Time,
            getServerTimeUtc: () => Server.TimeInUtc,
            convertUtcToChartTime: utcTime =>
                utcTime.Add(Application.UserTimeOffset),
            getEmaPoints: line =>
            {
                var values = line == EmaLine.Fast ? fastEma.Result : slowEma.Result;
                var lastIndex = Math.Max(0, Bars.Count - 1);
                var firstVisibleIndex = Math.Clamp(Chart.FirstVisibleBarIndex, 0, lastIndex);
                var lastVisibleIndex = Math.Clamp(Chart.LastVisibleBarIndex, firstVisibleIndex, lastIndex);
                var step = Math.Max(1, (lastVisibleIndex - firstVisibleIndex) / 240);
                var points = new List<ChartTimePricePoint>();
                for (var index = firstVisibleIndex; index <= lastVisibleIndex; index += step)
                {
                    points.Add(new ChartTimePricePoint(Bars.OpenTimes[index], values[index]));
                }

                if (points.Count == 0 || points[^1].Time != Bars.OpenTimes[lastVisibleIndex])
                {
                    points.Add(new ChartTimePricePoint(Bars.OpenTimes[lastVisibleIndex], values[lastVisibleIndex]));
                }

                return points;
            },
            getBollingerBandPoints: line =>
            {
                var values = line switch
                {
                    BollingerBandLine.Top => bollingerBands.Top,
                    BollingerBandLine.Main => bollingerBands.Main,
                    _ => bollingerBands.Bottom
                };
                var lastIndex = Math.Max(0, Bars.Count - 1);
                var firstVisibleIndex = Math.Clamp(Chart.FirstVisibleBarIndex, 0, lastIndex);
                var lastVisibleIndex = Math.Clamp(Chart.LastVisibleBarIndex, firstVisibleIndex, lastIndex);
                var step = Math.Max(1, (lastVisibleIndex - firstVisibleIndex) / 240);
                var points = new List<ChartTimePricePoint>();
                for (var index = firstVisibleIndex; index <= lastVisibleIndex; index += step)
                {
                    points.Add(new ChartTimePricePoint(Bars.OpenTimes[index], values[index]));
                }
                if (points.Count == 0 || points[^1].Time != Bars.OpenTimes[lastVisibleIndex])
                {
                    points.Add(new ChartTimePricePoint(Bars.OpenTimes[lastVisibleIndex], values[lastVisibleIndex]));
                }
                return points;
            },
            drawChartTrendLine: (name, startTime, startPrice, endTime, endPrice, color, thickness) => Chart.DrawTrendLine(name, startTime, startPrice, endTime, endPrice, color, thickness, LineStyle.Solid),
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
            getAccountEquity: () => Account.Equity,
            getAccountSummary: () => new AccountSummary(Account.Balance, Account.Equity, Account.Margin, Account.FreeMargin, Account.Asset.Name, Account.BrokerName, Account.Number, Account.IsLive),
            getLocalValue: key => LocalStorage.GetString(key),
            setLocalValue: (key, value) => LocalStorage.SetString(key, value),
            flushLocalValues: () => LocalStorage.Flush(LocalStorageScope.Instance),
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
            drawGraphHorizontalLine: (name, price, color, thickness, lineStyle) => Chart.DrawHorizontalLine(name, price, color, thickness, lineStyle),
            drawGraphVerticalLine: (name, time, color, thickness, lineStyle) => Chart.DrawVerticalLine(name, time, color, thickness, lineStyle),
            drawChartText: (name, text, time, price, color) =>
            {
                var chartText = Chart.DrawText(name, text, time, price, color);
                chartText.FontSize = 10;
                chartText.IsBold = true;
                chartText.HorizontalAlignment = HorizontalAlignment.Center;
                chartText.VerticalAlignment = VerticalAlignment.Top;
            },
            getChartViewport: () =>
            {
                var lastIndex = Math.Max(0, Bars.Count - 1);
                var firstVisibleIndex = Math.Clamp(Chart.FirstVisibleBarIndex, 0, lastIndex);
                var lastVisibleIndex = Math.Clamp(Chart.LastVisibleBarIndex, firstVisibleIndex, lastIndex);
                return new ChartViewport(Bars.OpenTimes[firstVisibleIndex], Bars.OpenTimes[lastVisibleIndex], Chart.BottomY, Chart.TopY);
            },
            getChartPriceRange: (start, end) =>
            {
                var firstIndex = Bars.OpenTimes.GetIndexByTime(start);
                while (firstIndex >= 0 && firstIndex < Bars.Count && Bars.OpenTimes[firstIndex] < start)
                {
                    firstIndex++;
                }

                if (firstIndex < 0 || firstIndex >= Bars.Count)
                {
                    return null;
                }

                var lastIndex = Bars.OpenTimes.GetIndexByTime(end.AddTicks(-1));
                while (lastIndex >= 0 && Bars.OpenTimes[lastIndex] >= end)
                {
                    lastIndex--;
                }

                if (lastIndex < firstIndex)
                {
                    return null;
                }

                var high = double.MinValue;
                var low = double.MaxValue;
                for (var index = firstIndex; index <= lastIndex; index++)
                {
                    high = Math.Max(high, Bars.HighPrices[index]);
                    low = Math.Min(low, Bars.LowPrices[index]);
                }

                return new ChartPriceRange(low, high);
            },
            drawChartRectangle: (name, start, end, bottomPrice, topPrice, color) =>
            {
                var rectangle = Chart.DrawRectangle(name, start, bottomPrice, end, topPrice, color);
                rectangle.IsFilled = true;
                rectangle.IsInteractive = false;
            },
            removeChartObject: name => Chart.RemoveObject(name),
            sendHttpRequest: (request, callback) => Http.SendAsync(request, callback),
            beginInvokeOnMainThread: action => BeginInvokeOnMainThread(action));

        Positions.Closed += OnPositionClosed;
        _modules = new RobotModuleRunner(new IRobotModule[]
        {
            new GridTradingModule(new GridConfiguration(GridEnabled, GridDirection, GridTargetProfit, GridTrailingLockPercent, GridDistanceAmount, GridUseAtr, GridAtrPeriod, GridMaxDrawdownPercent), gridStatus),
            new TelegramReportingModule(new TelegramReportingConfiguration(TelegramReportingEnabled, TelegramBotToken, TelegramChatId, TelegramUtcOffsetMinutes, TelegramEnableDaily, TelegramDailyHour, TelegramDailyMinute, TelegramEnableWeekly, TelegramWeeklyDay, TelegramWeeklyHour, TelegramWeeklyMinute, TelegramEnableMonthly, TelegramMonthlyDay, TelegramMonthlyHour, TelegramMonthlyMinute, TelegramAlertClosedTrades, TelegramSymbolFilter, TelegramLabelFilter, TelegramReportTitle)),
            new CalendarNewsModule(new CalendarNewsConfiguration(CalendarApiKey, CalendarHoursAhead, CalendarRefreshMinutes), calendarNewsState),
            new CalendarNewsChartOverlayModule(calendarNewsState),
            marketHoursOverlay,
            emaOverlay,
            bollingerBandsOverlay,
            _chartPanelModule = new ChartPanelModule(ChartPanelPosition, ChartPanelFontSize, gridStatus, GridAtrPeriod, GridUseAtr, calendarNewsState, marketHoursOverlay, emaOverlay, bollingerBandsOverlay)
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
        Positions.Closed -= OnPositionClosed;
        _modules?.Stop();
        _chartPanelModule = null;
        if (_userTimeOffsetChangedHandler is not null)
        {
            Application.UserTimeOffsetChanged -= _userTimeOffsetChangedHandler;
            _userTimeOffsetChangedHandler = null;
        }
    }

    private double GetTradeVolumeInUnits()
    {
        var requestedVolumeInUnits = Symbol.QuantityToVolumeInUnits(TradingVolumeLots);
        return Symbol.NormalizeVolumeInUnits(requestedVolumeInUnits, RoundingMode.Down);
    }

    private void OnPositionClosed(PositionClosedEventArgs args)
    {
        BeginInvokeOnMainThread(() => _chartPanelModule?.RefreshPerformance());
    }
}
