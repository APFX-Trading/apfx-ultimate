namespace cAlgo.Robots;

/// <summary>
/// Draws calendar events on the chart. Simultaneous events share an impact-priority line but keep separate labels.
/// </summary>
public sealed class CalendarNewsChartOverlayModule : IRobotModule
{
    private const string ObjectPrefix = "APFXUltimate.CalendarNews.Chart.";
    private readonly CalendarNewsState _state;
    private readonly HashSet<string> _objectNames = new(StringComparer.Ordinal);
    private RobotContext? _context;
    private int _renderedVersion = -1;

    public CalendarNewsChartOverlayModule(CalendarNewsState state)
    {
        _state = state ?? throw new ArgumentNullException(nameof(state));
    }

    public string Name => nameof(CalendarNewsChartOverlayModule);

    public void OnStart(RobotContext context)
    {
        _context = context;
        Render();
    }

    public void OnTick(RobotContext context) => Render();

    public void OnTimer(RobotContext context) => Render();

    public void OnStop(RobotContext context)
    {
        Clear();
        _context = null;
    }

    private void Render()
    {
        if (_context is null || _renderedVersion == _state.Version)
        {
            return;
        }

        var viewport = _context.GetChartViewport();
        var labelRange = viewport.TopPrice - viewport.BottomPrice;
        if (labelRange <= 0)
        {
            return;
        }

        var groups = _state.Items
            // Chart object timestamps are interpreted in the cBot's UTC timeline.
            // cTrader applies the user's display offset when it renders the chart.
            .GroupBy(item => item.Time.UtcDateTime)
            .OrderBy(group => group.Key)
            .ToArray();
        var activeNames = new HashSet<string>(StringComparer.Ordinal);

        var labelCount = groups.Sum(group => group.Count());
        var labelRow = 0;
        // Keep every release on an individual row while reserving the lower chart area for price action.
        var rowSpacing = Math.Min(0.045, 0.72 / Math.Max(1, labelCount));

        for (var groupIndex = 0; groupIndex < groups.Length; groupIndex++)
        {
            var group = groups[groupIndex];
            var importance = group.Max(item => item.Importance);
            var color = GetImpactColor(importance);
            var key = group.Key.ToString("yyyyMMddHHmmss");
            var lineName = ObjectPrefix + key + ".line";
            var displayedTime = _context.ConvertUtcToChartTime(group.Key);

            _context.DrawGraphVerticalLine(lineName, group.Key, color, importance >= 3 ? 2 : 1, LineStyle.Dots);
            activeNames.Add(lineName);

            foreach (var item in group.OrderByDescending(item => item.Importance).ThenBy(item => item.Currency).ThenBy(item => item.Name))
            {
                var labelName = ObjectPrefix + key + ".label." + labelRow;
                var results = FormatResults(item);
                var label = $"{displayedTime:ddd HH:mm}  {item.Currency} {item.Name}{results}";
                var labelPrice = viewport.TopPrice - labelRange * (0.04 + labelRow * rowSpacing);
                _context.DrawChartText(labelName, label, group.Key, labelPrice, GetImpactColor(item.Importance));
                activeNames.Add(labelName);
                labelRow++;
            }
        }

        foreach (var staleName in _objectNames.Except(activeNames).ToArray())
        {
            _context.RemoveChartObject(staleName);
        }

        _objectNames.Clear();
        _objectNames.UnionWith(activeNames);
        _renderedVersion = _state.Version;
    }

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
    }

    private static Color GetImpactColor(int importance) => importance switch
    {
        >= 3 => Color.FromHex("#FFFF6B7A"),
        2 => Color.FromHex("#FFF7B955"),
        _ => Color.FromHex("#FF6FCBFF")
    };

    private static string FormatResults(CalendarNewsItem item)
    {
        if (string.IsNullOrWhiteSpace(item.Actual) && string.IsNullOrWhiteSpace(item.Forecast) && string.IsNullOrWhiteSpace(item.Previous))
        {
            return string.Empty;
        }

        var actual = string.IsNullOrWhiteSpace(item.Actual) ? "-" : item.Actual;
        var forecast = string.IsNullOrWhiteSpace(item.Forecast) ? "-" : item.Forecast;
        var previous = string.IsNullOrWhiteSpace(item.Previous) ? "-" : item.Previous;
        return $"  |  A:{actual} F:{forecast} P:{previous}";
    }
}
