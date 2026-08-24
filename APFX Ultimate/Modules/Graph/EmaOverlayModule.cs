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
    private static readonly Color FastColor = Color.FromHex("#FF65F2A5");
    private static readonly Color SlowColor = Color.FromHex("#FF8B7CFF");
    private readonly HashSet<string> _objectNames = new(StringComparer.Ordinal);
    private readonly int _fastPeriod;
    private readonly int _slowPeriod;
    private RobotContext? _context;

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
        if (isEnabled)
        {
            Render();
        }
        else
        {
            Clear();
        }
    }

    private void Render()
    {
        if (_context is null || !IsEnabled)
        {
            return;
        }

        var activeNames = new HashSet<string>(StringComparer.Ordinal);
        DrawLine(EmaLine.Fast, FastColor, activeNames);
        DrawLine(EmaLine.Slow, SlowColor, activeNames);

        foreach (var staleName in _objectNames.Except(activeNames).ToArray())
        {
            _context.RemoveChartObject(staleName);
        }

        _objectNames.Clear();
        _objectNames.UnionWith(activeNames);
    }

    private void DrawLine(EmaLine line, Color color, HashSet<string> activeNames)
    {
        var points = _context!.GetEmaPoints(line);
        for (var index = 1; index < points.Count; index++)
        {
            var start = points[index - 1];
            var end = points[index];
            var name = $"{ObjectPrefix}{line}.{start.Time:yyyyMMddHHmmss}";
            _context.DrawChartTrendLine(name, start.Time, start.Price, end.Time, end.Price, color, 2);
            activeNames.Add(name);
        }
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
}
