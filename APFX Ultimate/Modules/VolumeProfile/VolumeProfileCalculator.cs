namespace cAlgo.Robots;

/// <summary>Builds daily profiles from broker-provided M1 tick volume.</summary>
public static class VolumeProfileCalculator
{
    public static IReadOnlyList<DailyVolumeProfile> Calculate(Bars bars, DateTime nowUtc, int daysToShow, double binSize)
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
            if (end <= start || CalculatePeriod(bars, start, end, "Day", binSize, 0.70) is not { } profile)
            {
                continue;
            }

            profiles.Add(profile);
        }

        return profiles;
    }

    public static DailyVolumeProfile? CalculatePeriod(Bars bars, DateTime start, DateTime end, string name, double binSize, double valueAreaPercent)
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

        if (binSize <= 0)
        {
            return null;
        }

        low = Math.Floor(low / binSize) * binSize;
        high = Math.Ceiling(high / binSize) * binSize;
        var rowHeight = binSize;
        var rowCount = Math.Max(1, (int)Math.Ceiling((high - low) / rowHeight));
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

        if (volumes.Sum() <= 0)
        {
            return null;
        }

        var (valueAreaLow, valueAreaHigh) = GetValueArea(volumes, pointOfControl, valueAreaPercent);
        return new DailyVolumeProfile(
            start,
            end,
            name,
            low,
            rowHeight,
            volumes,
            pointOfControl,
            valueAreaLow,
            valueAreaHigh,
            GetHighVolumeNodes(volumes, pointOfControl),
            GetLowVolumeNodes(volumes));
    }

    public static DailyVolumeProfile? Combine(IReadOnlyList<DailyVolumeProfile> profiles, DateTime start, DateTime end, string name, double valueAreaPercent)
    {
        if (profiles.Count == 0)
        {
            return null;
        }

        var rowHeight = profiles[0].RowHeight;
        if (rowHeight <= 0 || profiles.Any(profile => Math.Abs(profile.RowHeight - rowHeight) > rowHeight * 0.0001))
        {
            return null;
        }

        var low = profiles.Min(profile => profile.Low);
        var high = profiles.Max(profile => profile.Low + profile.RowHeight * profile.Volumes.Count);
        var volumes = new double[Math.Max(1, (int)Math.Ceiling((high - low) / rowHeight))];
        foreach (var profile in profiles)
        {
            var offset = (int)Math.Round((profile.Low - low) / rowHeight);
            for (var index = 0; index < profile.Volumes.Count; index++)
            {
                volumes[offset + index] += profile.Volumes[index];
            }
        }

        var pointOfControl = Array.IndexOf(volumes, volumes.Max());
        var (valueAreaLow, valueAreaHigh) = GetValueArea(volumes, pointOfControl, valueAreaPercent);
        return new DailyVolumeProfile(start, end, name, low, rowHeight, volumes, pointOfControl, valueAreaLow, valueAreaHigh, GetHighVolumeNodes(volumes, pointOfControl), GetLowVolumeNodes(volumes));
    }

    private static (int Low, int High) GetValueArea(IReadOnlyList<double> volumes, int pointOfControl, double percent)
    {
        var target = volumes.Sum() * Math.Clamp(percent, 0.01, 1);
        var low = pointOfControl;
        var high = pointOfControl;
        var accumulated = volumes[pointOfControl];
        while (accumulated < target && (low > 0 || high < volumes.Count - 1))
        {
            var below = low > 0 ? volumes[low - 1] : double.NegativeInfinity;
            var above = high < volumes.Count - 1 ? volumes[high + 1] : double.NegativeInfinity;
            if (above > below)
            {
                accumulated += above;
                high++;
            }
            else
            {
                accumulated += below;
                low--;
            }
        }

        return (low, high);
    }

    private static IReadOnlyList<int> GetHighVolumeNodes(IReadOnlyList<double> volumes, int pointOfControl) => Enumerable.Range(1, Math.Max(0, volumes.Count - 2))
        .Where(index => volumes[index] >= volumes[index - 1] && volumes[index] >= volumes[index + 1])
        .Where(index => volumes[index] >= volumes[pointOfControl] * 0.50)
        .OrderByDescending(index => volumes[index])
        .Take(3)
        .ToArray();

    private static IReadOnlyList<int> GetLowVolumeNodes(IReadOnlyList<double> volumes) => Enumerable.Range(1, Math.Max(0, volumes.Count - 2))
        .Where(index => volumes[index] <= volumes[index - 1] && volumes[index] <= volumes[index + 1] && volumes[index] > 0)
        .OrderBy(index => volumes[index])
        .Take(3)
        .ToArray();

    private static int ToBin(double price, double low, double rowHeight, int rowCount) =>
        Math.Clamp((int)Math.Floor((price - low) / rowHeight), 0, rowCount - 1);
}
