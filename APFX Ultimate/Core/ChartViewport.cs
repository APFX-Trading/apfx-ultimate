namespace cAlgo.Robots;

/// <summary>
/// The currently visible area of the chart, supplied to graph modules without exposing the chart directly.
/// </summary>
public readonly record struct ChartViewport(DateTime StartTime, DateTime EndTime, double BottomPrice, double TopPrice);
