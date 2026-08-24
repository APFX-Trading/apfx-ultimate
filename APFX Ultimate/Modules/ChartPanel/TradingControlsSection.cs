namespace cAlgo.Robots;

/// <summary>
/// Owns the chart-symbol trading buttons and their status feedback.
/// </summary>
public sealed class TradingControlsSection
{
    private const double ActionButtonWidth = 125;
    private readonly Action _refreshStatistics;
    private readonly double _fontSize;

    public TradingControlsSection(double fontSize, Action refreshStatistics)
    {
        _fontSize = fontSize;
        _refreshStatistics = refreshStatistics ?? throw new ArgumentNullException(nameof(refreshStatistics));
    }

    public ControlBase CreateControl(RobotContext context)
    {
        var section = new StackPanel
        {
            Orientation = Orientation.Vertical
        };
        section.AddChild(CreateSectionTitle("Quick Actions"));
        section.AddChild(CreateButtonRow(
            CreateButton("Buy", ActionButtonWidth, "#FF07964C", "#FF55E895", _ => OpenPosition(context, TradeType.Buy)),
            CreateButton("Sell", ActionButtonWidth, "#FFD23945", "#FFFF8992", _ => OpenPosition(context, TradeType.Sell)),
            CreateButton("Close Pendings", ActionButtonWidth, "#FFB96C0D", "#FFFFB74D", _ => ClosePendingOrders(context)),
            CreateButton("Close All", ActionButtonWidth, "#FFC41633", "#FFFF7890", _ => CloseAll(context))));

        return section;
    }

    private Button CreateButton(string text, double width, string backgroundColor, string borderColor, Action<ButtonClickEventArgs> onClick)
    {
        var button = new Button
        {
            Text = text,
            Width = width,
            Height = Math.Max(32, _fontSize * 2),
            Margin = new Thickness(4, 5, 4, 2),
            BackgroundColor = Color.FromHex(backgroundColor),
            ForegroundColor = Color.White,
            BorderColor = Color.FromHex(borderColor),
            BorderThickness = 1,
            CornerRadius = 3,
            FontWeight = FontWeight.Bold,
            FontSize = _fontSize + 1,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        button.Click += onClick;
        return button;
    }

    private TextBlock CreateSectionTitle(string text)
    {
        return new TextBlock
        {
            Text = text,
            ForegroundColor = Color.FromHex("#FF6FE3FF"),
            FontWeight = FontWeight.ExtraBold,
            FontSize = _fontSize + 1,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 2)
        };
    }

    private static StackPanel CreateButtonRow(params Button[] buttons)
    {
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 2, 0, 2)
        };
        foreach (var button in buttons)
        {
            row.AddChild(button);
        }
        return row;
    }

    private void OpenPosition(RobotContext context, TradeType tradeType)
    {
        var result = context.OpenMarketOrder(tradeType);
        ShowResult($"Open {tradeType}", result, context);
    }

    private void ClosePendingOrders(RobotContext context)
    {
        ShowBatchResult("Close pending orders", context.CancelChartPendingOrders(), context);
        _refreshStatistics();
    }

    private void CloseAll(RobotContext context)
    {
        var positionResults = context.CloseChartPositions();
        var pendingOrderResults = context.CancelChartPendingOrders();
        ShowBatchResult("Close all", positionResults.Concat(pendingOrderResults).ToArray(), context);
        _refreshStatistics();
    }

    private void ShowResult(string action, TradeResult result, RobotContext context)
    {
        var message = result.IsSuccessful ? $"{action}: completed" : $"{action}: failed ({result.Error})";
        ShowStatus(message, context);
    }

    private void ShowBatchResult(string action, IReadOnlyList<TradeResult> results, RobotContext context)
    {
        if (results.Count == 0)
        {
            ShowStatus($"{action}: nothing to process", context);
            return;
        }

        var failureCount = results.Count(result => !result.IsSuccessful);
        var message = failureCount == 0
            ? $"{action}: {results.Count} completed"
            : $"{action}: {results.Count - failureCount}/{results.Count} completed";
        ShowStatus(message, context);
    }

    private void ShowStatus(string message, RobotContext context)
    {
        context.Log(message);
    }
}
