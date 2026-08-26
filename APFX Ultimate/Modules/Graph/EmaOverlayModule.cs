namespace cAlgo.Robots;

public enum EmaLine
{
    Fast,
    Slow
}

/// <summary>
/// Draws fast and slow EMA paths directly on the main chart without creating indicator panes.
/// </summary>
public sealed class EmaOverlayModule : IRobotModule
{
    private const string ObjectPrefix = "APFXUltimate.Graph.Ema.";
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(5);
    private static readonly Color FastColor = Color.FromHex("#FF65F2A5");
    private static readonly Color SlowColor = Color.FromHex("#FFFD77D4");
    private readonly HashSet<string> _objectNames = new(StringComparer.Ordinal);
    private readonly int _fastPeriod;
    private readonly int _slowPeriod;
    private RobotContext? _context;
    private DateTime _nextRefreshAt = DateTime.MinValue;
    private DateTime _lastViewportStart = DateTime.MinValue;
    private DateTime _lastViewportEnd = DateTime.MinValue;

    public EmaOverlayModule(int fastPeriod, int slowPeriod, bool isEnabled)
    {
        _fastPeriod = fastPeriod;
        _slowPeriod = slowPeriod;
        IsEnabled = isEnabled;
    }

    public string Name => nameof(EmaOverlayModule);
    public bool IsEnabled { get; private set; }
    public string Label => $"EMA Fast / Slow  {_fastPeriod} / {_slowPeriod}";

    public void OnStart(RobotContext context)
    {
        _context = context;
        Render(force: true);
    }

    public void OnTick(RobotContext context)
    {
    }

    public void OnTimer(RobotContext context) => Render(force: false);

    public void OnStop(RobotContext context)
    {
        Clear();
        _context = null;
    }

    public void SetEnabled(bool isEnabled)
    {
        IsEnabled = isEnabled;
        if (isEnabled)
        {
            Render(force: true);
        }
        else
        {
            Clear();
        }
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

        var viewport = _context.GetChartViewport();
        if (_lastViewportStart != viewport.StartTime || _lastViewportEnd != viewport.EndTime)
        {
            RenderFullPath(viewport);
        }
        else
        {
            UpdateLatestSegment(EmaLine.Fast, FastColor);
            UpdateLatestSegment(EmaLine.Slow, SlowColor);
        }

        _nextRefreshAt = _context.ServerTime.Add(RefreshInterval);
    }

    private void RenderFullPath(ChartViewport viewport)
    {
        var activeNames = new HashSet<string>(StringComparer.Ordinal);
        DrawFullLine(EmaLine.Fast, FastColor, activeNames);
        DrawFullLine(EmaLine.Slow, SlowColor, activeNames);
        foreach (var staleName in _objectNames.Except(activeNames).ToArray()) _context!.RemoveChartObject(staleName);
        _objectNames.Clear();
        _objectNames.UnionWith(activeNames);
        _lastViewportStart = viewport.StartTime;
        _lastViewportEnd = viewport.EndTime;
    }

    private void UpdateLatestSegment(EmaLine line, Color color)
    {
        var points = _context!.GetEmaPoints(line);
        if (points.Count < 2) return;
        DrawSegment(line, points[^2], points[^1], color);
    }

    private void DrawFullLine(EmaLine line, Color color, HashSet<string> activeNames)
    {
        var points = _context!.GetEmaPoints(line);
        for (var index = 1; index < points.Count; index++)
        {
            DrawSegment(line, points[index - 1], points[index], color);
            var name = GetSegmentName(line, points[index - 1]);
            activeNames.Add(name);
        }
    }

    private void DrawSegment(EmaLine line, ChartTimePricePoint start, ChartTimePricePoint end, Color color) =>
        _context!.DrawChartTrendLine(GetSegmentName(line, start), start.Time, start.Price, end.Time, end.Price, color, 2);

    private static string GetSegmentName(EmaLine line, ChartTimePricePoint start) => $"{ObjectPrefix}{line}.{start.Time:yyyyMMddHHmmss}";

    private void Clear()
    {
        if (_context is null)
        {
            return;
        }

        foreach (var objectName in _objectNames)
        {
            _context.RemoveChartObject(objectName);
        }

        _objectNames.Clear();
        _lastViewportStart = DateTime.MinValue;
        _lastViewportEnd = DateTime.MinValue;
    }
}
