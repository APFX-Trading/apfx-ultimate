using System.Linq.Expressions;
using System.Reflection;
using System.Globalization;
using cAlgo.API;
using cAlgo.Robots;

CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
var fixture = new DrawdownFixture();
var statistics = new AccountStatisticsSection(DateTime.MinValue);
statistics.CreateControl(fixture.Context, 10);
statistics.TrackDrawdown(fixture.Context);
fixture.ExpectState("100|0|100|100", writes: 1, flushes: 0);

fixture.Now = fixture.Now.AddMinutes(1);
statistics.TrackDrawdown(fixture.Context);
fixture.ExpectState("100|0|100|100", writes: 1, flushes: 0);

fixture.Equity = 110;
statistics.TrackDrawdown(fixture.Context);
fixture.ExpectState("110|0|100|100", writes: 2, flushes: 0);

fixture.Equity = 90;
statistics.TrackDrawdown(fixture.Context);
fixture.ExpectState("110|20|110|90", writes: 3, flushes: 0);
statistics.RefreshIfDue(fixture.Context);
ExpectEveryPeriod(statistics, "20.00 (18.2 %)");

fixture.Now = fixture.Now.AddSeconds(16);
fixture.Equity = 80;
statistics.TrackDrawdown(fixture.Context);
statistics.RefreshIfDue(fixture.Context);
ExpectEveryPeriod(statistics, "30.00 (27.3 %)");
if (fixture.HistoryReads != 1) throw new Exception("Equity refresh unnecessarily reloads trade history.");

fixture.Now = fixture.Now.AddSeconds(16);
fixture.Equity = 200;
statistics.TrackDrawdown(fixture.Context);
statistics.RefreshIfDue(fixture.Context);
ExpectEveryPeriod(statistics, "30.00 (27.3 %)"); // Later peak must not change the old loss percentage.

statistics.FlushDrawdownState(fixture.Context);
fixture.ExpectState("200|30|110|80", writes: 6, flushes: 1);

var restored = new AccountStatisticsSection(DateTime.MinValue);
restored.CreateControl(fixture.Context, 10);
restored.TrackDrawdown(fixture.Context);
restored.Refresh(fixture.Context);
ExpectEveryPeriod(restored, "30.00 (27.3 %)");
fixture.ExpectState("200|30|110|80", writes: 7, flushes: 1);

fixture.Now = fixture.Now.Date.AddDays(1).AddHours(12);
fixture.Equity = 150;
restored.TrackDrawdown(fixture.Context);
restored.Refresh(fixture.Context);
ExpectPeriod(restored, "Today", "0.00 (0.0 %)");
foreach (var period in new[] { "Week", "Month", "3 Months", "6 Months", "12 Months" })
    ExpectPeriod(restored, period, "50.00 (25.0 %)");
var nextRestart = new AccountStatisticsSection(DateTime.MinValue);
nextRestart.CreateControl(fixture.Context, 10);
nextRestart.TrackDrawdown(fixture.Context);
nextRestart.Refresh(fixture.Context);
ExpectPeriod(nextRestart, "12 Months", "50.00 (25.0 %)");

var cutoff = new AccountStatisticsSection(fixture.Now.AddMinutes(1));
cutoff.CreateControl(fixture.Context, 10);
cutoff.TrackDrawdown(fixture.Context);
cutoff.Refresh(fixture.Context);
ExpectEveryPeriod(cutoff, "0.00");
fixture.Now = fixture.Now.AddMinutes(2);
cutoff.TrackDrawdown(fixture.Context);
fixture.Equity = 140;
cutoff.TrackDrawdown(fixture.Context);
cutoff.Refresh(fixture.Context);
ExpectEveryPeriod(cutoff, "10.00 (6.7 %)");
var restoredCutoff = new AccountStatisticsSection(fixture.Now.AddMinutes(-1));
restoredCutoff.CreateControl(fixture.Context, 10);
restoredCutoff.TrackDrawdown(fixture.Context);
restoredCutoff.Refresh(fixture.Context);
ExpectEveryPeriod(restoredCutoff, "10.00 (6.7 %)");

var legacy = new DrawdownFixture();
legacy.Seed("110|20");
var legacyStatistics = new AccountStatisticsSection(DateTime.MinValue);
legacyStatistics.CreateControl(legacy.Context, 10);
legacyStatistics.TrackDrawdown(legacy.Context);
legacyStatistics.Refresh(legacy.Context);
ExpectEveryPeriod(legacyStatistics, "20.00 (18.2 %)");
foreach (var value in new[] { "Infinity|20", "100|NaN", "broken", "100|20|Infinity|80" })
{
    var invalid = new DrawdownFixture();
    invalid.Seed(value);
    var clean = new AccountStatisticsSection(DateTime.MinValue);
    clean.TrackDrawdown(invalid.Context);
    invalid.ExpectState("100|0|100|100", 1, 0);
}

var unavailable = new DrawdownFixture { StorageUnavailable = true };
var memory = new AccountStatisticsSection(DateTime.MinValue);
memory.CreateControl(unavailable.Context, 10);
memory.TrackDrawdown(unavailable.Context);
unavailable.Equity = 90;
memory.TrackDrawdown(unavailable.Context);
memory.Refresh(unavailable.Context);
ExpectEveryPeriod(memory, "10.00 (10.0 %)");
memory.FlushDrawdownState(unavailable.Context);

foreach (var limits in new[] { (10d, 50d, 50d), (10d, 200d, 100d), (10d, 0d, 100d), (0d, 50d, 50d) })
{
    var status = new GridStatus();
    var grid = new GridTradingModule(new GridConfiguration(true, GridDirection.Both, 20, 80, 20, false, false, 14, limits.Item1, limits.Item2), status);
    typeof(GridTradingModule).GetField("_activeDirection", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(grid, TradeType.Buy);
    typeof(GridTradingModule).GetField("_startingEquity", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(grid, 1000d);
    var update = typeof(GridTradingModule).GetMethod("UpdateDrawdownStatus", BindingFlags.NonPublic | BindingFlags.Instance)!;
    var reached = typeof(GridTradingModule).GetMethod("HasReachedMaxDrawdown", BindingFlags.NonPublic | BindingFlags.Instance)!;
    var market = new MarketSnapshot() with { Equity = 1000 - limits.Item3 + 1 };
    update.Invoke(grid, new object[] { market });
    if (status.EffectiveDrawdownLimit != limits.Item3 || (bool)reached.Invoke(grid, null)!)
        throw new Exception("Incorrect limit or premature stop.");
    update.Invoke(grid, new object[] { market with { Equity = 1000 - limits.Item3 } });
    if (!(bool)reached.Invoke(grid, null)! || status.DrawdownRemaining != 0)
        throw new Exception("Limit must trigger at equality.");
}

var historyFixture = new DrawdownFixture();
historyFixture.Trades.Add(TestApiProxy.Make<HistoricalTrade>(new()
{
    ["ClosingTime"] = historyFixture.Now.AddMinutes(-3), ["Balance"] = 90d, ["NetProfit"] = -10d
}));
historyFixture.Trades.Add(TestApiProxy.Make<HistoricalTrade>(new()
{
    ["ClosingTime"] = historyFixture.Now.AddMinutes(-2), ["Balance"] = 80d, ["NetProfit"] = -10d
}));
historyFixture.Trades.Add(TestApiProxy.Make<HistoricalTrade>(new()
{
    ["ClosingTime"] = historyFixture.Now.AddMinutes(-1), ["Balance"] = 200d, ["NetProfit"] = 120d
}));
var historical = new AccountStatisticsSection(DateTime.MinValue);
historical.CreateControl(historyFixture.Context, 10);
historical.Refresh(historyFixture.Context);
ExpectEveryPeriod(historical, "20.00 (20.0 %)");

var boundaryFixture = new DrawdownFixture { Now = new DateTime(2026, 10, 12, 12, 0, 0) };
boundaryFixture.Seed(new DateTime(2026, 7, 11), "500|400|500|100");
boundaryFixture.Seed(new DateTime(2026, 7, 12), "100|0|100|100");
var boundaries = new AccountStatisticsSection(DateTime.MinValue);
boundaries.CreateControl(boundaryFixture.Context, 10);
boundaries.TrackDrawdown(boundaryFixture.Context);
boundaries.Refresh(boundaryFixture.Context);
ExpectPeriod(boundaries, "3 Months", "0.00 (0.0 %)");
ExpectPeriod(boundaries, "6 Months", "400.00 (80.0 %)");
boundaryFixture.Equity = 90;
boundaries.TrackDrawdown(boundaryFixture.Context);
boundaryFixture.Now = boundaryFixture.Now.AddDays(1);
boundaryFixture.Equity = 100;
boundaries.TrackDrawdown(boundaryFixture.Context);
boundaries.Refresh(boundaryFixture.Context);
ExpectPeriod(boundaries, "Today", "0.00 (0.0 %)");
ExpectPeriod(boundaries, "3 Months", "10.00 (10.0 %)");

var closingFixture = new DrawdownFixture { Equity = 950 };
closingFixture.Positions.Add(TestApiProxy.Make<Position>(new() { ["TradeType"] = TradeType.Buy }));
closingFixture.Positions.Add(TestApiProxy.Make<Position>(new() { ["TradeType"] = TradeType.Sell }));
closingFixture.PendingOrders.Add(TestApiProxy.Make<PendingOrder>(new()));
closingFixture.SeedBasket("1000");
var closingStatus = new GridStatus();
var closingGrid = new GridTradingModule(new GridConfiguration(true, GridDirection.Both, 20, 80, 20, false, false, 14, 10, 50), closingStatus);
typeof(GridTradingModule).GetField("_activeDirection", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(closingGrid, TradeType.Buy);
typeof(GridTradingModule).GetField("_startingEquity", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(closingGrid, 1000d);
closingGrid.OnTimer(closingFixture.Context);
closingGrid.OnTimer(closingFixture.Context);
if (closingFixture.CloseAttempts != 4 || !closingStatus.IsBasketActive || closingFixture.BasketBaseline != "1000")
    throw new Exception("Failed closes must retry both directions and retain the baseline.");
closingFixture.AllowClose = true;
closingGrid.OnTimer(closingFixture.Context);
closingGrid.OnTimer(closingFixture.Context);
if (closingFixture.Positions.Count != 0 || !closingStatus.IsBasketActive || closingFixture.BasketBaseline != "1000" || closingFixture.CancelAttempts != 4)
    throw new Exception("Pending cancellation failures must prevent rearming and retain the baseline.");
closingFixture.AllowCancel = true;
closingGrid.OnTimer(closingFixture.Context);
closingGrid.OnTimer(closingFixture.Context);
if (closingFixture.Positions.Count != 0 || closingStatus.IsBasketActive || closingFixture.BasketBaseline != "0")
    throw new Exception("Rearm only after every position has closed.");

var timerFixture = new DrawdownFixture { Equity = 940 };
timerFixture.Positions.Add(TestApiProxy.Make<Position>(new()
{
    ["TradeType"] = TradeType.Buy, ["NetProfit"] = -60d, ["Label"] = "manual"
}));
for (var restart = 0; restart < 2; restart++)
{
    var timerStatus = new GridStatus();
    var timerGrid = new GridTradingModule(new GridConfiguration(true, GridDirection.Both, 20, 80, 20, false, false, 14, 10, 50), timerStatus);
    timerGrid.OnTimer(timerFixture.Context);
    if (timerFixture.CloseAttempts != restart + 1 || timerStatus.StartingEquity != 1000 || timerFixture.BasketBaseline != "1000")
        throw new Exception("Timer must adopt/restore the basket and enforce DD without a chart tick.");
}
var accountFixture = new DrawdownFixture();
accountFixture.Trades.Add(TestApiProxy.Make<HistoricalTrade>(new()
{
    ["ClosingTime"] = accountFixture.Now.AddMinutes(-1), ["Balance"] = 90d, ["NetProfit"] = -10d
}));
var accountStatistics = new AccountStatisticsSection(DateTime.MinValue);
accountStatistics.CreateControl(accountFixture.Context, 10);
accountStatistics.TrackDrawdown(accountFixture.Context);
accountFixture.Equity = 70;
accountStatistics.TrackDrawdown(accountFixture.Context);
accountStatistics.Refresh(accountFixture.Context);
ExpectEveryPeriod(accountStatistics, "30.00 (30.0 %)");
var accountOne = accountFixture.Identity;
foreach (var identity in new[]
{
    new TradingAccountIdentity("Test Broker", 2, false),
    new TradingAccountIdentity("Other Broker", 1, false),
    new TradingAccountIdentity("Test Broker", 1, true)
})
{
    accountFixture.BrokerName = identity.BrokerName;
    accountFixture.AccountNumber = identity.AccountNumber;
    accountFixture.IsLive = identity.IsLive;
    accountFixture.Equity = 1000;
    accountFixture.Trades[0] = TestApiProxy.Make<HistoricalTrade>(new()
    {
        ["ClosingTime"] = accountFixture.Now.AddMinutes(-1), ["Balance"] = 995d, ["NetProfit"] = -5d
    });
    accountStatistics.TrackDrawdown(accountFixture.Context);
    accountStatistics.RefreshIfDue(accountFixture.Context); // Account change must bypass refresh throttle/history count cache.
    ExpectEveryPeriod(accountStatistics, "5.00 (0.5 %)");
    accountFixture.Equity = 990;
    accountStatistics.TrackDrawdown(accountFixture.Context);
    accountStatistics.Refresh(accountFixture.Context);
    ExpectEveryPeriod(accountStatistics, "10.00 (1.0 %)");
    var parallelFixture = new DrawdownFixture(accountFixture.Storage) { Equity = 990, BrokerName = identity.BrokerName, AccountNumber = identity.AccountNumber, IsLive = identity.IsLive };
    var parallel = new AccountStatisticsSection(DateTime.MinValue);
    parallel.CreateControl(parallelFixture.Context, 10);
    parallel.TrackDrawdown(parallelFixture.Context);
    parallel.Refresh(parallelFixture.Context);
    ExpectEveryPeriod(parallel, "10.00 (1.0 %)");
}
accountFixture.BrokerName = accountOne.BrokerName;
accountFixture.AccountNumber = accountOne.AccountNumber;
accountFixture.IsLive = accountOne.IsLive;
accountFixture.Equity = 70;
accountFixture.Trades.Clear();
accountStatistics.TrackDrawdown(accountFixture.Context);
accountStatistics.Refresh(accountFixture.Context);
ExpectEveryPeriod(accountStatistics, "30.00 (30.0 %)");

var partialAccounts = new DrawdownFixture();
var partialFrom = partialAccounts.Now.AddHours(-1);
var partialStatistics = new AccountStatisticsSection(partialFrom);
partialStatistics.CreateControl(partialAccounts.Context, 10);
partialStatistics.TrackDrawdown(partialAccounts.Context);
partialAccounts.Equity = 80;
partialStatistics.TrackDrawdown(partialAccounts.Context);
partialAccounts.AccountNumber = 2;
partialStatistics.TrackDrawdown(partialAccounts.Context);
partialStatistics.Refresh(partialAccounts.Context);
ExpectEveryPeriod(partialStatistics, "0.00");
partialAccounts.Equity = 70;
partialStatistics.TrackDrawdown(partialAccounts.Context);
partialStatistics.Refresh(partialAccounts.Context);
ExpectEveryPeriod(partialStatistics, "10.00 (12.5 %)");
partialAccounts.AccountNumber = 1;
partialAccounts.Equity = 80;
partialStatistics.TrackDrawdown(partialAccounts.Context);
partialStatistics.Refresh(partialAccounts.Context);
ExpectEveryPeriod(partialStatistics, "20.00 (20.0 %)");

var scopedGridStatus = new GridStatus();
var scopedGrid = new GridTradingModule(new GridConfiguration(true, GridDirection.Both, 20, 80, 20, false, false, 14, 10, 50), scopedGridStatus);
scopedGrid.OnTimer(timerFixture.Context);
timerFixture.AccountNumber = 2;
timerFixture.Equity = 1940;
scopedGrid.OnTimer(timerFixture.Context);
if (scopedGridStatus.StartingEquity != 2000 || timerFixture.BasketBaseline != "2000")
    throw new Exception("Account switch must discard the old active/closing basket and infer its own baseline.");
timerFixture.AccountNumber = 1;
timerFixture.Equity = 940;
scopedGrid.OnTimer(timerFixture.Context);
if (scopedGridStatus.StartingEquity != 1000 || timerFixture.BasketBaseline != "1000")
    throw new Exception("Returning to the original account must restore only its own baseline.");

var unscopedFixture = new DrawdownFixture { Equity = 940 };
unscopedFixture.Storage["APFXUltimateDailyDrawdown" + unscopedFixture.Now.ToString("yyyyMMdd")] = "100000|90000";
unscopedFixture.Storage["APFXUltimateGridBasketEquityV2TESTBuy"] = "100000";
var unscopedStatistics = new AccountStatisticsSection(DateTime.MinValue);
unscopedStatistics.CreateControl(unscopedFixture.Context, 10);
unscopedStatistics.TrackDrawdown(unscopedFixture.Context);
unscopedStatistics.Refresh(unscopedFixture.Context);
ExpectEveryPeriod(unscopedStatistics, "0.00 (0.0 %)");
unscopedFixture.Positions.Add(TestApiProxy.Make<Position>(new() { ["TradeType"] = TradeType.Buy, ["NetProfit"] = -60d, ["Label"] = "manual" }));
var unscopedStatus = new GridStatus();
new GridTradingModule(new GridConfiguration(true, GridDirection.Both, 20, 80, 20, false, false, 14, 10, 50), unscopedStatus).OnTimer(unscopedFixture.Context);
if (unscopedStatus.StartingEquity != 1000) throw new Exception("Unowned legacy baseline must be ignored.");
foreach (var key in accountFixture.Storage.Keys.Concat(unscopedFixture.Storage.Keys.Where(key => key.StartsWith("DD3"))))
    if (key.Length > 50 || !key.All(char.IsLetterOrDigit)) throw new Exception("Invalid cTrader storage key.");
var longKey = new TradingAccountIdentity(new string('B', 200), long.MaxValue, true).DrawdownStorageKey(new string('S', 200));
if (longKey.Length > 50 || !longKey.All(char.IsLetterOrDigit)) throw new Exception("Long account/symbol key exceeds platform limits.");
Console.WriteLine("Drawdown persistence and trading-account isolation regression checks passed.");

static void ExpectEveryPeriod(AccountStatisticsSection section, string expected)
{
    foreach (var period in new[] { "Today", "Week", "Month", "3 Months", "6 Months", "12 Months" })
        ExpectPeriod(section, period, expected);
}
static void ExpectPeriod(AccountStatisticsSection section, string period, string expected)
{
    var cells = (Dictionary<string, TextBlock>)typeof(AccountStatisticsSection)
        .GetField("_drawdownCells", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(section)!;
    if (cells[period].Text != expected) throw new Exception($"{period}: expected {expected}, got {cells[period].Text}.");
}

sealed class DrawdownFixture
{
    public readonly Dictionary<string, string> Storage;
    private Dictionary<string, string> _storage => Storage;
    public string BrokerName = "Test Broker";
    public long AccountNumber = 1;
    public bool IsLive;
    public TradingAccountIdentity Identity => new(BrokerName, AccountNumber, IsLive);
    public DateTime Now = new(2026, 9, 21, 12, 0, 0);
    public double Equity = 100;
    public int Writes { get; private set; }
    public int Flushes { get; private set; }
    public int HistoryReads { get; private set; }
    public bool StorageUnavailable;
    public List<HistoricalTrade> Trades = new();
    public List<Position> Positions = new();
    public List<PendingOrder> PendingOrders = new();
    public int CloseAttempts;
    public int CancelAttempts;
    public bool AllowClose;
    public bool AllowCancel;
    public RobotContext Context { get; }

    public DrawdownFixture(Dictionary<string, string>? storage = null)
    {
        Storage = storage ?? new();
        var services = new Dictionary<string, Delegate>
        {
            ["log"] = (Action<string>)(_ => { }),
            ["getServerTime"] = (Func<DateTime>)(() => Now),
            ["getAccountEquity"] = (Func<double>)(() => Equity),
            ["getAccountSummary"] = (Func<AccountSummary>)(() => new AccountSummary(Equity, Equity, 0, Equity, "USD", BrokerName, AccountNumber, IsLive)),
            ["getHistoricalTrades"] = (Func<IReadOnlyList<HistoricalTrade>>)(() => { HistoryReads++; return Trades.ToArray(); }),
            ["getHistoricalTradeCount"] = (Func<int>)(() => Trades.Count),
            ["getMarketSnapshot"] = (Func<MarketSnapshot>)(() => new MarketSnapshot() with { Equity = Equity, SymbolName = "TEST" }),
            ["getChartPositions"] = (Func<TradeType, IReadOnlyList<Position>>)(direction => Positions.Where(p => p.TradeType == direction).ToArray()),
            ["getGridPendingOrders"] = (Func<string, IReadOnlyList<PendingOrder>>)(_ => PendingOrders.ToArray()),
            ["removeChartObject"] = (Action<string>)(_ => { }),
            ["closePosition"] = (Func<Position, TradeResult>)(position => { CloseAttempts++; if (AllowClose) Positions.Remove(position); return null!; }),
            ["cancelPendingOrder"] = (Func<PendingOrder, TradeResult>)(order => { CancelAttempts++; if (AllowCancel) PendingOrders.Remove(order); return null!; }),
            ["getLocalValue"] = (Func<string, string?>)(key => StorageUnavailable ? throw new ArgumentException("Storage unavailable") : _storage.GetValueOrDefault(key)),
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

    public void Seed(string value) => Seed(Now.Date, value);
    public void Seed(DateTime date, string value) => _storage[Identity.DrawdownStorageKey("APFXUltimateDailyDrawdown" + date.ToString("yyyyMMdd"))] = value;
    private string BasketKey => Identity.DrawdownStorageKey("APFXUltimateGridBasketEquityV2" + System.Text.Json.JsonSerializer.Serialize(new { Symbol = "TEST", Direction = TradeType.Buy }));
    public void SeedBasket(string value) => _storage[BasketKey] = value;
    public string? BasketBaseline => _storage.GetValueOrDefault(BasketKey);

    public void ExpectState(string expected, int writes, int flushes)
    {
        var key = Identity.DrawdownStorageKey("APFXUltimateDailyDrawdown" + Now.ToString("yyyyMMdd"));
        if (_storage.GetValueOrDefault(key) != expected || Writes != writes || Flushes != flushes)
            throw new Exception($"Expected {expected} with {writes} writes and {flushes} flushes; got {_storage.GetValueOrDefault(key)} with {Writes} writes and {Flushes} flushes.");
    }
}

public class TestApiProxy : DispatchProxy
{
    private Dictionary<string, object> _values = new();
    public static T Make<T>(Dictionary<string, object> values) where T : class
    {
        var proxy = Create<T, TestApiProxy>();
        ((TestApiProxy)(object)proxy)._values = values;
        return proxy;
    }
    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        if (method is not null && method.Name.StartsWith("get_") && _values.TryGetValue(method.Name[4..], out var value)) return value;
        throw new Exception($"Unexpected API call: {method?.Name}");
    }
}
