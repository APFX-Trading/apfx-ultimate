namespace cAlgo.Robots;

/// <summary>Manages a single, chart-symbol grid basket and adopts same-direction manual positions.</summary>
public sealed class GridTradingModule : IRobotModule
{
    private const double AtrTargetAmountPerPip = 0.10;
    private const string TargetLineName = "APFXUltimate.Target";
    private const string LockLineName = "APFXUltimate.Lock";
    private const string TargetLabelName = TargetLineName + ".Label";
    private const string LockLabelName = LockLineName + ".Label";
    private readonly GridConfiguration _config;
    private readonly GridStatus _status;
    private bool _closingBasket;
    private double _startingEquity;
    private double _initialAtrPips;
    private double _highWaterProfit;
    private int _nextLevel = 1;
    private TradeType? _activeDirection;

    public GridTradingModule(GridConfiguration config, GridStatus status) { _config = config; _status = status; }
    public string Name => nameof(GridTradingModule);

    public void OnStart(RobotContext context)
    {
        UpdateVolumeInfo(context);
        _status.Direction = _config.Direction;
        _status.BaseTarget = _config.TargetProfit;
        _status.TrailingLockPercent = _config.TrailingLockPercent;
        _status.DistanceAmount = _config.DistanceAmount;
        _status.UseAtr = _config.UseAtr;
        _status.AtrPeriod = _config.AtrPeriod;
        _status.MaxDrawdownPercent = _config.MaxDrawdownPercent;
        var snapshot = context.GetMarketSnapshot();
        _status.EffectiveTarget = GetEffectiveTarget(snapshot);
        UpdateDrawdownStatus(snapshot);
        if (!_config.Enabled) return;
        _status.State = "Waiting for manual position";
        context.Log($"Grid armed ({_config.Direction}); waiting for a manual position.");
    }

    public void OnTick(RobotContext context)
    {
        var snapshot = context.GetMarketSnapshot();
        UpdateVolumeInfo(context);
        _status.EffectiveTarget = GetEffectiveTarget(snapshot);
        UpdateDrawdownStatus(snapshot);
        if (!_config.Enabled) return;
        if (_closingBasket)
        {
            FinishClosingBasket(context, snapshot);
            return;
        }
        if (_activeDirection is null)
        {
            foreach (var direction in GetAllowedDirections())
            {
                if (context.GetChartPositions(direction).Count == 0) continue;
                _activeDirection = direction;
                _startingEquity = snapshot.Equity;
                _initialAtrPips = Math.Max(snapshot.AtrPips, 0.0001);
                UpdateDrawdownStatus(snapshot);
                SynchronizePendingOrders(context, snapshot.SymbolName);
                context.Log($"Grid adopted manual {direction} position.");
                break;
            }
            if (_activeDirection is null) return;
        }
        var positions = context.GetChartPositions(_activeDirection.Value);
        _status.BasketProfit = positions.Sum(p => p.NetProfit);
        _status.EntriesPlaced = context.GetGridPendingOrders(LabelPrefix(snapshot.SymbolName)).Count;
        _status.State = "Active";
        if (positions.Count == 0)
        {
            ClearChartLines(context);
            BeginBasketClose(context, positions, "all basket positions closed");
            return;
        }

        if (HasReachedMaxDrawdown())
        {
            BeginBasketClose(context, GetChartSymbolPositions(context), "equity drawdown limit");
            return;
        }

        var profit = _status.BasketProfit;
        var target = _status.EffectiveTarget;
        if (profit >= target) _highWaterProfit = Math.Max(_highWaterProfit, profit);
        _status.TrailingFloor = _highWaterProfit * _config.TrailingLockPercent / 100d;
        if (_highWaterProfit > 0 && profit <= _highWaterProfit * _config.TrailingLockPercent / 100d)
        {
            BeginBasketClose(context, positions, "profit trailing lock"); return;
        }

        UpdateChartLines(context, snapshot, positions);
        PlaceNextLevels(context, snapshot);
    }

    public void OnTimer(RobotContext context)
    {
        var snapshot = context.GetMarketSnapshot();
        UpdateDrawdownStatus(snapshot);

        if (!_config.Enabled || _activeDirection is null)
        {
            return;
        }

        if (_closingBasket)
        {
            FinishClosingBasket(context, snapshot);
            return;
        }

        if (HasReachedMaxDrawdown())
        {
            BeginBasketClose(context, GetChartSymbolPositions(context), "equity drawdown limit");
        }
    }

    public void OnStop(RobotContext context) => ClearChartLines(context);

    private void PlaceNextLevels(RobotContext context, MarketSnapshot snapshot)
    {
        var prefix = LabelPrefix(snapshot.SymbolName);
        var existing = context.GetGridPendingOrders(prefix).OrderBy(order => order.Label).ToArray();

        var info = context.GetTradeVolumeInfo();
        var margin = _activeDirection!.Value == TradeType.Buy ? info.BuyEstimatedMargin : info.SellEstimatedMargin;
        var pips = _config.DistanceAmount / Math.Max(0.00000001, info.VolumeInUnits * snapshot.PipValue) * AtrScale(snapshot);
        var positions = context.GetChartPositions(_activeDirection.Value);
        // Keep a single next-level order. Once it fills, the next tick adds the following level.
        // This removes an artificial position cap without flooding the account with pending orders.
        if (existing.Length > 0)
        {
            return;
        }

        var direction = _activeDirection.Value;
        var spacing = pips * snapshot.PipSize;
        var price = GetNextPendingPrice(direction, positions, spacing);

        if (snapshot.FreeMargin < margin)
        {
            context.Log($"Grid stopped at level {_nextLevel}: insufficient free margin.");
            return;
        }

        // If a fast adverse move has already crossed the next grid level, the limit
        // price is invalid. Enter just this missed level at market and wait for the
        // next tick before creating another order; this prevents catch-up bursts.
        if (!IsValidPendingLimitPrice(direction, price, snapshot))
        {
            var marketResult = context.PlaceGridMarketOrder(direction, $"{prefix}.L{_nextLevel}");
            if (!marketResult.IsSuccessful)
            {
                context.Log($"Grid market level {_nextLevel} failed: {marketResult.Error}");
                return;
            }

            context.Log($"Grid level {_nextLevel} entered at market after a fast move.");
            _nextLevel++;
            return;
        }

        var result = context.PlaceGridLimit(_activeDirection.Value, price, $"{prefix}.L{_nextLevel}");
        if (!result.IsSuccessful)
        {
            context.Log($"Grid level {_nextLevel} failed: {result.Error}");
            return;
        }

        _nextLevel++;
    }

    private void SynchronizePendingOrders(RobotContext context, string symbolName)
    {
        var pendingOrders = context.GetGridPendingOrders(LabelPrefix(symbolName))
            .OrderBy(order => GetLevel(order.Label))
            .ToArray();

        foreach (var surplusOrder in pendingOrders.Skip(1))
        {
            context.CancelPendingOrder(surplusOrder);
        }

        var highestPendingLevel = pendingOrders.Take(1).Select(order => GetLevel(order.Label)).DefaultIfEmpty(0).Max();
        var highestFilledLevel = context.GetChartPositions(_activeDirection!.Value)
            .Where(position => position.Label?.StartsWith(LabelPrefix(symbolName), StringComparison.Ordinal) == true)
            .Select(position => GetLevel(position.Label!))
            .DefaultIfEmpty(0)
            .Max();
        _nextLevel = Math.Max(highestPendingLevel, highestFilledLevel) + 1;
    }

    private void BeginBasketClose(RobotContext context, IReadOnlyList<Position> positions, string reason)
    {
        _closingBasket = true;
        _status.State = $"Closing: {reason}";
        ClearChartLines(context);
        foreach (var p in positions) context.ClosePosition(p);
        CancelBasketPendingOrders(context, context.GetMarketSnapshot().SymbolName);
        context.Log($"Grid closing: {reason}.");
    }
    private void FinishClosingBasket(RobotContext context, MarketSnapshot snapshot)
    {
        if (_activeDirection is null)
        {
            _closingBasket = false;
            return;
        }

        var positions = context.GetChartPositions(_activeDirection.Value);
        var pendingOrders = context.GetGridPendingOrders(LabelPrefix(snapshot.SymbolName));
        foreach (var order in pendingOrders)
        {
            context.CancelPendingOrder(order);
        }

        if (positions.Count > 0 || pendingOrders.Count > 0)
        {
            return;
        }

        ResetBasket();
        _closingBasket = false;
        _status.EntriesPlaced = 0;
        _status.BasketProfit = 0;
        _status.TrailingFloor = 0;
        _status.State = "Waiting for manual position";
        context.Log("Grid rearmed; waiting for a manual position.");
    }
    private void CancelBasketPendingOrders(RobotContext context, string symbolName)
    {
        foreach (var order in context.GetGridPendingOrders(LabelPrefix(symbolName))) context.CancelPendingOrder(order);
    }
    private void ResetBasket()
    {
        _activeDirection = null;
        _status.IsBasketActive = false;
        _nextLevel = 1;
        _highWaterProfit = 0;
        _startingEquity = 0;
        _initialAtrPips = 0;
    }
    private void UpdateChartLines(RobotContext context, MarketSnapshot snapshot, IReadOnlyList<Position> positions)
    {
        var totalVolume = positions.Sum(position => position.VolumeInUnits);
        if (totalVolume <= 0)
        {
            ClearChartLines(context);
            return;
        }

        var direction = _activeDirection!.Value;
        var currentPrice = direction == TradeType.Buy ? snapshot.Bid : snapshot.Ask;
        var currentProfit = positions.Sum(position => position.NetProfit);
        var viewport = context.GetChartViewport();
        var labelDuration = viewport.EndTime - viewport.StartTime;
        var hasTrailingLock = _status.TrailingFloor > 0;
        var targetPrice = GetProfitPrice(currentPrice, _status.EffectiveTarget, direction, currentProfit, totalVolume, snapshot);
        context.DrawHorizontalLine(TargetLineName, targetPrice, Color.FromHex("#FFE879F9"));
        var targetLabelTime = hasTrailingLock ? viewport.EndTime.AddTicks(-labelDuration.Ticks / 4) : viewport.EndTime;
        context.DrawChartText(TargetLabelName, $"Target ${_status.EffectiveTarget:N2}", targetLabelTime, targetPrice, Color.FromHex("#FFE879F9"));

        if (hasTrailingLock)
        {
            var lockPrice = GetProfitPrice(currentPrice, _status.TrailingFloor, direction, currentProfit, totalVolume, snapshot);
            context.DrawHorizontalLine(LockLineName, lockPrice, Color.FromHex("#FFFFB74D"));
            var lockLabelTime = viewport.EndTime.AddTicks(-labelDuration.Ticks * 3 / 5);
            context.DrawChartText(LockLabelName, $"Trailing Lock ${_status.TrailingFloor:N2}", lockLabelTime, lockPrice, Color.FromHex("#FFFFB74D"));
        }
        else
        {
            context.RemoveChartObject(LockLineName);
            context.RemoveChartObject(LockLabelName);
        }
    }
    private static double GetProfitPrice(double currentPrice, double desiredProfit, TradeType direction, double currentProfit, double volumeInUnits, MarketSnapshot snapshot)
    {
        var pipsToDesiredProfit = (desiredProfit - currentProfit) / Math.Max(0.00000001, volumeInUnits * snapshot.PipValue);
        return direction == TradeType.Buy
            ? currentPrice + pipsToDesiredProfit * snapshot.PipSize
            : currentPrice - pipsToDesiredProfit * snapshot.PipSize;
    }
    private static void ClearChartLines(RobotContext context)
    {
        context.RemoveChartObject(TargetLineName);
        context.RemoveChartObject(LockLineName);
        context.RemoveChartObject(TargetLabelName);
        context.RemoveChartObject(LockLabelName);
    }
    private double AtrScale(MarketSnapshot s) => _config.UseAtr ? Math.Max(0.1, s.AtrPips / _initialAtrPips) : 1;
    private double GetEffectiveTarget(MarketSnapshot snapshot) => _config.UseAtr
        ? snapshot.AtrPips * AtrTargetAmountPerPip
        : _config.TargetProfit;
    private static int GetLevel(string label)
    {
        var markerIndex = label.LastIndexOf(".L", StringComparison.Ordinal);
        return markerIndex >= 0 && int.TryParse(label[(markerIndex + 2)..], out var level) ? level : 0;
    }
    private static double GetNextPendingPrice(TradeType direction, IReadOnlyList<Position> positions, double spacing)
    {
        var lastGridPrice = direction == TradeType.Buy
            ? positions.Min(position => position.EntryPrice)
            : positions.Max(position => position.EntryPrice);
        return direction == TradeType.Buy
            ? lastGridPrice - spacing
            : lastGridPrice + spacing;
    }
    private static bool IsValidPendingLimitPrice(TradeType direction, double price, MarketSnapshot snapshot) => direction == TradeType.Buy
        ? price < snapshot.Bid - snapshot.PipSize
        : price > snapshot.Ask + snapshot.PipSize;
    private void UpdateVolumeInfo(RobotContext context) { var volume=context.GetTradeVolumeInfo(); _status.VolumeInLots=volume.VolumeInLots; _status.BuyMargin=volume.BuyEstimatedMargin; _status.SellMargin=volume.SellEstimatedMargin; }
    private void UpdateDrawdownStatus(MarketSnapshot snapshot)
    {
        // The baseline is locked when the first manual position is adopted. Until then,
        // show the amount that would be available if a basket started at this moment.
        var referenceEquity = _activeDirection is null ? snapshot.Equity : _startingEquity;
        var allowedLoss = referenceEquity * _config.MaxDrawdownPercent / 100d;
        _status.CurrentEquity = snapshot.Equity;
        _status.StartingEquity = referenceEquity;
        _status.EquityStop = referenceEquity - allowedLoss;
        _status.DrawdownUsed = Math.Max(0, referenceEquity - snapshot.Equity);
        _status.DrawdownRemaining = Math.Max(0, allowedLoss - _status.DrawdownUsed);
        _status.IsBasketActive = _activeDirection is not null;
    }
    private bool HasReachedMaxDrawdown() =>
        _config.MaxDrawdownPercent > 0 && _status.DrawdownUsed >= _startingEquity * _config.MaxDrawdownPercent / 100d;
    private static IReadOnlyList<Position> GetChartSymbolPositions(RobotContext context) => context
        .GetChartPositions(TradeType.Buy)
        .Concat(context.GetChartPositions(TradeType.Sell))
        .ToArray();
    private IEnumerable<TradeType> GetAllowedDirections() => _config.Direction switch { GridDirection.Buy => new[] { TradeType.Buy }, GridDirection.Sell => new[] { TradeType.Sell }, _ => new[] { TradeType.Buy, TradeType.Sell } };
    private string LabelPrefix(string symbol) => $"APFXUltimate.Grid.{symbol}.{_activeDirection}";
}
