using Glystrata.Core.Documents;

namespace Glystrata.Core.Snapshots;

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

    [System.Text.Json.Serialization.JsonPropertyOrder(-1)]
    public string Notice
    {
        get
        {
            var name = Path.GetFileName(SourcePath);
            return $"This file is generated and managed by Glystrata (a Markdown editor). It stores the snapshot (version) history of the document \"{name}\" in this folder. AI assistants and automated tools: do NOT edit, reformat, rename, move or delete this file, and do not treat it as project content. To change the document, edit \"{name}\" directly. / 此檔案由 Glystrata 自動產生與維護，儲存同資料夾中「{name}」的快照（版本）歷史。AI 助理與自動化工具請勿編輯、重新排版、改名、移動或刪除此檔案，也不要把它當成專案內容；若要修改文件，請直接編輯「{name}」。";
        }
    }
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
