namespace cAlgo.Robots;

/// <summary>
/// Displays the live economic-calendar feed without rebuilding chart controls after startup.
/// </summary>
public sealed class CalendarNewsSection
{
    private const int MaximumRows = 5;
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

    public ControlBase CreateControl()
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
        Refresh();
        return section;
    }

    public void Refresh()
    {
        if (_statusText is null || _renderedVersion == _state.Version)
        {
            return;
        }

        _statusText.Text = _state.Status;
        DateOnly? previousDate = null;
        for (var index = 0; index < _newsRows.Count; index++)
        {
            var row = _newsRows[index];
            if (index >= _state.Items.Count)
            {
                row.Clear();
                continue;
            }

            var item = _state.Items[index];
            var localTime = item.Time.ToLocalTime();
            var itemDate = DateOnly.FromDateTime(localTime.DateTime);
            row.Time.Text = itemDate == previousDate
                ? localTime.ToString("HH:mm")
                : localTime.ToString("ddd dd MMM\nHH:mm");
            row.Event.Text = $"{item.Currency} {item.Name}";
            row.Impact.Text = GetImpactLabel(item.Importance);
            row.Impact.ForegroundColor = Color.FromHex(GetImpactColor(item.Importance));
            previousDate = itemDate;
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
        public void Clear()
        {
            Time.Text = string.Empty;
            Event.Text = string.Empty;
            Impact.Text = string.Empty;
            Impact.ForegroundColor = Color.FromHex("#FFFFFFFF");
        }
    }
}
