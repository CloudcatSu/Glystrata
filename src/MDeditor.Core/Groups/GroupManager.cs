using System.Collections.ObjectModel;
using MDeditor.Core.Documents;

namespace MDeditor.Core.Groups;

public sealed class GroupManager
{
    public ObservableCollection<Group> Groups { get; } = new();

    public Group CreateGroup(string name)
    {
        var group = new Group(name);
        Groups.Add(group);
        return group;
    }

    public bool RenameGroup(Guid groupId, string name)
    {
        var group = Find(groupId);
        if (group is null || string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        group.Name = name.Trim();
        return true;
    }

    public bool DeleteGroup(Guid groupId)
    {
        var group = Find(groupId);
        return group is not null && Groups.Remove(group);
    }

    public GroupItem? AddPath(Guid groupId, string path, GroupItemKind? kind = null)
    {
        var group = Find(groupId);
        if (group is null || string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var fullPath = Path.GetFullPath(path);
        var itemKind = kind ?? InferKind(fullPath);
        if (itemKind is null)
        {
            return null;
        }

        var existing = group.Items.FirstOrDefault(item => PathsEqual(item.Path, fullPath));
        if (existing is not null)
        {
            return existing;
        }

        var item = new GroupItem(fullPath, itemKind.Value);
        group.Items.Add(item);
        return item;
    }

    public bool RemovePath(Guid groupId, string path)
    {
        var group = Find(groupId);
        var item = group?.Items.FirstOrDefault(candidate => PathsEqual(candidate.Path, path));
        return item is not null && group!.Items.Remove(item);
    }

    public bool MovePath(Guid sourceGroupId, Guid targetGroupId, string path)
    {
        var source = Find(sourceGroupId);
        var target = Find(targetGroupId);
        if (source is null || target is null || source.Id == target.Id)
        {
            return false;
        }

        var sourceItem = source.Items.FirstOrDefault(candidate => PathsEqual(candidate.Path, path));
        if (sourceItem is null)
        {
            return false;
        }

        if (target.Items.All(candidate => !PathsEqual(candidate.Path, sourceItem.Path)))
        {
            target.Items.Add(new GroupItem(sourceItem.Path, sourceItem.Kind, sourceItem.Id));
        }

        source.Items.Remove(sourceItem);
        return true;
    }

    public Group? Find(Guid groupId) => Groups.FirstOrDefault(group => group.Id == groupId);

    public void MoveGroup(int oldIndex, int newIndex)
    {
        if (oldIndex < 0 || oldIndex >= Groups.Count || newIndex < 0 || newIndex >= Groups.Count || oldIndex == newIndex)
        {
            return;
        }

        Groups.Move(oldIndex, newIndex);
    }

    public IReadOnlyList<FileSystemEntry> EnumerateFolder(string path)
    {
        if (!Directory.Exists(path))
        {
            return Array.Empty<FileSystemEntry>();
        }

        try
        {
            var directory = new DirectoryInfo(path);
            var directories = directory.EnumerateDirectories()
                .Where(item => !item.Attributes.HasFlag(FileAttributes.Hidden))
                .Select(item => new FileSystemEntry(item.FullName, item.Name, GroupItemKind.Folder));
            var files = directory.EnumerateFiles()
                .Where(item => !item.Attributes.HasFlag(FileAttributes.Hidden) && !IsSnapshotSidecar(item.Name))
                .Select(item => new FileSystemEntry(item.FullName, item.Name, GroupItemKind.File));

            return directories.Concat(files)
                .OrderBy(item => item.Kind == GroupItemKind.File)
                .ThenBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();
        }
        catch (UnauthorizedAccessException)
        {
            return Array.Empty<FileSystemEntry>();
        }
        catch (IOException)
        {
            return Array.Empty<FileSystemEntry>();
        }
    }

    private static GroupItemKind? InferKind(string path)
    {
        if (Directory.Exists(path))
        {
            return GroupItemKind.Folder;
        }

        if (File.Exists(path))
        {
            return GroupItemKind.File;
        }

        return null;
    }

    private static bool PathsEqual(string left, string right) =>
        string.Equals(DocumentManager.CanonicalizePath(left), DocumentManager.CanonicalizePath(right), StringComparison.OrdinalIgnoreCase);

    private static bool IsSnapshotSidecar(string name) =>
        name.EndsWith(".mdeditor-snapshots.json", StringComparison.OrdinalIgnoreCase);
}

public sealed record FileSystemEntry(string Path, string Name, GroupItemKind Kind);
