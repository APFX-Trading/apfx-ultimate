using System.Text.Json;

namespace cAlgo.Robots;

/// <summary>Loads daily U.S. Treasury constant-maturity yields from FRED for display-only market context.</summary>
public sealed class TreasuryYieldModule : IRobotModule
{
    private const string Endpoint = "https://api.stlouisfed.org/fred/series/observations";
    private const string CacheKey = "APFXUltimateTreasuryYieldsCacheV1";
    private static readonly (string SeriesId, string Maturity)[] Series = { ("DGS2", "2Y"), ("DGS10", "10Y"), ("DGS30", "30Y") };
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromHours(36);
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMinutes(5);
    private readonly TreasuryYieldConfiguration _configuration;
    private readonly TreasuryYieldState _state;
    private DateTime _nextRefreshAt = DateTime.MinValue;
    private DateTime _requestStartedAtUtc = DateTime.MinValue;
    private long _requestSequence;
    private int _pendingRequests;
    private bool _requestFailed;
    private string _requestFailureStatus = "Some Treasury yields unavailable; retrying";
    private bool _showingCachedYields;

    public TreasuryYieldModule(TreasuryYieldConfiguration configuration, TreasuryYieldState state)
    {
        _configuration = configuration;
        _state = state;
    }

    public string Name => nameof(TreasuryYieldModule);
    public void OnStart(RobotContext context)
    {
        RestoreCache(context);
        RefreshIfDue(context);
    }
    public void OnTick(RobotContext context) => RefreshIfDue(context);
    public void OnTimer(RobotContext context) => RefreshIfDue(context);
    public void OnStop(RobotContext context)
    {
        _requestSequence++;
        _pendingRequests = 0;
    }

    private void RefreshIfDue(RobotContext context)
    {
        if (_pendingRequests > 0)
        {
            if (DateTime.UtcNow - _requestStartedAtUtc < RequestTimeout) return;
            _requestSequence++;
            _pendingRequests = 0;
            _nextRefreshAt = context.ServerTime.Add(RetryDelay);
            _state.SetStatus(WithCacheStatus("Request timed out; retrying later"));
            return;
        }

        if (context.ServerTime < _nextRefreshAt) return;
        if (string.IsNullOrWhiteSpace(_configuration.ApiKey))
        {
            _state.SetStatus(WithCacheStatus("Set FRED API Key to load yields"));
            _nextRefreshAt = context.ServerTime.AddMinutes(_configuration.RefreshMinutes);
            return;
        }

        var requestId = ++_requestSequence;
        _pendingRequests = Series.Length;
        _requestFailed = false;
        _requestFailureStatus = "Some Treasury yields unavailable; retrying";
        _state.SetStatus(_showingCachedYields ? "Cached Treasury yields; refreshing" : "Loading Treasury yields...");
        foreach (var series in Series)
        {
            RequestSeries(context, series, requestId);
        }
    }

    private void RequestSeries(RobotContext context, (string SeriesId, string Maturity) series, long requestId)
    {
        var uri = new Uri($"{Endpoint}?series_id={series.SeriesId}&api_key={Uri.EscapeDataString(_configuration.ApiKey.Trim())}&file_type=json&sort_order=desc&limit=10");
        var request = new HttpRequest(uri) { Timeout = RequestTimeout, Version = new Version(1, 1) };
        _requestStartedAtUtc = DateTime.UtcNow;
        try
        {
            context.SendHttpRequest(request, response => context.BeginInvokeOnMainThread(() => HandleResponse(context, response, series, requestId)));
        }
        catch (Exception exception)
        {
            context.Log($"Treasury request could not be started: {exception.Message}");
            _requestFailureStatus = "Treasury request could not be started";
            CompleteRequest(context, requestId, failed: true);
        }
    }

    private void HandleResponse(RobotContext context, HttpResponse response, (string SeriesId, string Maturity) series, long requestId)
    {
        if (requestId != _requestSequence || _pendingRequests == 0) return;
        if (!response.IsSuccessful)
        {
            context.Log($"Treasury request failed with HTTP {response.StatusCode}: {response.Exception?.Message ?? "no response body"}");
            if (response.StatusCode == 401 || _requestFailureStatus != "FRED API key rejected")
            {
                _requestFailureStatus = response.StatusCode == 401 ? "FRED API key rejected" : $"Treasury data unavailable ({response.StatusCode})";
            }
            CompleteRequest(context, requestId, failed: true);
            return;
        }

        try
        {
            _state.SetYield(ParseYield(response.Body, series.Maturity));
        }
        catch (JsonException exception)
        {
            context.Log($"Treasury response could not be parsed: {exception.Message}");
            _requestFailureStatus = "FRED returned invalid Treasury data";
            CompleteRequest(context, requestId, failed: true);
            return;
        }

        CompleteRequest(context, requestId, failed: false);
    }

    private void CompleteRequest(RobotContext context, long requestId, bool failed)
    {
        if (requestId != _requestSequence || _pendingRequests == 0) return;
        _requestFailed |= failed;
        if (--_pendingRequests != 0) return;

        _nextRefreshAt = context.ServerTime.Add(_requestFailed ? RetryDelay : TimeSpan.FromMinutes(_configuration.RefreshMinutes));
        _state.SetStatus(_requestFailed ? WithCacheStatus(_requestFailureStatus) : $"Updated {context.ServerTime:dd MMM HH:mm}");
        if (!_requestFailed)
        {
            _showingCachedYields = false;
            SaveCache(context);
        }
    }

    private void RestoreCache(RobotContext context)
    {
        try
        {
            var stored = context.GetLocalValue(CacheKey);
            if (string.IsNullOrWhiteSpace(stored)) return;
            var cache = JsonSerializer.Deserialize<TreasuryCache>(stored);
            if (cache?.Yields is null) return;
            var age = DateTimeOffset.UtcNow - cache.SavedAtUtc;
            if (age < TimeSpan.Zero || age > CacheLifetime || cache.Yields.Length != Series.Length
                || cache.Yields.Select(yield => yield.Maturity).Distinct(StringComparer.Ordinal).Count() != Series.Length) return;
            foreach (var yield in cache.Yields)
            {
                if (!Series.Any(series => series.Maturity == yield.Maturity) || !double.IsFinite(yield.Value)
                    || yield.PreviousValue is { } previous && !double.IsFinite(previous)) return;
            }

            foreach (var yield in cache.Yields) _state.SetYield(yield);
            _showingCachedYields = true;
        }
        catch (JsonException) { }
        catch (ArgumentException) { }
    }

    private string WithCacheStatus(string message) => _showingCachedYields ? $"Cached yields; {message}" : message;

    private void SaveCache(RobotContext context)
    {
        try
        {
            context.SetLocalValue(CacheKey, JsonSerializer.Serialize(new TreasuryCache(DateTimeOffset.UtcNow, _state.Yields.Values.ToArray())));
        }
        catch (ArgumentException) { }
    }

    private sealed record TreasuryCache(DateTimeOffset SavedAtUtc, TreasuryYield[] Yields);

    private static TreasuryYield ParseYield(string body, string maturity)
    {
        using var document = JsonDocument.Parse(body);
        if (!document.RootElement.TryGetProperty("observations", out var observations) || observations.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException("FRED response does not contain observations.");
        }

        var values = observations.EnumerateArray()
            .Select(observation => (Date: observation.TryGetProperty("date", out var date) ? date.GetString() : null, Value: observation.TryGetProperty("value", out var value) ? value.GetString() : null))
            .Where(observation => DateTime.TryParse(observation.Date, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal, out _)
                && double.TryParse(observation.Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _))
            .Select(observation => (Date: DateTime.Parse(observation.Date!, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal), Value: double.Parse(observation.Value!, System.Globalization.CultureInfo.InvariantCulture)))
            .ToArray();
        if (values.Length == 0) throw new JsonException("FRED response has no numeric observations.");
        return new TreasuryYield(maturity, values[0].Date, values[0].Value, values.Length > 1 ? values[1].Value : null);
    }
}
