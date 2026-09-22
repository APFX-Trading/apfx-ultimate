namespace cAlgo.Robots;

/// <summary>
/// Displays the live economic-calendar feed without rebuilding chart controls after startup.
/// </summary>
public sealed class CalendarNewsSection
{
    private const int MaximumEvents = 100;
    private const int MaximumVisibleEvents = 8;
    private readonly double _fontSize;
    private readonly CalendarNewsState _state;
    private readonly List<NewsRow> _newsRows = new();
    private Grid? _newsGrid;
    private TextBlock? _statusText;
    private int _renderedVersion = -1;

    public CalendarNewsSection(double fontSize, CalendarNewsState state)
    {
        _fontSize = fontSize;
        _state = state;
    }

    public ControlBase CreateControl(RobotContext context)
    {
        var section = new Grid(3, 1)
        {
            VerticalAlignment = VerticalAlignment.Top
        };
        section.Rows[0].SetHeightToAuto();
        section.Rows[1].SetHeightToAuto();
        section.Rows[2].SetHeightToAuto();
        section.AddChild(new TextBlock
        {
            Text = "Calendar News",
            ForegroundColor = Color.FromHex("#FF6FE3FF"),
            FontWeight = FontWeight.ExtraBold,
            FontSize = _fontSize + 1,
            HorizontalAlignment = HorizontalAlignment.Center
        }, 0, 0);
        _statusText = new TextBlock
        {
            Text = string.Empty,
            ForegroundColor = Color.FromHex("#FFBDEBFF"),
            FontSize = Math.Max(9, _fontSize - 2),
            Margin = new Thickness(0, 1, 0, 4)
        };
        section.AddChild(_statusText, 1, 0);

        _newsGrid = new Grid(1, 6);
        _newsGrid.Rows[0].SetHeightToAuto();
        AddCell(_newsGrid, "When", 0, 0, true);
        AddCell(_newsGrid, "Event", 0, 1, true);
        AddCell(_newsGrid, "A", 0, 2, true);
        AddCell(_newsGrid, "F", 0, 3, true);
        AddCell(_newsGrid, "P", 0, 4, true);
        AddCell(_newsGrid, "Impact", 0, 5, true);

        section.AddChild(new ScrollViewer
        {
            Content = _newsGrid,
            BackgroundColor = Color.FromHex("#FF112237"),
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalAlignment = VerticalAlignment.Top,
            // Preserve the compact panel height while allowing the full calendar feed to scroll.
            Height = (MaximumVisibleEvents + 3) * Math.Max(16, _fontSize + 5)
        }, 2, 0);
        Refresh(context);
        return section;
    }

    public void Refresh(RobotContext context)
    {
        if (_statusText is null || _renderedVersion == _state.Version)
        {
            return;
        }

        _statusText.Text = _state.Status;
        var rowIndex = 0;
        DateOnly? previousDate = null;
        foreach (var item in _state.Items
            .Take(MaximumEvents))
        {
            var chartTime = context.ConvertUtcToChartTime(item.Time.UtcDateTime);
            var itemDate = DateOnly.FromDateTime(chartTime);
            if (itemDate != previousDate)
            {
                EnsureRow(rowIndex);
                _newsRows[rowIndex].SetDate(chartTime.ToString("ddd dd MMM"));
                rowIndex++;
            }

            EnsureRow(rowIndex);
            var row = _newsRows[rowIndex];
            row.Time.Text = chartTime.ToString("HH:mm");
            row.Event.Text = $"{item.Currency} {item.Name}";
            row.Actual.Text = DisplayValue(item.Actual);
            row.Forecast.Text = DisplayValue(item.Forecast);
            row.Previous.Text = DisplayValue(item.Previous);
            row.Impact.Text = GetImpactLabel(item.Importance);
            row.Impact.ForegroundColor = Color.FromHex(GetImpactColor(item.Importance));
            previousDate = itemDate;
            rowIndex++;
        }

        for (; rowIndex < _newsRows.Count; rowIndex++)
        {
            _newsRows[rowIndex].Clear();
        }

        _renderedVersion = _state.Version;
    }

    private void EnsureRow(int index)
    {
        while (_newsRows.Count <= index)
        {
            var rowIndex = _newsRows.Count + 1;
            _newsGrid!.AddRow().SetHeightToAuto();
            _newsRows.Add(new NewsRow(
                AddCell(_newsGrid, string.Empty, rowIndex, 0, false),
                AddCell(_newsGrid, string.Empty, rowIndex, 1, false),
                AddCell(_newsGrid, string.Empty, rowIndex, 2, false),
                AddCell(_newsGrid, string.Empty, rowIndex, 3, false),
                AddCell(_newsGrid, string.Empty, rowIndex, 4, false),
                AddCell(_newsGrid, string.Empty, rowIndex, 5, false)));
        }
    }

    private TextBlock AddCell(Grid grid, string text, int row, int column, bool isHeader)
    {
        var cell = new TextBlock
        {
            Text = text,
            ForegroundColor = Color.FromHex(isHeader ? "#FFBDEBFF" : "#FFFFFFFF"),
            FontWeight = isHeader ? FontWeight.ExtraBold : FontWeight.Normal,
            FontSize = Math.Max(9, _fontSize - 1),
            Margin = new Thickness(2)
        };
        grid.AddChild(cell, row, column);
        return cell;
    }

    private static string GetImpactLabel(int importance) => importance switch
    {
        3 => "High",
        2 => "Medium",
        1 => "Low",
        _ => "None"
    };

    private static string GetImpactColor(int importance) => importance switch
    {
        >= 3 => "#FFFF8B98",
        2 => "#FFFFD06A",
        _ => "#FFBDEBFF"
    };

    private static string DisplayValue(string? value) => string.IsNullOrWhiteSpace(value) ? "-" : value;

    private sealed record NewsRow(TextBlock Time, TextBlock Event, TextBlock Actual, TextBlock Forecast, TextBlock Previous, TextBlock Impact)
    {
        public void SetDate(string date)
        {
            Time.Text = date;
            Event.Text = string.Empty;
            Actual.Text = string.Empty;
            Forecast.Text = string.Empty;
            Previous.Text = string.Empty;
            Impact.Text = string.Empty;
        }

        public void Clear()
        {
            Time.Text = string.Empty;
            Event.Text = string.Empty;
            Actual.Text = string.Empty;
            Forecast.Text = string.Empty;
            Previous.Text = string.Empty;
            Impact.Text = string.Empty;
            Impact.ForegroundColor = Color.FromHex("#FFFFFFFF");
        }
    }
}
