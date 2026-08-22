namespace cAlgo.Robots;

public readonly record struct CalendarNewsItem(DateTimeOffset Time, string Currency, string Name, int Importance);
