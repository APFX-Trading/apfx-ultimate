namespace cAlgo.Robots;

public enum PanelPosition
{
    TopLeft,
    TopCenter,
    TopRight,
    CenterLeft,
    CenterCenter,
    CenterRight,
    BottomLeft,
    BottomCenter,
    BottomRight
}

public static class PanelPositionExtensions
{
    public static (HorizontalAlignment Horizontal, VerticalAlignment Vertical) GetAlignments(this PanelPosition position)
    {
        return position switch
        {
            PanelPosition.TopLeft => (HorizontalAlignment.Left, VerticalAlignment.Top),
            PanelPosition.TopCenter => (HorizontalAlignment.Center, VerticalAlignment.Top),
            PanelPosition.TopRight => (HorizontalAlignment.Right, VerticalAlignment.Top),
            PanelPosition.CenterLeft => (HorizontalAlignment.Left, VerticalAlignment.Center),
            PanelPosition.CenterCenter => (HorizontalAlignment.Center, VerticalAlignment.Center),
            PanelPosition.CenterRight => (HorizontalAlignment.Right, VerticalAlignment.Center),
            PanelPosition.BottomLeft => (HorizontalAlignment.Left, VerticalAlignment.Bottom),
            PanelPosition.BottomCenter => (HorizontalAlignment.Center, VerticalAlignment.Bottom),
            PanelPosition.BottomRight => (HorizontalAlignment.Right, VerticalAlignment.Bottom),
            _ => throw new ArgumentOutOfRangeException(nameof(position), position, "Unsupported panel position.")
        };
    }

    public static string GetDisplayName(this PanelPosition position)
    {
        return position switch
        {
            PanelPosition.TopLeft => "Top-Left",
            PanelPosition.TopCenter => "Top-Center",
            PanelPosition.TopRight => "Top-Right",
            PanelPosition.CenterLeft => "Center-Left",
            PanelPosition.CenterCenter => "Center-Center",
            PanelPosition.CenterRight => "Center-Right",
            PanelPosition.BottomLeft => "Bottom-Left",
            PanelPosition.BottomCenter => "Bottom-Center",
            PanelPosition.BottomRight => "Bottom-Right",
            _ => throw new ArgumentOutOfRangeException(nameof(position), position, "Unsupported panel position.")
        };
    }
}
