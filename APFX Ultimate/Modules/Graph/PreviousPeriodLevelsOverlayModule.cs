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
        Draw("Day.High", "Previous Day High", levels.PreviousDay.High, Color.FromHex("#FF4FC3F7"), LineStyle.Dots, 1, activeNames);
        Draw("Day.Low", "Previous Day Low", levels.PreviousDay.Low, Color.FromHex("#FFFF7F96"), LineStyle.Dots, 2, activeNames);
        Draw("Day.Close", "Previous Day Close", levels.PreviousDay.Close, Color.FromHex("#FFE9EEF5"), LineStyle.Dots, 3, activeNames);
        Draw("Week.High", "Previous Week High", levels.PreviousWeek.High, Color.FromHex("#FFB57BFF"), LineStyle.Solid, 4, activeNames);
        Draw("Week.Low", "Previous Week Low", levels.PreviousWeek.Low, Color.FromHex("#FFFFA657"), LineStyle.Solid, 5, activeNames);
        Draw("Week.Close", "Previous Week Close", levels.PreviousWeek.Close, Color.FromHex("#FFFFDF78"), LineStyle.Solid, 6, activeNames);
        RemoveStaleObjects(activeNames);
    }

    private void Draw(string key, string label, double price, Color color, LineStyle lineStyle, int labelSlot, HashSet<string> activeNames)
    {
        var name = ObjectPrefix + key;
        _context!.DrawGraphHorizontalLine(name, price, color, 1, lineStyle);
        activeNames.Add(name);
        var labelName = name + ".Label";
        _context.DrawChartText(labelName, label, GetLabelTime(labelSlot), price, color);
        activeNames.Add(labelName);
    }

    private DateTime GetLabelTime(int slot)
    {
        var viewport = _context!.GetChartViewport();
        var visibleDuration = viewport.EndTime - viewport.StartTime;
        return viewport.EndTime.AddTicks(-visibleDuration.Ticks * slot / 7);
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
