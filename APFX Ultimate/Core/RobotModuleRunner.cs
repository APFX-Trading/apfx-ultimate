namespace cAlgo.Robots;

/// <summary>
/// Runs modules in registration order and isolates failures to the module that caused them.
/// </summary>
public sealed class RobotModuleRunner
{
    private readonly IReadOnlyList<IRobotModule> _modules;
    private RobotContext? _context;

    public RobotModuleRunner(IReadOnlyList<IRobotModule> modules)
    {
        _modules = modules ?? throw new ArgumentNullException(nameof(modules));
    }

    public void Start(RobotContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));

        foreach (var module in _modules)
        {
            Run(module, module.OnStart);
        }
    }

    public void Tick()
    {
        if (_context is null)
        {
            return;
        }

        foreach (var module in _modules)
        {
            Run(module, module.OnTick);
        }
    }

    public void Timer()
    {
        if (_context is null)
        {
            return;
        }

        foreach (var module in _modules)
        {
            Run(module, module.OnTimer);
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

    private void Run(IRobotModule module, Action<RobotContext> lifecycleAction)
    {
        try
        {
            lifecycleAction(_context!);
        }
        catch (Exception exception)
        {
            _context!.Log($"Module '{module.Name}' failed ({exception.GetType().Name}): {exception}");
        }
    }
}
