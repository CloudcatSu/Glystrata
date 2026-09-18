using Glystrata.Core.Documents;

namespace Glystrata.Core.Persistence;

/// <summary>
/// In-memory list of recently opened files, newest first. Owns ordering, de-duplication
/// (by canonical path, per <see cref="DocumentManager.CanonicalizePath"/>) and the cap on
/// entry count. Persistence is a separate concern: callers pair this with
/// <see cref="RecentFilesState"/> via <see cref="LoadFrom"/> / <see cref="ToState"/>, the same
/// way <see cref="GroupsState"/> pairs with <c>GroupManager</c>.
/// </summary>
public sealed class RecentFilesService
{
    public const int MaxEntries = 10;

    private readonly List<RecentFileEntry> _files = new();

    public IReadOnlyList<RecentFileEntry> Files => _files;

    public void Add(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        if (TryCanonicalize(path) is not { } canonical)
        {
            return;
        }

        _files.RemoveAll(entry => string.Equals(TryCanonicalize(entry.Path), canonical, StringComparison.OrdinalIgnoreCase));

        _files.Insert(0, new RecentFileEntry
        {
            Path = path,
            LastOpenedUtc = DateTime.UtcNow
        });

        if (_files.Count > MaxEntries)
        {
            _files.RemoveRange(MaxEntries, _files.Count - MaxEntries);
        }
    }

    /// <summary>
    /// Records a file as the oldest entry rather than the newest, and only if it is not listed
    /// already. Used to seed the list from the documents a restored session reopened: they belong in
    /// the list, but treating every restart as "you just opened all of these" would push out the files
    /// you closed yesterday, which are the ones a recent list is actually for.
    /// </summary>
    public void AddIfMissing(string path, DateTime lastOpenedUtc)
    {
        if (string.IsNullOrWhiteSpace(path) || _files.Count >= MaxEntries)
        {
            return;
        }

        if (TryCanonicalize(path) is not { } canonical)
        {
            return;
        }

        if (_files.Exists(entry => string.Equals(TryCanonicalize(entry.Path), canonical, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        _files.Add(new RecentFileEntry { Path = path, LastOpenedUtc = lastOpenedUtc });
    }

    public bool Remove(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        return TryCanonicalize(path) is { } canonical &&
            _files.RemoveAll(entry => string.Equals(TryCanonicalize(entry.Path), canonical, StringComparison.OrdinalIgnoreCase)) > 0;
    }

    public void Clear() => _files.Clear();

    /// <summary>
    /// Replaces the in-memory list from a loaded state, tolerating a corrupt-ish file:
    /// entries with a null/empty/whitespace path are dropped, duplicates (by canonical path)
    /// are dropped keeping the first occurrence, and the result is truncated to
    /// <see cref="MaxEntries"/>. Whether a path still exists on disk is deliberately not
    /// checked here - that is a question for the moment the menu opens, not load time.
    /// </summary>
    public void LoadFrom(RecentFilesState state)
    {
        _files.Clear();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // `"files": null`, a null element, or a path that is not a valid path at all are all things a
        // hand-edited or half-written recent.json can contain, and none of them may stop the editor
        // from starting.
        foreach (var entry in state.Files ?? new List<RecentFileEntry>())
        {
            if (entry is null || string.IsNullOrWhiteSpace(entry.Path))
            {
                continue;
            }

            if (TryCanonicalize(entry.Path) is not { } canonical || !seen.Add(canonical))
            {
                continue;
            }

            _files.Add(new RecentFileEntry
            {
                Path = entry.Path,
                LastOpenedUtc = entry.LastOpenedUtc
            });

            if (_files.Count >= MaxEntries)
            {
                break;
            }
        }
    }

    private static string? TryCanonicalize(string path)
    {
        try
        {
            return DocumentManager.CanonicalizePath(path);
        }
        catch (Exception exception) when (exception is ArgumentException or PathTooLongException or NotSupportedException)
        {
            return null;
        }
    }

    public RecentFilesState ToState() => new()
    {
        Files = _files.Select(entry => new RecentFileEntry
        {
            Path = entry.Path,
            LastOpenedUtc = entry.LastOpenedUtc
        }).ToList()
    };
}
