namespace cAlgo.Robots;

public readonly record struct CalendarNewsItem(
    DateTimeOffset Time,
    string Currency,
    string Name,
    int Importance,
    string? Actual,
    string? Forecast,
    string? Previous);
