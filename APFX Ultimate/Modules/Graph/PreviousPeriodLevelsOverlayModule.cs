namespace cAlgo.Robots;

/// <summary>Displays previous completed day and week high, low, and close levels on the main chart.</summary>
public sealed class PreviousPeriodLevelsOverlayModule : IRobotModule
{
    private const string ObjectPrefix = "APFXUltimate.Graph.PreviousLevels.";
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

        var activeNames = new HashSet<string>(StringComparer.Ordinal);
        Draw("Day.High", "Previous Day High", levels.PreviousDay.High, GraphColorPalette.PreviousDayHigh, LineStyle.Dots, activeNames);
        Draw("Day.Low", "Previous Day Low", levels.PreviousDay.Low, GraphColorPalette.PreviousDayLow, LineStyle.Dots, activeNames);
        Draw("Day.Close", "Previous Day Close", levels.PreviousDay.Close, GraphColorPalette.PreviousDayClose, LineStyle.Dots, activeNames);
        Draw("Week.High", "Previous Week High", levels.PreviousWeek.High, GraphColorPalette.PreviousWeekHigh, LineStyle.Solid, activeNames);
        Draw("Week.Low", "Previous Week Low", levels.PreviousWeek.Low, GraphColorPalette.PreviousWeekLow, LineStyle.Solid, activeNames);
        Draw("Week.Close", "Previous Week Close", levels.PreviousWeek.Close, GraphColorPalette.PreviousWeekClose, LineStyle.Solid, activeNames);
        RemoveStaleObjects(activeNames);
    }

    private void Draw(string key, string label, double price, Color color, LineStyle lineStyle, HashSet<string> activeNames)
    {
        var name = ObjectPrefix + key;
        _context!.DrawGraphHorizontalLine(name, price, color, 1, lineStyle);
        activeNames.Add(name);
        var labelName = name + ".Label";
        _context.DrawChartText(labelName, label, GetLabelTime(), price, color);
        activeNames.Add(labelName);
    }

    private DateTime GetLabelTime()
    {
        var viewport = _context!.GetChartViewport();
        return viewport.StartTime.AddTicks((viewport.EndTime - viewport.StartTime).Ticks / 2);
    }

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
