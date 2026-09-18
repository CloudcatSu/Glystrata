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

    /// <summary>Hex colour shown as a bar beside the group and its documents' tabs, or null for none.
    /// The value is stored here rather than as an index into the settings palette so that editing or
    /// removing a palette entry can never leave a group pointing at something that is gone.</summary>
    public string? Color { get; set; }

    public ObservableCollection<GroupItem> Items { get; } = new();
}
