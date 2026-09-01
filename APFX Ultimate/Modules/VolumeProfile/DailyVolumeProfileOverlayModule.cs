namespace cAlgo.Robots;

/// <summary>Draws a compact tick-volume profile for each of the latest trading days.</summary>
public sealed class DailyVolumeProfileOverlayModule : IRobotModule
{
    private const string ObjectPrefix = "APFXUltimate.Graph.VolumeProfile.";
    private const int MaximumHistogramBarsPerProfile = 120;
    private const double LabelCollisionViewportFraction = 0.03;
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(30);
    private readonly VolumeProfileConfiguration _configuration;
    private readonly DailyVolumeProfileCache _profileCache;
    private readonly double _pipSize;
    private readonly Func<IReadOnlyList<double>> _getMarketPulseLevels;
    private readonly HashSet<string> _objectNames = new(StringComparer.Ordinal);
    private RobotContext? _context;
    private DateTime _nextRefreshAt = DateTime.MinValue;
    private DateTime _lastRenderedMinute = DateTime.MinValue;

    public DailyVolumeProfileOverlayModule(VolumeProfileConfiguration configuration, DailyVolumeProfileCache profileCache, double pipSize, Func<IReadOnlyList<double>> getMarketPulseLevels, bool isEnabled)
    {
        _configuration = configuration;
        _profileCache = profileCache ?? throw new ArgumentNullException(nameof(profileCache));
        _pipSize = pipSize;
        _getMarketPulseLevels = getMarketPulseLevels ?? throw new ArgumentNullException(nameof(getMarketPulseLevels));
        IsEnabled = isEnabled;
    }

    public string Name => nameof(DailyVolumeProfileOverlayModule);
    public string Label => $"Volume Profile  {_configuration.DaysToShow} days";
    public bool IsEnabled { get; private set; }

    public void OnStart(RobotContext context)
    {
        _context = context;
        Render(force: true);
    }

    public void OnTick(RobotContext context) { }

    public void OnTimer(RobotContext context) => Render(force: false);

    public void OnStop(RobotContext context)
    {
        Clear();
        _context = null;
    }

    public void SetEnabled(bool isEnabled)
    {
        IsEnabled = isEnabled;
        if (!isEnabled)
        {
            Clear();
            return;
        }

        Render(force: true);
    }

    private void Render(bool force)
    {
        if (_context is null || !IsEnabled)
        {
            return;
        }

        if (!force && _context.ServerTime < _nextRefreshAt)
        {
            return;
        }

        var currentMinute = new DateTime(_context.ServerTimeUtc.Year, _context.ServerTimeUtc.Month, _context.ServerTimeUtc.Day, _context.ServerTimeUtc.Hour, _context.ServerTimeUtc.Minute, 0, DateTimeKind.Unspecified);
        if (!force && currentMinute == _lastRenderedMinute)
        {
            _nextRefreshAt = _context.ServerTime.Add(RefreshInterval);
            return;
        }

        var binSize = _configuration.BinSizePips * _pipSize;
        var profiles = _profileCache.GetProfiles(_context.ServerTimeUtc, _configuration.DaysToShow, binSize, _configuration.ValueAreaPercent).ToList();
        if (_configuration.ShowSessions)
        {
            profiles.AddRange(_profileCache.GetSessionProfiles(_context.ServerTimeUtc, binSize, _configuration.ValueAreaPercent));
        }
        if (_configuration.ShowComposite && _profileCache.GetCompositeProfile(_context.ServerTimeUtc, 5, binSize, _configuration.ValueAreaPercent) is { } composite)
        {
            profiles.Add(composite);
        }

        var activeNames = new HashSet<string>(StringComparer.Ordinal);
        var labels = new List<ProfileLevelLabel>();
        foreach (var profile in profiles)
        {
            var maximumVolume = profile.Volumes.DefaultIfEmpty().Max();
            if (maximumVolume <= 0)
            {
                continue;
            }

            var isDeveloping = profile.EndTime >= _context.ServerTimeUtc.AddMinutes(-1);
            var profileEnd = profile.StartTime.AddTicks((profile.EndTime - profile.StartTime).Ticks * 35 / 100);
            if (_configuration.ShowHistogram)
            {
                DrawHistogram(profile, profileEnd, maximumVolume, isDeveloping, activeNames);
            }

            if (profile.Name != "Day" || profile.EndTime >= _context.ServerTimeUtc.Date.AddDays(-1))
            {
                DrawKeyLevels(profile, activeNames, labels);
            }
        }

        DrawLabels(labels, activeNames);

        foreach (var staleName in _objectNames.Except(activeNames).ToArray())
        {
            _context.RemoveChartObject(staleName);
        }

        _objectNames.Clear();
        _objectNames.UnionWith(activeNames);
        _lastRenderedMinute = currentMinute;
        _nextRefreshAt = _context.ServerTime.Add(RefreshInterval);
    }

    private void DrawHistogram(DailyVolumeProfile profile, DateTime profileEnd, double maximumVolume, bool isDeveloping, HashSet<string> activeNames)
    {
        var binGroupSize = Math.Max(1, (int)Math.Ceiling(profile.Volumes.Count / (double)MaximumHistogramBarsPerProfile));
        for (var firstBin = 0; firstBin < profile.Volumes.Count; firstBin += binGroupSize)
        {
            var lastBin = Math.Min(profile.Volumes.Count, firstBin + binGroupSize);
            var volume = 0d;
            for (var bin = firstBin; bin < lastBin; bin++) volume += profile.Volumes[bin];
            if (volume <= 0) continue;

            var binStart = profile.Low + firstBin * profile.RowHeight;
            var binEnd = profile.Low + lastBin * profile.RowHeight;
            var normalizedMaximum = maximumVolume * (lastBin - firstBin);
            var barEnd = profile.StartTime.AddTicks((long)((profileEnd - profile.StartTime).Ticks * volume / Math.Max(1, normalizedMaximum)));
            var name = ObjectPrefix + profile.Name + "." + profile.StartTime.ToString("yyyyMMddHHmm") + ".bin." + firstBin;
            var includesPoc = profile.PointOfControlIndex >= firstBin && profile.PointOfControlIndex < lastBin;
            var alpha = isDeveloping ? 105 : 45;
            var color = includesPoc
                ? Color.FromArgb(isDeveloping ? 165 : 95, GraphColorPalette.VolumeProfilePointOfControl)
                : Color.FromArgb(alpha, GraphColorPalette.VolumeProfile);
            _context!.DrawChartRectangle(name, profile.StartTime, barEnd, binStart, binEnd, color);
            activeNames.Add(name);
        }
    }

    private void DrawKeyLevels(DailyVolumeProfile profile, HashSet<string> activeNames, List<ProfileLevelLabel> labels)
    {
        var prefix = ObjectPrefix + profile.Name + "." + profile.StartTime.ToString("yyyyMMddHHmm");
        if (_configuration.ShowPointOfControl)
        {
            DrawLevel(prefix + ".poc", "POC", profile.PointOfControlPrice, GraphColorPalette.VolumeProfilePointOfControl, profile, activeNames, labels, 2);
        }
        if (_configuration.ShowValueArea)
        {
            DrawLevel(prefix + ".vah", "VAH", profile.ValueAreaHigh, GraphColorPalette.VolumeProfileValueArea, profile, activeNames, labels, 1);
            DrawLevel(prefix + ".val", "VAL", profile.ValueAreaLow, GraphColorPalette.VolumeProfileValueArea, profile, activeNames, labels, 1);
        }
        if (_configuration.ShowNodes)
        {
            foreach (var index in profile.HighVolumeNodeIndices)
            {
                DrawLevel(prefix + ".hvn." + index, "HVN", profile.GetBinMidpoint(index), GraphColorPalette.VolumeProfileHighVolumeNode, profile, activeNames, labels, 1);
            }
            foreach (var index in profile.LowVolumeNodeIndices)
            {
                DrawLevel(prefix + ".lvn." + index, "LVN", profile.GetBinMidpoint(index), GraphColorPalette.VolumeProfileLowVolumeNode, profile, activeNames, labels, 1);
            }
        }
    }

    private void DrawLevel(string name, string label, double price, Color color, DailyVolumeProfile profile, HashSet<string> activeNames, List<ProfileLevelLabel> labels, int thickness)
    {
        var isConfluent = HasConfluence(price, profile.RowHeight);
        var displayColor = isConfluent ? GraphColorPalette.VolumeProfileConfluence : color;
        _context!.DrawChartTrendLine(name, profile.StartTime, price, _context.ServerTimeUtc, price, displayColor, isConfluent ? thickness + 1 : thickness);
        activeNames.Add(name);
        labels.Add(new ProfileLevelLabel(name + ".label", $"{profile.Name} {label}{(isConfluent ? " *" : string.Empty)}", price, displayColor));
    }

    private void DrawLabels(IReadOnlyList<ProfileLevelLabel> labels, HashSet<string> activeNames)
    {
        if (labels.Count == 0) return;

        var viewport = _context!.GetChartViewport();
        var duration = viewport.EndTime - viewport.StartTime;
        var midpoint = viewport.StartTime.AddTicks(duration.Ticks / 2);
        var threshold = Math.Max(0, viewport.TopPrice - viewport.BottomPrice) * LabelCollisionViewportFraction;
        var ordered = labels.OrderBy(label => label.Price).ThenBy(label => label.Name, StringComparer.Ordinal).ToArray();
        for (var start = 0; start < ordered.Length;)
        {
            var end = start + 1;
            while (end < ordered.Length && Math.Abs(ordered[end].Price - ordered[end - 1].Price) <= threshold) end++;
            var count = end - start;
            for (var index = 0; index < count; index++)
            {
                var time = count == 1
                    ? midpoint
                    : viewport.StartTime.AddTicks((long)(duration.Ticks * (0.20 + 0.60 * index / (count - 1d))));
                var label = ordered[start + index];
                _context.DrawChartText(label.Name, label.Text, time, label.Price, label.Color);
                activeNames.Add(label.Name);
            }
            start = end;
        }
    }

    private readonly record struct ProfileLevelLabel(string Name, string Text, double Price, Color Color);

    private bool HasConfluence(double price, double tolerance)
    {
        var previous = _context!.GetPreviousPeriodLevels();
        if (previous is { } levels)
        {
            var prices = new[]
            {
                levels.PreviousDay.High, levels.PreviousDay.Low, levels.PreviousDay.Close,
                levels.PreviousWeek.High, levels.PreviousWeek.Low, levels.PreviousWeek.Close
            };
            if (prices.Any(level => Math.Abs(level - price) <= tolerance)) return true;
        }

        var vwap = _context.GetDailyVwapData().Vwap;
        return (vwap.Count > 0 && Math.Abs(vwap[^1].Price - price) <= tolerance)
            || _getMarketPulseLevels().Any(level => Math.Abs(level - price) <= tolerance);
    }

    private void Clear()
    {
        if (_context is null)
        {
            return;
        }

        foreach (var name in _objectNames)
        {
            _context.RemoveChartObject(name);
        }

        _objectNames.Clear();
    }
}
