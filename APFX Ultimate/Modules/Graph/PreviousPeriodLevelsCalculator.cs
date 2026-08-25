namespace cAlgo.Robots;

/// <summary>Finds completed prior daily and weekly bars without relying on bar collection order.</summary>
public static class PreviousPeriodLevelsCalculator
{
    public static PreviousPeriodLevels? Calculate(Bars dailyBars, Bars weeklyBars, DateTime nowUtc)
    {
        var currentWeekStart = nowUtc.Date.AddDays(-((int)nowUtc.DayOfWeek + 6) % 7);
        if (TryGetLastCompletedBar(dailyBars, nowUtc.Date) is not { } previousDay ||
            TryGetLastCompletedBar(weeklyBars, currentWeekStart) is not { } previousWeek)
        {
            return null;
        }

        return new PreviousPeriodLevels(previousDay, previousWeek);
    }

    private static PeriodLevels? TryGetLastCompletedBar(Bars bars, DateTime currentPeriodStart)
    {
        for (var index = bars.Count - 1; index >= 0; index--)
        {
            if (bars.OpenTimes[index] >= currentPeriodStart)
            {
                continue;
            }

            return new PeriodLevels(bars.HighPrices[index], bars.LowPrices[index], bars.ClosePrices[index]);
        }

        return null;
    }
}
