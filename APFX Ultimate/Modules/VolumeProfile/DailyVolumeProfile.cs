namespace cAlgo.Robots;

/// <summary>A tick-volume distribution across fixed-size price bins for one trading period.</summary>
public readonly record struct DailyVolumeProfile(
    DateTime StartTime,
    DateTime EndTime,
    string Name,
    double Low,
    double RowHeight,
    IReadOnlyList<double> Volumes,
    int PointOfControlIndex,
    int ValueAreaLowIndex,
    int ValueAreaHighIndex,
    IReadOnlyList<int> HighVolumeNodeIndices,
    IReadOnlyList<int> LowVolumeNodeIndices)
{
    public double PointOfControlPrice => GetBinMidpoint(PointOfControlIndex);
    public double ValueAreaLow => Low + ValueAreaLowIndex * RowHeight;
    public double ValueAreaHigh => Low + (ValueAreaHighIndex + 1) * RowHeight;
    public double GetBinMidpoint(int index) => Low + (index + 0.5) * RowHeight;
}

public readonly record struct VolumeProfileConfiguration(
    int DaysToShow,
    double BinSizePips,
    double ValueAreaPercent,
    bool ShowHistogram,
    bool ShowPointOfControl,
    bool ShowValueArea,
    bool ShowNodes,
    bool ShowSessions,
    bool ShowComposite);
