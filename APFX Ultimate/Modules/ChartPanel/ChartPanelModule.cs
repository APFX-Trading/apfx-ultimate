namespace cAlgo.Robots;

/// <summary>
/// Displays the composed chart panel and delegates each panel feature to a focused section.
/// </summary>
public sealed class ChartPanelModule : IRobotModule
{
    private readonly PanelPosition _position;
    private readonly double _fontSize;
    private readonly GridStatus? _gridStatus;
    private readonly int _atrPeriod;
    private readonly CalendarNewsState _calendarNewsState;
    private readonly MarketHoursOverlayModule _marketHoursOverlay;
    private readonly EmaOverlayModule _emaOverlay;
    private readonly BollingerBandsOverlayModule _bollingerBandsOverlay;
    private readonly DailyVwapOverlayModule _dailyVwapOverlay;
    private readonly PreviousPeriodLevelsOverlayModule _previousPeriodLevelsOverlay;
    private readonly MarketPulseLevelsOverlayModule _marketPulseLevelsOverlay;
    private readonly DailyVolumeProfileOverlayModule _volumeProfileOverlay;
    private readonly OilVariationSection? _oilVariation;
    private readonly TreasuryYieldSection? _treasuryYields;
    private Border? _panel;
    private AccountStatisticsSection? _accountStatistics;
    private OpenPositionStatisticsSection? _openPositionStatistics;
    private ChartContextSection? _chartContext;
    private TradingControlsSection? _tradingControls;
    private IndicatorStatusSection? _indicatorStatus;
    private CalendarNewsSection? _calendarNews;
    private GraphControlsSection? _graphControls;
    private RobotContext? _context;

    public ChartPanelModule(PanelPosition position, double fontSize, GridStatus? gridStatus, int atrPeriod, CalendarNewsState calendarNewsState, MarketHoursOverlayModule marketHoursOverlay, EmaOverlayModule emaOverlay, BollingerBandsOverlayModule bollingerBandsOverlay, DailyVwapOverlayModule dailyVwapOverlay, PreviousPeriodLevelsOverlayModule previousPeriodLevelsOverlay, MarketPulseLevelsOverlayModule marketPulseLevelsOverlay, DailyVolumeProfileOverlayModule volumeProfileOverlay, OilVariationSection? oilVariation, TreasuryYieldState? treasuryYieldState)
    {
        _position = position;
        _fontSize = fontSize;
        _gridStatus = gridStatus;
        _atrPeriod = atrPeriod;
        _calendarNewsState = calendarNewsState;
        _marketHoursOverlay = marketHoursOverlay;
        _emaOverlay = emaOverlay;
        _bollingerBandsOverlay = bollingerBandsOverlay;
        _dailyVwapOverlay = dailyVwapOverlay;
        _previousPeriodLevelsOverlay = previousPeriodLevelsOverlay;
        _marketPulseLevelsOverlay = marketPulseLevelsOverlay;
        _volumeProfileOverlay = volumeProfileOverlay;
        _oilVariation = oilVariation;
        _treasuryYields = treasuryYieldState is null ? null : new TreasuryYieldSection(fontSize, treasuryYieldState);
    }

    public string Name => nameof(ChartPanelModule);

    public void OnStart(RobotContext context)
    {
        _context = context;
        var content = ChartPanelLayout.CreateContent(_position, _fontSize);
        var layoutGrid = new Grid(2, 2);
        layoutGrid.Columns[0].SetWidthInStars(1);
        layoutGrid.Columns[1].SetWidthInStars(1.5);
        var primaryColumn = new Grid(3, 1);
        primaryColumn.Rows[0].SetHeightToAuto();
        primaryColumn.Rows[1].SetHeightToAuto();
        primaryColumn.Rows[2].SetHeightToAuto();

        _accountStatistics = new AccountStatisticsSection();
        _accountStatistics.TrackDrawdown(context);
        _openPositionStatistics = new OpenPositionStatisticsSection(_fontSize, _gridStatus);
        _chartContext = new ChartContextSection(_fontSize, _marketHoursOverlay, _marketPulseLevelsOverlay);
        _tradingControls = new TradingControlsSection(_fontSize, () => RefreshAccountData(context));
        _indicatorStatus = new IndicatorStatusSection(_fontSize, _atrPeriod);
        _calendarNews = new CalendarNewsSection(_fontSize, _calendarNewsState);
        _graphControls = new GraphControlsSection(_fontSize, _marketHoursOverlay, _emaOverlay, _bollingerBandsOverlay, _dailyVwapOverlay, _previousPeriodLevelsOverlay, _marketPulseLevelsOverlay, _volumeProfileOverlay);
        var liveStatusSection = ChartPanelLayout.CreateSection(_openPositionStatistics.CreateControl(context));
        var chartContextSection = ChartPanelLayout.CreateSection(_chartContext.CreateControl(context));
        var performanceSection = ChartPanelLayout.CreateSection(_accountStatistics.CreateControl(context, _fontSize));
        primaryColumn.AddChild(liveStatusSection, 0, 0);
        primaryColumn.AddChild(chartContextSection, 1, 0);
        primaryColumn.AddChild(performanceSection, 2, 0);

        var rightRowCount = 3 + (_oilVariation is null ? 0 : 1) + (_treasuryYields is null ? 0 : 1);
        var rightColumn = new Grid(rightRowCount, 1);
        for (var row = 0; row < rightRowCount; row++) rightColumn.Rows[row].SetHeightToAuto();
        var calendarSection = ChartPanelLayout.CreateSection(_calendarNews.CreateControl(context));
        calendarSection.Margin = new Thickness(8, 7, 0, 0);
        var indicatorsSection = ChartPanelLayout.CreateSection(_indicatorStatus.CreateControl(context));
        indicatorsSection.Margin = new Thickness(8, 7, 0, 0);
        var oilVariationSection = _oilVariation is null ? null : ChartPanelLayout.CreateSection(_oilVariation.CreateControl(context));
        if (oilVariationSection is not null) oilVariationSection.Margin = new Thickness(8, 7, 0, 0);
        var treasuryYieldSection = _treasuryYields is null ? null : ChartPanelLayout.CreateSection(_treasuryYields.CreateControl(context));
        if (treasuryYieldSection is not null) treasuryYieldSection.Margin = new Thickness(8, 7, 0, 0);
        var graphSection = ChartPanelLayout.CreateSection(_graphControls.CreateControl());
        graphSection.Margin = new Thickness(8, 7, 0, 0);
        rightColumn.AddChild(calendarSection, 0, 0);
        rightColumn.AddChild(indicatorsSection, 1, 0);
        var nextRightRow = 2;
        if (oilVariationSection is not null) rightColumn.AddChild(oilVariationSection, nextRightRow++, 0);
        if (treasuryYieldSection is not null) rightColumn.AddChild(treasuryYieldSection, nextRightRow++, 0);
        rightColumn.AddChild(graphSection, nextRightRow, 0);
        var quickActionsSection = ChartPanelLayout.CreateSection(_tradingControls.CreateControl(context));

        layoutGrid.AddChild(primaryColumn, 0, 0);
        layoutGrid.AddChild(rightColumn, 0, 1);
        layoutGrid.AddChild(quickActionsSection, 1, 0, 1, 2);
        content.AddChild(layoutGrid);

        _panel = ChartPanelLayout.CreateBorder(content, _position);
        context.AddChartControl(_panel);
        _calendarNews.Refresh(context);
    }

    public void OnTick(RobotContext context)
    {
        _accountStatistics?.TrackDrawdown(context);
        _accountStatistics?.RefreshIfDue(context);
        _openPositionStatistics?.RefreshIfDue(context);
        _chartContext?.RefreshIfDue(context);
        _indicatorStatus?.RefreshIfDue(context);
        _oilVariation?.RefreshIfDue(context);
        _treasuryYields?.Refresh(context);
        _calendarNews?.Refresh(context);
    }

    public void OnTimer(RobotContext context)
    {
        _calendarNews?.Refresh(context);
        _chartContext?.RefreshIfDue(context);
    }

    public void OnStop(RobotContext context)
    {
        _accountStatistics?.FlushDrawdownState(context);
        if (_panel is null)
        {
            return;
        }

        context.RemoveChartControl(_panel);
        _panel = null;
        _accountStatistics = null;
        _openPositionStatistics = null;
        _chartContext = null;
        _tradingControls = null;
        _indicatorStatus = null;
        _calendarNews = null;
        _graphControls = null;
        _context = null;
    }

    /// <summary>
    /// Immediately refreshes account-derived panel values after a cTrader position-close event.
    /// </summary>
    public void RefreshPerformance()
    {
        if (_context is not null)
        {
            RefreshAccountData(_context);
        }
    }

    private void RefreshAccountData(RobotContext context)
    {
        _accountStatistics?.Refresh(context);
        _openPositionStatistics?.Refresh(context);
    }
}
