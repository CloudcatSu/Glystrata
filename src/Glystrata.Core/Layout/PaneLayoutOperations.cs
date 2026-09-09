namespace Glystrata.Core.Layout;

public static class PaneLayoutOperations
{
    public static Guid GetFirstPaneId(PaneLayoutNode root) => root switch
    {
        PaneLayoutNode.EditorPane pane => pane.PaneId,
        PaneLayoutNode.Split split => GetFirstPaneId(split.First),
        _ => throw new InvalidOperationException("未知的窗格布局節點。")
    };

    public static IEnumerable<Guid> EnumeratePaneIds(PaneLayoutNode root)
    {
        if (root is PaneLayoutNode.EditorPane pane)
        {
            yield return pane.PaneId;
            yield break;
        }

        var split = (PaneLayoutNode.Split)root;
        foreach (var id in EnumeratePaneIds(split.First))
        {
            yield return id;
        }

        foreach (var id in EnumeratePaneIds(split.Second))
        {
            yield return id;
        }
    }

    public static PaneLayoutNode CollapseToSinglePane(PaneLayoutNode root) =>
        new PaneLayoutNode.EditorPane(GetFirstPaneId(root));

    public static PaneLayoutNode? RemovePane(PaneLayoutNode root, Guid paneId, out bool removed)
    {
        if (root is PaneLayoutNode.EditorPane pane)
        {
            removed = pane.PaneId == paneId;
            return removed ? null : pane;
        }

        var split = (PaneLayoutNode.Split)root;
        if (ContainsPane(split.First, paneId))
        {
            var first = RemovePane(split.First, paneId, out removed);
            return first is null ? split.Second : split with { First = first };
        }

        if (ContainsPane(split.Second, paneId))
        {
            var second = RemovePane(split.Second, paneId, out removed);
            return second is null ? split.First : split with { Second = second };
        }

        removed = false;
        return split;
    }

    public static bool ContainsPane(PaneLayoutNode root, Guid paneId) => root switch
    {
        PaneLayoutNode.EditorPane pane => pane.PaneId == paneId,
        PaneLayoutNode.Split split => ContainsPane(split.First, paneId) || ContainsPane(split.Second, paneId),
        _ => false
    };
}
