namespace cAlgo.Robots;

public sealed class CalendarNewsState
{
    public IReadOnlyList<CalendarNewsItem> Items { get; private set; } = Array.Empty<CalendarNewsItem>();
    public string Status { get; private set; } = "Calendar loading...";
    public int Version { get; private set; }

    public void SetItems(IReadOnlyList<CalendarNewsItem> items, DateTimeOffset updatedAt)
    {
        Items = items;
        Status = items.Count == 0
            ? "No upcoming events"
            : $"Updated {updatedAt:HH:mm}";
        Version++;
    }

    public void SetStatus(string status)
    {
        Status = status;
        Version++;
    }
}
