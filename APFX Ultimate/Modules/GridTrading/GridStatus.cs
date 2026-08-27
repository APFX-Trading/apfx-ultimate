namespace cAlgo.Robots;
public sealed class GridStatus
{
    public string State { get; set; } = "Disabled";
    public bool IsBasketActive { get; set; }
    public int EntriesPlaced { get; set; }
    public double BasketProfit { get; set; }
    public double EffectiveTarget { get; set; }
    public double TrailingFloor { get; set; }
    public double EquityStop { get; set; }
    public double VolumeInLots { get; set; }
    public double BuyMargin { get; set; }
    public double SellMargin { get; set; }
    public GridDirection Direction { get; set; }
    public double BaseTarget { get; set; }
    public double TrailingLockPercent { get; set; }
    public double DistanceAmount { get; set; }
    public bool UseAtr { get; set; }
    public int AtrPeriod { get; set; }
    public double MaxDrawdownPercent { get; set; }
    public double MaxDrawdownAmount { get; set; }
    public double PercentageDrawdownLimit { get; set; }
    public double EffectiveDrawdownLimit { get; set; }
    public double StartingEquity { get; set; }
    public double CurrentEquity { get; set; }
    public double DrawdownUsed { get; set; }
    public double DrawdownRemaining { get; set; }
}
