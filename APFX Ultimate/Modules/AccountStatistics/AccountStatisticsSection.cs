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
        new("3 Months", now => now.AddMonths(-3)),
        new("6 Months", now => now.AddMonths(-6)),
        new("12 Months", now => now.AddMonths(-12))
    };

    private readonly Dictionary<string, TextBlock> _winLossCells = new();
    private readonly Dictionary<string, TextBlock> _profitLossCells = new();
    private readonly Dictionary<string, TextBlock> _drawdownCells = new();
    private readonly Dictionary<string, TickDrawdownWindow> _tickDrawdownWindows = new();
    private readonly DateTime _performanceFrom;
    private DateTime _nextRefreshAt = DateTime.MinValue;
    private DateTime _lastStatisticsDate = DateTime.MinValue;
    private int _lastHistoricalTradeCount = -1;
    private PersistedDailyDrawdown? _dailyDrawdown;
    private DateTime _dailyDrawdownDate = DateTime.MinValue;
    private bool _dailyDrawdownStorageUnavailable;

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
        var now = context.ServerTime;
        if (now < _nextRefreshAt)
        {
            return;
        }

        var historicalTradeCount = context.GetHistoricalTradeCount();
        if (historicalTradeCount == _lastHistoricalTradeCount && now.Date == _lastStatisticsDate.Date)
        {
            _nextRefreshAt = now.AddSeconds(15);
            return;
        }

        Refresh(context, historicalTradeCount);
    }

    public void Refresh(RobotContext context)
    {
        Refresh(context, context.GetHistoricalTradeCount());
    }

    private void Refresh(RobotContext context, int historicalTradeCount)
    {
        var now = context.ServerTime;
        var historicalTrades = context.GetHistoricalTrades()
            .OrderBy(trade => trade.ClosingTime)
            .ToArray();

        foreach (var period in Periods)
        {
            var periodStart = GetEffectivePeriodStart(period, now);
            var summary = Summarize(historicalTrades, periodStart, now);
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
    /// This is intentionally in-memory only and runs in constant time on every tick.
    /// </summary>
    public void TrackDrawdown(RobotContext context)
    {
        var now = context.ServerTime;
        var equity = context.GetAccountEquity();
        TrackPersistedDailyDrawdown(context, now, equity);
        foreach (var period in Periods)
        {
            var periodStart = GetEffectivePeriodStart(period, now);
            if (periodStart > now)
            {
                _tickDrawdownWindows.Remove(period.Name);
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
        if (period.Name == "Today" && _performanceFrom <= periodStart && _dailyDrawdown is { } persisted && persisted.MaximumDrawdown > summary.MaximumDrawdown)
        {
            return (persisted.MaximumDrawdown, persisted.PeakEquity);
        }

        if (!_tickDrawdownWindows.TryGetValue(period.Name, out var tickDrawdown) || tickDrawdown.MaximumDrawdown <= summary.MaximumDrawdown)
        {
            return (summary.MaximumDrawdown, summary.PeakBalance);
        }

        return (tickDrawdown.MaximumDrawdown, tickDrawdown.PeakEquity);
    }

    private void TrackPersistedDailyDrawdown(RobotContext context, DateTime now, double equity)
    {
        if (_dailyDrawdownDate != now.Date || _dailyDrawdown is null)
        {
            FlushDrawdownState(context);
            _dailyDrawdownDate = now.Date;
            _dailyDrawdown = LoadDailyDrawdown(context, now.Date, equity);
            PersistDailyDrawdown(context);
        }

        var drawdown = _dailyDrawdown!.Value;
        var peakChanged = equity > drawdown.PeakEquity;
        if (peakChanged)
        {
            drawdown = drawdown with { PeakEquity = equity };
        }

        var maximumDrawdown = Math.Max(drawdown.MaximumDrawdown, drawdown.PeakEquity - equity);
        var newMaximumDrawdown = maximumDrawdown > drawdown.MaximumDrawdown;
        if (newMaximumDrawdown)
        {
            drawdown = drawdown with { MaximumDrawdown = maximumDrawdown };
        }

        _dailyDrawdown = drawdown;
        if (peakChanged || newMaximumDrawdown)
        {
            PersistDailyDrawdown(context);
        }
    }

    private PersistedDailyDrawdown LoadDailyDrawdown(RobotContext context, DateTime date, double equity)
    {
        try
        {
            var value = context.GetLocalValue(GetDailyDrawdownStorageKey(date));
            if (value is not null)
            {
                var parts = value.Split('|');
                if (parts.Length == 2
                    && double.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var peakEquity)
                    && double.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var maximumDrawdown)
                    && peakEquity > 0
                    && maximumDrawdown >= 0)
                {
                    return new PersistedDailyDrawdown(Math.Max(peakEquity, equity), maximumDrawdown);
                }
            }
        }
        catch (ArgumentException)
        {
            _dailyDrawdownStorageUnavailable = true;
            context.Log("Daily drawdown persistence is unavailable; continuing with in-memory tracking.");
        }

        return new PersistedDailyDrawdown(equity, 0);
    }

    private void PersistDailyDrawdown(RobotContext context)
    {
        if (_dailyDrawdownStorageUnavailable)
        {
            return;
        }

        var value = _dailyDrawdown!.Value;
        try
        {
            context.SetLocalValue(
                GetDailyDrawdownStorageKey(_dailyDrawdownDate),
                string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{value.PeakEquity:R}|{value.MaximumDrawdown:R}"));
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
    private readonly record struct PersistedDailyDrawdown(double PeakEquity, double MaximumDrawdown);

    private sealed class TickDrawdownWindow
    {
        public TickDrawdownWindow(DateTime periodStart, double initialEquity)
        {
            PeriodStart = periodStart;
            PeakEquity = initialEquity;
        }

        public DateTime PeriodStart { get; }
        public double PeakEquity { get; private set; }
        public double MaximumDrawdown { get; private set; }

        public void Observe(double equity)
        {
            if (equity > PeakEquity)
            {
                PeakEquity = equity;
                return;
            }

            MaximumDrawdown = Math.Max(MaximumDrawdown, PeakEquity - equity);
        }
    }
}
