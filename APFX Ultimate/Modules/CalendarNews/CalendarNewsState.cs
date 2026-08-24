namespace cAlgo.Robots;

public sealed class CalendarNewsState
{
    public IReadOnlyList<CalendarNewsItem> Items { get; private set; } = Array.Empty<CalendarNewsItem>();
    public string Status { get; private set; } = "Calendar loading...";
    public int Version { get; private set; }
    private DateTimeOffset? _updatedAtUtc;

    public void SetItems(IReadOnlyList<CalendarNewsItem> items, DateTimeOffset updatedAtUtc, DateTime displayedUpdatedAt)
    {
        Items = items;
        _updatedAtUtc = updatedAtUtc;
        Status = items.Count == 0
            ? "No upcoming events"
            : $"Updated {displayedUpdatedAt:HH:mm}";
        Version++;
    }

    public void SetStatus(string status)
    {
        Status = status;
        Version++;
    }

    public void RefreshDisplayedTimes(TimeSpan userTimeOffset)
    {
        if (_updatedAtUtc is not null && Items.Count > 0)
        {
            Status = $"Updated {_updatedAtUtc.Value.UtcDateTime.Add(userTimeOffset):HH:mm}";
        }

        Version++;
    }
}
