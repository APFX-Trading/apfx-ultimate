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
    private readonly HashSet<string> _objectNames = new(StringComparer.Ordinal);
    private readonly int _period;
    private readonly double _standardDeviations;
    private RobotContext? _context;

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
        Render();
    }

    public void OnTick(RobotContext context)
    {
    }

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
        if (_context is null || !IsEnabled) return;

        var activeNames = new HashSet<string>(StringComparer.Ordinal);
        DrawLine(BollingerBandLine.Top, Color.FromHex("#FF6FCBFF"), activeNames);
        DrawLine(BollingerBandLine.Main, Color.FromHex("#FFFFD06A"), activeNames);
        DrawLine(BollingerBandLine.Bottom, Color.FromHex("#FF6FCBFF"), activeNames);
        foreach (var staleName in _objectNames.Except(activeNames).ToArray()) _context.RemoveChartObject(staleName);
        _objectNames.Clear();
        _objectNames.UnionWith(activeNames);
    }

    private void DrawLine(BollingerBandLine line, Color color, HashSet<string> activeNames)
    {
        var points = _context!.GetBollingerBandPoints(line);
        for (var index = 1; index < points.Count; index++)
        {
            var start = points[index - 1];
            var end = points[index];
            var name = $"{ObjectPrefix}{line}.{start.Time:yyyyMMddHHmmss}";
            _context.DrawChartTrendLine(name, start.Time, start.Price, end.Time, end.Price, color, 1);
            activeNames.Add(name);
        }
    }

    private void Clear()
    {
        if (_context is null) return;
        foreach (var objectName in _objectNames) _context.RemoveChartObject(objectName);
        _objectNames.Clear();
    }
}
