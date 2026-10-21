namespace cAlgo.Robots;

/// <summary>Current UTC-day VWAP and one/two/three/four standard-deviation bands.</summary>
public sealed record DailyVwapData(
    IReadOnlyList<ChartTimePricePoint> Vwap,
    IReadOnlyList<ChartTimePricePoint> UpperOne,
    IReadOnlyList<ChartTimePricePoint> LowerOne,
    IReadOnlyList<ChartTimePricePoint> UpperTwo,
    IReadOnlyList<ChartTimePricePoint> LowerTwo,
    IReadOnlyList<ChartTimePricePoint> UpperThree,
    IReadOnlyList<ChartTimePricePoint> LowerThree,
    IReadOnlyList<ChartTimePricePoint> UpperFour,
    IReadOnlyList<ChartTimePricePoint> LowerFour)
{
    public static DailyVwapData Empty { get; } = new(
        Array.Empty<ChartTimePricePoint>(),
        Array.Empty<ChartTimePricePoint>(),
        Array.Empty<ChartTimePricePoint>(),
        Array.Empty<ChartTimePricePoint>(),
        Array.Empty<ChartTimePricePoint>(),
        Array.Empty<ChartTimePricePoint>(),
        Array.Empty<ChartTimePricePoint>(),
        Array.Empty<ChartTimePricePoint>(),
        Array.Empty<ChartTimePricePoint>());
}
