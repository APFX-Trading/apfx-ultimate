using System.Linq.Expressions;
using cAlgo.API;
using cAlgo.Robots;

var fixture = new VwapFixture();
var overlay = new DailyVwapOverlayModule(isEnabled: true);
fixture.PointCount = 400;
fixture.Now = fixture.Start.AddMinutes(399);
overlay.OnStart(fixture.Context);
if (fixture.Objects.Count > 9 * 59 + 1)
    throw new Exception($"VWAP drew {fixture.Objects.Count} startup objects; expected at most 532.");
fixture.AssertFourthBands();

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

if (fixture.Objects.Count > 9 * 59 + 1)
    throw new Exception($"VWAP retained {fixture.Objects.Count} chart objects; expected at most 532.");
fixture.AssertFourthBands();

fixture.Start = fixture.Start.AddDays(1);
fixture.Now = fixture.Start.AddMinutes(1);
fixture.PointCount = 2;
overlay.OnTimer(fixture.Context);
if (fixture.Objects.Count != 10)
    throw new Exception($"VWAP retained stale objects after the session changed: {fixture.Objects.Count}.");
fixture.AssertFourthBands();

overlay.SetEnabled(false);
if (fixture.Objects.Count != 0)
    throw new Exception("Disabling VWAP must remove all bands.");
overlay.SetEnabled(true);
fixture.AssertFourthBands();

overlay.OnStop(fixture.Context);
if (fixture.Objects.Count != 0)
    throw new Exception($"VWAP left {fixture.Objects.Count} chart objects after stopping.");

Console.WriteLine("VWAP overlay regression checks passed.");

sealed class VwapFixture
{
    public DateTime Start = new(2026, 9, 21, 0, 0, 0);
    public DateTime Now;
    public int PointCount = 2;
    public HashSet<string> Objects { get; } = new(StringComparer.Ordinal);
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
            ["getChartViewport"] = (Func<ChartViewport>)(() => new(Start, Now, 1, 2)),
            ["drawChartTrendLine"] = (Action<string, DateTime, double, DateTime, double, Color, int>)((name, _, _, _, endPrice, _, _) => { Objects.Add(name); _endPrices[name] = endPrice; }),
            ["drawChartText"] = (Action<string, string, DateTime, double, Color>)((name, _, _, _, _) => Objects.Add(name)),
            ["removeChartObject"] = (Action<string>)(name => { Objects.Remove(name); _endPrices.Remove(name); })
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
        IReadOnlyList<ChartTimePricePoint> Band(double offset) => Enumerable.Range(0, PointCount)
            .Select(index => new ChartTimePricePoint(Start.AddMinutes(index), 1.1 + offset + index * 0.00001))
            .ToArray();

        return new DailyVwapData(Band(0), Band(0.001), Band(-0.001), Band(0.002), Band(-0.002), Band(0.003), Band(-0.003), Band(0.004), Band(-0.004));
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
