namespace cAlgo.Robots;

/// <summary>
/// A point-in-time account summary used by live panel sections.
/// </summary>
public readonly record struct OpenPositionSnapshot(
    int OpenPositionCount,
    double UnrealizedNetProfit,
    double Equity,
    double UsedMargin,
    double FreeMargin);
