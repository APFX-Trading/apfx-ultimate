namespace cAlgo.Robots;

/// <summary>Calculates a UTC-session VWAP using broker M1 tick volume.</summary>
public static class DailyVwapCalculator
{
    public static DailyVwapData Calculate(Bars bars, DateTime nowUtc)
    {
        if (bars.Count == 0)
        {
            return DailyVwapData.Empty;
        }

        var sessionStart = nowUtc.Date;
        var firstIndex = bars.OpenTimes.GetIndexByTime(sessionStart);
        while (firstIndex >= 0 && firstIndex < bars.Count && bars.OpenTimes[firstIndex] < sessionStart)
        {
            firstIndex++;
        }

        if (firstIndex < 0 || firstIndex >= bars.Count)
        {
            return DailyVwapData.Empty;
        }

        var vwap = new List<ChartTimePricePoint>();
        var upperOne = new List<ChartTimePricePoint>();
        var lowerOne = new List<ChartTimePricePoint>();
        var upperTwo = new List<ChartTimePricePoint>();
        var lowerTwo = new List<ChartTimePricePoint>();
        var upperThree = new List<ChartTimePricePoint>();
        var lowerThree = new List<ChartTimePricePoint>();
        var upperFour = new List<ChartTimePricePoint>();
        var lowerFour = new List<ChartTimePricePoint>();
        double weightedPrice = 0;
        double weightedPriceSquared = 0;
        double totalVolume = 0;

        for (var index = firstIndex; index < bars.Count && bars.OpenTimes[index] <= nowUtc; index++)
        {
            var volume = bars.TickVolumes[index];
            if (volume <= 0)
            {
                continue;
            }

            var typicalPrice = (bars.HighPrices[index] + bars.LowPrices[index] + bars.ClosePrices[index]) / 3d;
            totalVolume += volume;
            weightedPrice += typicalPrice * volume;
            weightedPriceSquared += typicalPrice * typicalPrice * volume;
            var average = weightedPrice / totalVolume;
            var variance = Math.Max(0, weightedPriceSquared / totalVolume - average * average);
            var deviation = Math.Sqrt(variance);
            var time = bars.OpenTimes[index];
            vwap.Add(new ChartTimePricePoint(time, average));
            upperOne.Add(new ChartTimePricePoint(time, average + deviation));
            lowerOne.Add(new ChartTimePricePoint(time, average - deviation));
            upperTwo.Add(new ChartTimePricePoint(time, average + 2 * deviation));
            lowerTwo.Add(new ChartTimePricePoint(time, average - 2 * deviation));
            upperThree.Add(new ChartTimePricePoint(time, average + 3 * deviation));
            lowerThree.Add(new ChartTimePricePoint(time, average - 3 * deviation));
            upperFour.Add(new ChartTimePricePoint(time, average + 4 * deviation));
            lowerFour.Add(new ChartTimePricePoint(time, average - 4 * deviation));
        }

        return new DailyVwapData(vwap, upperOne, lowerOne, upperTwo, lowerTwo, upperThree, lowerThree, upperFour, lowerFour);
    }
}
