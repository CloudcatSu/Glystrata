using MDeditor.Core.Documents;

namespace MDeditor.Core.Snapshots;

public enum RestoreMode
{
    ReplaceCurrent,
    SaveAsNewFile
}

public sealed record SnapshotInfo(
    Guid Id,
    DateTime CreatedUtc,
    string Text,
    FileEncodingKind Encoding,
    LineEndingKind LineEnding,
    FileFingerprint? SourceFingerprint);

internal sealed class SnapshotSidecar
{
    public int SchemaVersion { get; set; } = 1;
    public string SourcePath { get; set; } = string.Empty;
    public List<SnapshotEntry> Snapshots { get; set; } = new();
}

internal sealed class SnapshotEntry
{
    public Guid Id { get; set; }
    public DateTime CreatedUtc { get; set; }
    public string Text { get; set; } = string.Empty;
    public FileEncodingKind Encoding { get; set; }
    public LineEndingKind LineEnding { get; set; }
    public FileFingerprint? SourceFingerprint { get; set; }

    public SnapshotInfo ToInfo() => new(Id, CreatedUtc, Text, Encoding, LineEnding, SourceFingerprint);

    public static SnapshotEntry FromInfo(SnapshotInfo info) => new()
    {
        Id = info.Id,
        CreatedUtc = info.CreatedUtc,
        Text = info.Text,
        Encoding = info.Encoding,
        LineEnding = info.LineEnding,
        SourceFingerprint = info.SourceFingerprint
    };
}
