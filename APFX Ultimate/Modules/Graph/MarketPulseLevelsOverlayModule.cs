namespace cAlgo.Robots;

/// <summary>
/// Draws only the strong, confirmed support/resistance portion of APFX Market Pulse.
/// Levels are formed from confirmed swings, strengthened by later tests, and labelled
/// with their original chart time. It has no signal or order-execution behaviour.
/// </summary>
public sealed class MarketPulseLevelsOverlayModule : IRobotModule
{
    private const string ObjectPrefix = "APFXUltimate.Graph.MarketPulseLevel.";
    private readonly Bars _bars;
    private readonly AverageTrueRange _atr;
    private readonly Symbol _symbol;
    private readonly int _swingLeft;
    private readonly int _swingRight;
    private readonly int _minimumTests;
    private readonly int _maximumLevelsPerSide;
    private readonly double _mergeAtr;
    private readonly List<StrongLevel> _levels = new();
    private readonly HashSet<string> _objectNames = new(StringComparer.Ordinal);
    private RobotContext? _context;
    private int _lastProcessedClosedIndex = -1;

    public MarketPulseLevelsOverlayModule(Bars bars, AverageTrueRange atr, Symbol symbol, int swingLeft, int swingRight, int minimumTests, int maximumLevelsPerSide, double mergeAtr, bool isEnabled)
    {
        _bars = bars ?? throw new ArgumentNullException(nameof(bars));
        _atr = atr ?? throw new ArgumentNullException(nameof(atr));
        _symbol = symbol ?? throw new ArgumentNullException(nameof(symbol));
        _swingLeft = swingLeft;
        _swingRight = swingRight;
        _minimumTests = minimumTests;
        _maximumLevelsPerSide = maximumLevelsPerSide;
        _mergeAtr = mergeAtr;
        IsEnabled = isEnabled;
    }

    public string Name => nameof(MarketPulseLevelsOverlayModule);
    public string Label => "Strong S/R Levels (time)";
    public bool IsEnabled { get; private set; }

    public StrongLevelsSnapshot GetNearestLevels(double price)
    {
        var confirmedLevels = _levels.Where(level => level.Tests >= _minimumTests);
        var support = confirmedLevels
            .Where(level => level.Price < price)
            .OrderByDescending(level => level.Price)
            .Select(level => (double?)level.Price)
            .FirstOrDefault();
        var resistance = confirmedLevels
            .Where(level => level.Price > price)
            .OrderBy(level => level.Price)
            .Select(level => (double?)level.Price)
            .FirstOrDefault();
        return new StrongLevelsSnapshot(support, resistance);
    }

    public void OnStart(RobotContext context)
    {
        _context = context;
        Rebuild();
        Render();
    }

    public void OnTick(RobotContext context) { }

    public void OnTimer(RobotContext context)
    {
        ProcessNewClosedBars();
        Render();
    }

    public void OnStop(RobotContext context)
    {
        Clear();
        _context = null;
    }

    public void SetEnabled(bool isEnabled)
    {
        IsEnabled = isEnabled;
        if (isEnabled)
        {
            Rebuild();
            Render();
        }
        else
        {
            Clear();
        }
    }

    private void Rebuild()
    {
        _levels.Clear();
        var lastClosedIndex = _bars.Count - 2;
        if (lastClosedIndex <= _swingLeft + _swingRight)
        {
            _lastProcessedClosedIndex = lastClosedIndex;
            return;
        }

        // Bounded initial work keeps chart startup responsive on long histories.
        var startIndex = Math.Max(_swingLeft + _swingRight, lastClosedIndex - 2500);
        for (var closedIndex = startIndex; closedIndex <= lastClosedIndex; closedIndex++)
        {
            ProcessClosedBar(closedIndex);
        }

        _lastProcessedClosedIndex = lastClosedIndex;
    }

    private void ProcessNewClosedBars()
    {
        var lastClosedIndex = _bars.Count - 2;
        if (lastClosedIndex <= _lastProcessedClosedIndex)
        {
            return;
        }

        for (var closedIndex = Math.Max(_swingLeft + _swingRight, _lastProcessedClosedIndex + 1); closedIndex <= lastClosedIndex; closedIndex++)
        {
            ProcessClosedBar(closedIndex);
        }

        _lastProcessedClosedIndex = lastClosedIndex;
    }

    private void ProcessClosedBar(int closedIndex)
    {
        var swingIndex = closedIndex - _swingRight;
        ConfirmSwing(swingIndex, isHigh: true);
        ConfirmSwing(swingIndex, isHigh: false);
        UpdateTests(closedIndex);
    }

    private void ConfirmSwing(int index, bool isHigh)
    {
        if (index < _swingLeft || index + _swingRight >= _bars.Count)
        {
            return;
        }

        var price = isHigh ? _bars.HighPrices[index] : _bars.LowPrices[index];
        for (var candidate = index - _swingLeft; candidate <= index + _swingRight; candidate++)
        {
            if (candidate == index)
            {
                continue;
            }

            if (isHigh && _bars.HighPrices[candidate] >= price || !isHigh && _bars.LowPrices[candidate] <= price)
            {
                return;
            }
        }

        var tolerance = Math.Max(_symbol.TickSize * 2, GetAtr(index) * _mergeAtr);
        var nearest = _levels
            .Where(level => Math.Abs(level.Price - price) <= tolerance)
            .OrderBy(level => Math.Abs(level.Price - price))
            .FirstOrDefault();
        if (nearest is null)
        {
            _levels.Add(new StrongLevel(price, _bars.OpenTimes[index], index));
            return;
        }

        nearest.Tests++;
        nearest.LastTestIndex = index;
    }

    private void UpdateTests(int index)
    {
        var atr = GetAtr(index);
        var halfZone = Math.Max(_symbol.TickSize * 2, atr * Math.Min(0.20, _mergeAtr / 2));
        foreach (var level in _levels)
        {
            var priceInsideZone = _bars.HighPrices[index] >= level.Price - halfZone && _bars.LowPrices[index] <= level.Price + halfZone;
            var distance = Math.Abs(_bars.ClosePrices[index] - level.Price);
            if (priceInsideZone && level.IsArmedForTest && index - level.LastTestIndex >= _swingRight)
            {
                level.Tests++;
                level.LastTestIndex = index;
                level.IsArmedForTest = false;
            }
            else if (!priceInsideZone && distance >= atr * 0.50)
            {
                level.IsArmedForTest = true;
            }
        }
    }

    private void Render()
    {
        if (_context is null || !IsEnabled || _bars.Count == 0)
        {
            return;
        }

        var currentPrice = _bars.ClosePrices[^1];
        var spacing = Math.Max(_symbol.TickSize * 2, GetAtr(_bars.Count - 1) * _mergeAtr);
        var activeLevels = SelectLevels(level => level.Price <= currentPrice, spacing)
            .Concat(SelectLevels(level => level.Price > currentPrice, spacing))
            .ToArray();
        var activeNames = new HashSet<string>(StringComparer.Ordinal);
        var viewport = _context.GetChartViewport();
        var midpoint = viewport.StartTime.AddTicks((viewport.EndTime - viewport.StartTime).Ticks / 2);
        foreach (var level in activeLevels)
        {
            var key = ObjectPrefix + level.CreatedAt.ToString("yyyyMMddHHmmss") + "." + level.Price.ToString("F6", System.Globalization.CultureInfo.InvariantCulture);
            var isResistance = level.Price > currentPrice;
            var color = isResistance ? GraphColorPalette.StrongResistance : GraphColorPalette.StrongSupport;
            var lineEndTime = _context.ServerTime > level.CreatedAt ? _context.ServerTime : viewport.EndTime;
            _context.DrawChartTrendLine(key, level.CreatedAt, level.Price, lineEndTime, level.Price, color, 2);
            activeNames.Add(key);
            var labelTime = level.CreatedAt > midpoint ? level.CreatedAt : midpoint;
            var labelName = key + ".Label";
            var role = isResistance ? "Strong Resistance" : "Strong Support";
            _context.DrawChartText(labelName, $"{role} x{level.Tests}", labelTime, level.Price, color);
            activeNames.Add(labelName);
        }

        foreach (var staleName in _objectNames.Except(activeNames).ToArray())
        {
            _context.RemoveChartObject(staleName);
        }

        _objectNames.Clear();
        _objectNames.UnionWith(activeNames);
    }

    private IEnumerable<StrongLevel> SelectLevels(Func<StrongLevel, bool> sideFilter, double spacing) => _levels
        .Where(level => level.Tests >= _minimumTests)
        .Where(sideFilter)
        .OrderByDescending(level => level.Tests)
        .ThenByDescending(level => level.LastTestIndex)
        .Aggregate(new List<StrongLevel>(), (selected, candidate) =>
        {
            if (selected.All(existing => Math.Abs(existing.Price - candidate.Price) >= spacing)) selected.Add(candidate);
            return selected;
        })
        .OrderBy(level => Math.Abs(level.Price - _bars.ClosePrices[^1]))
        .Take(_maximumLevelsPerSide);

    private double GetAtr(int index)
    {
        var value = _atr.Result[Math.Clamp(index, 0, _atr.Result.Count - 1)];
        return double.IsNaN(value) || value <= 0 ? _symbol.TickSize * 10 : value;
    }

    private void Clear()
    {
        if (_context is null) return;
        foreach (var name in _objectNames) _context.RemoveChartObject(name);
        _objectNames.Clear();
    }

    private sealed class StrongLevel
    {
        public StrongLevel(double price, DateTime createdAt, int index)
        {
            Price = price;
            CreatedAt = createdAt;
            LastTestIndex = index;
        }

        public double Price { get; }
        public DateTime CreatedAt { get; }
        public int Tests { get; set; } = 1;
        public int LastTestIndex { get; set; }
        public bool IsArmedForTest { get; set; } = true;
    }
}
