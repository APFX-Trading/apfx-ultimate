namespace cAlgo.Robots;

/// <summary>
/// The normalized trade volume and the current margin estimate for each market direction.
/// </summary>
public readonly record struct TradeVolumeInfo(
    double VolumeInLots,
    double VolumeInUnits,
    double BuyEstimatedMargin,
    double SellEstimatedMargin);
