namespace cAlgo.Robots;

/// <summary>
/// Houses display-only chart controls so further graph overlays can be added without growing the main panel module.
/// </summary>
public sealed class GraphControlsSection
{
    private readonly double _fontSize;
    private readonly GridTradingModule _gridTrading;
    private readonly MarketHoursOverlayModule _marketHoursOverlay;
    private readonly EmaOverlayModule _emaOverlay;
    private readonly BollingerBandsOverlayModule _bollingerBandsOverlay;
    private readonly DailyVwapOverlayModule _dailyVwapOverlay;
    private readonly PreviousPeriodLevelsOverlayModule _previousPeriodLevelsOverlay;
    private readonly MarketPulseLevelsOverlayModule _marketPulseLevelsOverlay;
    private readonly DailyVolumeProfileOverlayModule _volumeProfileOverlay;

    public GraphControlsSection(double fontSize, MarketHoursOverlayModule marketHoursOverlay, EmaOverlayModule emaOverlay, BollingerBandsOverlayModule bollingerBandsOverlay, DailyVwapOverlayModule dailyVwapOverlay, PreviousPeriodLevelsOverlayModule previousPeriodLevelsOverlay, MarketPulseLevelsOverlayModule marketPulseLevelsOverlay, DailyVolumeProfileOverlayModule volumeProfileOverlay, GridTradingModule gridTrading)
    {
        _gridTrading = gridTrading ?? throw new ArgumentNullException(nameof(gridTrading));
        _fontSize = fontSize;
        _marketHoursOverlay = marketHoursOverlay ?? throw new ArgumentNullException(nameof(marketHoursOverlay));
        _emaOverlay = emaOverlay ?? throw new ArgumentNullException(nameof(emaOverlay));
        _bollingerBandsOverlay = bollingerBandsOverlay ?? throw new ArgumentNullException(nameof(bollingerBandsOverlay));
        _dailyVwapOverlay = dailyVwapOverlay ?? throw new ArgumentNullException(nameof(dailyVwapOverlay));
        _previousPeriodLevelsOverlay = previousPeriodLevelsOverlay ?? throw new ArgumentNullException(nameof(previousPeriodLevelsOverlay));
        _marketPulseLevelsOverlay = marketPulseLevelsOverlay ?? throw new ArgumentNullException(nameof(marketPulseLevelsOverlay));
        _volumeProfileOverlay = volumeProfileOverlay ?? throw new ArgumentNullException(nameof(volumeProfileOverlay));
    }

    public ControlBase CreateControl()
    {
        var section = new StackPanel { Orientation = Orientation.Vertical };
        section.AddChild(new TextBlock
        {
            Text = "Graph",
            ForegroundColor = Color.FromHex("#FF6FE3FF"),
            FontWeight = FontWeight.ExtraBold,
            FontSize = _fontSize + 1,
            HorizontalAlignment = HorizontalAlignment.Center
        });

        section.AddChild(CreateSessionToggle(GlobalMarketSession.Asia, "Asia  00:00-09:00 UTC", GraphColorPalette.AsiaSession));
        section.AddChild(CreateSessionToggle(GlobalMarketSession.Europe, "Europe  07:00-16:00 UTC", GraphColorPalette.EuropeSession));
        section.AddChild(CreateSessionToggle(GlobalMarketSession.UnitedStates, "US  13:00-22:00 UTC", GraphColorPalette.UnitedStatesSession));
        if (_marketHoursOverlay.SupportsPsychologicalLevels)
        {
            section.AddChild(CreatePsychologicalLevelsToggle());
        }
        section.AddChild(CreateEmaToggle());
        section.AddChild(CreateBollingerBandsToggle());
        section.AddChild(CreateOverlayToggle(_dailyVwapOverlay.Label, _dailyVwapOverlay.IsEnabled, _dailyVwapOverlay.SetEnabled, GraphColorPalette.Vwap, GraphColorPalette.VwapOneDeviation, GraphColorPalette.VwapTwoDeviation, GraphColorPalette.VwapThreeDeviation));
        section.AddChild(CreateOverlayToggle(_previousPeriodLevelsOverlay.Label, _previousPeriodLevelsOverlay.IsEnabled, _previousPeriodLevelsOverlay.SetEnabled, GraphColorPalette.PreviousDayHigh, GraphColorPalette.PreviousDayLow, GraphColorPalette.PreviousDayClose, GraphColorPalette.PreviousWeekHigh, GraphColorPalette.PreviousWeekLow, GraphColorPalette.PreviousWeekClose));
        section.AddChild(CreateOverlayToggle(_marketPulseLevelsOverlay.Label, _marketPulseLevelsOverlay.IsEnabled, _marketPulseLevelsOverlay.SetEnabled, GraphColorPalette.StrongSupport, GraphColorPalette.StrongResistance));
        section.AddChild(CreateVolumeProfileToggle());
        section.AddChild(CreateOverlayToggle("TP / next entry preview", _gridTrading.IsPreviewEnabled, _gridTrading.SetPreviewEnabled, Color.FromHex("#FFE879F9"), Color.FromHex("#FF6FE3FF")));
        return section;
    }

    private ControlBase CreateSessionToggle(GlobalMarketSession session, string text, Color color)
    {
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 4, 0, 0)
        };
        var checkBox = new CheckBox
        {
            Text = text,
            IsChecked = _marketHoursOverlay.IsEnabled(session),
            ForegroundColor = Color.White,
            FontSize = Math.Max(9, _fontSize - 1)
        };
        checkBox.Click += args => _marketHoursOverlay.SetEnabled(session, args.CheckBox.IsChecked == true);
        row.AddChild(checkBox);
        AddColorSwatch(row, color);
        return row;
    }

    private ControlBase CreatePsychologicalLevelsToggle()
    {
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 4, 0, 0)
        };
        var checkBox = new CheckBox
        {
            Text = _marketHoursOverlay.PsychologicalLevelsLabel,
            IsChecked = _marketHoursOverlay.IsPsychologicalLevelsEnabled,
            ForegroundColor = Color.White,
            FontSize = Math.Max(9, _fontSize - 1)
        };
        checkBox.Click += args => _marketHoursOverlay.SetPsychologicalLevelsEnabled(args.CheckBox.IsChecked == true);
        row.AddChild(checkBox);
        AddColorSwatch(row, GraphColorPalette.PriceLevelMinor);
        AddColorSwatch(row, GraphColorPalette.PriceLevelMid);
        AddColorSwatch(row, GraphColorPalette.PriceLevelMajor);
        return row;
    }

    private static void AddColorSwatch(StackPanel parent, Color color)
    {
        parent.AddChild(new Border
        {
            Width = 11,
            Height = 11,
            BackgroundColor = color,
            CornerRadius = 2,
            Margin = new Thickness(5, 3, 0, 2)
        });
    }

    private ControlBase CreateEmaToggle()
    {
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 4, 0, 0)
        };
        var checkBox = new CheckBox
        {
            Text = _emaOverlay.Label,
            IsChecked = _emaOverlay.IsEnabled,
            ForegroundColor = Color.White,
            FontSize = Math.Max(9, _fontSize - 1)
        };
        checkBox.Click += args => _emaOverlay.SetEnabled(args.CheckBox.IsChecked == true);
        row.AddChild(checkBox);
        AddColorSwatch(row, GraphColorPalette.EmaFast);
        AddColorSwatch(row, GraphColorPalette.EmaSlow);
        return row;
    }

    private ControlBase CreateBollingerBandsToggle()
    {
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 4, 0, 0)
        };
        var checkBox = new CheckBox
        {
            Text = _bollingerBandsOverlay.Label,
            IsChecked = _bollingerBandsOverlay.IsEnabled,
            ForegroundColor = Color.White,
            FontSize = Math.Max(9, _fontSize - 1)
        };
        checkBox.Click += args => _bollingerBandsOverlay.SetEnabled(args.CheckBox.IsChecked == true);
        row.AddChild(checkBox);
        AddColorSwatch(row, GraphColorPalette.BollingerOuter);
        AddColorSwatch(row, GraphColorPalette.BollingerMiddle);
        return row;
    }

    private ControlBase CreateVolumeProfileToggle()
    {
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 4, 0, 0)
        };
        var checkBox = new CheckBox
        {
            Text = _volumeProfileOverlay.Label,
            IsChecked = _volumeProfileOverlay.IsEnabled,
            ForegroundColor = Color.White,
            FontSize = Math.Max(9, _fontSize - 1)
        };
        checkBox.Click += args => _volumeProfileOverlay.SetEnabled(args.CheckBox.IsChecked == true);
        row.AddChild(checkBox);
        AddColorSwatch(row, GraphColorPalette.VolumeProfile);
        AddColorSwatch(row, GraphColorPalette.VolumeProfilePointOfControl);
        return row;
    }

    private ControlBase CreateOverlayToggle(string text, bool isEnabled, Action<bool> setEnabled, params Color[] colors)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };
        var checkBox = new CheckBox
        {
            Text = text,
            IsChecked = isEnabled,
            ForegroundColor = Color.White,
            FontSize = Math.Max(9, _fontSize - 1)
        };
        checkBox.Click += args => setEnabled(args.CheckBox.IsChecked == true);
        row.AddChild(checkBox);
        foreach (var color in colors) AddColorSwatch(row, color);
        return row;
    }
}
