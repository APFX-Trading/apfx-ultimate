namespace cAlgo.Robots;
public readonly record struct GridConfiguration(bool Enabled, GridDirection Direction, double TargetProfit, double TrailingLockPercent, double DistanceAmount, bool UseAtrDistance, bool UseAtr, int AtrPeriod, double MaxDrawdownPercent, double MaxDrawdownAmount, int InitialEntries = 3, double DistanceIncrementPips = 50);
