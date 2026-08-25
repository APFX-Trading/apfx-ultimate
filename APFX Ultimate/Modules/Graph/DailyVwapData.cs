namespace cAlgo.Robots;

/// <summary>Current UTC-day VWAP and one/two standard-deviation bands.</summary>
public sealed record DailyVwapData(
    IReadOnlyList<ChartTimePricePoint> Vwap,
    IReadOnlyList<ChartTimePricePoint> UpperOne,
    IReadOnlyList<ChartTimePricePoint> LowerOne,
    IReadOnlyList<ChartTimePricePoint> UpperTwo,
    IReadOnlyList<ChartTimePricePoint> LowerTwo)
{
    public static DailyVwapData Empty { get; } = new(
        Array.Empty<ChartTimePricePoint>(),
        Array.Empty<ChartTimePricePoint>(),
        Array.Empty<ChartTimePricePoint>(),
        Array.Empty<ChartTimePricePoint>(),
        Array.Empty<ChartTimePricePoint>());
}
