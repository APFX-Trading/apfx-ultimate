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
        Draw("Day.High", levels.PreviousDay.High, Color.FromHex("#FF4FC3F7"), LineStyle.Dots, activeNames);
        Draw("Day.Low", levels.PreviousDay.Low, Color.FromHex("#FFFF7F96"), LineStyle.Dots, activeNames);
        Draw("Day.Close", levels.PreviousDay.Close, Color.FromHex("#FFE9EEF5"), LineStyle.Dots, activeNames);
        Draw("Week.High", levels.PreviousWeek.High, Color.FromHex("#FFB57BFF"), LineStyle.Solid, activeNames);
        Draw("Week.Low", levels.PreviousWeek.Low, Color.FromHex("#FFFFA657"), LineStyle.Solid, activeNames);
        Draw("Week.Close", levels.PreviousWeek.Close, Color.FromHex("#FFFFDF78"), LineStyle.Solid, activeNames);
        RemoveStaleObjects(activeNames);
    }

    private void Draw(string key, double price, Color color, LineStyle lineStyle, HashSet<string> activeNames)
    {
        var name = ObjectPrefix + key;
        _context!.DrawGraphHorizontalLine(name, price, color, 1, lineStyle);
        activeNames.Add(name);
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
