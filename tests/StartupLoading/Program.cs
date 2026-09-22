using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using cAlgo.API;
using cAlgo.Robots;

var fixture = new StartupFixture();
var newsState = new CalendarNewsState();
var newsSection = new CalendarNewsSection(11, newsState);
newsSection.CreateControl(fixture.Context);
ExpectNewsRows(newsSection, 0);

var eventTime = DateTimeOffset.UtcNow.AddHours(1);
var items = new[]
{
    new CalendarNewsItem(eventTime, "USD", "Test event 1", 3, null, "1.0", "0.9"),
    new CalendarNewsItem(eventTime.AddMinutes(30), "USD", "Test event 2", 2, null, null, null)
};
newsState.SetItems(items, DateTimeOffset.UtcNow, DateTime.UtcNow);
newsSection.Refresh(fixture.Context);
ExpectNewsRows(newsSection, 3); // One date heading and two events.

fixture.Store("APFXUltimateCalendarNewsCacheV1", JsonSerializer.Serialize(new
{
    SavedAtUtc = DateTimeOffset.UtcNow,
    HoursAhead = 168,
    MinimumImpact = CalendarMinimumImpact.Low,
    Items = items
}));
var restoredNews = new CalendarNewsState();
var calendar = new CalendarNewsModule(new CalendarNewsConfiguration("test-key", 168, 5, CalendarMinimumImpact.Low), restoredNews);
calendar.OnStart(fixture.Context);
if (restoredNews.Items.Count != 2 || fixture.Requests != 1 || !restoredNews.Status.Contains("Cached"))
    throw new Exception("Calendar cache was not displayed while refreshing.");

fixture.Store("APFXUltimateCalendarNewsCacheV1", JsonSerializer.Serialize(new
{
    SavedAtUtc = DateTimeOffset.UtcNow.AddHours(-7),
    HoursAhead = 168,
    MinimumImpact = CalendarMinimumImpact.Low,
    Items = items
}));
var staleNews = new CalendarNewsState();
new CalendarNewsModule(new CalendarNewsConfiguration("test-key", 168, 5, CalendarMinimumImpact.Low), staleNews).OnStart(fixture.Context);
if (staleNews.Items.Count != 0 || fixture.Requests != 2)
    throw new Exception("Expired calendar data was displayed or refresh was not started.");

fixture.Store("APFXUltimateTreasuryYieldsCacheV1", JsonSerializer.Serialize(new
{
    SavedAtUtc = DateTimeOffset.UtcNow,
    Yields = new[]
    {
        new TreasuryYield("2Y", DateTime.UtcNow.Date, 3.5, 3.4),
        new TreasuryYield("10Y", DateTime.UtcNow.Date, 4.0, 3.9),
        new TreasuryYield("30Y", DateTime.UtcNow.Date, 4.5, 4.4)
    }
}));
var yieldsState = new TreasuryYieldState();
var yields = new TreasuryYieldModule(new TreasuryYieldConfiguration("test-key", 60), yieldsState);
yields.OnStart(fixture.Context);
if (yieldsState.Yields.Count != 3 || fixture.Requests != 5 || !yieldsState.Status.Contains("Cached"))
    throw new Exception("Treasury cache was not displayed or all three requests were not started together.");

fixture.Store("APFXUltimateTreasuryYieldsCacheV1", JsonSerializer.Serialize(new
{
    SavedAtUtc = DateTimeOffset.UtcNow.AddHours(-48),
    Yields = yieldsState.Yields.Values.ToArray()
}));
var staleYields = new TreasuryYieldState();
new TreasuryYieldModule(new TreasuryYieldConfiguration("test-key", 60), staleYields).OnStart(fixture.Context);
if (staleYields.Yields.Count != 0 || fixture.Requests != 8)
    throw new Exception("Expired Treasury yields were displayed or all requests were not started.");

yields.OnStop(fixture.Context);
Console.WriteLine("Startup loading regression checks passed.");

static void ExpectNewsRows(CalendarNewsSection section, int expected)
{
    var rows = typeof(CalendarNewsSection).GetField("_newsRows", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(section) as System.Collections.ICollection;
    if (rows?.Count != expected)
        throw new Exception($"Expected {expected} allocated calendar rows, got {rows?.Count}.");
}

sealed class StartupFixture
{
    private readonly Dictionary<string, string> _storage = new();
    public int Requests { get; private set; }
    public RobotContext Context { get; }

    public StartupFixture()
    {
        var services = new Dictionary<string, Delegate>
        {
            ["log"] = (Action<string>)(_ => { }),
            ["getServerTime"] = (Func<DateTime>)(() => DateTime.UtcNow),
            ["convertUtcToChartTime"] = (Func<DateTime, DateTime>)(time => time),
            ["getLocalValue"] = (Func<string, string?>)(key => _storage.GetValueOrDefault(key)),
            ["setLocalValue"] = (Action<string, string>)((key, value) => _storage[key] = value),
            ["sendHttpRequest"] = (Action<HttpRequest, Action<HttpResponse>>)((_, _) => Requests++),
            ["beginInvokeOnMainThread"] = (Action<Action>)(action => action())
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

    public void Store(string key, string value) => _storage[key] = value;
}
