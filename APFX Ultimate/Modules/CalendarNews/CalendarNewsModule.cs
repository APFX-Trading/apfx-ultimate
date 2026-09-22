using System.Text.Json;

namespace cAlgo.Robots;

/// <summary>
/// Loads the configured AP Forex Trading calendar feed without blocking market-tick processing.
/// </summary>
public sealed class CalendarNewsModule : IRobotModule
{
    private const string Endpoint = "https://apforextrading.com/api/calendar/next";
    private const string CacheKey = "APFXUltimateCalendarNewsCacheV1";
    private const int MaximumNewsItems = 100;
    private const int MaximumFutureNewsItems = 5;
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromHours(6);
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(30);
    private readonly CalendarNewsConfiguration _configuration;
    private readonly CalendarNewsState _state;
    private DateTime _nextRefreshAt = DateTime.MinValue;
    private DateTime _requestStartedAtUtc = DateTime.MinValue;
    private bool _requestPending;
    private long _requestSequence;
    private long _pendingRequestId;
    private bool _showingCachedItems;

    public CalendarNewsModule(CalendarNewsConfiguration configuration, CalendarNewsState state)
    {
        _configuration = configuration;
        _state = state;
    }

    public string Name => nameof(CalendarNewsModule);

    public void OnStart(RobotContext context)
    {
        RestoreCache(context);
        RefreshIfDue(context);
    }

    public void OnTick(RobotContext context) => RefreshIfDue(context);

    public void OnTimer(RobotContext context) => RefreshIfDue(context);

    public void OnStop(RobotContext context)
    {
        _requestPending = false;
        _requestSequence++;
    }

    private void RefreshIfDue(RobotContext context)
    {
        if (_requestPending)
        {
            if (DateTime.UtcNow - _requestStartedAtUtc < RequestTimeout)
            {
                return;
            }

            _requestPending = false;
            _nextRefreshAt = context.ServerTime.Add(RetryDelay);
            _state.SetStatus(WithCacheStatus("Request timed out; retrying"));
            context.Log("Calendar request timed out before a response was received.");
            return;
        }

        if (context.ServerTime < _nextRefreshAt)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_configuration.ApiKey))
        {
            _state.SetStatus(WithCacheStatus("Set Calendar API Key to load news"));
            _nextRefreshAt = context.ServerTime.AddMinutes(_configuration.RefreshMinutes);
            return;
        }

        var importanceLevels = string.Join(",", Enumerable.Range((int)_configuration.MinimumImpact, 4 - (int)_configuration.MinimumImpact));
        var request = new HttpRequest(new Uri($"{Endpoint}?hours={_configuration.HoursAhead}&limit={MaximumNewsItems}&importanceLevelsCsv={importanceLevels}"))
        {
            Timeout = RequestTimeout,
            Version = new Version(1, 1)
        };
        request.Headers.Add("x-api-key", _configuration.ApiKey.Trim());

        var requestId = ++_requestSequence;
        _pendingRequestId = requestId;
        _requestStartedAtUtc = DateTime.UtcNow;
        _requestPending = true;
        _state.SetStatus(_showingCachedItems ? "Cached calendar; refreshing" : "Calendar request in progress");

        try
        {
            context.SendHttpRequest(request, response => context.BeginInvokeOnMainThread(() => HandleResponse(context, response, requestId)));
        }
        catch (Exception exception)
        {
            _requestPending = false;
            _nextRefreshAt = context.ServerTime.Add(RetryDelay);
            _state.SetStatus(WithCacheStatus("Request could not be started"));
            context.Log($"Calendar request could not be started: {exception.Message}");
        }
    }

    private void HandleResponse(RobotContext context, HttpResponse response, long requestId)
    {
        if (!_requestPending || requestId != _pendingRequestId)
        {
            return;
        }

        _requestPending = false;
        _nextRefreshAt = context.ServerTime.AddMinutes(_configuration.RefreshMinutes);

        try
        {
            if (!response.IsSuccessful)
            {
                var status = response.StatusCode;
                _state.SetStatus(WithCacheStatus(status == 401 ? "API key rejected" : $"Unavailable ({status})"));
                context.Log($"Calendar request failed with HTTP {status}: {response.Exception?.Message ?? "no response body"}");
                return;
            }

            var parsedItems = ParseItems(response.Body);
            var items = SelectDisplayItems(parsedItems, context);
            var updatedAtUtc = DateTimeOffset.UtcNow;
            _state.SetItems(items, updatedAtUtc, context.ConvertUtcToChartTime(updatedAtUtc.UtcDateTime));
            _showingCachedItems = false;
            SaveCache(context, new CalendarCache(updatedAtUtc, _configuration.HoursAhead, _configuration.MinimumImpact, parsedItems.ToArray()));
        }
        catch (JsonException exception)
        {
            _state.SetStatus(WithCacheStatus("Returned invalid data"));
            context.Log($"Calendar response could not be parsed: {exception.Message}");
        }
        catch (Exception exception)
        {
            _state.SetStatus(WithCacheStatus("Response could not be processed"));
            context.Log($"Calendar response handling failed: {exception.Message}");
        }
    }

    private void RestoreCache(RobotContext context)
    {
        try
        {
            var stored = context.GetLocalValue(CacheKey);
            if (string.IsNullOrWhiteSpace(stored)) return;
            var cache = JsonSerializer.Deserialize<CalendarCache>(stored);
            if (cache?.Items is null) return;
            var age = DateTimeOffset.UtcNow - cache.SavedAtUtc;
            if (age < TimeSpan.Zero || age > CacheLifetime || cache.Items.Length > MaximumNewsItems
                || cache.Items.Any(item => string.IsNullOrWhiteSpace(item.Currency) || string.IsNullOrWhiteSpace(item.Name) || item.Importance is < 0 or > 3)
                || cache.HoursAhead != _configuration.HoursAhead || cache.MinimumImpact != _configuration.MinimumImpact)
            {
                return;
            }

            var items = SelectDisplayItems(cache.Items, context);
            _state.SetItems(items, cache.SavedAtUtc, context.ConvertUtcToChartTime(cache.SavedAtUtc.UtcDateTime));
            _showingCachedItems = true;
        }
        catch (JsonException) { }
        catch (ArgumentException) { }
    }

    private string WithCacheStatus(string message) => _showingCachedItems ? $"Cached calendar; {message}" : $"Calendar {message}";

    private static void SaveCache(RobotContext context, CalendarCache cache)
    {
        try
        {
            context.SetLocalValue(CacheKey, JsonSerializer.Serialize(cache));
        }
        catch (ArgumentException) { }
    }

    private sealed record CalendarCache(DateTimeOffset SavedAtUtc, int HoursAhead, CalendarMinimumImpact MinimumImpact, CalendarNewsItem[] Items);

    private static IReadOnlyList<CalendarNewsItem> ParseItems(string body)
    {
        using var document = JsonDocument.Parse(body);
        if (!document.RootElement.TryGetProperty("items", out var itemsElement) || itemsElement.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException("Calendar response does not contain items.");
        }

        var items = new List<CalendarNewsItem>();
        foreach (var item in itemsElement.EnumerateArray())
        {
            if (!item.TryGetProperty("time", out var timeElement) || !DateTimeOffset.TryParse(timeElement.GetString(), out var time) ||
                !item.TryGetProperty("event", out var eventElement) ||
                !eventElement.TryGetProperty("currency", out var currencyElement) ||
                !eventElement.TryGetProperty("name", out var nameElement) ||
                !eventElement.TryGetProperty("importance", out var importanceElement) ||
                !importanceElement.TryGetInt32(out var importance))
            {
                continue;
            }

            var currency = currencyElement.GetString();
            var name = nameElement.GetString();
            if (string.IsNullOrWhiteSpace(currency) || string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            items.Add(new CalendarNewsItem(
                time,
                currency,
                name,
                importance,
                GetOptionalValue(item, "actual"),
                GetOptionalValue(item, "forecast"),
                GetOptionalValue(item, "previous")));
        }

        return items.OrderBy(item => item.Time).ToArray();
    }

    private static string? GetOptionalValue(JsonElement item, string propertyName)
    {
        if (!item.TryGetProperty(propertyName, out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString();
    }

    private static IReadOnlyList<CalendarNewsItem> SelectDisplayItems(IReadOnlyList<CalendarNewsItem> items, RobotContext context)
    {
        var chartNow = context.ConvertUtcToChartTime(DateTime.UtcNow);
        var todayItems = items
            .Where(item => context.ConvertUtcToChartTime(item.Time.UtcDateTime).Date == chartNow.Date);
        var futureItems = items
            .Where(item => context.ConvertUtcToChartTime(item.Time.UtcDateTime).Date > chartNow.Date)
            .Take(MaximumFutureNewsItems);

        return todayItems
            .Concat(futureItems)
            .OrderBy(item => item.Time)
            .ToArray();
    }
}
