using Glystrata.Core.Layout;

namespace Glystrata.Core.Persistence;

public sealed class SessionState
{
    public int SchemaVersion { get; set; } = 1;
    public Guid RootNodeId { get; set; }
    public List<PaneNodeState> Nodes { get; set; } = new();
    public List<SessionViewState> Views { get; set; } = new();
    public Guid? ActiveViewId { get; set; }

    /// <summary>Documents taken out into windows of their own. Their views are listed in
    /// <see cref="Views"/> like any other; this only records which window each one sits in and where.
    /// Shared with the Mac build's session format as "floatingWindows".</summary>
    public List<FloatingWindowState> FloatingWindows { get; set; } = new();
}

public sealed class FloatingWindowState
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public List<Guid> ViewIds { get; set; } = new();
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
