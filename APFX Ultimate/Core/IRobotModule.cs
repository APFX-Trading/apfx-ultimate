namespace cAlgo.Robots;

/// <summary>
/// A self-contained part of the robot's behaviour.
/// Add new capabilities as modules instead of growing the robot entry point.
/// </summary>
public interface IRobotModule
{
    string Name { get; }

    void OnStart(RobotContext context);

    void OnTick(RobotContext context);

    void OnTimer(RobotContext context) { }

    void OnStop(RobotContext context);
}
