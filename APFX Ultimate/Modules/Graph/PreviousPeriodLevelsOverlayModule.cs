namespace cAlgo.Robots;

/// <summary>Displays previous completed day and week high, low, and close levels on the main chart.</summary>
public sealed class PreviousPeriodLevelsOverlayModule : IRobotModule
{
    private const string ObjectPrefix = "APFXUltimate.Graph.PreviousLevels.";
    private const double LabelCollisionViewportFraction = 0.03;
    private readonly HashSet<string> _objectNames = new(StringComparer.Ordinal);
    private RobotContext? _context;

    public PreviousPeriodLevelsOverlayModule(bool isEnabled) => IsEnabled = isEnabled;

    public string Name => nameof(PreviousPeriodLevelsOverlayModule);
    public string Label => "Previous Day / Week  H / L / C";
    public bool IsEnabled { get; private set; }

    public void OnStart(RobotContext context)
    {
        _context = context;
        Render();
    }

    public void OnTick(RobotContext context) { }
    public void OnTimer(RobotContext context) => Render();

    public void OnStop(RobotContext context)
    {
        Clear();
        _context = null;
    }

    public void SetEnabled(bool isEnabled)
    {
        IsEnabled = isEnabled;
        if (isEnabled) Render(); else Clear();
    }

    private void Render()
    {
        if (_context is null || !IsEnabled || _context.GetPreviousPeriodLevels() is not { } levels)
        {
            return;
        }

        var displayLevels = new[]
        {
            new DisplayLevel("Day.High", "Previous Day High", levels.PreviousDay.High, GraphColorPalette.PreviousDayHigh, LineStyle.Dots),
            new DisplayLevel("Day.Low", "Previous Day Low", levels.PreviousDay.Low, GraphColorPalette.PreviousDayLow, LineStyle.Dots),
            new DisplayLevel("Day.Close", "Previous Day Close", levels.PreviousDay.Close, GraphColorPalette.PreviousDayClose, LineStyle.Dots),
            new DisplayLevel("Week.High", "Previous Week High", levels.PreviousWeek.High, GraphColorPalette.PreviousWeekHigh, LineStyle.Solid),
            new DisplayLevel("Week.Low", "Previous Week Low", levels.PreviousWeek.Low, GraphColorPalette.PreviousWeekLow, LineStyle.Solid),
            new DisplayLevel("Week.Close", "Previous Week Close", levels.PreviousWeek.Close, GraphColorPalette.PreviousWeekClose, LineStyle.Solid),
        };
        var labelTimes = GetLabelTimes(displayLevels);
        var activeNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var level in displayLevels)
        {
            Draw(level, labelTimes[level.Key], activeNames);
        }
        RemoveStaleObjects(activeNames);
    }

    private void Draw(DisplayLevel level, DateTime labelTime, HashSet<string> activeNames)
    {
        var name = ObjectPrefix + level.Key;
        _context!.DrawGraphHorizontalLine(name, level.Price, level.Color, 1, level.LineStyle);
        activeNames.Add(name);
        var labelName = name + ".Label";
        _context.DrawChartText(labelName, level.Label, labelTime, level.Price, level.Color);
        activeNames.Add(labelName);
    }

    private Dictionary<string, DateTime> GetLabelTimes(IReadOnlyList<DisplayLevel> levels)
    {
        var viewport = _context!.GetChartViewport();
        var duration = viewport.EndTime - viewport.StartTime;
        var midpoint = viewport.StartTime.AddTicks(duration.Ticks / 2);
        var collisionThreshold = Math.Max(0, viewport.TopPrice - viewport.BottomPrice) * LabelCollisionViewportFraction;
        var labelTimes = new Dictionary<string, DateTime>(StringComparer.Ordinal);

        var orderedLevels = levels.OrderBy(level => level.Price).ThenBy(level => level.Key, StringComparer.Ordinal).ToArray();
        for (var start = 0; start < orderedLevels.Length;)
        {
            var end = start + 1;
            while (end < orderedLevels.Length && Math.Abs(orderedLevels[end].Price - orderedLevels[end - 1].Price) <= collisionThreshold)
            {
                end++;
            }

            var groupCount = end - start;
            if (groupCount == 1)
            {
                labelTimes[orderedLevels[start].Key] = midpoint;
            }
            else
            {
                for (var index = 0; index < groupCount; index++)
                {
                    var fraction = 0.25 + 0.5 * index / (groupCount - 1d);
                    labelTimes[orderedLevels[start + index].Key] = viewport.StartTime.AddTicks((long)(duration.Ticks * fraction));
                }
            }

            start = end;
        }

        return labelTimes;
    }

    private readonly record struct DisplayLevel(string Key, string Label, double Price, Color Color, LineStyle LineStyle);

    private void RemoveStaleObjects(HashSet<string> activeNames)
    {
        foreach (var staleName in _objectNames.Except(activeNames).ToArray()) _context!.RemoveChartObject(staleName);
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
