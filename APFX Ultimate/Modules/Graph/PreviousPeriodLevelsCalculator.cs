namespace cAlgo.Robots;

/// <summary>Finds the bars immediately preceding cTrader's active daily and weekly candles.</summary>
public static class PreviousPeriodLevelsCalculator
{
    public static PreviousPeriodLevels? Calculate(Bars dailyBars, Bars weeklyBars)
    {
        if (TryGetPreviousCompletedBar(dailyBars) is not { } previousDay ||
            TryGetPreviousCompletedBar(weeklyBars) is not { } previousWeek)
        {
            return null;
        }

        return new PreviousPeriodLevels(previousDay, previousWeek);
    }

    private static PeriodLevels? TryGetPreviousCompletedBar(Bars bars)
    {
        // The last bar is cTrader's live candle, even when the broker session opens
        // outside UTC midnight. The preceding bar is therefore the last completed one.
        if (bars.Count < 2)
        {
            return null;
        }

        var index = bars.Count - 2;
        return new PeriodLevels(bars.HighPrices[index], bars.LowPrices[index], bars.ClosePrices[index]);
    }
}
