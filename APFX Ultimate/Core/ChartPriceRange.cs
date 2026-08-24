namespace cAlgo.Robots;

/// <summary>
/// The high and low reached by chart bars in a requested time interval.
/// </summary>
public readonly record struct ChartPriceRange(double Low, double High);
