using MDeditor.Core.Layout;

namespace MDeditor.Core.Persistence;

public sealed class SessionState
{
    public int SchemaVersion { get; set; } = 1;
    public Guid RootNodeId { get; set; }
    public List<PaneNodeState> Nodes { get; set; } = new();
    public List<SessionViewState> Views { get; set; } = new();
    public Guid? ActiveViewId { get; set; }
}

public sealed class PaneNodeState
{
    public Guid Id { get; set; }
    public bool IsSplit { get; set; }
    public SplitOrientation Orientation { get; set; }
    public double Ratio { get; set; } = 0.5;
    public Guid? FirstNodeId { get; set; }
    public Guid? SecondNodeId { get; set; }
}

public sealed class SessionViewState
{
    public Guid ViewId { get; set; }
    public string Path { get; set; } = string.Empty;
    public Guid? SourceGroupId { get; set; }
    public Guid PaneId { get; set; }
    public int CaretOffset { get; set; }
    public double HorizontalOffset { get; set; }
    public double VerticalOffset { get; set; }
}
