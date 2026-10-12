namespace cAlgo.Robots;

/// <summary>
/// Shows realized account-wide results from historical closing deals.
/// </summary>
public sealed class AccountStatisticsSection
{
    private const string DailyDrawdownStoragePrefix = "APFXUltimateDailyDrawdown";
    private static readonly StatisticsPeriod[] Periods =
    {
        new("Today", now => now.Date),
        new("Week", StartOfWeek),
        new("Month", now => new DateTime(now.Year, now.Month, 1)),
        new("3 Months", now => now.Date.AddMonths(-3)),
        new("6 Months", now => now.Date.AddMonths(-6)),
        new("12 Months", now => now.Date.AddMonths(-12))
    };

    private readonly Dictionary<string, TextBlock> _winLossCells = new();
    private readonly Dictionary<string, TextBlock> _profitLossCells = new();
    private readonly Dictionary<string, TextBlock> _drawdownCells = new();
    private readonly Dictionary<string, TickDrawdownWindow> _tickDrawdownWindows = new();
    private readonly SortedDictionary<DateTime, PersistedDailyDrawdown> _dailyHistory = new();
    private HistoricalTrade[] _historicalTrades = Array.Empty<HistoricalTrade>();
    private bool _historyLoaded;
    private TickDrawdownWindow? _partialDayWindow;
    private readonly DateTime _performanceFrom;
    private DateTime _nextRefreshAt = DateTime.MinValue;
    private DateTime _lastStatisticsDate = DateTime.MinValue;
    private int _lastHistoricalTradeCount = -1;
    private PersistedDailyDrawdown? _dailyDrawdown;
    private DateTime _dailyDrawdownDate = DateTime.MinValue;
    private bool _dailyDrawdownStorageUnavailable;
    private TradingAccountIdentity? _accountIdentity;
    private TradingAccountIdentity AccountIdentity => _accountIdentity ?? throw new InvalidOperationException("Account scope has not been initialized.");

    private void EnsureAccount(RobotContext context)
    {
        var identity = TradingAccountIdentity.From(context.GetAccountSummary());
        if (_accountIdentity == identity) return;
        // Flush under the previous identity before discarding its in-memory curve.
        FlushDrawdownState(context);
        _accountIdentity = identity;
        _tickDrawdownWindows.Clear();
        _dailyHistory.Clear();
        _historicalTrades = Array.Empty<HistoricalTrade>();
        _historyLoaded = false;
        _partialDayWindow = null;
        _dailyDrawdown = null;
        _dailyDrawdownDate = DateTime.MinValue;
        _dailyDrawdownStorageUnavailable = false;
        _nextRefreshAt = DateTime.MinValue;
        _lastStatisticsDate = DateTime.MinValue;
        _lastHistoricalTradeCount = -1;
    }

    public AccountStatisticsSection(DateTime performanceFrom)
    {
        _performanceFrom = performanceFrom;
    }

    public ControlBase CreateControl(RobotContext context, double fontSize)
    {
        var section = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Margin = new Thickness(0)
        };
        section.AddChild(new TextBlock
        {
            Text = "Performance",
            ForegroundColor = Color.FromHex("#FF6FE3FF"),
            FontWeight = FontWeight.ExtraBold,
            FontSize = fontSize + 1,
            HorizontalAlignment = HorizontalAlignment.Center
        });

        var grid = new Grid(Periods.Length + 1, 4)
        {
            Margin = new Thickness(0, 4, 0, 0)
        };
        AddCell(grid, "Period", 0, 0, isHeader: true, fontSize);
        AddCell(grid, "W / L (Win %)", 0, 1, isHeader: true, fontSize);
        AddCell(grid, "P / L (%)", 0, 2, isHeader: true, fontSize);
        AddCell(grid, "DD (%)", 0, 3, isHeader: true, fontSize);

        for (var index = 0; index < Periods.Length; index++)
        {
            var period = Periods[index];
            AddCell(grid, period.Name, index + 1, 0, isHeader: false, fontSize);
            _winLossCells[period.Name] = AddCell(grid, "-", index + 1, 1, isHeader: false, fontSize);
            _profitLossCells[period.Name] = AddCell(grid, "-", index + 1, 2, isHeader: false, fontSize);
            _drawdownCells[period.Name] = AddCell(grid, "-", index + 1, 3, isHeader: false, fontSize);
        }

        section.AddChild(grid);
        return section;
    }

    public void RefreshIfDue(RobotContext context)
    {
        EnsureAccount(context);
        var now = context.ServerTime;
        if (now < _nextRefreshAt)
        {
            return;
        }

        var historicalTradeCount = context.GetHistoricalTradeCount();
        Refresh(context, historicalTradeCount);
    }

    public void Refresh(RobotContext context)
    {
        Refresh(context, context.GetHistoricalTradeCount());
    }

    private void Refresh(RobotContext context, int historicalTradeCount)
    {
        EnsureAccount(context);
        var now = context.ServerTime;
        if (historicalTradeCount != _lastHistoricalTradeCount || now.Date != _lastStatisticsDate.Date)
        {
            _historicalTrades = context.GetHistoricalTrades().OrderBy(trade => trade.ClosingTime).ToArray();
        }

        foreach (var period in Periods)
        {
            var periodStart = GetEffectivePeriodStart(period, now);
            var summary = Summarize(_historicalTrades, periodStart, now);
            var drawdown = GetDrawdown(period, periodStart, summary);
            _winLossCells[period.Name].Text = FormatWinLoss(summary);
            _profitLossCells[period.Name].Text = FormatProfitLoss(summary.NetProfit, summary.StartingBalance);
            _profitLossCells[period.Name].ForegroundColor = summary.NetProfit switch
            {
                > 0 => Color.FromHex("#FF65F2A5"),
                < 0 => Color.FromHex("#FFFF8B98"),
                _ => Color.FromHex("#FFBDEBFF")
            };
            _drawdownCells[period.Name].Text = FormatDrawdown(drawdown.Amount, drawdown.PeakBalance);
            _drawdownCells[period.Name].ForegroundColor = Color.FromHex("#FFBDEBFF");
        }

        _nextRefreshAt = now.AddSeconds(15);
        _lastStatisticsDate = now.Date;
        _lastHistoricalTradeCount = historicalTradeCount;
    }

    /// <summary>
    /// Records the high-water mark and maximum equity drop for each visible period.
    /// Daily equity summaries are persisted; tick windows handle intraday cutoffs.
    /// </summary>
    public void TrackDrawdown(RobotContext context)
    {
        EnsureAccount(context);
        var now = context.ServerTime;
        var equity = context.GetAccountEquity();
        if (!double.IsFinite(equity)) return;
        if (!_historyLoaded)
        {
            for (var date = now.Date.AddMonths(-12); date < now.Date; date = date.AddDays(1))
            {
                if (LoadDailyDrawdown(context, date) is { } saved) _dailyHistory[date] = saved;
            }
            _historyLoaded = true;
        }
        TrackPersistedDailyDrawdown(context, now, equity);
        if (_performanceFrom.TimeOfDay != TimeSpan.Zero && _performanceFrom <= now && _performanceFrom >= now.Date.AddMonths(-12))
        {
            var key = "APFXUltimateDrawdownFrom" + _performanceFrom.ToString("yyyyMMddHHmmssfffffff", System.Globalization.CultureInfo.InvariantCulture);
            var initialize = _partialDayWindow is null;
            _partialDayWindow ??= new TickDrawdownWindow(_performanceFrom, equity, LoadDailyDrawdown(context, _performanceFrom.Date, key));
            var before = _partialDayWindow.State;
            _partialDayWindow.Observe(equity);
            if (initialize || before != _partialDayWindow.State) PersistDailyDrawdown(context, key, _partialDayWindow.State);
        }
        foreach (var period in Periods)
        {
            var periodStart = GetEffectivePeriodStart(period, now);
            if (periodStart > now)
            {
                _tickDrawdownWindows.Remove(period.Name);
                continue;
            }
            if (periodStart == _performanceFrom && _partialDayWindow is not null)
            {
                _tickDrawdownWindows[period.Name] = _partialDayWindow;
                continue;
            }
            if (!_tickDrawdownWindows.TryGetValue(period.Name, out var window) || window.PeriodStart != periodStart)
            {
                _tickDrawdownWindows[period.Name] = new TickDrawdownWindow(periodStart, equity);
                continue;
            }

            window.Observe(equity);
        }
    }

    public void FlushDrawdownState(RobotContext context)
    {
        if (_dailyDrawdown is null || _dailyDrawdownDate == DateTime.MinValue || _dailyDrawdownStorageUnavailable)
        {
            return;
        }

        PersistDailyDrawdown(context);
        if (!_dailyDrawdownStorageUnavailable)
        {
            try
            {
                context.FlushLocalValues();
            }
            catch (ArgumentException)
            {
                _dailyDrawdownStorageUnavailable = true;
                context.Log("Daily drawdown persistence is unavailable; continuing with in-memory tracking.");
            }
        }
    }

    private (double Amount, double PeakBalance) GetDrawdown(StatisticsPeriod period, DateTime periodStart, StatisticsSummary summary)
    {
        var result = (Amount: summary.MaximumDrawdown, PeakBalance: summary.PeakBalance);
        var peak = double.NegativeInfinity;
        foreach (var entry in _dailyHistory)
        {
            // A whole-day summary cannot safely represent a partial first day.
            if (entry.Key < periodStart || entry.Key > _dailyDrawdownDate) continue;
            var day = entry.Value;
            var crossDayDrop = peak - day.MinimumEquity;
            if (crossDayDrop > result.Amount) result = (crossDayDrop, peak);
            if (day.MaximumDrawdown > result.Amount) result = (day.MaximumDrawdown, day.PeakAtMaximumDrawdown);
            if (result.Amount == 0 && day.PeakEquity > result.PeakBalance) result.PeakBalance = day.PeakEquity;
            peak = Math.Max(peak, day.PeakEquity);
        }
        if (_tickDrawdownWindows.TryGetValue(period.Name, out var window) && window.MaximumDrawdown > result.Amount)
            result = (window.MaximumDrawdown, window.PeakAtMaximumDrawdown);
        return result;
    }

    private void TrackPersistedDailyDrawdown(RobotContext context, DateTime now, double equity)
    {
        if (_dailyDrawdownDate != now.Date || _dailyDrawdown is null)
        {
            FlushDrawdownState(context);
            _dailyDrawdownDate = now.Date;
            _dailyDrawdown = LoadDailyDrawdown(context, now.Date) ?? new PersistedDailyDrawdown(equity, 0, equity, equity);
            foreach (var expired in _dailyHistory.Keys.Where(date => date < now.Date.AddMonths(-12)).ToArray())
                _dailyHistory.Remove(expired);
            PersistDailyDrawdown(context);
        }

        var drawdown = _dailyDrawdown!.Value;
        var peakChanged = equity > drawdown.PeakEquity;
        if (peakChanged)
        {
            drawdown = drawdown with { PeakEquity = equity };
        }

        var minimumChanged = equity < drawdown.MinimumEquity;
        drawdown = drawdown with { MinimumEquity = Math.Min(drawdown.MinimumEquity, equity) };
        var maximumDrawdown = Math.Max(drawdown.MaximumDrawdown, drawdown.PeakEquity - equity);
        var newMaximumDrawdown = maximumDrawdown > drawdown.MaximumDrawdown;
        if (newMaximumDrawdown)
        {
            drawdown = drawdown with { MaximumDrawdown = maximumDrawdown, PeakAtMaximumDrawdown = drawdown.PeakEquity };
        }

        _dailyDrawdown = drawdown;
        _dailyHistory[now.Date] = drawdown;
        if (peakChanged || newMaximumDrawdown || minimumChanged)
        {
            PersistDailyDrawdown(context);
        }
    }

    private PersistedDailyDrawdown? LoadDailyDrawdown(RobotContext context, DateTime date, string? storageKey = null)
    {
        if (_dailyDrawdownStorageUnavailable) return null;
        try
        {
            var value = context.GetLocalValue(AccountIdentity.DrawdownStorageKey(storageKey ?? GetDailyDrawdownStorageKey(date)));
            if (value is not null)
            {
                var parts = value.Split('|');
                if ((parts.Length == 2 || parts.Length == 4)
                    && double.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var peakEquity)
                    && double.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var maximumDrawdown)
                    && double.IsFinite(peakEquity) && double.IsFinite(maximumDrawdown)
                    && maximumDrawdown >= 0)
                {
                    // Legacy records lack the original loss peak and daily low.
                    if (parts.Length == 2)
                        return new PersistedDailyDrawdown(peakEquity, maximumDrawdown, peakEquity, peakEquity - maximumDrawdown);
                    if (double.TryParse(parts[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var lossPeak)
                        && double.TryParse(parts[3], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var low)
                        && double.IsFinite(lossPeak) && double.IsFinite(low)
                        && lossPeak <= peakEquity && low <= peakEquity)
                        return new PersistedDailyDrawdown(peakEquity, maximumDrawdown, lossPeak, low);
                }
            }
        }
        catch (ArgumentException)
        {
            _dailyDrawdownStorageUnavailable = true;
            context.Log("Daily drawdown persistence is unavailable; continuing with in-memory tracking.");
        }

        return null;
    }

    private void PersistDailyDrawdown(RobotContext context)
        => PersistDailyDrawdown(context, GetDailyDrawdownStorageKey(_dailyDrawdownDate), _dailyDrawdown!.Value);

    private void PersistDailyDrawdown(RobotContext context, string key, PersistedDailyDrawdown value)
    {
        if (_dailyDrawdownStorageUnavailable)
        {
            return;
        }

        try
        {
            context.SetLocalValue(
                AccountIdentity.DrawdownStorageKey(key),
                string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{value.PeakEquity:R}|{value.MaximumDrawdown:R}|{value.PeakAtMaximumDrawdown:R}|{value.MinimumEquity:R}"));
        }
        catch (ArgumentException)
        {
            _dailyDrawdownStorageUnavailable = true;
            context.Log("Daily drawdown persistence is unavailable; continuing with in-memory tracking.");
        }
    }

    private static string GetDailyDrawdownStorageKey(DateTime date) => DailyDrawdownStoragePrefix + date.ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture);

    private static TextBlock AddCell(Grid grid, string text, int row, int column, bool isHeader, double fontSize)
    {
        var cell = new TextBlock
        {
            Text = text,
            ForegroundColor = isHeader ? Color.FromHex("#FFBDEBFF") : Color.White,
            FontWeight = isHeader ? FontWeight.ExtraBold : FontWeight.Normal,
            FontSize = fontSize,
            Margin = column == 0 ? new Thickness(2) : new Thickness(6, 2, 2, 2)
        };
        grid.AddChild(cell, row, column);
        return cell;
    }

    private static StatisticsSummary Summarize(IReadOnlyList<HistoricalTrade> historicalTrades, DateTime startAt, DateTime endAt)
    {
        var trades = historicalTrades
            .Where(trade => trade.ClosingTime >= startAt && trade.ClosingTime <= endAt)
            .ToArray();
        var wins = trades.Count(trade => trade.NetProfit > 0);
        var losses = trades.Count(trade => trade.NetProfit < 0);
        var startingBalance = trades.Length == 0 ? 0 : trades[0].Balance - trades[0].NetProfit;
        var netProfit = trades.Sum(trade => trade.NetProfit);
        var maximumDrawdown = 0d;
        var peakBalance = startingBalance;
        var peakBalanceAtMaximumDrawdown = startingBalance;

        foreach (var trade in trades)
        {
            // HistoricalTrade.Balance is cTrader's actual account balance after the deal.
            // Using it avoids reconstructing a balance curve from P/L, which can be wrong after deposits or withdrawals.
            var balanceAfterTrade = trade.Balance;
            if (balanceAfterTrade > peakBalance)
            {
                peakBalance = balanceAfterTrade;
            }

            var drawdown = peakBalance - balanceAfterTrade;
            if (drawdown > maximumDrawdown)
            {
                maximumDrawdown = drawdown;
                peakBalanceAtMaximumDrawdown = peakBalance;
            }
        }

        return new StatisticsSummary(wins, losses, netProfit, startingBalance, maximumDrawdown, peakBalanceAtMaximumDrawdown);
    }

    private static DateTime StartOfWeek(DateTime now)
    {
        var daysSinceMonday = ((int)now.DayOfWeek + 6) % 7;
        return now.Date.AddDays(-daysSinceMonday);
    }

    private DateTime GetEffectivePeriodStart(StatisticsPeriod period, DateTime now)
    {
        var periodStart = period.StartAt(now);
        return _performanceFrom > periodStart ? _performanceFrom : periodStart;
    }

    private static string FormatWinLoss(StatisticsSummary summary)
    {
        var decisiveTradeCount = summary.Wins + summary.Losses;
        if (decisiveTradeCount == 0)
        {
            return "0 / 0 (-)";
        }

        var winRate = summary.Wins * 100d / decisiveTradeCount;
        return $"{FormatCompactCount(summary.Wins)} / {FormatCompactCount(summary.Losses)} ({winRate:N1}%)";
    }

    private static string FormatProfitLoss(double netProfit, double startingBalance)
    {
        var amount = netProfit >= 0 ? $"+{FormatCompact(netProfit)}" : $"-{FormatCompact(Math.Abs(netProfit))}";
        if (startingBalance <= 0)
        {
            return amount;
        }

        return $"{amount} ({netProfit / startingBalance:P2})";
    }

    private static string FormatDrawdown(double drawdown, double peakBalance)
    {
        if (peakBalance <= 0)
        {
            return FormatCompact(drawdown);
        }

        return $"{FormatCompact(drawdown)} ({drawdown / peakBalance:P1})";
    }

    private static string FormatCompact(double value)
    {
        var absoluteValue = Math.Abs(value);
        return absoluteValue switch
        {
            >= 1_000_000 => $"{value / 1_000_000d:0.0}m",
            >= 1_000 => $"{value / 1_000d:0.0}k",
            _ => value.ToString("N2")
        };
    }

    private static string FormatCompactCount(int value) => value switch
    {
        >= 1_000_000 => $"{value / 1_000_000d:0.0}m",
        >= 1_000 => $"{value / 1_000d:0.0}k",
        _ => value.ToString("N0")
    };

    private sealed record StatisticsPeriod(string Name, Func<DateTime, DateTime> StartAt);

    private readonly record struct StatisticsSummary(int Wins, int Losses, double NetProfit, double StartingBalance, double MaximumDrawdown, double PeakBalance);
    private readonly record struct PersistedDailyDrawdown(double PeakEquity, double MaximumDrawdown, double PeakAtMaximumDrawdown, double MinimumEquity);

    private sealed class TickDrawdownWindow
    {
        public TickDrawdownWindow(DateTime periodStart, double initialEquity, PersistedDailyDrawdown? saved = null)
        {
            PeriodStart = periodStart;
            PeakEquity = saved?.PeakEquity ?? initialEquity;
            MaximumDrawdown = saved?.MaximumDrawdown ?? 0;
            PeakAtMaximumDrawdown = saved?.PeakAtMaximumDrawdown ?? initialEquity;
            MinimumEquity = saved?.MinimumEquity ?? initialEquity;
        }

        public DateTime PeriodStart { get; }
        public double PeakEquity { get; private set; }
        public double MaximumDrawdown { get; private set; }
        public double PeakAtMaximumDrawdown { get; private set; }
        private double MinimumEquity { get; set; }
        public PersistedDailyDrawdown State => new(PeakEquity, MaximumDrawdown, PeakAtMaximumDrawdown, MinimumEquity);

        public void Observe(double equity)
        {
            MinimumEquity = Math.Min(MinimumEquity, equity);
            if (equity > PeakEquity)
            {
                PeakEquity = equity;
                return;
            }

            if (PeakEquity - equity > MaximumDrawdown)
            {
                MaximumDrawdown = PeakEquity - equity;
                PeakAtMaximumDrawdown = PeakEquity;
            }
        }
    }
}
