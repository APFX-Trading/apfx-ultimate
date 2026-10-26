using System.Linq.Expressions;
using System.Reflection;
using cAlgo.API;
using cAlgo.Robots;

// Run with: dotnet run --project tests/GridPreview -p:AlgoPublish=false
var fixture = new PreviewFixture();
fixture.SeedLegacyPendingPreview();
var module = fixture.CreateModule(GridDirection.Both);
module.OnStart(fixture.Context);
fixture.ExpectCount(2);
fixture.ExpectPrice("Buy.Target", 1.1022);
fixture.ExpectPrice("Sell.Target", 1.0980);
fixture.ExpectRightOfCandle("Buy.Target");
fixture.ExpectRightOfCandle("Sell.Target");

fixture.Market = fixture.Market with { Bid = 1.1100, Ask = 1.1102 };
module.OnTick(fixture.Context);
fixture.ExpectCount(2);
fixture.ExpectPrice("Buy.Target", 1.1122);

fixture.Volume = fixture.Volume with { VolumeInUnits = 20000 };
module.OnTimer(fixture.Context);
fixture.ExpectPrice("Buy.Target", 1.1112);
module.OnStop(fixture.Context);
fixture.ExpectCount(0);

foreach (var direction in new[] { GridDirection.Buy, GridDirection.Sell })
{
    module = fixture.CreateModule(direction);
    module.OnStart(fixture.Context);
    fixture.ExpectCount(1);
    if (fixture.Lines.Keys.Any(key => !key.Contains($".{direction}.")))
        throw new Exception("Preview includes a disabled direction.");
    module.OnStop(fixture.Context);
}

module = fixture.CreateModule(GridDirection.Both, enabled: false);
module.OnStart(fixture.Context);
module.OnTick(fixture.Context);
module.OnTimer(fixture.Context);
fixture.ExpectCount(0);

fixture.Volume = fixture.Volume with { VolumeInUnits = 10000 };
module = fixture.CreateModule(GridDirection.Buy, atr: true);
fixture.Market = fixture.Market with { AtrPips = 100 };
module.OnStart(fixture.Context);
fixture.ExpectPrice("Buy.Target", 1.1112);
fixture.ExpectMatchingAtrAmounts(10);
fixture.Market = fixture.Market with { AtrPips = 64 };
module.OnTimer(fixture.Context);
fixture.ExpectPrice("Buy.Target", 1.11084);
fixture.ExpectMatchingAtrAmounts(6.4);
fixture.Volume = fixture.Volume with { VolumeInUnits = 20000 };
module.OnTimer(fixture.Context);
fixture.ExpectPrice("Buy.Target", 1.11052);
fixture.ExpectMatchingAtrAmounts(6.4); // Monetary ATR rule stays fixed when order volume changes.
fixture.Volume = fixture.Volume with { VolumeInUnits = 10000 };
fixture.Market = fixture.Market with { AtrPips = 400 };
module.OnTimer(fixture.Context);
fixture.ExpectPrice("Buy.Target", 1.1132);
fixture.ExpectMatchingAtrAmounts(30);
fixture.Market = fixture.Market with { AtrPips = 250 };
module.OnTick(fixture.Context);
fixture.ExpectPrice("Buy.Target", 1.1127);
fixture.ExpectMatchingAtrAmounts(25);
module.OnStop(fixture.Context);
module = fixture.CreateModule(GridDirection.Both, atr: true);
foreach (var test in new[] { (0d, 0d), (64d, 6.4d), (100d, 10d), (150d, 15d), (300d, 30d), (1000d, 30d) })
{
    fixture.Market = fixture.Market with { AtrPips = test.Item1 };
    module.OnStart(fixture.Context);
    var expected = test.Item2;
    fixture.ExpectPrice("Buy.Target", 1.1102 + expected * 0.0001);
    fixture.ExpectPrice("Sell.Target", 1.1100 - expected * 0.0001);
    fixture.ExpectMatchingAtrAmounts(expected);
    module.OnStop(fixture.Context);
}

fixture.Volume = fixture.Volume with { VolumeInUnits = 0 };
module.OnTimer(fixture.Context);
fixture.ExpectCount(0);
fixture.Volume = fixture.Volume with { VolumeInUnits = 10000 };
module = fixture.CreateModule(GridDirection.Both);
module.OnStart(fixture.Context);
module.SetPreviewEnabled(false);
fixture.ExpectCount(0);
module.OnTick(fixture.Context);
module.OnTimer(fixture.Context);
fixture.ExpectCount(0);
module.OnStop(fixture.Context);
module = fixture.CreateModule(GridDirection.Both);
module.OnStart(fixture.Context);
fixture.ExpectCount(0); // Panel choice survives restart with unchanged settings.
module.SetPreviewEnabled(true);
fixture.ExpectCount(2); // Immediate redraw without waiting for a tick.
module.OnStop(fixture.Context);
module = fixture.CreateModule(GridDirection.Both, previewEnabled: false);
module.OnStart(fixture.Context);
fixture.ExpectCount(0); // Changed bot parameter overrides the saved panel choice.
module.SetPreviewEnabled(true);
module.OnStop(fixture.Context);
module = fixture.CreateModule(GridDirection.Both, previewEnabled: false);
module.OnStart(fixture.Context);
fixture.ExpectCount(2); // Panel override also persists when the default is off.
module.OnStop(fixture.Context);
CheckProgressiveSpacing();
Console.WriteLine("Grid preview regression checks passed.");

static void CheckProgressiveSpacing()
{
    var distanceMethod = typeof(GridTradingModule).GetMethod("GetEffectiveDistance",
        BindingFlags.NonPublic | BindingFlags.Instance, null,
        new[] { typeof(MarketSnapshot), typeof(int), typeof(double) }, null)!;
    var priceMethod = typeof(GridTradingModule).GetMethod("GetNextPendingPrice", BindingFlags.NonPublic | BindingFlags.Static)!;
    var config = new GridConfiguration(true, GridDirection.Both, 20, 80, 20, false, false, 14, 10, 0);
    var market = new MarketSnapshot() with { PipSize = 1, PipValue = 1 };

    double Distance(GridConfiguration settings, int count, double volume = 1, double atrPips = 0)
    {
        // A fresh instance for every calculation also checks recovery from the open
        // entry count rather than dependence on an in-memory submitted-order counter.
        var grid = new GridTradingModule(settings, new GridStatus());
        return (double)distanceMethod.Invoke(grid, new object[] { market with { AtrPips = atrPips }, count, volume })!;
    }
    void Expect(double actual, double expected)
    {
        if (Math.Abs(actual - expected) > 1e-8)
            throw new Exception($"Grid spacing: expected {expected}, got {actual}.");
    }

    foreach (var direction in new[] { TradeType.Sell, TradeType.Buy })
    {
        var sign = direction == TradeType.Sell ? 1 : -1;
        var positions = new List<Position>();
        var price = 52300d;
        foreach (var offset in new[] { 20d, 40d, 110d, 230d, 400d, 620d })
        {
            positions.Add(SpacingPositionProxy.Make(price));
            var amount = Distance(config, positions.Count);
            price = (double)priceMethod.Invoke(null, new object[] { direction, positions, amount / market.PipValue * market.PipSize })!;
            Expect(price, 52300 + sign * offset);
        }
    }

    Expect(Distance(config, 0), 20);
    Expect(Distance(config with { InitialEntries = 1 }, 1), 70);
    Expect(Distance(config with { InitialEntries = 4 }, 3), 20);
    Expect(Distance(config with { InitialEntries = 4 }, 4), 70);
    Expect(Distance(config with { DistanceIncrementPips = 0 }, 100), 20);
    Expect(Distance(config with { DistanceIncrementPips = 5 }, 3), 25);
    Expect(Distance(config with { DistanceIncrementPips = 5 }, 7), 45);
    Expect(Distance(config with { DistanceAmount = 1 }, 1), 15);
    Expect(Distance(config with { DistanceAmount = 100 }, 1), 30);
    Expect(Distance(config, 3, volume: 2), 120);
    Expect(Distance(config, 3, volume: 0.1), 25);
    foreach (var atr in new[] { 0d, 64d, 150d, 250d, 400d })
    {
        Expect(Distance(config with { UseAtrDistance = true }, 1, atrPips: atr), Math.Clamp(atr * 0.1, 15, 30));
        foreach (var count in new[] { 3, 4, 5 })
            Expect(Distance(config with { UseAtrDistance = true }, count, atrPips: atr),
                Math.Clamp(atr * 0.1, 15, 30) + (count - 2) * 50);
    }
    Console.WriteLine("Progressive grid spacing regression checks passed.");
}

public class SpacingPositionProxy : DispatchProxy
{
    private double _price;
    public static Position Make(double price)
    {
        var position = Create<Position, SpacingPositionProxy>();
        ((SpacingPositionProxy)(object)position)._price = price;
        return position;
    }
    protected override object? Invoke(MethodInfo? method, object?[]? args) => method?.Name == "get_EntryPrice"
        ? _price : throw new Exception($"Unexpected position API call: {method?.Name}");
}

sealed class PreviewFixture
{
    public MarketSnapshot Market = new("TEST", 1.1000, 1.1002, 0.0001, 0.0001,
        10000, 10000, 100, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
    public TradeVolumeInfo Volume = new(0.1, 10000, 100, 100);
    public GridStatus Status { get; private set; } = new();
    public Dictionary<string, double> Lines { get; } = new();
    private readonly Dictionary<string, (DateTime Start, DateTime End)> _segments = new();
    private readonly HashSet<string> _labels = new();
    private readonly Dictionary<string, string> _storage = new();
    public RobotContext Context { get; }

    public PreviewFixture()
    {
        var time = new DateTime(2026, 9, 16, 12, 0, 0, DateTimeKind.Utc);
        var services = new Dictionary<string, Delegate>
        {
            ["log"] = (Action<string>)(_ => { }),
            ["getAccountSummary"] = (Func<AccountSummary>)(() => new AccountSummary(Market.Equity, Market.Equity, 0, Market.Equity, "USD", "Test Broker", 1, false)),
            ["getLocalValue"] = (Func<string, string?>)(key => _storage.GetValueOrDefault(key)),
            ["setLocalValue"] = (Action<string, string>)((key, value) => _storage[key] = value),
            ["flushLocalValues"] = (Action)(() => { }),
            ["getServerTime"] = (Func<DateTime>)(() => time),
            ["getMarketSnapshot"] = (Func<MarketSnapshot>)(() => Market),
            ["getTradeVolumeInfo"] = (Func<TradeVolumeInfo>)(() => Volume),
            ["getChartViewport"] = (Func<ChartViewport>)(() => new(time.AddHours(-2), time, 1, 2)),
            ["getChartContextSnapshot"] = (Func<ChartContextSnapshot>)(() => new("TEST", "Minute", 1.1, 1.2, 1.0, time.AddMinutes(-1), TimeSpan.FromMinutes(1))),
            ["getChartPositions"] = (Func<TradeType, IReadOnlyList<Position>>)(_ => Array.Empty<Position>()),
            ["getDailyVolumeProfiles"] = (Func<int, int, IReadOnlyList<DailyVolumeProfile>>)((_, _) => Array.Empty<DailyVolumeProfile>()),
            ["drawChartTrendLine"] = (Action<string, DateTime, double, DateTime, double, Color, int>)((name, start, price, end, endPrice, _, _) =>
            {
                if (end <= start || end - start > TimeSpan.FromMinutes(15) || price != endPrice)
                    throw new Exception("Expected a short horizontal segment.");
                Lines[name] = price;
                _segments[name] = (start, end);
            }),
            ["drawChartText"] = (Action<string, string, DateTime, double, Color>)((name, _, _, _, _) => _labels.Add(name)),
            ["removeChartObject"] = (Action<string>)(name => { Lines.Remove(name); _segments.Remove(name); _labels.Remove(name); })
        };

        // Fail on any unexpected service use, especially trading operations.
        var constructor = typeof(RobotContext).GetConstructors().Single();
        Context = (RobotContext)constructor.Invoke(constructor.GetParameters().Select(parameter =>
        {
            if (services.TryGetValue(parameter.Name ?? "", out var service)) return service;
            var invoke = parameter.ParameterType.GetMethod("Invoke") ?? throw new Exception("Expected delegate.");
            var args = invoke.GetParameters().Select(p => Expression.Parameter(p.ParameterType)).ToArray();
            var error = Expression.Constant(new InvalidOperationException($"Unexpected service: {parameter.Name}"));
            return Expression.Lambda(parameter.ParameterType, Expression.Throw(error, invoke.ReturnType), args).Compile();
        }).ToArray());
    }

    public GridTradingModule CreateModule(GridDirection direction, bool enabled = true, bool atr = false, bool previewEnabled = true)
    {
        Status = new GridStatus();
        return new(new GridConfiguration(enabled, direction, 20, 80, 20, atr, atr, 14, 10, 0), Status, previewEnabled);
    }

    public void ExpectMatchingAtrAmounts(double expected)
    {
        var distance = Math.Max(15, expected);
        if (Status.EffectiveTarget != expected || Status.DistanceAmount != distance)
            throw new Exception($"Expected target {expected} and distance {distance}, got {Status.EffectiveTarget}/{Status.DistanceAmount}.");
    }

    public void SeedLegacyPendingPreview()
    {
        foreach (var direction in new[] { "Buy", "Sell" })
        {
            var name = $"APFXUltimate.Preview.{direction}.Pending";
            Lines[name] = 1.1;
            _labels.Add(name + ".Label");
        }
    }

    public void ExpectCount(int count)
    {
        if (Lines.Count != count || _labels.Count != count)
            throw new Exception($"Expected {count} lines and labels, got {Lines.Count}/{_labels.Count}.");
    }

    public void ExpectPrice(string suffix, double expected)
    {
        var actual = Lines[$"APFXUltimate.Preview.{suffix}"];
        if (Math.Abs(actual - expected) > 1e-10)
            throw new Exception($"{suffix}: expected {expected}, got {actual}.");
    }

    public void ExpectRightOfCandle(string suffix)
    {
        var segment = _segments[$"APFXUltimate.Preview.{suffix}"];
        var candleTime = new DateTime(2026, 9, 16, 11, 59, 0, DateTimeKind.Utc);
        if (segment.Start <= candleTime || segment.Start - candleTime > TimeSpan.FromSeconds(15)
            || segment.End - segment.Start > TimeSpan.FromSeconds(90))
            throw new Exception($"{suffix} must be short and immediately to the right of the current candle.");
    }
}
