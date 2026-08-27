namespace cAlgo.Robots;

/// <summary>
/// Shared chart and legend colors. Each overlay family has its own hue family
/// so enabled drawings remain identifiable when several overlays are active.
/// </summary>
public static class GraphColorPalette
{
    public static readonly Color AsiaSession = Color.FromHex("#FF8B7CFF");
    public static readonly Color EuropeSession = Color.FromHex("#FF2CB8F5");
    public static readonly Color UnitedStatesSession = Color.FromHex("#FFF7B955");

    public static readonly Color PriceLevelMinor = Color.FromHex("#FF627E9B");
    public static readonly Color PriceLevelMinorMuted = Color.FromHex("#FF35424F");
    public static readonly Color PriceLevelMinorDistant = Color.FromHex("#FF202A33");
    public static readonly Color PriceLevelMid = Color.FromHex("#FF79D5F7");
    public static readonly Color PriceLevelMidMuted = Color.FromHex("#FF315F71");
    public static readonly Color PriceLevelMidDistant = Color.FromHex("#FF1D3A47");
    public static readonly Color PriceLevelMajor = Color.FromHex("#FFE6B85C");
    public static readonly Color PriceLevelMajorMuted = Color.FromHex("#FF7A5D1E");
    public static readonly Color PriceLevelMajorDistant = Color.FromHex("#FF483713");

    public static readonly Color EmaFast = Color.FromHex("#FF65F2A5");
    public static readonly Color EmaSlow = Color.FromHex("#FFFD77D4");

    public static readonly Color BollingerOuter = Color.FromHex("#FF6F8CFF");
    public static readonly Color BollingerMiddle = Color.FromHex("#FFC7D2FE");

    public static readonly Color Vwap = Color.FromHex("#FFFF5C8A");
    public static readonly Color VwapOneDeviation = Color.FromHex("#FFFF9BBA");
    public static readonly Color VwapTwoDeviation = Color.FromHex("#FFFFC6D8");

    public static readonly Color PreviousDayHigh = Color.FromHex("#FF4FC3F7");
    public static readonly Color PreviousDayLow = Color.FromHex("#FFFF7F96");
    public static readonly Color PreviousDayClose = Color.FromHex("#FFE9EEF5");
    public static readonly Color PreviousWeekHigh = Color.FromHex("#FFB57BFF");
    public static readonly Color PreviousWeekLow = Color.FromHex("#FFFFA657");
    public static readonly Color PreviousWeekClose = Color.FromHex("#FFFFDF78");

    public static readonly Color StrongSupport = Color.FromHex("#FF55D6BE");
    public static readonly Color StrongResistance = Color.FromHex("#FFFF7F96");

    public static readonly Color VolumeProfile = Color.FromHex("#FF38BDF8");
    public static readonly Color VolumeProfilePointOfControl = Color.FromHex("#FFFFB74D");
}
