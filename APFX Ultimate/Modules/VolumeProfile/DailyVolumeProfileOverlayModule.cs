namespace cAlgo.Robots;

/// <summary>Draws a compact tick-volume profile for each of the latest trading days.</summary>
public sealed class DailyVolumeProfileOverlayModule : IRobotModule
{
    private const string ObjectPrefix = "APFXUltimate.Graph.VolumeProfile.";
    private const int RowsPerDay = 24;
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(30);
    private readonly int _daysToShow;
    private readonly HashSet<string> _objectNames = new(StringComparer.Ordinal);
    private RobotContext? _context;
    private DateTime _nextRefreshAt = DateTime.MinValue;

    public DailyVolumeProfileOverlayModule(int daysToShow, bool isEnabled)
    {
        _daysToShow = daysToShow;
        IsEnabled = isEnabled;
    }

    public string Name => nameof(DailyVolumeProfileOverlayModule);
    public string Label => $"Volume Profile  {_daysToShow} days";
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

        var activeNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var profile in _context.GetDailyVolumeProfiles(_daysToShow, RowsPerDay))
        {
            var maximumVolume = profile.Volumes.DefaultIfEmpty().Max();
            if (maximumVolume <= 0)
            {
                continue;
            }

            var profileEnd = profile.StartTime.AddTicks((profile.EndTime - profile.StartTime).Ticks * 35 / 100);
            for (var bin = 0; bin < profile.Volumes.Count; bin++)
            {
                var volume = profile.Volumes[bin];
                if (volume <= 0)
                {
                    continue;
                }

                var binStart = profile.Low + bin * profile.RowHeight;
                var binEnd = binStart + profile.RowHeight;
                var barEnd = profile.StartTime.AddTicks((long)((profileEnd - profile.StartTime).Ticks * volume / maximumVolume));
                var name = ObjectPrefix + profile.StartTime.ToString("yyyyMMdd") + ".bin." + bin;
                var color = bin == profile.PointOfControlIndex
                    ? Color.FromArgb(150, GraphColorPalette.VolumeProfilePointOfControl)
                    : Color.FromArgb(82, GraphColorPalette.VolumeProfile);
                _context.DrawChartRectangle(name, profile.StartTime, barEnd, binStart, binEnd, color);
                activeNames.Add(name);
            }

            var pointOfControl = profile.Low + (profile.PointOfControlIndex + 0.5) * profile.RowHeight;
            var pocName = ObjectPrefix + profile.StartTime.ToString("yyyyMMdd") + ".poc";
            _context.DrawChartTrendLine(pocName, profile.StartTime, pointOfControl, profile.EndTime, pointOfControl, GraphColorPalette.VolumeProfilePointOfControl, 1);
            activeNames.Add(pocName);
        }

        foreach (var staleName in _objectNames.Except(activeNames).ToArray())
        {
            _context.RemoveChartObject(staleName);
        }

        _objectNames.Clear();
        _objectNames.UnionWith(activeNames);
        _nextRefreshAt = _context.ServerTime.Add(RefreshInterval);
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
