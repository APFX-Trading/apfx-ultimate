using System.Text.Json;

namespace cAlgo.Robots;

/// <summary>Loads daily U.S. Treasury constant-maturity yields from FRED for display-only market context.</summary>
public sealed class TreasuryYieldModule : IRobotModule
{
    private const string Endpoint = "https://api.stlouisfed.org/fred/series/observations";
    private static readonly (string SeriesId, string Maturity)[] Series = { ("DGS2", "2Y"), ("DGS10", "10Y"), ("DGS30", "30Y") };
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMinutes(5);
    private readonly TreasuryYieldConfiguration _configuration;
    private readonly TreasuryYieldState _state;
    private DateTime _nextRefreshAt = DateTime.MinValue;
    private DateTime _requestStartedAtUtc = DateTime.MinValue;
    private int _seriesIndex;
    private bool _requestPending;

    public TreasuryYieldModule(TreasuryYieldConfiguration configuration, TreasuryYieldState state)
    {
        _configuration = configuration;
        _state = state;
    }

    public string Name => nameof(TreasuryYieldModule);
    public void OnStart(RobotContext context) => RefreshIfDue(context);
    public void OnTick(RobotContext context) => RefreshIfDue(context);
    public void OnTimer(RobotContext context) => RefreshIfDue(context);
    public void OnStop(RobotContext context) { }

    private void RefreshIfDue(RobotContext context)
    {
        if (_requestPending)
        {
            if (DateTime.UtcNow - _requestStartedAtUtc < RequestTimeout) return;
            _requestPending = false;
            _nextRefreshAt = context.ServerTime.Add(RetryDelay);
            _state.SetStatus("Treasury request timed out; retrying later");
            return;
        }

        if (context.ServerTime < _nextRefreshAt) return;
        if (string.IsNullOrWhiteSpace(_configuration.ApiKey))
        {
            _state.SetStatus("Set FRED API Key to load Treasury yields");
            _nextRefreshAt = context.ServerTime.AddMinutes(_configuration.RefreshMinutes);
            return;
        }

        _seriesIndex = 0;
        _state.SetStatus("Loading Treasury yields...");
        RequestCurrentSeries(context);
    }

    private void RequestCurrentSeries(RobotContext context)
    {
        var series = Series[_seriesIndex];
        var uri = new Uri($"{Endpoint}?series_id={series.SeriesId}&api_key={Uri.EscapeDataString(_configuration.ApiKey.Trim())}&file_type=json&sort_order=desc&limit=10");
        var request = new HttpRequest(uri) { Timeout = RequestTimeout, Version = new Version(1, 1) };
        _requestPending = true;
        _requestStartedAtUtc = DateTime.UtcNow;
        try
        {
            context.SendHttpRequest(request, response => context.BeginInvokeOnMainThread(() => HandleResponse(context, response, series)));
        }
        catch (Exception exception)
        {
            _requestPending = false;
            _nextRefreshAt = context.ServerTime.Add(RetryDelay);
            _state.SetStatus("Treasury request could not be started");
            context.Log($"Treasury request could not be started: {exception.Message}");
        }
    }

    private void HandleResponse(RobotContext context, HttpResponse response, (string SeriesId, string Maturity) series)
    {
        if (!_requestPending) return;
        _requestPending = false;
        if (!response.IsSuccessful)
        {
            _nextRefreshAt = context.ServerTime.Add(RetryDelay);
            _state.SetStatus(response.StatusCode == 401 ? "FRED API key rejected" : $"Treasury data unavailable ({response.StatusCode})");
            context.Log($"Treasury request failed with HTTP {response.StatusCode}: {response.Exception?.Message ?? "no response body"}");
            return;
        }

        try
        {
            _state.SetYield(ParseYield(response.Body, series.Maturity));
        }
        catch (JsonException exception)
        {
            _nextRefreshAt = context.ServerTime.Add(RetryDelay);
            _state.SetStatus("FRED returned invalid Treasury data");
            context.Log($"Treasury response could not be parsed: {exception.Message}");
            return;
        }

        _seriesIndex++;
        if (_seriesIndex < Series.Length)
        {
            RequestCurrentSeries(context);
            return;
        }

        _nextRefreshAt = context.ServerTime.AddMinutes(_configuration.RefreshMinutes);
        _state.SetStatus($"Updated {context.ServerTime:dd MMM HH:mm}");
    }

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
