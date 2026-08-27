namespace cAlgo.Robots;

/// <summary>
/// Chart-symbol values derived from the active bar series and daily bars.
/// </summary>
public readonly record struct ChartContextSnapshot(
    string SymbolName,
    string TimeFrameName,
    double DailyOpen,
    double DailyHigh,
    double DailyLow,
    DateTime CurrentBarOpenTime,
    TimeSpan CurrentBarDuration)
{
    public bool IsAvailable => DailyHigh > 0 && DailyLow > 0 && CurrentBarOpenTime != DateTime.MinValue;
}
