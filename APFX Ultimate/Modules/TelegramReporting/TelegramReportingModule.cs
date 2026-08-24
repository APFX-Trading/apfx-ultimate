using System.Text;

namespace cAlgo.Robots;

/// <summary>Sends scheduled account-statistics reports and optional close alerts through Telegram or a webhook.</summary>
public sealed class TelegramReportingModule : IRobotModule
{
    private const string StoragePrefix = "APFXUltimate.Telegram.";
    private readonly TelegramReportingConfiguration _configuration;
    private readonly HashSet<string> _symbols;
    private readonly HashSet<string> _labels;
    private string _dailyKey = string.Empty;
    private string _weeklyKey = string.Empty;
    private string _monthlyKey = string.Empty;
    private int _lastClosingDealId;
    private bool _sending;

    public TelegramReportingModule(TelegramReportingConfiguration configuration)
    {
        _configuration = configuration;
        _symbols = ParseFilter(configuration.SymbolFilter);
        _labels = ParseFilter(configuration.LabelFilter);
    }

    public string Name => nameof(TelegramReportingModule);

    public void OnStart(RobotContext context)
    {
        if (!_configuration.Enabled)
        {
            return;
        }

        _dailyKey = context.GetLocalValue(StoragePrefix + "Daily") ?? string.Empty;
        _weeklyKey = context.GetLocalValue(StoragePrefix + "Weekly") ?? string.Empty;
        _monthlyKey = context.GetLocalValue(StoragePrefix + "Monthly") ?? string.Empty;
        _lastClosingDealId = int.TryParse(context.GetLocalValue(StoragePrefix + "LastClosingDeal"), out var savedDeal)
            ? savedDeal
            : context.GetHistoricalTrades().Select(trade => trade.ClosingDealId).DefaultIfEmpty(0).Max();
        context.Log("Telegram reporting module armed.");
    }

    public void OnTick(RobotContext context) { }

    public void OnTimer(RobotContext context)
    {
        if (!_configuration.Enabled || _sending)
        {
            return;
        }

        var now = ToLocal(context.ServerTimeUtc);
        CheckDaily(context, now);
        CheckWeekly(context, now);
        CheckMonthly(context, now);
        CheckClosedTrades(context);
    }

    public void OnStop(RobotContext context) { }

    private void CheckDaily(RobotContext context, DateTime now)
    {
        if (!_configuration.EnableDaily)
        {
            return;
        }

        var scheduled = now.Date.AddHours(_configuration.DailyHour).AddMinutes(_configuration.DailyMinute);
        if (now < scheduled)
        {
            return;
        }

        var end = scheduled.Date;
        var start = end.AddDays(-1);
        var key = "D" + start.ToString("yyyyMMdd");
        if (_dailyKey != key)
        {
            SendPeriod(context, "DAILY", start, end, key, value => _dailyKey = value, StoragePrefix + "Daily");
        }
    }

    private void CheckWeekly(RobotContext context, DateTime now)
    {
        if (!_configuration.EnableWeekly)
        {
            return;
        }

        var daysSinceScheduledDay = ((int)now.DayOfWeek - (int)_configuration.WeeklyDay + 7) % 7;
        var scheduled = now.Date.AddDays(-daysSinceScheduledDay)
            .AddHours(_configuration.WeeklyHour)
            .AddMinutes(_configuration.WeeklyMinute);
        if (now < scheduled)
        {
            scheduled = scheduled.AddDays(-7);
        }

        var end = StartOfWeek(scheduled.Date);
        var start = end.AddDays(-7);
        var key = "W" + start.ToString("yyyyMMdd");
        if (_weeklyKey != key)
        {
            SendPeriod(context, "WEEKLY", start, end, key, value => _weeklyKey = value, StoragePrefix + "Weekly");
        }
    }

    private void CheckMonthly(RobotContext context, DateTime now)
    {
        if (!_configuration.EnableMonthly)
        {
            return;
        }

        var scheduled = new DateTime(now.Year, now.Month, _configuration.MonthlyDay, _configuration.MonthlyHour, _configuration.MonthlyMinute, 0);
        if (now < scheduled)
        {
            scheduled = scheduled.AddMonths(-1);
        }

        var end = new DateTime(scheduled.Year, scheduled.Month, 1);
        var start = end.AddMonths(-1);
        var key = "M" + start.ToString("yyyyMM");
        if (_monthlyKey != key)
        {
            SendPeriod(context, "MONTHLY", start, end, key, value => _monthlyKey = value, StoragePrefix + "Monthly");
        }
    }

    private void SendPeriod(RobotContext context, string period, DateTime localStart, DateTime localEnd, string key, Action<string> updateKey, string storageKey)
    {
        var report = CalculateReport(context, FromLocal(localStart), FromLocal(localEnd));
        Send(context, FormatReport(context, period, localStart, localEnd, report), success =>
        {
            if (!success)
            {
                return;
            }

            updateKey(key);
            context.SetLocalValue(storageKey, key);
            context.FlushLocalValues();
        });
    }

    private void CheckClosedTrades(RobotContext context)
    {
        if (!_configuration.AlertClosedTrades)
        {
            return;
        }

        var trades = context.GetHistoricalTrades()
            .Where(trade => trade.ClosingDealId > _lastClosingDealId && IsIncluded(trade))
            .OrderBy(trade => trade.ClosingDealId)
            .ToArray();
        if (trades.Length == 0)
        {
            return;
        }

        var maxClosingDealId = trades[^1].ClosingDealId;
        var text = new StringBuilder("<b>APFX Trades Closed</b>\n");
        text.AppendLine("Positions: <b>" + trades.Length + "</b>");
        text.AppendLine("Combined net: <b>" + FormatMoney(context, trades.Sum(trade => trade.NetProfit)) + "</b>\n");
        foreach (var trade in trades.Take(10))
        {
            text.AppendLine("<b>" + Escape(trade.SymbolName) + " " + trade.TradeType + "</b>");
            text.AppendLine("Net: " + FormatMoney(context, trade.NetProfit) + " | Pips: " + trade.Pips.ToString("0.0"));
            text.AppendLine("Closed: " + ToLocal(trade.ClosingTime).ToString("dd MMM yyyy HH:mm") + "\n");
        }

        Send(context, Limit(text.ToString()), success =>
        {
            if (!success)
            {
                return;
            }

            _lastClosingDealId = maxClosingDealId;
            context.SetLocalValue(StoragePrefix + "LastClosingDeal", maxClosingDealId.ToString());
            context.FlushLocalValues();
        });
    }

    private Report CalculateReport(RobotContext context, DateTime startUtc, DateTime endUtc)
    {
        var trades = context.GetHistoricalTrades()
            .Where(trade => trade.ClosingTime >= startUtc && trade.ClosingTime < endUtc && IsIncluded(trade))
            .OrderBy(trade => trade.ClosingTime)
            .ToArray();
        var winningTrades = trades.Where(trade => trade.NetProfit > 0).ToArray();
        var losingTrades = trades.Where(trade => trade.NetProfit < 0).ToArray();
        var netProfit = trades.Sum(trade => trade.NetProfit);
        var grossProfit = trades.Sum(trade => trade.GrossProfit);
        var commissions = trades.Sum(trade => trade.Commissions);
        var swap = trades.Sum(trade => trade.Swap);
        var peakProfit = 0d;
        var runningProfit = 0d;
        var maxDrawdown = 0d;
        foreach (var trade in trades)
        {
            runningProfit += trade.NetProfit;
            peakProfit = Math.Max(peakProfit, runningProfit);
            maxDrawdown = Math.Max(maxDrawdown, peakProfit - runningProfit);
        }

        return new Report(
            trades.Length,
            winningTrades.Length,
            losingTrades.Length,
            trades.Length - winningTrades.Length - losingTrades.Length,
            netProfit,
            grossProfit,
            commissions,
            swap,
            trades.Sum(trade => trade.Pips),
            trades.Length == 0 ? 0 : 100d * winningTrades.Length / trades.Length,
            winningTrades.Length == 0 ? 0 : winningTrades.Average(trade => trade.NetProfit),
            losingTrades.Length == 0 ? 0 : losingTrades.Average(trade => trade.NetProfit),
            maxDrawdown,
            trades);
    }

    private string FormatReport(RobotContext context, string period, DateTime localStart, DateTime localEnd, Report report)
    {
        var account = context.GetAccountSummary();
        var builder = new StringBuilder();
        builder.AppendLine("<b>" + Escape(_configuration.ReportTitle) + " — " + period + "</b>");
        builder.AppendLine("<code>" + localStart.ToString("dd MMM yyyy HH:mm") + " → " + localEnd.ToString("dd MMM yyyy HH:mm") + "</code>\n");
        builder.AppendLine("<b>Performance</b>");
        builder.AppendLine("Trades: <b>" + report.Count + "</b> | W/L/BE: " + report.Wins + "/" + report.Losses + "/" + report.Breakeven);
        builder.AppendLine("Win rate: <b>" + report.WinRate.ToString("0.0") + "%</b>");
        builder.AppendLine("Net: <b>" + FormatMoney(account.Currency, report.NetProfit) + "</b>");
        builder.AppendLine("Gross: " + FormatMoney(account.Currency, report.GrossProfit) + " | Commission: " + FormatMoney(account.Currency, report.Commissions) + " | Swap: " + FormatMoney(account.Currency, report.Swap));
        builder.AppendLine("Pips: " + report.Pips.ToString("0.0") + " | Avg win/loss: " + FormatMoney(account.Currency, report.AverageWin) + " / " + FormatMoney(account.Currency, report.AverageLoss));
        builder.AppendLine("Realised max DD: " + FormatMoney(account.Currency, -report.MaxDrawdown) + "\n");
        builder.AppendLine("<b>Account now</b>");
        builder.AppendLine((account.IsLive ? "LIVE" : "DEMO") + " | " + Escape(account.BrokerName));
        builder.AppendLine("Balance: " + FormatMoney(account.Currency, account.Balance) + " | Equity: " + FormatMoney(account.Currency, account.Equity));
        builder.AppendLine("Margin: " + FormatMoney(account.Currency, account.Margin) + " | Free: " + FormatMoney(account.Currency, account.FreeMargin));
        return Limit(builder.ToString());
    }

    private void Send(RobotContext context, string text, Action<bool> completed)
    {
        if (_sending)
        {
            completed(false);
            return;
        }

        var request = CreateRequest(context, text);
        if (request is null)
        {
            completed(false);
            return;
        }

        _sending = true;
        try
        {
            context.SendHttpRequest(request, response => context.BeginInvokeOnMainThread(() =>
            {
                _sending = false;
                if (!response.IsSuccessful)
                {
                    context.Log("Telegram report failed: HTTP " + response.StatusCode);
                    completed(false);
                    return;
                }

                context.Log("Telegram report sent successfully.");
                completed(true);
            }));
        }
        catch (Exception exception)
        {
            _sending = false;
            context.Log("Telegram report could not be sent: " + exception.Message);
            completed(false);
        }
    }

    private HttpRequest? CreateRequest(RobotContext context, string text)
    {
        if (string.IsNullOrWhiteSpace(_configuration.BotToken) || string.IsNullOrWhiteSpace(_configuration.ChatId))
        {
            context.Log("Telegram reporting needs a bot token and chat ID.");
            return null;
        }

        var url = "https://api.telegram.org/bot" + _configuration.BotToken.Trim() + "/sendMessage";
        var body = "{\"chat_id\":\"" + EscapeJson(_configuration.ChatId.Trim()) + "\",\"text\":\"" + EscapeJson(text) + "\",\"parse_mode\":\"HTML\",\"disable_web_page_preview\":true}";
        var request = new HttpRequest(new Uri(url)) { Method = HttpMethod.Post, Body = body, Timeout = TimeSpan.FromSeconds(20) };
        request.Headers.Add("Content-Type", "application/json");
        return request;
    }

    private bool IsIncluded(HistoricalTrade trade) => Matches(_symbols, trade.SymbolName) && Matches(_labels, trade.Label);
    private static bool Matches(HashSet<string> filter, string? value) => filter.Count == 0 || filter.Contains((value ?? string.Empty).Trim());
    private static HashSet<string> ParseFilter(string filter) => new((filter ?? string.Empty).Split(',').Select(value => value.Trim()).Where(value => value.Length > 0), StringComparer.OrdinalIgnoreCase);
    private DateTime ToLocal(DateTime utc) => DateTime.SpecifyKind(utc, DateTimeKind.Utc).AddMinutes(_configuration.UtcOffsetMinutes);
    private DateTime FromLocal(DateTime local) => DateTime.SpecifyKind(local.AddMinutes(-_configuration.UtcOffsetMinutes), DateTimeKind.Utc);
    private static DateTime StartOfWeek(DateTime date) => date.AddDays(-((int)date.DayOfWeek + 6) % 7).Date;
    private static string Escape(string value) => (value ?? string.Empty).Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
    private static string EscapeJson(string value) => JsonSerializer.Serialize(value)[1..^1];
    private static string FormatMoney(RobotContext context, double value) => FormatMoney(context.GetAccountSummary().Currency, value);
    private static string FormatMoney(string currency, double value) => Escape(currency) + " " + value.ToString("0.00;-0.00;0.00");
    private static string Limit(string value) => value.Length <= 3900 ? value : value[..3800] + "\n\n<i>Truncated.</i>";

    private readonly record struct Report(int Count, int Wins, int Losses, int Breakeven, double NetProfit, double GrossProfit, double Commissions, double Swap, double Pips, double WinRate, double AverageWin, double AverageLoss, double MaxDrawdown, IReadOnlyList<HistoricalTrade> Trades);
}
