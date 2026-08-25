namespace cAlgo.Robots;

/// <summary>A daily tick-volume distribution across evenly sized price bins.</summary>
public readonly record struct DailyVolumeProfile(
    DateTime StartTime,
    DateTime EndTime,
    double Low,
    double RowHeight,
    IReadOnlyList<double> Volumes,
    int PointOfControlIndex);
