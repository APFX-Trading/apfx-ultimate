namespace cAlgo.Robots;

/// <summary>Renders the daily VWAP and its first and second deviation bands on the main chart.</summary>
public sealed class DailyVwapOverlayModule : IRobotModule
{
    private const string ObjectPrefix = "APFXUltimate.Graph.Vwap.";
    private const int MaximumPointsPerLine = 120;
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(5);
    private readonly HashSet<string> _objectNames = new(StringComparer.Ordinal);
    private RobotContext? _context;
    private DateTime _nextRefreshAt = DateTime.MinValue;

    public DailyVwapOverlayModule(bool isEnabled) => IsEnabled = isEnabled;

    public string Name => nameof(DailyVwapOverlayModule);
    public string Label => "Daily VWAP  +/-1 / +/-2 SD";
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
        if (isEnabled) Render(force: true); else Clear();
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

        var data = _context.GetDailyVwapData();
        var activeNames = new HashSet<string>(StringComparer.Ordinal);
        DrawLine("Vwap", data.Vwap, Color.FromHex("#FFFF5C8A"), 2, activeNames);
        DrawLine("UpperOne", data.UpperOne, Color.FromHex("#FF55D6BE"), 1, activeNames);
        DrawLine("LowerOne", data.LowerOne, Color.FromHex("#FF55D6BE"), 1, activeNames);
        DrawLine("UpperTwo", data.UpperTwo, Color.FromHex("#FF7C8CFF"), 1, activeNames);
        DrawLine("LowerTwo", data.LowerTwo, Color.FromHex("#FF7C8CFF"), 1, activeNames);
        RemoveStaleObjects(activeNames);
        _nextRefreshAt = _context.ServerTime.Add(RefreshInterval);
    }

    private void DrawLine(string key, IReadOnlyList<ChartTimePricePoint> points, Color color, int thickness, HashSet<string> activeNames)
    {
        var sampledPoints = Downsample(points);
        for (var index = 1; index < sampledPoints.Count; index++)
        {
            var start = sampledPoints[index - 1];
            var end = sampledPoints[index];
            var name = $"{ObjectPrefix}{key}.{start.Time:yyyyMMddHHmmss}";
            _context!.DrawChartTrendLine(name, start.Time, start.Price, end.Time, end.Price, color, thickness);
            activeNames.Add(name);
        }
    }

    private static IReadOnlyList<ChartTimePricePoint> Downsample(IReadOnlyList<ChartTimePricePoint> points)
    {
        if (points.Count <= MaximumPointsPerLine)
        {
            return points;
        }

        var step = Math.Max(1, (points.Count - 1) / (MaximumPointsPerLine - 1));
        var sampled = new List<ChartTimePricePoint>();
        for (var index = 0; index < points.Count; index += step)
        {
            sampled.Add(points[index]);
        }

        if (sampled[^1].Time != points[^1].Time)
        {
            sampled.Add(points[^1]);
        }

        return sampled;
    }

    private void RemoveStaleObjects(HashSet<string> activeNames)
    {
        foreach (var staleName in _objectNames.Except(activeNames).ToArray())
        {
            _context!.RemoveChartObject(staleName);
        }

        _objectNames.Clear();
        _objectNames.UnionWith(activeNames);
    }

    private void Clear()
    {
        if (_context is null) return;
        foreach (var name in _objectNames) _context.RemoveChartObject(name);
        _objectNames.Clear();
    }
}
