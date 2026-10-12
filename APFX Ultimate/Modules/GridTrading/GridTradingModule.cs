namespace cAlgo.Robots;

/// <summary>Manages a single, chart-symbol grid basket and adopts same-direction manual positions.</summary>
public sealed class GridTradingModule : IRobotModule
{
    private const double AtrAmountPerPip = 0.10;
    private const double MinimumGridAmount = 15.0;
    private const double MaximumGridAmount = 30.0;
    private const string BasketEquityStoragePrefix = "APFXUltimateGridBasketEquityV2";
    private const string TargetLineName = "APFXUltimate.Target";
    private const string LockLineName = "APFXUltimate.Lock";
    private const string TargetLabelName = TargetLineName + ".Label";
    private const string LockLabelName = LockLineName + ".Label";
    private const string PreviewPrefix = "APFXUltimate.Preview";
    private const string PreviewStorageKey = "APFXUltimateTradePreviewV1";
    private readonly bool _configuredPreviewEnabled;
    private RobotContext? _context;
    private readonly GridConfiguration _config;
    private readonly GridStatus _status;
    private bool _closingBasket;
    private bool _closeAllSymbolPositions;
    private double _startingEquity;
    private double _highWaterProfit;
    private int _nextLevel = 1;
    private TradeType? _activeDirection;
    private DateTime _nextVolumeProfileRefreshAt = DateTime.MinValue;
    private DailyVolumeProfile? _currentVolumeProfile;
    private bool _insufficientMarginLogged;
    private TradingAccountIdentity? _accountIdentity;
    private TradingAccountIdentity AccountIdentity => _accountIdentity ?? throw new InvalidOperationException("Account scope has not been initialized.");

    private void EnsureAccount(RobotContext context)
    {
        var identity = TradingAccountIdentity.From(context.GetAccountSummary());
        if (_accountIdentity == identity) return;
        if (_accountIdentity is not null)
        {
            ClearChartLines(context);
            ResetBasket();
            _closingBasket = false;
            _closeAllSymbolPositions = false;
            _status.EntriesPlaced = 0;
            _status.BasketProfit = 0;
            _status.TrailingFloor = 0;
            _status.State = _config.Enabled ? "Waiting for manual position" : "Disabled";
        }
        _accountIdentity = identity;
    }

    public GridTradingModule(GridConfiguration config, GridStatus status, bool previewEnabled = true)
    {
        _config = config;
        _status = status;
        _configuredPreviewEnabled = previewEnabled;
        IsPreviewEnabled = previewEnabled;
    }
    public string Name => nameof(GridTradingModule);
    public bool IsPreviewEnabled { get; private set; }

    public void SetPreviewEnabled(bool enabled)
    {
        IsPreviewEnabled = enabled;
        if (_context is null) return;
        SavePreviewPreference(_context);
        UpdatePreviewLines(_context, _context.GetMarketSnapshot());
    }

    private void SavePreviewPreference(RobotContext context)
    {
        context.SetLocalValue(PreviewStorageKey, $"{_configuredPreviewEnabled}:{IsPreviewEnabled}");
        context.FlushLocalValues();
    }

    public void OnStart(RobotContext context)
    {
        EnsureAccount(context);
        _context = context;
        ClearPreviewLines(context);
        IsPreviewEnabled = _configuredPreviewEnabled;
        var saved = context.GetLocalValue(PreviewStorageKey)?.Split(':');
        if (saved is { Length: 2 }
            && bool.TryParse(saved[0], out var previousSetting)
            && previousSetting == _configuredPreviewEnabled
            && bool.TryParse(saved[1], out var savedEnabled))
        {
            IsPreviewEnabled = savedEnabled;
        }
        SavePreviewPreference(context);
        UpdateVolumeInfo(context);
        _status.Direction = _config.Direction;
        _status.BaseTarget = _config.TargetProfit;
        _status.TrailingLockPercent = _config.TrailingLockPercent;
        _status.UseAtrDistance = _config.UseAtrDistance;
        _status.UseAtr = _config.UseAtr;
        _status.AtrPeriod = _config.AtrPeriod;
        _status.MaxDrawdownPercent = _config.MaxDrawdownPercent;
        _status.MaxDrawdownAmount = _config.MaxDrawdownAmount;
        var snapshot = context.GetMarketSnapshot();
        _status.EffectiveTarget = GetEffectiveTarget(snapshot);
        _status.DistanceAmount = GetEffectiveDistance(snapshot);
        UpdateVolumeProfileStatus(context, snapshot);
        UpdateDrawdownStatus(snapshot);
        if (!_config.Enabled) return;
        _status.State = "Waiting for manual position";
        UpdatePreviewLines(context, snapshot);
        context.Log($"Grid armed ({_config.Direction}); waiting for a manual position.");
    }

    public void OnTick(RobotContext context)
    {
        EnsureAccount(context);
        var snapshot = context.GetMarketSnapshot();
        UpdateVolumeInfo(context);
        _status.EffectiveTarget = GetEffectiveTarget(snapshot);
        _status.DistanceAmount = GetEffectiveDistance(snapshot);
        UpdateVolumeProfileStatus(context, snapshot);
        UpdateDrawdownStatus(snapshot);
        if (!_config.Enabled) return;
        if (_closingBasket)
        {
            FinishClosingBasket(context, snapshot);
            return;
        }
        if (_activeDirection is null)
        {
            if (!TryAdoptBasket(context, snapshot))
            {
                UpdatePreviewLines(context, snapshot);
                return;
            }
        }
        if (_activeDirection is not { } activeDirection) return;
        ClearPreviewLines(context);
        var positions = context.GetChartPositions(activeDirection);
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
            BeginBasketClose(context, GetChartSymbolPositions(context), "equity drawdown limit", closeAllSymbolPositions: true);
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
        EnsureAccount(context);
        var snapshot = context.GetMarketSnapshot();
        _status.EffectiveTarget = GetEffectiveTarget(snapshot);
        _status.DistanceAmount = GetEffectiveDistance(snapshot);
        UpdateDrawdownStatus(snapshot);
        UpdatePreviewLines(context, snapshot);

        if (!_config.Enabled || (_activeDirection is null && !TryAdoptBasket(context, snapshot)))
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
            BeginBasketClose(context, GetChartSymbolPositions(context), "equity drawdown limit", closeAllSymbolPositions: true);
        }
    }

    public void OnStop(RobotContext context)
    {
        ClearChartLines(context);
        _context = null;
    }

    private bool TryAdoptBasket(RobotContext context, MarketSnapshot snapshot)
    {
        foreach (var direction in GetAllowedDirections())
        {
            var positions = context.GetChartPositions(direction);
            if (positions.Count == 0) continue;
            _startingEquity = LoadOrCreateBasketStartingEquity(context, snapshot, direction, positions);
            _activeDirection = direction;
            UpdateDrawdownStatus(snapshot);
            SynchronizePendingOrders(context, snapshot.SymbolName);
            context.Log($"Grid adopted manual {direction} position.");
            return true;
        }
        return false;
    }

    private void PlaceNextLevels(RobotContext context, MarketSnapshot snapshot)
    {
        var prefix = LabelPrefix(snapshot.SymbolName);
        var existing = context.GetGridPendingOrders(prefix).OrderBy(order => order.Label).ToArray();

        var info = context.GetTradeVolumeInfo();
        var margin = _activeDirection!.Value == TradeType.Buy ? info.BuyEstimatedMargin : info.SellEstimatedMargin;
        var pips = GetEffectiveDistance(snapshot) / Math.Max(0.00000001, info.VolumeInUnits * snapshot.PipValue);
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
        _status.PendingOrderNearHighVolumeNode = IsNearHighVolumeNode(price);
        if (_status.PendingOrderNearHighVolumeNode)
        {
            context.Log($"Grid level {_nextLevel} is near a high-volume profile node.");
        }

        if (snapshot.FreeMargin < margin)
        {
            if (!_insufficientMarginLogged)
            {
                context.Log($"Grid stopped at level {_nextLevel}: insufficient free margin.");
                _insufficientMarginLogged = true;
            }
            return;
        }

        _insufficientMarginLogged = false;

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

    private void BeginBasketClose(RobotContext context, IReadOnlyList<Position> positions, string reason, bool closeAllSymbolPositions = false)
    {
        _closingBasket = true;
        _closeAllSymbolPositions = closeAllSymbolPositions;
        _status.State = $"Closing: {reason}";
        ClearChartLines(context);
        CancelBasketPendingOrders(context, context.GetMarketSnapshot().SymbolName);
        foreach (var p in positions) context.ClosePosition(p);
        context.Log($"Grid closing: {reason}.");
    }
    private void FinishClosingBasket(RobotContext context, MarketSnapshot snapshot)
    {
        if (_activeDirection is null)
        {
            _closingBasket = false;
            return;
        }

        var positions = _closeAllSymbolPositions
            ? GetChartSymbolPositions(context)
            : context.GetChartPositions(_activeDirection.Value);
        var pendingOrders = context.GetGridPendingOrders(LabelPrefix(snapshot.SymbolName));
        foreach (var order in pendingOrders)
        {
            context.CancelPendingOrder(order);
        }
        foreach (var position in positions) context.ClosePosition(position);

        if (positions.Count > 0 || pendingOrders.Count > 0)
        {
            return;
        }

        ClearBasketStartingEquity(context, snapshot.SymbolName);
        ResetBasket();
        _closingBasket = false;
        _closeAllSymbolPositions = false;
        UpdateDrawdownStatus(snapshot);
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
    }

    private double LoadOrCreateBasketStartingEquity(RobotContext context, MarketSnapshot snapshot, TradeType direction, IReadOnlyList<Position> positions)
    {
        var storageKey = GetBasketEquityStorageKey(snapshot.SymbolName, direction);
        if (double.TryParse(context.GetLocalValue(storageKey), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var storedEquity)
            && double.IsFinite(storedEquity) && storedEquity > 0)
        {
            return storedEquity;
        }

        var inferredStartingEquity = snapshot.Equity - positions.Sum(position => position.NetProfit);
        var startingEquity = inferredStartingEquity > 0 ? inferredStartingEquity : snapshot.Equity;
        context.SetLocalValue(storageKey, startingEquity.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
        context.FlushLocalValues();
        return startingEquity;
    }

    private void ClearBasketStartingEquity(RobotContext context, string symbolName)
    {
        context.SetLocalValue(GetBasketEquityStorageKey(symbolName, TradeType.Buy), "0");
        context.SetLocalValue(GetBasketEquityStorageKey(symbolName, TradeType.Sell), "0");
        context.FlushLocalValues();
    }

    private string GetBasketEquityStorageKey(string symbolName, TradeType direction) => AccountIdentity.DrawdownStorageKey(
        BasketEquityStoragePrefix + JsonSerializer.Serialize(new { Symbol = symbolName, Direction = direction }));
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
        ClearPreviewLines(context);
        context.RemoveChartObject(TargetLineName);
        context.RemoveChartObject(LockLineName);
        context.RemoveChartObject(TargetLabelName);
        context.RemoveChartObject(LockLabelName);
    }
    private void UpdatePreviewLines(RobotContext context, MarketSnapshot snapshot)
    {
        if (!IsPreviewEnabled || !_config.Enabled || _closingBasket || _activeDirection is not null
            || GetChartSymbolPositions(context).Count > 0)
        {
            ClearPreviewLines(context);
            return;
        }

        var volume = context.GetTradeVolumeInfo().VolumeInUnits;
        var amountPerPip = volume * snapshot.PipValue;
        var target = GetEffectiveTarget(snapshot);
        if (!double.IsFinite(amountPerPip) || amountPerPip <= 0
            || !double.IsFinite(snapshot.PipSize) || snapshot.PipSize <= 0
            || !double.IsFinite(snapshot.Bid) || snapshot.Bid <= 0
            || !double.IsFinite(snapshot.Ask) || snapshot.Ask <= 0
            || !double.IsFinite(target) || target < 0)
        {
            ClearPreviewLines(context);
            return;
        }

        var viewport = context.GetChartViewport();
        var chartContext = context.GetChartContextSnapshot();
        var duration = viewport.EndTime - viewport.StartTime;
        if (duration.Ticks <= 0 || chartContext.CurrentBarOpenTime == DateTime.MinValue || chartContext.CurrentBarDuration.Ticks <= 0)
        {
            ClearPreviewLines(context);
            return;
        }

        var minimumLineTicks = Math.Max(1, duration.Ticks / 100);
        var maximumLineTicks = Math.Max(minimumLineTicks, duration.Ticks / 80);
        var lineDuration = TimeSpan.FromTicks(Math.Clamp(chartContext.CurrentBarDuration.Ticks / 2, minimumLineTicks, maximumLineTicks));
        var gapTicks = Math.Max(1, Math.Min(chartContext.CurrentBarDuration.Ticks / 5, lineDuration.Ticks / 5));
        var start = chartContext.CurrentBarOpenTime.AddTicks(gapTicks);
        var end = start.Add(lineDuration);
        foreach (var direction in GetAllowedDirections())
        {
            var isBuy = direction == TradeType.Buy;
            var sign = isBuy ? 1d : -1d;
            // Opening buys fill at Ask and sells at Bid. These are gross-profit
            // estimates; actual basket targets also account for incurred costs.
            var entry = isBuy ? snapshot.Ask : snapshot.Bid;
            var targetPrice = entry + sign * target / amountPerPip * snapshot.PipSize;
            DrawPreviewLine(context, direction, "Target", $"{direction} TP ~{target:N2}", targetPrice, start, end, Color.FromHex("#FFE879F9"));
        }
    }

    private static void DrawPreviewLine(RobotContext context, TradeType direction, string kind, string label, double price, DateTime start, DateTime end, Color color)
    {
        var name = $"{PreviewPrefix}.{direction}.{kind}";
        context.DrawChartTrendLine(name, start, price, end, price, color, 2);
        context.DrawChartText(name + ".Label", label, start.AddTicks((end - start).Ticks / 2), price, color);
    }

    private static void ClearPreviewLines(RobotContext context)
    {
        foreach (var direction in new[] { TradeType.Buy, TradeType.Sell })
        foreach (var kind in new[] { "Target", "Pending" })
        {
            var name = $"{PreviewPrefix}.{direction}.{kind}";
            context.RemoveChartObject(name);
            context.RemoveChartObject(name + ".Label");
        }
    }
    private double GetEffectiveTarget(MarketSnapshot snapshot) => _config.UseAtr
        ? GetAtrAmount(snapshot)
        : _config.TargetProfit;
    private double GetEffectiveDistance(MarketSnapshot snapshot) => _config.UseAtrDistance
        ? GetAtrAmount(snapshot)
        : Math.Clamp(_config.DistanceAmount, MinimumGridAmount, MaximumGridAmount);
    private static double GetAtrAmount(MarketSnapshot snapshot) =>
        Math.Clamp(snapshot.AtrPips * AtrAmountPerPip, 0, MaximumGridAmount);
    private void UpdateVolumeProfileStatus(RobotContext context, MarketSnapshot snapshot)
    {
        if (context.ServerTime >= _nextVolumeProfileRefreshAt)
        {
            _currentVolumeProfile = context.GetDailyVolumeProfiles(1, 1).LastOrDefault();
            _nextVolumeProfileRefreshAt = context.ServerTime.AddMinutes(1);
        }

        if (_currentVolumeProfile is not { } profile || profile.Volumes is null || profile.Volumes.Count == 0)
        {
            _status.NearestProfileLevelDistancePips = 0;
            return;
        }

        var currentPrice = (snapshot.Bid + snapshot.Ask) / 2d;
        _status.NearestProfileLevelDistancePips = new[] { profile.PointOfControlPrice, profile.ValueAreaHigh, profile.ValueAreaLow }
            .Min(level => Math.Abs(level - currentPrice) / snapshot.PipSize);
    }
    private bool IsNearHighVolumeNode(double price)
    {
        return _currentVolumeProfile is { } profile
            && profile.Volumes is not null
            && profile.HighVolumeNodeIndices.Any(index => Math.Abs(profile.GetBinMidpoint(index) - price) <= profile.RowHeight);
    }
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
        var percentageLimit = referenceEquity * Math.Max(0, _config.MaxDrawdownPercent) / 100d;
        var cashLimit = Math.Max(0, _config.MaxDrawdownAmount);
        var allowedLoss = percentageLimit > 0 && cashLimit > 0
            ? Math.Min(percentageLimit, cashLimit)
            : Math.Max(percentageLimit, cashLimit);
        _status.CurrentEquity = snapshot.Equity;
        _status.StartingEquity = referenceEquity;
        _status.PercentageDrawdownLimit = percentageLimit;
        _status.EffectiveDrawdownLimit = allowedLoss;
        _status.EquityStop = referenceEquity - allowedLoss;
        _status.DrawdownUsed = Math.Max(0, referenceEquity - snapshot.Equity);
        _status.DrawdownRemaining = Math.Max(0, allowedLoss - _status.DrawdownUsed);
        _status.IsBasketActive = _activeDirection is not null;
    }
    private bool HasReachedMaxDrawdown() =>
        _status.EffectiveDrawdownLimit > 0 && _status.DrawdownUsed >= _status.EffectiveDrawdownLimit;
    private static IReadOnlyList<Position> GetChartSymbolPositions(RobotContext context) => context
        .GetChartPositions(TradeType.Buy)
        .Concat(context.GetChartPositions(TradeType.Sell))
        .ToArray();
    private IEnumerable<TradeType> GetAllowedDirections() => _config.Direction switch { GridDirection.Buy => new[] { TradeType.Buy }, GridDirection.Sell => new[] { TradeType.Sell }, _ => new[] { TradeType.Buy, TradeType.Sell } };
    private string LabelPrefix(string symbol) => $"APFXUltimate.Grid.{symbol}.{_activeDirection}";
}
