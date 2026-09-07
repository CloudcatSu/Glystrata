namespace MDeditor.Core.Layout;

public enum SplitOrientation
{
    Horizontal,
    Vertical
}

public abstract record PaneLayoutNode(Guid Id)
{
    public sealed record EditorPane(Guid PaneId) : PaneLayoutNode(PaneId);

    public sealed record Split(
        Guid SplitId,
        SplitOrientation Orientation,
        double Ratio,
        PaneLayoutNode First,
        PaneLayoutNode Second) : PaneLayoutNode(SplitId);
}
