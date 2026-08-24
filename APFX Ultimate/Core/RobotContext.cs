namespace cAlgo.Robots;

/// <summary>
/// Explicit services supplied to modules by the robot host.
/// Extend this with narrowly scoped operations as modules need them.
/// </summary>
public sealed class RobotContext
{
    private readonly Action<string> _log;
    private readonly Func<DateTime> _getServerTime;
    private readonly Func<DateTime> _getServerTimeUtc;
    private readonly Func<DateTime, DateTime> _convertUtcToChartTime;
    private readonly Func<EmaLine, IReadOnlyList<ChartTimePricePoint>> _getEmaPoints;
    private readonly Func<BollingerBandLine, IReadOnlyList<ChartTimePricePoint>> _getBollingerBandPoints;
    private readonly Action<string, DateTime, double, DateTime, double, Color, int> _drawChartTrendLine;
    private readonly Action<ControlBase> _addChartControl;
    private readonly Action<ControlBase> _removeChartControl;
    private readonly Func<TradeType, TradeResult> _openMarketOrder;
    private readonly Func<IReadOnlyList<TradeResult>> _closeChartPositions;
    private readonly Func<IReadOnlyList<TradeResult>> _cancelChartPendingOrders;
    private readonly Func<IReadOnlyList<HistoricalTrade>> _getHistoricalTrades;
    private readonly Func<int> _getHistoricalTradeCount;
    private readonly Func<double> _getAccountEquity;
    private readonly Func<AccountSummary> _getAccountSummary;
    private readonly Func<string, string?> _getLocalValue;
    private readonly Action<string, string> _setLocalValue;
    private readonly Action _flushLocalValues;
    private readonly Func<OpenPositionSnapshot> _getOpenPositionSnapshot;
    private readonly Func<TradeVolumeInfo> _getTradeVolumeInfo;
    private readonly Func<TradeType, IReadOnlyList<Position>> _getChartPositions;
    private readonly Func<string, IReadOnlyList<PendingOrder>> _getGridPendingOrders;
    private readonly Func<TradeType, double, string, TradeResult> _placeGridLimit;
    private readonly Func<Position, TradeResult> _closePosition;
    private readonly Func<PendingOrder, TradeResult> _cancelPendingOrder;
    private readonly Func<MarketSnapshot> _getMarketSnapshot;
    private readonly Action<string, double, Color> _drawHorizontalLine;
    private readonly Action<string, double, Color, int, LineStyle> _drawGraphHorizontalLine;
    private readonly Action<string, DateTime, Color, int, LineStyle> _drawGraphVerticalLine;
    private readonly Action<string, string, DateTime, double, Color> _drawChartText;
    private readonly Func<ChartViewport> _getChartViewport;
    private readonly Func<DateTime, DateTime, ChartPriceRange?> _getChartPriceRange;
    private readonly Action<string, DateTime, DateTime, double, double, Color> _drawChartRectangle;
    private readonly Action<string> _removeChartObject;
    private readonly Action<HttpRequest, Action<HttpResponse>> _sendHttpRequest;
    private readonly Action<Action> _beginInvokeOnMainThread;

    public RobotContext(
        Action<string> log,
        Func<DateTime> getServerTime,
        Func<DateTime> getServerTimeUtc,
        Func<DateTime, DateTime> convertUtcToChartTime,
        Func<EmaLine, IReadOnlyList<ChartTimePricePoint>> getEmaPoints,
        Func<BollingerBandLine, IReadOnlyList<ChartTimePricePoint>> getBollingerBandPoints,
        Action<string, DateTime, double, DateTime, double, Color, int> drawChartTrendLine,
        Action<ControlBase> addChartControl,
        Action<ControlBase> removeChartControl,
        Func<TradeType, TradeResult> openMarketOrder,
        Func<IReadOnlyList<TradeResult>> closeChartPositions,
        Func<IReadOnlyList<TradeResult>> cancelChartPendingOrders,
        Func<IReadOnlyList<HistoricalTrade>> getHistoricalTrades,
        Func<int> getHistoricalTradeCount,
        Func<double> getAccountEquity,
        Func<AccountSummary> getAccountSummary,
        Func<string, string?> getLocalValue,
        Action<string, string> setLocalValue,
        Action flushLocalValues,
        Func<OpenPositionSnapshot> getOpenPositionSnapshot,
        Func<TradeVolumeInfo> getTradeVolumeInfo,
        Func<TradeType, IReadOnlyList<Position>> getChartPositions,
        Func<string, IReadOnlyList<PendingOrder>> getGridPendingOrders,
        Func<TradeType, double, string, TradeResult> placeGridLimit,
        Func<Position, TradeResult> closePosition,
        Func<PendingOrder, TradeResult> cancelPendingOrder,
        Func<MarketSnapshot> getMarketSnapshot,
        Action<string, double, Color> drawHorizontalLine,
        Action<string, double, Color, int, LineStyle> drawGraphHorizontalLine,
        Action<string, DateTime, Color, int, LineStyle> drawGraphVerticalLine,
        Action<string, string, DateTime, double, Color> drawChartText,
        Func<ChartViewport> getChartViewport,
        Func<DateTime, DateTime, ChartPriceRange?> getChartPriceRange,
        Action<string, DateTime, DateTime, double, double, Color> drawChartRectangle,
        Action<string> removeChartObject,
        Action<HttpRequest, Action<HttpResponse>> sendHttpRequest,
        Action<Action> beginInvokeOnMainThread)
    {
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _getServerTime = getServerTime ?? throw new ArgumentNullException(nameof(getServerTime));
        _getServerTimeUtc = getServerTimeUtc ?? throw new ArgumentNullException(nameof(getServerTimeUtc));
        _convertUtcToChartTime = convertUtcToChartTime ?? throw new ArgumentNullException(nameof(convertUtcToChartTime));
        _getEmaPoints = getEmaPoints ?? throw new ArgumentNullException(nameof(getEmaPoints));
        _getBollingerBandPoints = getBollingerBandPoints ?? throw new ArgumentNullException(nameof(getBollingerBandPoints));
        _drawChartTrendLine = drawChartTrendLine ?? throw new ArgumentNullException(nameof(drawChartTrendLine));
        _addChartControl = addChartControl ?? throw new ArgumentNullException(nameof(addChartControl));
        _removeChartControl = removeChartControl ?? throw new ArgumentNullException(nameof(removeChartControl));
        _openMarketOrder = openMarketOrder ?? throw new ArgumentNullException(nameof(openMarketOrder));
        _closeChartPositions = closeChartPositions ?? throw new ArgumentNullException(nameof(closeChartPositions));
        _cancelChartPendingOrders = cancelChartPendingOrders ?? throw new ArgumentNullException(nameof(cancelChartPendingOrders));
        _getHistoricalTrades = getHistoricalTrades ?? throw new ArgumentNullException(nameof(getHistoricalTrades));
        _getHistoricalTradeCount = getHistoricalTradeCount ?? throw new ArgumentNullException(nameof(getHistoricalTradeCount));
        _getAccountEquity = getAccountEquity ?? throw new ArgumentNullException(nameof(getAccountEquity));
        _getAccountSummary = getAccountSummary ?? throw new ArgumentNullException(nameof(getAccountSummary));
        _getLocalValue = getLocalValue ?? throw new ArgumentNullException(nameof(getLocalValue));
        _setLocalValue = setLocalValue ?? throw new ArgumentNullException(nameof(setLocalValue));
        _flushLocalValues = flushLocalValues ?? throw new ArgumentNullException(nameof(flushLocalValues));
        _getOpenPositionSnapshot = getOpenPositionSnapshot ?? throw new ArgumentNullException(nameof(getOpenPositionSnapshot));
        _getTradeVolumeInfo = getTradeVolumeInfo ?? throw new ArgumentNullException(nameof(getTradeVolumeInfo));
        _getChartPositions = getChartPositions ?? throw new ArgumentNullException(nameof(getChartPositions));
        _getGridPendingOrders = getGridPendingOrders ?? throw new ArgumentNullException(nameof(getGridPendingOrders));
        _placeGridLimit = placeGridLimit ?? throw new ArgumentNullException(nameof(placeGridLimit));
        _closePosition = closePosition ?? throw new ArgumentNullException(nameof(closePosition));
        _cancelPendingOrder = cancelPendingOrder ?? throw new ArgumentNullException(nameof(cancelPendingOrder));
        _getMarketSnapshot = getMarketSnapshot ?? throw new ArgumentNullException(nameof(getMarketSnapshot));
        _drawHorizontalLine = drawHorizontalLine ?? throw new ArgumentNullException(nameof(drawHorizontalLine));
        _drawGraphHorizontalLine = drawGraphHorizontalLine ?? throw new ArgumentNullException(nameof(drawGraphHorizontalLine));
        _drawGraphVerticalLine = drawGraphVerticalLine ?? throw new ArgumentNullException(nameof(drawGraphVerticalLine));
        _drawChartText = drawChartText ?? throw new ArgumentNullException(nameof(drawChartText));
        _getChartViewport = getChartViewport ?? throw new ArgumentNullException(nameof(getChartViewport));
        _getChartPriceRange = getChartPriceRange ?? throw new ArgumentNullException(nameof(getChartPriceRange));
        _drawChartRectangle = drawChartRectangle ?? throw new ArgumentNullException(nameof(drawChartRectangle));
        _removeChartObject = removeChartObject ?? throw new ArgumentNullException(nameof(removeChartObject));
        _sendHttpRequest = sendHttpRequest ?? throw new ArgumentNullException(nameof(sendHttpRequest));
        _beginInvokeOnMainThread = beginInvokeOnMainThread ?? throw new ArgumentNullException(nameof(beginInvokeOnMainThread));
    }

    public DateTime ServerTime => _getServerTime();
    public DateTime ServerTimeUtc => _getServerTimeUtc();
    public DateTime ConvertUtcToChartTime(DateTime utcTime) => _convertUtcToChartTime(utcTime);
    public IReadOnlyList<ChartTimePricePoint> GetEmaPoints(EmaLine line) => _getEmaPoints(line);
    public IReadOnlyList<ChartTimePricePoint> GetBollingerBandPoints(BollingerBandLine line) => _getBollingerBandPoints(line);
    public void DrawChartTrendLine(string name, DateTime startTime, double startPrice, DateTime endTime, double endPrice, Color color, int thickness) => _drawChartTrendLine(name, startTime, startPrice, endTime, endPrice, color, thickness);

    public void Log(string message) => _log(message);

    public void AddChartControl(ControlBase control) => _addChartControl(control);

    public void RemoveChartControl(ControlBase control) => _removeChartControl(control);

    public TradeResult OpenMarketOrder(TradeType tradeType) => _openMarketOrder(tradeType);

    public IReadOnlyList<TradeResult> CloseChartPositions() => _closeChartPositions();

    public IReadOnlyList<TradeResult> CancelChartPendingOrders() => _cancelChartPendingOrders();

    public IReadOnlyList<HistoricalTrade> GetHistoricalTrades() => _getHistoricalTrades();
    public int GetHistoricalTradeCount() => _getHistoricalTradeCount();
    public double GetAccountEquity() => _getAccountEquity();
    public AccountSummary GetAccountSummary() => _getAccountSummary();
    public string? GetLocalValue(string key) => _getLocalValue(key);
    public void SetLocalValue(string key, string value) => _setLocalValue(key, value);
    public void FlushLocalValues() => _flushLocalValues();

    public OpenPositionSnapshot GetOpenPositionSnapshot() => _getOpenPositionSnapshot();

    public TradeVolumeInfo GetTradeVolumeInfo() => _getTradeVolumeInfo();
    public IReadOnlyList<Position> GetChartPositions(TradeType direction) => _getChartPositions(direction);
    public IReadOnlyList<PendingOrder> GetGridPendingOrders(string labelPrefix) => _getGridPendingOrders(labelPrefix);
    public TradeResult PlaceGridLimit(TradeType direction, double price, string label) => _placeGridLimit(direction, price, label);
    public TradeResult ClosePosition(Position position) => _closePosition(position);
    public TradeResult CancelPendingOrder(PendingOrder order) => _cancelPendingOrder(order);
    public MarketSnapshot GetMarketSnapshot() => _getMarketSnapshot();
    public void DrawHorizontalLine(string name, double price, Color color) => _drawHorizontalLine(name, price, color);
    public void DrawGraphHorizontalLine(string name, double price, Color color, int thickness, LineStyle lineStyle) => _drawGraphHorizontalLine(name, price, color, thickness, lineStyle);
    public void DrawGraphVerticalLine(string name, DateTime time, Color color, int thickness, LineStyle lineStyle) => _drawGraphVerticalLine(name, time, color, thickness, lineStyle);
    public void DrawChartText(string name, string text, DateTime time, double price, Color color) => _drawChartText(name, text, time, price, color);
    public ChartViewport GetChartViewport() => _getChartViewport();
    public ChartPriceRange? GetChartPriceRange(DateTime start, DateTime end) => _getChartPriceRange(start, end);
    public void DrawChartRectangle(string name, DateTime start, DateTime end, double bottomPrice, double topPrice, Color color) => _drawChartRectangle(name, start, end, bottomPrice, topPrice, color);
    public void RemoveChartObject(string name) => _removeChartObject(name);
    public void SendHttpRequest(HttpRequest request, Action<HttpResponse> callback) => _sendHttpRequest(request, callback);
    public void BeginInvokeOnMainThread(Action action) => _beginInvokeOnMainThread(action);
}
