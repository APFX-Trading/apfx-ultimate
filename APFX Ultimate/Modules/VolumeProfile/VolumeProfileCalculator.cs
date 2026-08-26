namespace cAlgo.Robots;

/// <summary>Builds daily profiles from broker-provided M1 tick volume.</summary>
public static class VolumeProfileCalculator
{
    public static IReadOnlyList<DailyVolumeProfile> Calculate(Bars bars, DateTime nowUtc, int daysToShow, int rowCount)
    {
        if (bars.Count == 0)
        {
            return Array.Empty<DailyVolumeProfile>();
        }

        var profiles = new List<DailyVolumeProfile>();
        for (var offset = daysToShow - 1; offset >= 0; offset--)
        {
            var start = nowUtc.Date.AddDays(-offset);
            var end = offset == 0 ? nowUtc : start.AddDays(1);
            if (end <= start || CalculateDay(bars, start, end, rowCount) is not { } profile)
            {
                continue;
            }

            profiles.Add(profile);
        }

        return profiles;
    }

    public static DailyVolumeProfile? CalculateDay(Bars bars, DateTime start, DateTime end, int rowCount)
    {
        var firstIndex = bars.OpenTimes.GetIndexByTime(start);
        while (firstIndex >= 0 && firstIndex < bars.Count && bars.OpenTimes[firstIndex] < start)
        {
            firstIndex++;
        }

        if (firstIndex < 0 || firstIndex >= bars.Count)
        {
            return null;
        }

        var lastIndex = bars.OpenTimes.GetIndexByTime(end.AddTicks(-1));
        while (lastIndex >= 0 && bars.OpenTimes[lastIndex] >= end)
        {
            lastIndex--;
        }

        if (lastIndex < firstIndex)
        {
            return null;
        }

        var low = double.MaxValue;
        var high = double.MinValue;
        for (var index = firstIndex; index <= lastIndex; index++)
        {
            low = Math.Min(low, bars.LowPrices[index]);
            high = Math.Max(high, bars.HighPrices[index]);
        }

        if (high <= low)
        {
            return null;
        }

        var rowHeight = (high - low) / rowCount;
        var volumes = new double[rowCount];
        for (var index = firstIndex; index <= lastIndex; index++)
        {
            var lowBin = ToBin(bars.LowPrices[index], low, rowHeight, rowCount);
            var highBin = ToBin(bars.HighPrices[index], low, rowHeight, rowCount);
            var binCount = highBin - lowBin + 1;
            var volumePerBin = bars.TickVolumes[index] / Math.Max(1, binCount);
            for (var bin = lowBin; bin <= highBin; bin++)
            {
                volumes[bin] += volumePerBin;
            }
        }

        var pointOfControl = 0;
        for (var index = 1; index < volumes.Length; index++)
        {
            if (volumes[index] > volumes[pointOfControl])
            {
                pointOfControl = index;
            }
        }

        return volumes.Sum() > 0
            ? new DailyVolumeProfile(start, end, low, rowHeight, volumes, pointOfControl)
            : null;
    }

    private static int ToBin(double price, double low, double rowHeight, int rowCount) =>
        Math.Clamp((int)Math.Floor((price - low) / rowHeight), 0, rowCount - 1);
}
