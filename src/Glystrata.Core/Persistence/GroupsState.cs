using Glystrata.Core.Groups;

namespace Glystrata.Core.Persistence;

public sealed class GroupsState
{
    public int SchemaVersion { get; set; } = 1;
    public List<GroupStateItem> Groups { get; set; } = new();

    public static GroupsState FromGroups(IEnumerable<Group> groups) => new()
    {
        Groups = groups.Select(group => new GroupStateItem
        {
            Id = group.Id,
            Name = group.Name,
            Items = group.Items.Select(item => new GroupItemState
            {
                Id = item.Id,
                Path = item.Path,
                Kind = item.Kind
            }).ToList()
        }).ToList()
    };

    public void ApplyTo(GroupManager manager)
    {
        manager.Groups.Clear();
        foreach (var groupState in Groups)
        {
            var group = new Group(groupState.Name, groupState.Id);
            foreach (var item in groupState.Items)
            {
                group.Items.Add(new GroupItem(item.Path, item.Kind, item.Id));
            }

            manager.Groups.Add(group);
        }
    }
}

public sealed class GroupStateItem
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<GroupItemState> Items { get; set; } = new();
}

public sealed class GroupItemState
{
    public Guid Id { get; set; }
    public string Path { get; set; } = string.Empty;
    public GroupItemKind Kind { get; set; }
}
