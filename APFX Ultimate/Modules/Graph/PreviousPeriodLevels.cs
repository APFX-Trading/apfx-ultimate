namespace cAlgo.Robots;

/// <summary>High, low, and close from the most recently completed daily and weekly bars.</summary>
public readonly record struct PreviousPeriodLevels(
    PeriodLevels PreviousDay,
    PeriodLevels PreviousWeek);

public readonly record struct PeriodLevels(double High, double Low, double Close);
