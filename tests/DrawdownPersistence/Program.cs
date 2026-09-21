using System.Linq.Expressions;
using cAlgo.Robots;

var fixture = new DrawdownFixture();
var statistics = new AccountStatisticsSection(DateTime.MinValue);
statistics.TrackDrawdown(fixture.Context);
fixture.ExpectState("100|0", writes: 1, flushes: 0);

fixture.Now = fixture.Now.AddMinutes(1);
statistics.TrackDrawdown(fixture.Context);
fixture.ExpectState("100|0", writes: 1, flushes: 0);

fixture.Equity = 110;
statistics.TrackDrawdown(fixture.Context);
fixture.ExpectState("110|0", writes: 2, flushes: 0);

fixture.Equity = 90;
statistics.TrackDrawdown(fixture.Context);
fixture.ExpectState("110|20", writes: 3, flushes: 0);

statistics.FlushDrawdownState(fixture.Context);
fixture.ExpectState("110|20", writes: 4, flushes: 1);

var restored = new AccountStatisticsSection(DateTime.MinValue);
restored.TrackDrawdown(fixture.Context);
fixture.ExpectState("110|20", writes: 5, flushes: 1);
Console.WriteLine("Drawdown persistence regression checks passed.");

sealed class DrawdownFixture
{
    private readonly Dictionary<string, string> _storage = new();
    public DateTime Now = new(2026, 9, 21, 12, 0, 0);
    public double Equity = 100;
    public int Writes { get; private set; }
    public int Flushes { get; private set; }
    public RobotContext Context { get; }

    public DrawdownFixture()
    {
        var services = new Dictionary<string, Delegate>
        {
            ["log"] = (Action<string>)(_ => { }),
            ["getServerTime"] = (Func<DateTime>)(() => Now),
            ["getAccountEquity"] = (Func<double>)(() => Equity),
            ["getLocalValue"] = (Func<string, string?>)(key => _storage.GetValueOrDefault(key)),
            ["setLocalValue"] = (Action<string, string>)((key, value) => { _storage[key] = value; Writes++; }),
            ["flushLocalValues"] = (Action)(() => Flushes++)
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

    public void ExpectState(string expected, int writes, int flushes)
    {
        var key = "APFXUltimateDailyDrawdown" + Now.ToString("yyyyMMdd");
        if (_storage.GetValueOrDefault(key) != expected || Writes != writes || Flushes != flushes)
            throw new Exception($"Expected {expected} with {writes} writes and {flushes} flushes; got {_storage.GetValueOrDefault(key)} with {Writes} writes and {Flushes} flushes.");
    }
}
