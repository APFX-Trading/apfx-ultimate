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

    public IReadOnlyList<DailyVolumeProfile> GetProfiles(DateTime nowUtc, int daysToShow, int rowCount)
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
                ? GetActiveProfile(start, end, rowCount, nowUtc)
                : GetCompletedProfile(start, end, rowCount);
            if (profile is { } value)
            {
                profiles.Add(value);
            }
        }

        return profiles;
    }

    private DailyVolumeProfile? GetCompletedProfile(DateTime start, DateTime end, int rowCount)
    {
        var key = new ProfileCacheKey(start, rowCount);
        if (!_completedProfiles.TryGetValue(key, out var profile))
        {
            profile = VolumeProfileCalculator.CalculateDay(_bars, start, end, rowCount);
            _completedProfiles[key] = profile;
        }

        return profile;
    }

    private DailyVolumeProfile? GetActiveProfile(DateTime start, DateTime end, int rowCount, DateTime nowUtc)
    {
        var key = new ProfileCacheKey(start, rowCount);
        var minute = new DateTime(nowUtc.Year, nowUtc.Month, nowUtc.Day, nowUtc.Hour, nowUtc.Minute, 0, DateTimeKind.Unspecified);
        if (_activeKey != key || _activeMinute != minute)
        {
            _activeProfile = VolumeProfileCalculator.CalculateDay(_bars, start, end, rowCount);
            _activeKey = key;
            _activeMinute = minute;
        }

        return _activeProfile;
    }

    private readonly record struct ProfileCacheKey(DateTime StartTime, int RowCount);
}
