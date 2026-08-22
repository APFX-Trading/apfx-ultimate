namespace cAlgo.Robots;

/// <summary>
/// Explicit services supplied to modules by the robot host.
/// Extend this with narrowly scoped operations as modules need them.
/// </summary>
public sealed class RobotContext
{
    private readonly Action<string> _log;
    private readonly Func<DateTime> _getServerTime;
    private readonly Action<ControlBase> _addChartControl;
    private readonly Action<ControlBase> _removeChartControl;
    private readonly Func<TradeType, TradeResult> _openMarketOrder;
    private readonly Func<IReadOnlyList<TradeResult>> _closeChartPositions;
    private readonly Func<IReadOnlyList<TradeResult>> _cancelChartPendingOrders;
    private readonly Func<IReadOnlyList<HistoricalTrade>> _getHistoricalTrades;
    private readonly Func<int> _getHistoricalTradeCount;
    private readonly Func<OpenPositionSnapshot> _getOpenPositionSnapshot;
    private readonly Func<TradeVolumeInfo> _getTradeVolumeInfo;
    private readonly Func<TradeType, IReadOnlyList<Position>> _getChartPositions;
    private readonly Func<string, IReadOnlyList<PendingOrder>> _getGridPendingOrders;
    private readonly Func<TradeType, double, string, TradeResult> _placeGridLimit;
    private readonly Func<Position, TradeResult> _closePosition;
    private readonly Func<PendingOrder, TradeResult> _cancelPendingOrder;
    private readonly Func<MarketSnapshot> _getMarketSnapshot;
    private readonly Action<string, double, Color> _drawHorizontalLine;
    private readonly Action<string> _removeChartObject;
    private readonly Action<HttpRequest, Action<HttpResponse>> _sendHttpRequest;
    private readonly Action<Action> _beginInvokeOnMainThread;

    public RobotContext(
        Action<string> log,
        Func<DateTime> getServerTime,
        Action<ControlBase> addChartControl,
        Action<ControlBase> removeChartControl,
        Func<TradeType, TradeResult> openMarketOrder,
        Func<IReadOnlyList<TradeResult>> closeChartPositions,
        Func<IReadOnlyList<TradeResult>> cancelChartPendingOrders,
        Func<IReadOnlyList<HistoricalTrade>> getHistoricalTrades,
        Func<int> getHistoricalTradeCount,
        Func<OpenPositionSnapshot> getOpenPositionSnapshot,
        Func<TradeVolumeInfo> getTradeVolumeInfo,
        Func<TradeType, IReadOnlyList<Position>> getChartPositions,
        Func<string, IReadOnlyList<PendingOrder>> getGridPendingOrders,
        Func<TradeType, double, string, TradeResult> placeGridLimit,
        Func<Position, TradeResult> closePosition,
        Func<PendingOrder, TradeResult> cancelPendingOrder,
        Func<MarketSnapshot> getMarketSnapshot,
        Action<string, double, Color> drawHorizontalLine,
        Action<string> removeChartObject,
        Action<HttpRequest, Action<HttpResponse>> sendHttpRequest,
        Action<Action> beginInvokeOnMainThread)
    {
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _getServerTime = getServerTime ?? throw new ArgumentNullException(nameof(getServerTime));
        _addChartControl = addChartControl ?? throw new ArgumentNullException(nameof(addChartControl));
        _removeChartControl = removeChartControl ?? throw new ArgumentNullException(nameof(removeChartControl));
        _openMarketOrder = openMarketOrder ?? throw new ArgumentNullException(nameof(openMarketOrder));
        _closeChartPositions = closeChartPositions ?? throw new ArgumentNullException(nameof(closeChartPositions));
        _cancelChartPendingOrders = cancelChartPendingOrders ?? throw new ArgumentNullException(nameof(cancelChartPendingOrders));
        _getHistoricalTrades = getHistoricalTrades ?? throw new ArgumentNullException(nameof(getHistoricalTrades));
        _getHistoricalTradeCount = getHistoricalTradeCount ?? throw new ArgumentNullException(nameof(getHistoricalTradeCount));
        _getOpenPositionSnapshot = getOpenPositionSnapshot ?? throw new ArgumentNullException(nameof(getOpenPositionSnapshot));
        _getTradeVolumeInfo = getTradeVolumeInfo ?? throw new ArgumentNullException(nameof(getTradeVolumeInfo));
        _getChartPositions = getChartPositions ?? throw new ArgumentNullException(nameof(getChartPositions));
        _getGridPendingOrders = getGridPendingOrders ?? throw new ArgumentNullException(nameof(getGridPendingOrders));
        _placeGridLimit = placeGridLimit ?? throw new ArgumentNullException(nameof(placeGridLimit));
        _closePosition = closePosition ?? throw new ArgumentNullException(nameof(closePosition));
        _cancelPendingOrder = cancelPendingOrder ?? throw new ArgumentNullException(nameof(cancelPendingOrder));
        _getMarketSnapshot = getMarketSnapshot ?? throw new ArgumentNullException(nameof(getMarketSnapshot));
        _drawHorizontalLine = drawHorizontalLine ?? throw new ArgumentNullException(nameof(drawHorizontalLine));
        _removeChartObject = removeChartObject ?? throw new ArgumentNullException(nameof(removeChartObject));
        _sendHttpRequest = sendHttpRequest ?? throw new ArgumentNullException(nameof(sendHttpRequest));
        _beginInvokeOnMainThread = beginInvokeOnMainThread ?? throw new ArgumentNullException(nameof(beginInvokeOnMainThread));
    }

    public DateTime ServerTime => _getServerTime();

    public void Log(string message) => _log(message);

    public void AddChartControl(ControlBase control) => _addChartControl(control);

    public void RemoveChartControl(ControlBase control) => _removeChartControl(control);

    public TradeResult OpenMarketOrder(TradeType tradeType) => _openMarketOrder(tradeType);

    public IReadOnlyList<TradeResult> CloseChartPositions() => _closeChartPositions();

    public IReadOnlyList<TradeResult> CancelChartPendingOrders() => _cancelChartPendingOrders();

    public IReadOnlyList<HistoricalTrade> GetHistoricalTrades() => _getHistoricalTrades();
    public int GetHistoricalTradeCount() => _getHistoricalTradeCount();

    public OpenPositionSnapshot GetOpenPositionSnapshot() => _getOpenPositionSnapshot();

    public TradeVolumeInfo GetTradeVolumeInfo() => _getTradeVolumeInfo();
    public IReadOnlyList<Position> GetChartPositions(TradeType direction) => _getChartPositions(direction);
    public IReadOnlyList<PendingOrder> GetGridPendingOrders(string labelPrefix) => _getGridPendingOrders(labelPrefix);
    public TradeResult PlaceGridLimit(TradeType direction, double price, string label) => _placeGridLimit(direction, price, label);
    public TradeResult ClosePosition(Position position) => _closePosition(position);
    public TradeResult CancelPendingOrder(PendingOrder order) => _cancelPendingOrder(order);
    public MarketSnapshot GetMarketSnapshot() => _getMarketSnapshot();
    public void DrawHorizontalLine(string name, double price, Color color) => _drawHorizontalLine(name, price, color);
    public void RemoveChartObject(string name) => _removeChartObject(name);
    public void SendHttpRequest(HttpRequest request, Action<HttpResponse> callback) => _sendHttpRequest(request, callback);
    public void BeginInvokeOnMainThread(Action action) => _beginInvokeOnMainThread(action);
}
