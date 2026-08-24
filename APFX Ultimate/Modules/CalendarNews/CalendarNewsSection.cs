namespace cAlgo.Robots;

/// <summary>
/// Displays the live economic-calendar feed without rebuilding chart controls after startup.
/// </summary>
public sealed class CalendarNewsSection
{
    private const int MaximumEvents = 5;
    private const int MaximumRows = MaximumEvents * 2;
    private readonly double _fontSize;
    private readonly CalendarNewsState _state;
    private readonly List<NewsRow> _newsRows = new();
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
            VerticalAlignment = VerticalAlignment.Stretch
        };
        section.Rows[0].SetHeightToAuto();
        section.Rows[1].SetHeightToAuto();
        section.Rows[2].SetHeightInStars(1);
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

        var newsGrid = new Grid(MaximumRows + 1, 3);
        foreach (var row in newsGrid.Rows)
        {
            row.SetHeightToAuto();
        }
        AddCell(newsGrid, "When", 0, 0, true);
        AddCell(newsGrid, "Event", 0, 1, true);
        AddCell(newsGrid, "Impact", 0, 2, true);
        for (var row = 1; row <= MaximumRows; row++)
        {
            _newsRows.Add(new NewsRow(
                AddCell(newsGrid, string.Empty, row, 0, false),
                AddCell(newsGrid, string.Empty, row, 1, false),
                AddCell(newsGrid, string.Empty, row, 2, false)));
        }

        section.AddChild(new ScrollViewer
        {
            Content = newsGrid,
            BackgroundColor = Color.FromHex("#FF112237"),
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalAlignment = VerticalAlignment.Stretch
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
        var chartNow = context.ConvertUtcToChartTime(DateTime.UtcNow);
        foreach (var item in _state.Items
            .Where(item => context.ConvertUtcToChartTime(item.Time.UtcDateTime) >= chartNow)
            .Take(MaximumEvents))
        {
            var chartTime = context.ConvertUtcToChartTime(item.Time.UtcDateTime);
            var itemDate = DateOnly.FromDateTime(chartTime);
            if (itemDate != previousDate && rowIndex < _newsRows.Count)
            {
                _newsRows[rowIndex].SetDate(chartTime.ToString("ddd dd MMM"));
                rowIndex++;
            }

            if (rowIndex >= _newsRows.Count)
            {
                break;
            }

            var row = _newsRows[rowIndex];
            row.Time.Text = chartTime.ToString("HH:mm");
            row.Event.Text = $"{item.Currency} {item.Name}";
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

    private sealed record NewsRow(TextBlock Time, TextBlock Event, TextBlock Impact)
    {
        public void SetDate(string date)
        {
            Time.Text = date;
            Event.Text = string.Empty;
            Impact.Text = string.Empty;
        }

        public void Clear()
        {
            Time.Text = string.Empty;
            Event.Text = string.Empty;
            Impact.Text = string.Empty;
            Impact.ForegroundColor = Color.FromHex("#FFFFFFFF");
        }
    }
}
