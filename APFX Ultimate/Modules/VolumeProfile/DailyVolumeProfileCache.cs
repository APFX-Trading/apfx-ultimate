namespace cAlgo.Robots;

/// <summary>
/// Keeps completed daily profiles in memory for the bot instance. Only the active day
/// is recalculated, once per minute, because its tick volume can still change.
/// </summary>
public sealed class DailyVolumeProfileCache
{
    private readonly Bars _bars;
    private readonly Dictionary<ProfileCacheKey, DailyVolumeProfile?> _completedProfiles = new();
    private ProfileCacheKey? _activeKey;
    private DateTime _activeMinute = DateTime.MinValue;
    private DailyVolumeProfile? _activeProfile;

    public DailyVolumeProfileCache(Bars bars) => _bars = bars ?? throw new ArgumentNullException(nameof(bars));

    public IReadOnlyList<DailyVolumeProfile> GetProfiles(DateTime nowUtc, int daysToShow, double binSize, double valueAreaPercent)
    {
        if (_bars.Count == 0)
        {
            return Array.Empty<DailyVolumeProfile>();
        }

        var profiles = new List<DailyVolumeProfile>();
        for (var offset = daysToShow - 1; offset >= 0; offset--)
        {
            var start = nowUtc.Date.AddDays(-offset);
            var end = offset == 0 ? nowUtc : start.AddDays(1);
            if (end <= start)
            {
                continue;
            }

            var profile = offset == 0
                ? GetActiveProfile(start, end, binSize, valueAreaPercent, nowUtc)
                : GetCompletedProfile(start, end, binSize, valueAreaPercent);
            if (profile is { } value)
            {
                profiles.Add(value);
            }
        }

        return profiles;
    }

    public IReadOnlyList<DailyVolumeProfile> GetSessionProfiles(DateTime nowUtc, double binSize, double valueAreaPercent)
    {
        var profiles = new List<DailyVolumeProfile>();
        AddSessionProfile(profiles, nowUtc, nowUtc.Date.AddHours(7), nowUtc.Date.AddHours(16), "London", binSize, valueAreaPercent);
        AddSessionProfile(profiles, nowUtc, nowUtc.Date.AddHours(13), nowUtc.Date.AddHours(22), "New York", binSize, valueAreaPercent);
        return profiles;
    }

    public DailyVolumeProfile? GetCompositeProfile(DateTime nowUtc, int days, double binSize, double valueAreaPercent)
    {
        var profiles = GetProfiles(nowUtc, days, binSize, valueAreaPercent);
        return VolumeProfileCalculator.Combine(profiles, nowUtc.Date.AddDays(1 - days), nowUtc, $"{days}D Composite", valueAreaPercent);
    }

    private void AddSessionProfile(List<DailyVolumeProfile> profiles, DateTime nowUtc, DateTime start, DateTime end, string name, double binSize, double valueAreaPercent)
    {
        var effectiveEnd = nowUtc < end ? nowUtc : end;
        if (effectiveEnd > start && VolumeProfileCalculator.CalculatePeriod(_bars, start, effectiveEnd, name, binSize, valueAreaPercent) is { } profile)
        {
            profiles.Add(profile);
        }
    }

    private DailyVolumeProfile? GetCompletedProfile(DateTime start, DateTime end, double binSize, double valueAreaPercent)
    {
        var key = new ProfileCacheKey(start, binSize, valueAreaPercent);
        if (!_completedProfiles.TryGetValue(key, out var profile))
        {
            profile = VolumeProfileCalculator.CalculatePeriod(_bars, start, end, "Day", binSize, valueAreaPercent);
            _completedProfiles[key] = profile;
        }

        return profile;
    }

    private DailyVolumeProfile? GetActiveProfile(DateTime start, DateTime end, double binSize, double valueAreaPercent, DateTime nowUtc)
    {
        var key = new ProfileCacheKey(start, binSize, valueAreaPercent);
        var minute = new DateTime(nowUtc.Year, nowUtc.Month, nowUtc.Day, nowUtc.Hour, nowUtc.Minute, 0, DateTimeKind.Unspecified);
        if (_activeKey != key || _activeMinute != minute)
        {
            _activeProfile = VolumeProfileCalculator.CalculatePeriod(_bars, start, end, "Day", binSize, valueAreaPercent);
            _activeKey = key;
            _activeMinute = minute;
        }

        return _activeProfile;
    }

    private readonly record struct ProfileCacheKey(DateTime StartTime, double BinSize, double ValueAreaPercent);
}
