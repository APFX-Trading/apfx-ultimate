namespace cAlgo.Robots;

/// <summary>
/// A price point on the chart timeline, used by lightweight graph overlays.
/// </summary>
public readonly record struct ChartTimePricePoint(DateTime Time, double Price);
