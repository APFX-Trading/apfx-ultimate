using System.Linq.Expressions;
using cAlgo.API;
using cAlgo.Robots;

var fixture = new VwapFixture();
var overlay = new DailyVwapOverlayModule(isEnabled: true);
fixture.PointCount = 400;
fixture.Now = fixture.Start.AddMinutes(399);
overlay.OnStart(fixture.Context);
if (fixture.Objects.Count > 9 * 59 + 9)
    throw new Exception($"VWAP drew {fixture.Objects.Count} startup objects; expected at most 540.");
fixture.AssertFourthBands();
fixture.AssertLabels();

overlay.OnStop(fixture.Context);
fixture.PointCount = 2;
fixture.Now = fixture.Start.AddMinutes(1);
overlay = new DailyVwapOverlayModule(isEnabled: true);
overlay.OnStart(fixture.Context);

for (var minute = 3; minute <= 400; minute++)
{
    fixture.PointCount = minute;
    fixture.Now = fixture.Start.AddMinutes(minute - 1);
    overlay.OnTimer(fixture.Context);
}

if (fixture.Objects.Count > 9 * 59 + 9)
    throw new Exception($"VWAP retained {fixture.Objects.Count} chart objects; expected at most 540.");
fixture.AssertFourthBands();
fixture.AssertLabels();

var reads = fixture.DataReads;
fixture.Market = fixture.Market with { Bid = 1.105, Ask = 1.1052 };
overlay.OnTick(fixture.Context);
fixture.AssertLabels();
if (fixture.DataReads != reads)
    throw new Exception("Tick label updates must not recalculate the VWAP paths.");
fixture.Market = fixture.Market with { PipSize = 0.001 };
overlay.OnTick(fixture.Context);
fixture.AssertLabels();

fixture.Start = fixture.Start.AddDays(1);
fixture.Now = fixture.Start.AddMinutes(1);
fixture.PointCount = 2;
overlay.OnTimer(fixture.Context);
if (fixture.Objects.Count != 18)
    throw new Exception($"VWAP retained stale objects after the session changed: {fixture.Objects.Count}.");
fixture.AssertFourthBands();
fixture.AssertLabels();

overlay.SetEnabled(false);
if (fixture.Objects.Count != 0)
    throw new Exception("Disabling VWAP must remove all bands.");
overlay.OnTick(fixture.Context);
if (fixture.Objects.Count != 0 || fixture.Labels.Count != 0)
    throw new Exception("Tick updates must not restore disabled labels.");
overlay.SetEnabled(true);
fixture.AssertFourthBands();
fixture.AssertLabels();

fixture.Market = fixture.Market with { PipSize = 0 };
overlay.OnTick(fixture.Context);
if (fixture.Labels.Values.Any(text => !text.EndsWith(" | -- pips", StringComparison.Ordinal)))
    throw new Exception("Invalid pip size must display unavailable distance.");
fixture.Market = fixture.Market with { PipSize = 0.0001 };
fixture.PointCount = 0;
fixture.Now = fixture.Now.AddSeconds(5);
overlay.OnTimer(fixture.Context);
overlay.OnTick(fixture.Context);
if (fixture.Objects.Count != 0 || fixture.Labels.Count != 0)
    throw new Exception("Empty VWAP data must remove paths and labels.");
fixture.PointCount = 2;
fixture.Now = fixture.Now.AddSeconds(5);
overlay.OnTimer(fixture.Context);
fixture.AssertLabels();

overlay.OnStop(fixture.Context);
if (fixture.Objects.Count != 0)
    throw new Exception($"VWAP left {fixture.Objects.Count} chart objects after stopping.");

Console.WriteLine("VWAP overlay regression checks passed.");

sealed class VwapFixture
{
    public DateTime Start = new(2026, 9, 21, 0, 0, 0);
    public DateTime Now;
    public int PointCount = 2;
    public int DataReads;
    public MarketSnapshot Market = new() { Bid = 1.1, Ask = 1.1002, PipSize = 0.0001 };
    public HashSet<string> Objects { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, string> Labels { get; } = new(StringComparer.Ordinal);
    private readonly Dictionary<string, double> _endPrices = new(StringComparer.Ordinal);
    public RobotContext Context { get; }

    public VwapFixture()
    {
        Now = Start.AddMinutes(1);
        var services = new Dictionary<string, Delegate>
        {
            ["log"] = (Action<string>)(_ => { }),
            ["getServerTime"] = (Func<DateTime>)(() => Now),
            ["getDailyVwapData"] = (Func<DailyVwapData>)GetData,
            ["getMarketSnapshot"] = (Func<MarketSnapshot>)(() => Market),
            ["getChartViewport"] = (Func<ChartViewport>)(() => new(Start, Now, 1, 2)),
            ["drawChartTrendLine"] = (Action<string, DateTime, double, DateTime, double, Color, int>)((name, _, _, _, endPrice, _, _) => { Objects.Add(name); _endPrices[name] = endPrice; }),
            ["drawChartText"] = (Action<string, string, DateTime, double, Color>)((name, text, _, price, _) => { Objects.Add(name); Labels[name] = text; _endPrices[name] = price; }),
            ["removeChartObject"] = (Action<string>)(name => { Objects.Remove(name); _endPrices.Remove(name); Labels.Remove(name); })
        };

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

    private DailyVwapData GetData()
    {
        DataReads++;
        IReadOnlyList<ChartTimePricePoint> Band(double offset) => Enumerable.Range(0, PointCount)
            .Select(index => new ChartTimePricePoint(Start.AddMinutes(index), 1.1 + offset + index * 0.00001))
            .ToArray();

        return new DailyVwapData(Band(0), Band(0.001), Band(-0.001), Band(0.002), Band(-0.002), Band(0.003), Band(-0.003), Band(0.004), Band(-0.004));
    }

    public void AssertLabels()
    {
        if (Labels.Count != 9) throw new Exception($"Expected nine VWAP labels, got {Labels.Count}.");
        var bands = new[] { ("Vwap", "Daily VWAP", 0d),
            ("UpperOne", "VWAP +1 SD", 0.001), ("LowerOne", "VWAP -1 SD", -0.001),
            ("UpperTwo", "VWAP +2 SD", 0.002), ("LowerTwo", "VWAP -2 SD", -0.002),
            ("UpperThree", "VWAP +3 SD", 0.003), ("LowerThree", "VWAP -3 SD", -0.003),
            ("UpperFour", "VWAP +4 SD", 0.004), ("LowerFour", "VWAP -4 SD", -0.004) };
        foreach (var (key, title, offset) in bands)
        {
            var name = $"APFXUltimate.Graph.Vwap.{key}.Label";
            var price = 1.1 + offset + (PointCount - 1) * 0.00001;
            var expected = FormattableString.Invariant($"{title} | {price:F0} | {Math.Abs(price - Market.Bid) / Market.PipSize:F0} pips");
            if (Labels[name] != expected || Math.Abs(_endPrices[name] - price) > 1e-12)
                throw new Exception($"{key}: expected {expected}, got {Labels[name]}.");
        }
    }

    public void AssertFourthBands()
    {
        foreach (var (key, offset) in new[] { ("UpperFour", 0.004), ("LowerFour", -0.004) })
        {
            var points = _endPrices.Where(pair => pair.Key.Contains($".{key}.")).ToArray();
            var expected = 1.1 + offset + (PointCount - 1) * 0.00001;
            if (points.Length == 0 || !points.Any(pair => Math.Abs(pair.Value - expected) < 1e-12))
                throw new Exception($"{key} was not rendered at the current fourth-band price.");
        }
    }
}
