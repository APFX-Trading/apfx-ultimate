namespace cAlgo.Robots;

/// <summary>
/// Draws upcoming calendar events on the chart. Events sharing a timestamp are represented by one line and label.
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

        for (var groupIndex = 0; groupIndex < groups.Length; groupIndex++)
        {
            var group = groups[groupIndex];
            var importance = group.Max(item => item.Importance);
            var color = GetImpactColor(importance);
            var key = group.Key.ToString("yyyyMMddHHmmss");
            var lineName = ObjectPrefix + key + ".line";
            var labelName = ObjectPrefix + key + ".label";
            var eventText = string.Join(" · ", group.Select(item => $"{item.Currency} {item.Name}"));
            var displayedTime = _context.ConvertUtcToChartTime(group.Key);
            var label = $"{displayedTime:ddd HH:mm}  {eventText}";
            var labelPrice = viewport.TopPrice - labelRange * (0.04 + (groupIndex % 3) * 0.07);

            _context.DrawGraphVerticalLine(lineName, group.Key, color, importance >= 3 ? 2 : 1, LineStyle.Dots);
            _context.DrawChartText(labelName, label, group.Key, labelPrice, color);
            activeNames.Add(lineName);
            activeNames.Add(labelName);
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
}
