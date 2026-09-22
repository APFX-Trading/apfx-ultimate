namespace cAlgo.Robots;

/// <summary>
/// Runs modules in registration order and isolates failures to the module that caused them.
/// </summary>
public sealed class RobotModuleRunner
{
    private static readonly TimeSpan SlowOperationThreshold = TimeSpan.FromMilliseconds(250);
    private readonly IReadOnlyList<IRobotModule> _modules;
    private readonly HashSet<string> _reportedSlowOperations = new(StringComparer.Ordinal);
    private RobotContext? _context;
    private bool _measureFirstTick = true;

    public RobotModuleRunner(IReadOnlyList<IRobotModule> modules)
    {
        _modules = modules ?? throw new ArgumentNullException(nameof(modules));
    }

    public void Start(RobotContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _measureFirstTick = true;
        _reportedSlowOperations.Clear();

        foreach (var module in _modules)
        {
            Run(module, module.OnStart, "start");
        }
    }

    public void Tick()
    {
        if (_context is null)
        {
            return;
        }

        var timedPhase = _measureFirstTick ? "first tick" : null;
        foreach (var module in _modules)
        {
            Run(module, module.OnTick, timedPhase);
        }
        _measureFirstTick = false;
    }

    public void Timer()
    {
        if (_context is null)
        {
            return;
        }

        foreach (var module in _modules)
        {
            Run(module, module.OnTimer, "timer");
        }
    }

    public void Stop()
    {
        if (_context is null)
        {
            return;
        }

        foreach (var module in _modules.Reverse())
        {
            Run(module, module.OnStop);
        }
    }

    private void Run(IRobotModule module, Action<RobotContext> lifecycleAction, string? timedPhase = null)
    {
        var startedAt = timedPhase is null ? 0L : System.Diagnostics.Stopwatch.GetTimestamp();
        try
        {
            lifecycleAction(_context!);
        }
        catch (Exception exception)
        {
            _context!.Log($"Module '{module.Name}' failed ({exception.GetType().Name}): {exception}");
        }
        finally
        {
            if (timedPhase is not null)
            {
                var elapsedMilliseconds = (System.Diagnostics.Stopwatch.GetTimestamp() - startedAt) * 1000d / System.Diagnostics.Stopwatch.Frequency;
                if (elapsedMilliseconds >= SlowOperationThreshold.TotalMilliseconds && _reportedSlowOperations.Add(module.Name + "." + timedPhase))
                {
                    _context!.Log($"Slow module {module.Name} {timedPhase}: {elapsedMilliseconds:N0} ms");
                }
            }
        }
    }
}
