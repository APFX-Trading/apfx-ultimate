using System.Linq.Expressions;
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
fixture.Market = fixture.Market with { AtrPips = 400 };
module.OnTimer(fixture.Context);
fixture.ExpectPrice("Buy.Target", 1.1142);

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
Console.WriteLine("Grid preview regression checks passed.");

sealed class PreviewFixture
{
    public MarketSnapshot Market = new("TEST", 1.1000, 1.1002, 0.0001, 0.0001,
        10000, 10000, 100, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
    public TradeVolumeInfo Volume = new(0.1, 10000, 100, 100);
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

    public GridTradingModule CreateModule(GridDirection direction, bool enabled = true, bool atr = false, bool previewEnabled = true) =>
        new(new GridConfiguration(enabled, direction, 20, 80, 20, atr, atr, 14, 10, 0), new GridStatus(), previewEnabled);

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
        var candleEnd = new DateTime(2026, 9, 16, 12, 0, 0, DateTimeKind.Utc);
        if (segment.Start <= candleEnd || segment.End - segment.Start >= TimeSpan.FromMinutes(12))
            throw new Exception($"{suffix} must be short and to the right of the current candle.");
    }
}
