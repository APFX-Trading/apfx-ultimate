namespace cAlgo.Robots;

public enum GlobalMarketSession
{
    Asia,
    Europe,
    UnitedStates
}

/// <summary>
/// Draws the major global trading sessions in UTC: Asia (00:00-09:00), Europe (07:00-16:00), and US (13:00-22:00).
/// Each session can be independently displayed from the Graph panel.
/// </summary>
public sealed class MarketHoursOverlayModule : IRobotModule
{
    private const string ObjectPrefix = "APFXUltimate.Graph.Session.";
    private const string PsychologicalLevelObjectPrefix = "APFXUltimate.Graph.PsychologicalLevel.";
    private const int SessionBackgroundOpacity = 48;
    private const int SessionEdgeOpacity = 92;
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(15);
    private static readonly SessionDefinition[] SessionDefinitions =
    {
        new(GlobalMarketSession.Asia, TimeSpan.Zero, TimeSpan.FromHours(9), GraphColorPalette.AsiaSession),
        new(GlobalMarketSession.Europe, TimeSpan.FromHours(7), TimeSpan.FromHours(16), GraphColorPalette.EuropeSession),
        new(GlobalMarketSession.UnitedStates, TimeSpan.FromHours(13), TimeSpan.FromHours(22), GraphColorPalette.UnitedStatesSession)
    };

    private readonly Dictionary<GlobalMarketSession, bool> _enabledSessions;
    private readonly HashSet<string> _renderedObjectNames = new(StringComparer.Ordinal);
    private readonly HashSet<string> _psychologicalLevelObjectNames = new(StringComparer.Ordinal);
    private readonly HashSet<string> _upcomingSessionObjectNames = new(StringComparer.Ordinal);
    private readonly string _symbolName;
    private readonly PsychologicalLevelPreset? _psychologicalLevelPreset;
    private RobotContext? _context;
    private double? _lastPsychologicalLevelMinimum;
    private double? _lastPsychologicalLevelMaximum;
    private DateTime _nextRefreshAt = DateTime.MinValue;
    private DateTime _lastViewportStart = DateTime.MinValue;
    private DateTime _lastViewportEnd = DateTime.MinValue;

    public MarketHoursOverlayModule(bool showAsia, bool showEurope, bool showUnitedStates, bool showPsychologicalLevels, string symbolName)
    {
        _enabledSessions = new Dictionary<GlobalMarketSession, bool>
        {
            [GlobalMarketSession.Asia] = showAsia,
            [GlobalMarketSession.Europe] = showEurope,
            [GlobalMarketSession.UnitedStates] = showUnitedStates
        };
        IsPsychologicalLevelsEnabled = showPsychologicalLevels;
        _symbolName = symbolName ?? throw new ArgumentNullException(nameof(symbolName));
        _psychologicalLevelPreset = GetPsychologicalLevelPreset(_symbolName);
    }

    public string Name => nameof(MarketHoursOverlayModule);

    public bool IsEnabled(GlobalMarketSession session) => _enabledSessions[session];
    public bool SupportsPsychologicalLevels => _psychologicalLevelPreset is not null;
    public bool IsPsychologicalLevelsEnabled { get; private set; }
    public string PsychologicalLevelsLabel => _psychologicalLevelPreset?.Label ?? string.Empty;

    public void OnStart(RobotContext context)
    {
        _context = context;
        Render(force: true);
    }

    public void OnTick(RobotContext context)
    {
    }

    public void OnTimer(RobotContext context) => Render(force: false);

    public void OnStop(RobotContext context)
    {
        Clear();
        _context = null;
    }

    public void SetEnabled(GlobalMarketSession session, bool isEnabled)
    {
        _enabledSessions[session] = isEnabled;
        Render(force: true);
    }

    public void SetPsychologicalLevelsEnabled(bool isEnabled)
    {
        IsPsychologicalLevelsEnabled = isEnabled;
        _lastPsychologicalLevelMinimum = null;
        _lastPsychologicalLevelMaximum = null;

        if (!isEnabled)
        {
            ClearPsychologicalLevels();
            return;
        }

        RenderPsychologicalLevels();
    }

    private void Render(bool force)
    {
        if (_context is null)
        {
            return;
        }

        var viewport = _context.GetChartViewport();
        if (viewport.EndTime <= viewport.StartTime || viewport.TopPrice <= viewport.BottomPrice)
        {
            return;
        }

        var viewportChanged = _lastViewportStart != viewport.StartTime || _lastViewportEnd != viewport.EndTime;
        if (!force && !viewportChanged && _context.ServerTime < _nextRefreshAt)
        {
            return;
        }

        var activeNames = new HashSet<string>(StringComparer.Ordinal);
        for (var date = viewport.StartTime.Date; date <= viewport.EndTime.Date; date = date.AddDays(1))
        {
            if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            {
                continue;
            }

            foreach (var session in SessionDefinitions.Where(definition => IsEnabled(definition.Session)))
            {
                var start = date.Add(session.Start);
                var end = date.Add(session.End);
                if (end <= viewport.StartTime || start >= viewport.EndTime)
                {
                    continue;
                }

                var key = $"{date:yyyyMMdd}.{session.Session}";
                var visibleStart = start < viewport.StartTime ? viewport.StartTime : start;
                var visibleEnd = end > viewport.EndTime ? viewport.EndTime : end;
                var priceRange = _context.GetChartPriceRange(start, end);
                if (priceRange is null || priceRange.Value.High <= priceRange.Value.Low)
                {
                    continue;
                }

                Draw(activeNames, ObjectPrefix + key + ".block", visibleStart, visibleEnd, priceRange.Value.Low, priceRange.Value.High, Color.FromArgb(SessionBackgroundOpacity, session.Color));

                var markerDuration = TimeSpan.FromMinutes(30);
                DrawMarkerIfVisible(activeNames, ObjectPrefix + key + ".start", start, start.Add(markerDuration), viewport, priceRange.Value.Low, priceRange.Value.High, Color.FromArgb(SessionEdgeOpacity, session.Color));
                DrawMarkerIfVisible(activeNames, ObjectPrefix + key + ".end", end.Subtract(markerDuration), end, viewport, priceRange.Value.Low, priceRange.Value.High, Color.FromArgb(SessionEdgeOpacity, session.Color));
            }
        }

        foreach (var staleName in _renderedObjectNames.Except(activeNames).ToArray())
        {
            _context.RemoveChartObject(staleName);
        }

        _renderedObjectNames.Clear();
        _renderedObjectNames.UnionWith(activeNames);
        RenderPsychologicalLevels();
        RenderUpcomingSessionMarker();
        _lastViewportStart = viewport.StartTime;
        _lastViewportEnd = viewport.EndTime;
        _nextRefreshAt = _context.ServerTime.Add(RefreshInterval);
    }

    private void RenderUpcomingSessionMarker()
    {
        if (_context is null)
        {
            return;
        }

        var nextSession = GetNextEnabledSession(_context.ServerTime);
        var activeNames = new HashSet<string>(StringComparer.Ordinal);
        if (nextSession is not null)
        {
            var name = ObjectPrefix + "Upcoming." + nextSession.Value.Start.ToString("yyyyMMddHHmmss") + "." + nextSession.Value.Definition.Session;
            _context.DrawGraphVerticalLine(name, nextSession.Value.Start, Color.FromArgb(180, nextSession.Value.Definition.Color), 2, LineStyle.Dots);
            activeNames.Add(name);
        }

        foreach (var staleName in _upcomingSessionObjectNames.Except(activeNames).ToArray())
        {
            _context.RemoveChartObject(staleName);
        }

        _upcomingSessionObjectNames.Clear();
        _upcomingSessionObjectNames.UnionWith(activeNames);
    }

    private (SessionDefinition Definition, DateTime Start)? GetNextEnabledSession(DateTime from)
    {
        (SessionDefinition Definition, DateTime Start)? next = null;
        for (var date = from.Date; date <= from.Date.AddDays(7); date = date.AddDays(1))
        {
            if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            {
                continue;
            }

            foreach (var session in SessionDefinitions.Where(definition => IsEnabled(definition.Session)))
            {
                var start = date.Add(session.Start);
                if (start <= from || (next is not null && start >= next.Value.Start))
                {
                    continue;
                }

                next = (session, start);
            }
        }

        return next;
    }

    private void RenderPsychologicalLevels()
    {
        if (_context is null || !SupportsPsychologicalLevels || !IsPsychologicalLevelsEnabled)
        {
            return;
        }

        var preset = _psychologicalLevelPreset!;
        var viewport = _context.GetChartViewport();
        var minimum = Math.Floor(viewport.BottomPrice / preset.MinorStep) * preset.MinorStep;
        var maximum = Math.Ceiling(viewport.TopPrice / preset.MinorStep) * preset.MinorStep;
        if (_lastPsychologicalLevelMinimum == minimum && _lastPsychologicalLevelMaximum == maximum)
        {
            return;
        }

        var activeNames = new HashSet<string>(StringComparer.Ordinal);
        for (var price = minimum; price <= maximum + preset.MinorStep / 100; price += preset.MinorStep)
        {
            var roundedPrice = Math.Round(price, 6);
            var (color, thickness, lineStyle) = GetPsychologicalLevelStyle(roundedPrice, preset);
            var name = PsychologicalLevelObjectPrefix + roundedPrice.ToString("F6", System.Globalization.CultureInfo.InvariantCulture);
            _context.DrawGraphHorizontalLine(name, roundedPrice, color, thickness, lineStyle);
            activeNames.Add(name);
        }

        foreach (var staleName in _psychologicalLevelObjectNames.Except(activeNames).ToArray())
        {
            _context.RemoveChartObject(staleName);
        }

        _psychologicalLevelObjectNames.Clear();
        _psychologicalLevelObjectNames.UnionWith(activeNames);
        _lastPsychologicalLevelMinimum = minimum;
        _lastPsychologicalLevelMaximum = maximum;
    }

    private static (Color Color, int Thickness, LineStyle LineStyle) GetPsychologicalLevelStyle(double price, PsychologicalLevelPreset preset)
    {
        if (IsMultipleOf(price, preset.MajorStep))
        {
            return (GraphColorPalette.PriceLevelMajor, 2, LineStyle.Solid);
        }

        if (IsMultipleOf(price, preset.MediumStep))
        {
            return (GraphColorPalette.PriceLevelMid, 1, LineStyle.Solid);
        }

        return (GraphColorPalette.PriceLevelMinor, 1, LineStyle.Dots);
    }

    private static bool IsMultipleOf(double value, double step) => Math.Abs(value / step - Math.Round(value / step)) < 0.00001;

    private static PsychologicalLevelPreset? GetPsychologicalLevelPreset(string symbolName)
    {
        var symbol = symbolName.ToUpperInvariant();
        if (MatchesSymbol(symbol, "US30", "DJ30", "WS30", "WALLSTREET", "DOW30") ||
            MatchesSymbol(symbol, "NAS100", "USTEC", "US100", "NDX", "NQ100") ||
            MatchesSymbol(symbol, "GER40", "DE40", "DAX40", "DAX") ||
            MatchesSymbol(symbol, "UK100", "FTSE100", "FTSE"))
        {
            return new PsychologicalLevelPreset("Price Levels  25 / 50 / 100", 25, 50, 100);
        }

        if (MatchesSymbol(symbol, "US500", "SPX500", "SP500", "USSPX", "SPX"))
        {
            return new PsychologicalLevelPreset("Price Levels  5 / 10 / 25", 5, 10, 25);
        }

        if (MatchesSymbol(symbol, "XAU", "GOLD"))
        {
            return new PsychologicalLevelPreset("Gold Levels  $25 / $50 / $100", 25, 50, 100);
        }

        if (MatchesSymbol(symbol, "XAG", "SILVER"))
        {
            return new PsychologicalLevelPreset("Silver Levels  .25 / .50 / 1.00", 0.25, 0.5, 1);
        }

        if (MatchesSymbol(symbol, "BTC", "BITCOIN"))
        {
            return new PsychologicalLevelPreset("BTC Levels  $500 / $1k / $5k", 500, 1000, 5000);
        }

        if (MatchesSymbol(symbol, "ETH", "ETHEREUM"))
        {
            return new PsychologicalLevelPreset("ETH Levels  $25 / $50 / $100", 25, 50, 100);
        }

        if (MatchesSymbol(symbol, "USOIL", "WTI", "XTI", "UKOIL", "BRENT"))
        {
            return new PsychologicalLevelPreset("Oil Levels  $1 / $2.5 / $5", 1, 2.5, 5);
        }

        if (IsForexPair(symbol))
        {
            return symbol.Contains("JPY", StringComparison.Ordinal)
                ? new PsychologicalLevelPreset("JPY Levels  .25 / .50 / 1.00", 0.25, 0.5, 1)
                : new PsychologicalLevelPreset("FX Levels  25 / 50 / 100 pips", 0.0025, 0.005, 0.01);
        }

        return null;
    }

    private static bool MatchesSymbol(string symbol, params string[] aliases) => aliases.Any(alias => symbol.StartsWith(alias, StringComparison.Ordinal) || symbol.Contains(alias, StringComparison.Ordinal));

    private static bool IsForexPair(string symbol)
    {
        if (symbol.Length < 6)
        {
            return false;
        }

        var baseCurrency = symbol[..3];
        var quoteCurrency = symbol.Substring(3, 3);
        return IsKnownCurrency(baseCurrency) && IsKnownCurrency(quoteCurrency);
    }

    private static bool IsKnownCurrency(string currency) => currency is "AUD" or "CAD" or "CHF" or "EUR" or "GBP" or "JPY" or "NZD" or "USD";

    private void DrawMarkerIfVisible(HashSet<string> names, string name, DateTime start, DateTime end, ChartViewport viewport, double low, double high, Color color)
    {
        if (end <= viewport.StartTime || start >= viewport.EndTime)
        {
            return;
        }

        Draw(names, name, start < viewport.StartTime ? viewport.StartTime : start, end > viewport.EndTime ? viewport.EndTime : end, low, high, color);
    }

    private void Draw(HashSet<string> names, string name, DateTime start, DateTime end, double low, double high, Color color)
    {
        if (end <= start)
        {
            return;
        }

        _context!.DrawChartRectangle(name, start, end, low, high, color);
        names.Add(name);
    }

    private void Clear()
    {
        if (_context is null)
        {
            return;
        }

        foreach (var objectName in _renderedObjectNames)
        {
            _context.RemoveChartObject(objectName);
        }

        _renderedObjectNames.Clear();
        ClearUpcomingSessionMarker();
        ClearPsychologicalLevels();
        _lastViewportStart = DateTime.MinValue;
        _lastViewportEnd = DateTime.MinValue;
    }

    private void ClearUpcomingSessionMarker()
    {
        if (_context is null)
        {
            return;
        }

        foreach (var objectName in _upcomingSessionObjectNames)
        {
            _context.RemoveChartObject(objectName);
        }

        _upcomingSessionObjectNames.Clear();
    }

    private void ClearPsychologicalLevels()
    {
        if (_context is null)
        {
            return;
        }

        foreach (var objectName in _psychologicalLevelObjectNames)
        {
            _context.RemoveChartObject(objectName);
        }

        _psychologicalLevelObjectNames.Clear();
        _lastPsychologicalLevelMinimum = null;
        _lastPsychologicalLevelMaximum = null;
    }

    private sealed record SessionDefinition(GlobalMarketSession Session, TimeSpan Start, TimeSpan End, Color Color);
    private sealed record PsychologicalLevelPreset(string Label, double MinorStep, double MediumStep, double MajorStep);
}
