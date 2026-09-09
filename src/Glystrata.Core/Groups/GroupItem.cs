namespace Glystrata.Core.Groups;

public enum GroupItemKind
{
    File,
    Folder
}

public sealed class GroupItem
{
    public GroupItem(string path, GroupItemKind kind, Guid? id = null)
    {
        Id = id ?? Guid.NewGuid();
        Path = System.IO.Path.GetFullPath(path);
        Kind = kind;
    }

    public Guid Id { get; }

    public string Path { get; }

    public GroupItemKind Kind { get; }

    public bool Exists => Kind == GroupItemKind.Folder ? Directory.Exists(Path) : File.Exists(Path);

    public string DisplayName =>
        Kind == GroupItemKind.Folder
            ? new DirectoryInfo(Path).Name
            : new FileInfo(Path).Name;
}
