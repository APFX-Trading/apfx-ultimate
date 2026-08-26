namespace cAlgo.Robots;

public enum BollingerBandLine
{
    Top,
    Main,
    Bottom
}

/// <summary>
/// Draws Bollinger Band paths directly on the main chart without adding an indicator pane.
/// </summary>
public sealed class BollingerBandsOverlayModule : IRobotModule
{
    private const string ObjectPrefix = "APFXUltimate.Graph.Bollinger.";
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(5);
    private readonly HashSet<string> _objectNames = new(StringComparer.Ordinal);
    private readonly int _period;
    private readonly double _standardDeviations;
    private RobotContext? _context;
    private DateTime _nextRefreshAt = DateTime.MinValue;
    private DateTime _lastViewportStart = DateTime.MinValue;
    private DateTime _lastViewportEnd = DateTime.MinValue;

    public BollingerBandsOverlayModule(int period, double standardDeviations, bool isEnabled)
    {
        _period = period;
        _standardDeviations = standardDeviations;
        IsEnabled = isEnabled;
    }

    public string Name => nameof(BollingerBandsOverlayModule);
    public bool IsEnabled { get; private set; }
    public string Label => $"Bollinger Bands  {_period} / {_standardDeviations:0.0}";

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
        if (isEnabled) Render(force: true); else Clear();
    }

    private void Render(bool force)
    {
        if (_context is null || !IsEnabled) return;

        if (!force && _context.ServerTime < _nextRefreshAt) return;

        var viewport = _context.GetChartViewport();
        if (_lastViewportStart != viewport.StartTime || _lastViewportEnd != viewport.EndTime)
        {
            RenderFullPath(viewport);
        }
        else
        {
            UpdateLatestSegment(BollingerBandLine.Top, GraphColorPalette.BollingerOuter);
            UpdateLatestSegment(BollingerBandLine.Main, GraphColorPalette.BollingerMiddle);
            UpdateLatestSegment(BollingerBandLine.Bottom, GraphColorPalette.BollingerOuter);
        }
        _nextRefreshAt = _context.ServerTime.Add(RefreshInterval);
    }

    private void RenderFullPath(ChartViewport viewport)
    {
        var activeNames = new HashSet<string>(StringComparer.Ordinal);
        DrawFullLine(BollingerBandLine.Top, GraphColorPalette.BollingerOuter, activeNames);
        DrawFullLine(BollingerBandLine.Main, GraphColorPalette.BollingerMiddle, activeNames);
        DrawFullLine(BollingerBandLine.Bottom, GraphColorPalette.BollingerOuter, activeNames);
        foreach (var staleName in _objectNames.Except(activeNames).ToArray()) _context!.RemoveChartObject(staleName);
        _objectNames.Clear();
        _objectNames.UnionWith(activeNames);
        _lastViewportStart = viewport.StartTime;
        _lastViewportEnd = viewport.EndTime;
    }

    private void UpdateLatestSegment(BollingerBandLine line, Color color)
    {
        var points = _context!.GetBollingerBandPoints(line);
        if (points.Count < 2) return;
        DrawSegment(line, points[^2], points[^1], color);
    }

    private void DrawFullLine(BollingerBandLine line, Color color, HashSet<string> activeNames)
    {
        var points = _context!.GetBollingerBandPoints(line);
        for (var index = 1; index < points.Count; index++)
        {
            DrawSegment(line, points[index - 1], points[index], color);
            var name = GetSegmentName(line, points[index - 1]);
            activeNames.Add(name);
        }
    }

    private void DrawSegment(BollingerBandLine line, ChartTimePricePoint start, ChartTimePricePoint end, Color color) =>
        _context!.DrawChartTrendLine(GetSegmentName(line, start), start.Time, start.Price, end.Time, end.Price, color, 1);

    private static string GetSegmentName(BollingerBandLine line, ChartTimePricePoint start) => $"{ObjectPrefix}{line}.{start.Time:yyyyMMddHHmmss}";

    private void Clear()
    {
        if (_context is null) return;
        foreach (var objectName in _objectNames) _context.RemoveChartObject(objectName);
        _objectNames.Clear();
        _lastViewportStart = DateTime.MinValue;
        _lastViewportEnd = DateTime.MinValue;
    }
}
