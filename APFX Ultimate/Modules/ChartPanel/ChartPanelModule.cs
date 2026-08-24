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
    private readonly bool _atrScalingEnabled;
    private readonly CalendarNewsState _calendarNewsState;
    private readonly MarketHoursOverlayModule _marketHoursOverlay;
    private readonly EmaOverlayModule _emaOverlay;
    private readonly BollingerBandsOverlayModule _bollingerBandsOverlay;
    private Border? _panel;
    private AccountStatisticsSection? _accountStatistics;
    private OpenPositionStatisticsSection? _openPositionStatistics;
    private TradingControlsSection? _tradingControls;
    private IndicatorStatusSection? _indicatorStatus;
    private CalendarNewsSection? _calendarNews;
    private GraphControlsSection? _graphControls;
    private RobotContext? _context;

    public ChartPanelModule(PanelPosition position, double fontSize, GridStatus? gridStatus, int atrPeriod, bool atrScalingEnabled, CalendarNewsState calendarNewsState, MarketHoursOverlayModule marketHoursOverlay, EmaOverlayModule emaOverlay, BollingerBandsOverlayModule bollingerBandsOverlay)
    {
        _position = position;
        _fontSize = fontSize;
        _gridStatus = gridStatus;
        _atrPeriod = atrPeriod;
        _atrScalingEnabled = atrScalingEnabled;
        _calendarNewsState = calendarNewsState;
        _marketHoursOverlay = marketHoursOverlay;
        _emaOverlay = emaOverlay;
        _bollingerBandsOverlay = bollingerBandsOverlay;
    }

    public string Name => nameof(ChartPanelModule);

    public void OnStart(RobotContext context)
    {
        _context = context;
        var content = ChartPanelLayout.CreateContent(_position, _fontSize);
        var layoutGrid = new Grid(2, 2);
        layoutGrid.Columns[0].SetWidthInStars(1);
        layoutGrid.Columns[1].SetWidthInStars(1.5);
        var primaryColumn = new Grid(2, 1);
        primaryColumn.Rows[0].SetHeightToAuto();
        primaryColumn.Rows[1].SetHeightInStars(1);

        _accountStatistics = new AccountStatisticsSection();
        _accountStatistics.TrackDrawdown(context);
        _openPositionStatistics = new OpenPositionStatisticsSection(_fontSize, _gridStatus);
        _tradingControls = new TradingControlsSection(_fontSize, () => RefreshAccountData(context));
        _indicatorStatus = new IndicatorStatusSection(_fontSize, _atrPeriod, _atrScalingEnabled);
        _calendarNews = new CalendarNewsSection(_fontSize, _calendarNewsState);
        _graphControls = new GraphControlsSection(_fontSize, _marketHoursOverlay, _emaOverlay, _bollingerBandsOverlay);
        var liveStatusSection = ChartPanelLayout.CreateSection(_openPositionStatistics.CreateControl(context));
        var performanceSection = ChartPanelLayout.CreateSection(_accountStatistics.CreateControl(context, _fontSize));
        performanceSection.VerticalAlignment = VerticalAlignment.Stretch;
        primaryColumn.AddChild(liveStatusSection, 0, 0);
        primaryColumn.AddChild(performanceSection, 1, 0);

        var rightColumn = new Grid(3, 1);
        rightColumn.Rows[0].SetHeightInStars(1);
        rightColumn.Rows[1].SetHeightToAuto();
        rightColumn.Rows[2].SetHeightToAuto();
        var calendarSection = ChartPanelLayout.CreateSection(_calendarNews.CreateControl(context));
        calendarSection.Margin = new Thickness(8, 7, 0, 0);
        calendarSection.VerticalAlignment = VerticalAlignment.Stretch;
        var indicatorsSection = ChartPanelLayout.CreateSection(_indicatorStatus.CreateControl(context));
        indicatorsSection.Margin = new Thickness(8, 7, 0, 0);
        var graphSection = ChartPanelLayout.CreateSection(_graphControls.CreateControl());
        graphSection.Margin = new Thickness(8, 7, 0, 0);
        rightColumn.AddChild(calendarSection, 0, 0);
        rightColumn.AddChild(indicatorsSection, 1, 0);
        rightColumn.AddChild(graphSection, 2, 0);
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
        _indicatorStatus?.RefreshIfDue(context);
        _calendarNews?.Refresh(context);
    }

    public void OnTimer(RobotContext context) => _calendarNews?.Refresh(context);

    public void OnStop(RobotContext context)
    {
        if (_panel is null)
        {
            return;
        }

        context.RemoveChartControl(_panel);
        _panel = null;
        _accountStatistics = null;
        _openPositionStatistics = null;
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
