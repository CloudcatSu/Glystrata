using System.Collections.ObjectModel;

namespace Glystrata.Core.Groups;

public sealed class Group
{
    public Group(string name, Guid? id = null)
    {
        Id = id ?? Guid.NewGuid();
        Name = string.IsNullOrWhiteSpace(name) ? "未命名群組" : name.Trim();
    }

    public Guid Id { get; }

    public string Name { get; set; }

    public ObservableCollection<GroupItem> Items { get; } = new();
}
