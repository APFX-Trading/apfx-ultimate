namespace cAlgo.Robots;

/// <summary>
/// Houses display-only chart controls so further graph overlays can be added without growing the main panel module.
/// </summary>
public sealed class GraphControlsSection
{
    private readonly double _fontSize;
    private readonly MarketHoursOverlayModule _marketHoursOverlay;
    private readonly EmaOverlayModule _emaOverlay;
    private readonly BollingerBandsOverlayModule _bollingerBandsOverlay;
    private readonly DailyVwapOverlayModule _dailyVwapOverlay;
    private readonly PreviousPeriodLevelsOverlayModule _previousPeriodLevelsOverlay;
    private readonly DailyVolumeProfileOverlayModule _volumeProfileOverlay;

    public GraphControlsSection(double fontSize, MarketHoursOverlayModule marketHoursOverlay, EmaOverlayModule emaOverlay, BollingerBandsOverlayModule bollingerBandsOverlay, DailyVwapOverlayModule dailyVwapOverlay, PreviousPeriodLevelsOverlayModule previousPeriodLevelsOverlay, DailyVolumeProfileOverlayModule volumeProfileOverlay)
    {
        _fontSize = fontSize;
        _marketHoursOverlay = marketHoursOverlay ?? throw new ArgumentNullException(nameof(marketHoursOverlay));
        _emaOverlay = emaOverlay ?? throw new ArgumentNullException(nameof(emaOverlay));
        _bollingerBandsOverlay = bollingerBandsOverlay ?? throw new ArgumentNullException(nameof(bollingerBandsOverlay));
        _dailyVwapOverlay = dailyVwapOverlay ?? throw new ArgumentNullException(nameof(dailyVwapOverlay));
        _previousPeriodLevelsOverlay = previousPeriodLevelsOverlay ?? throw new ArgumentNullException(nameof(previousPeriodLevelsOverlay));
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

        section.AddChild(CreateSessionToggle(GlobalMarketSession.Asia, "Asia  00:00-09:00 UTC", Color.FromHex("#FF8B7CFF")));
        section.AddChild(CreateSessionToggle(GlobalMarketSession.Europe, "Europe  07:00-16:00 UTC", Color.FromHex("#FF2CB8F5")));
        section.AddChild(CreateSessionToggle(GlobalMarketSession.UnitedStates, "US  13:00-22:00 UTC", Color.FromHex("#FFF7B955")));
        if (_marketHoursOverlay.SupportsPsychologicalLevels)
        {
            section.AddChild(CreatePsychologicalLevelsToggle());
        }
        section.AddChild(CreateEmaToggle());
        section.AddChild(CreateBollingerBandsToggle());
        section.AddChild(CreateOverlayToggle(_dailyVwapOverlay.Label, _dailyVwapOverlay.IsEnabled, _dailyVwapOverlay.SetEnabled, "#FFFF5C8A", "#FF55D6BE", "#FF7C8CFF"));
        section.AddChild(CreateOverlayToggle(_previousPeriodLevelsOverlay.Label, _previousPeriodLevelsOverlay.IsEnabled, _previousPeriodLevelsOverlay.SetEnabled, "#FF4FC3F7", "#FFFF7F96", "#FFE9EEF5", "#FFB57BFF", "#FFFFA657", "#FFFFDF78"));
        section.AddChild(CreateVolumeProfileToggle());
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
        AddColorSwatch(row, Color.FromHex("#FF627E9B"));
        AddColorSwatch(row, Color.FromHex("#FF79D5F7"));
        AddColorSwatch(row, Color.FromHex("#FFE6B85C"));
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
        AddColorSwatch(row, Color.FromHex("#FF65F2A5"));
        AddColorSwatch(row, Color.FromHex("#FFFD77D4"));
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
        AddColorSwatch(row, Color.FromHex("#FF4FC3F7"));
        AddColorSwatch(row, Color.FromHex("#FFF3F6FA"));
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
        AddColorSwatch(row, Color.FromHex("#FF2CB8F5"));
        AddColorSwatch(row, Color.FromHex("#FFFFB74D"));
        return row;
    }

    private ControlBase CreateOverlayToggle(string text, bool isEnabled, Action<bool> setEnabled, params string[] colors)
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
        foreach (var color in colors) AddColorSwatch(row, Color.FromHex(color));
        return row;
    }
}
