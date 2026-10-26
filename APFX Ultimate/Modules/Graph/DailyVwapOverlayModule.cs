namespace cAlgo.Robots;

/// <summary>Renders the daily VWAP and its first four deviation bands on the main chart.</summary>
public sealed class DailyVwapOverlayModule : IRobotModule
{
    private const string ObjectPrefix = "APFXUltimate.Graph.Vwap.";
    private const int MaximumPointsPerLine = 60;
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(5);
    private readonly HashSet<string> _objectNames = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Queue<string>> _segmentNames = new(StringComparer.Ordinal);
    private readonly Dictionary<string, (string Text, double Price, Color Color)> _labels = new(StringComparer.Ordinal);
    private RobotContext? _context;
    private DateTime _nextRefreshAt = DateTime.MinValue;
    private DateTime _renderedSessionDate = DateTime.MinValue;

    public DailyVwapOverlayModule(bool isEnabled) => IsEnabled = isEnabled;

    public string Name => nameof(DailyVwapOverlayModule);
    public string Label => "Daily VWAP  +/-1 / +/-2 / +/-3 / +/-4 SD";
    public bool IsEnabled { get; private set; }

    public void OnStart(RobotContext context)
    {
        _context = context;
        Render(force: true);
    }

    public void OnTick(RobotContext context) => RefreshLabels();
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
        var sessionDate = data.Vwap.Count == 0 ? DateTime.MinValue : data.Vwap[0].Time.Date;
        if (force || sessionDate != _renderedSessionDate)
        {
            RenderFullPath(data);
            _renderedSessionDate = sessionDate;
        }
        else
        {
            UpdateLatestSegments(data);
        }

        RefreshLabels();
        _nextRefreshAt = _context.ServerTime.Add(RefreshInterval);
    }

    private void RenderFullPath(DailyVwapData data)
    {
        var activeNames = new HashSet<string>(StringComparer.Ordinal);
        _segmentNames.Clear();
        DrawFullLine("Vwap", data.Vwap, GraphColorPalette.Vwap, 2, "Daily VWAP", activeNames);
        DrawFullLine("UpperOne", data.UpperOne, GraphColorPalette.VwapOneDeviation, 1, null, activeNames);
        DrawFullLine("LowerOne", data.LowerOne, GraphColorPalette.VwapOneDeviation, 1, null, activeNames);
        DrawFullLine("UpperTwo", data.UpperTwo, GraphColorPalette.VwapTwoDeviation, 1, null, activeNames);
        DrawFullLine("LowerTwo", data.LowerTwo, GraphColorPalette.VwapTwoDeviation, 1, null, activeNames);
        DrawFullLine("UpperThree", data.UpperThree, GraphColorPalette.VwapThreeDeviation, 1, null, activeNames);
        DrawFullLine("LowerThree", data.LowerThree, GraphColorPalette.VwapThreeDeviation, 1, null, activeNames);
        DrawFullLine("UpperFour", data.UpperFour, GraphColorPalette.VwapFourDeviation, 1, null, activeNames);
        DrawFullLine("LowerFour", data.LowerFour, GraphColorPalette.VwapFourDeviation, 1, null, activeNames);
        RemoveStaleObjects(activeNames);
    }

    private void UpdateLatestSegments(DailyVwapData data)
    {
        DrawLatestSegment("Vwap", data.Vwap, GraphColorPalette.Vwap, 2, "Daily VWAP");
        DrawLatestSegment("UpperOne", data.UpperOne, GraphColorPalette.VwapOneDeviation, 1, null);
        DrawLatestSegment("LowerOne", data.LowerOne, GraphColorPalette.VwapOneDeviation, 1, null);
        DrawLatestSegment("UpperTwo", data.UpperTwo, GraphColorPalette.VwapTwoDeviation, 1, null);
        DrawLatestSegment("LowerTwo", data.LowerTwo, GraphColorPalette.VwapTwoDeviation, 1, null);
        DrawLatestSegment("UpperThree", data.UpperThree, GraphColorPalette.VwapThreeDeviation, 1, null);
        DrawLatestSegment("LowerThree", data.LowerThree, GraphColorPalette.VwapThreeDeviation, 1, null);
        DrawLatestSegment("UpperFour", data.UpperFour, GraphColorPalette.VwapFourDeviation, 1, null);
        DrawLatestSegment("LowerFour", data.LowerFour, GraphColorPalette.VwapFourDeviation, 1, null);
    }

    private void DrawFullLine(string key, IReadOnlyList<ChartTimePricePoint> points, Color color, int thickness, string? label, HashSet<string> activeNames)
    {
        var sampledPoints = Downsample(points);
        var segmentNames = new Queue<string>();
        _segmentNames[key] = segmentNames;
        for (var index = 1; index < sampledPoints.Count; index++)
        {
            DrawSegment(key, sampledPoints[index - 1], sampledPoints[index], color, thickness);
            var name = GetSegmentName(key, sampledPoints[index - 1]);
            activeNames.Add(name);
            segmentNames.Enqueue(name);
        }

        DrawLabel(key, label, sampledPoints, color, activeNames);
    }

    private void DrawLatestSegment(string key, IReadOnlyList<ChartTimePricePoint> points, Color color, int thickness, string? label)
    {
        var sampledPoints = Downsample(points);
        if (sampledPoints.Count >= 2)
        {
            DrawSegment(key, sampledPoints[^2], sampledPoints[^1], color, thickness);
            var name = GetSegmentName(key, sampledPoints[^2]);
            if (_objectNames.Add(name))
            {
                var segmentNames = _segmentNames[key];
                segmentNames.Enqueue(name);
                if (segmentNames.Count > MaximumPointsPerLine - 1)
                {
                    var oldest = segmentNames.Dequeue();
                    _context!.RemoveChartObject(oldest);
                    _objectNames.Remove(oldest);
                }
            }
        }

        DrawLabel(key, label, sampledPoints, color, null);
    }

    private void DrawLabel(string key, string? label, IReadOnlyList<ChartTimePricePoint> sampledPoints, Color color, HashSet<string>? activeNames)
    {
        var labelName = ObjectPrefix + key + ".Label";
        if (sampledPoints.Count > 0)
        {
            var text = label ?? key switch
            {
                "UpperOne" => "VWAP +1 SD", "LowerOne" => "VWAP -1 SD",
                "UpperTwo" => "VWAP +2 SD", "LowerTwo" => "VWAP -2 SD",
                "UpperThree" => "VWAP +3 SD", "LowerThree" => "VWAP -3 SD",
                "UpperFour" => "VWAP +4 SD", "LowerFour" => "VWAP -4 SD",
                _ => key
            };
            _labels[labelName] = (text, sampledPoints[^1].Price, color);
            _objectNames.Add(labelName);
            activeNames?.Add(labelName);
        }
        else
        {
            _labels.Remove(labelName);
            _objectNames.Remove(labelName);
            _context!.RemoveChartObject(labelName);
        }
    }

    private void RefreshLabels()
    {
        if (_context is null || !IsEnabled || _labels.Count == 0) return;
        var market = _context.GetMarketSnapshot();
        var time = _context.GetChartViewport().EndTime;
        var hasDistance = double.IsFinite(market.Bid) && market.Bid > 0
            && double.IsFinite(market.PipSize) && market.PipSize > 0;
        foreach (var (name, label) in _labels)
        {
            var distance = hasDistance
                ? FormattableString.Invariant($" | {Math.Abs(label.Price - market.Bid) / market.PipSize:F0} pips")
                : " | -- pips";
            var text = FormattableString.Invariant($"{label.Text} | {label.Price:F0}") + distance;
            _context.DrawChartText(name, text, time, label.Price, label.Color);
        }
    }

    private void DrawSegment(string key, ChartTimePricePoint start, ChartTimePricePoint end, Color color, int thickness) =>
        _context!.DrawChartTrendLine(GetSegmentName(key, start), start.Time, start.Price, end.Time, end.Price, color, thickness);

    private static string GetSegmentName(string key, ChartTimePricePoint start) => $"{ObjectPrefix}{key}.{start.Time:yyyyMMddHHmmss}";

    private static IReadOnlyList<ChartTimePricePoint> Downsample(IReadOnlyList<ChartTimePricePoint> points)
    {
        if (points.Count <= MaximumPointsPerLine)
        {
            return points;
        }

        var step = Math.Max(1, (points.Count - 1 + MaximumPointsPerLine - 2) / (MaximumPointsPerLine - 1));
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
            _labels.Remove(staleName);
        }

        _objectNames.Clear();
        _objectNames.UnionWith(activeNames);
    }

    private void Clear()
    {
        if (_context is null) return;
        foreach (var name in _objectNames) _context.RemoveChartObject(name);
        _objectNames.Clear();
        _segmentNames.Clear();
        _labels.Clear();
        _renderedSessionDate = DateTime.MinValue;
    }
}
