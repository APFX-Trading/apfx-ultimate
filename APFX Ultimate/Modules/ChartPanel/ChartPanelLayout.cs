namespace cAlgo.Robots;

public static class ChartPanelLayout
{
    public static StackPanel CreateContent(PanelPosition position, double fontSize)
    {
        var content = new StackPanel
        {
            Orientation = Orientation.Vertical
        };
        content.AddChild(new TextBlock
        {
            Text = "APFX Ultimate",
            ForegroundColor = Color.FromHex("#FF6FE3FF"),
            FontWeight = FontWeight.ExtraBold,
            FontSize = fontSize + 4,
            HorizontalAlignment = HorizontalAlignment.Center
        });

        return content;
    }

    public static Border CreateSection(ControlBase content)
    {
        return new Border
        {
            Child = content,
            BackgroundColor = Color.FromHex("#FF112237"),
            BorderColor = Color.FromHex("#FF254866"),
            BorderThickness = 1,
            CornerRadius = 5,
            Padding = 7,
            Margin = new Thickness(0, 7, 0, 0)
        };
    }

    public static Border CreateBorder(ControlBase content, PanelPosition position)
    {
        var (horizontalAlignment, verticalAlignment) = position.GetAlignments();

        return new Border
        {
            Child = content,
            HorizontalAlignment = horizontalAlignment,
            VerticalAlignment = verticalAlignment,
            BackgroundColor = Color.FromHex("#F20B1726"),
            BorderColor = Color.FromHex("#FF28B9F5"),
            BorderThickness = 2,
            CornerRadius = 4,
            Padding = 8,
            Margin = 12
        };
    }
}
