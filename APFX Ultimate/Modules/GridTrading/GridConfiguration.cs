namespace cAlgo.Robots;
public readonly record struct GridConfiguration(bool Enabled, GridDirection Direction, int AdditionalEntries, double TargetProfit, double TrailingLockPercent, double DistanceAmount, bool UseAtr, int AtrPeriod, double MaxDrawdownPercent);
