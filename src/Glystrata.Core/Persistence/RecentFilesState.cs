namespace Glystrata.Core.Persistence;

public sealed class RecentFilesState
{
    public int SchemaVersion { get; set; } = 1;
    public List<RecentFileEntry> Files { get; set; } = new();
}

public sealed class RecentFileEntry
{
    public string Path { get; set; } = string.Empty;
    public DateTime LastOpenedUtc { get; set; }
}
