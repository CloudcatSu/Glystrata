using Glystrata.Core.Documents;

namespace Glystrata.Core.Snapshots;

public interface ISnapshotService
{
    Task<IReadOnlyList<SnapshotInfo>> ListAsync(string sourcePath, CancellationToken cancellationToken = default);
    Task<SnapshotInfo?> CreateAsync(DocumentSession document, int maxSnapshots, CancellationToken cancellationToken = default);
    Task DeleteAsync(string sourcePath, Guid snapshotId, CancellationToken cancellationToken = default);
    Task<bool> UpdateNoteAsync(string sourcePath, Guid snapshotId, string note, CancellationToken cancellationToken = default);
    Task DeleteAllAsync(string sourcePath, CancellationToken cancellationToken = default);
    Task<SnapshotInfo?> GetAsync(string sourcePath, Guid snapshotId, CancellationToken cancellationToken = default);
    bool HideSidecarFiles { get; set; }
}

public sealed class SnapshotService : ISnapshotService
{
    private readonly SnapshotSidecarStore _store;

    public SnapshotService(SnapshotSidecarStore? store = null)
    {
        _store = store ?? new SnapshotSidecarStore();
    }

    public bool HideSidecarFiles
    {
        get => _store.HideSidecarFiles;
        set => _store.HideSidecarFiles = value;
    }

    public Task<IReadOnlyList<SnapshotInfo>> ListAsync(string sourcePath, CancellationToken cancellationToken = default) =>
        _store.ReadAsync(sourcePath, cancellationToken);

    public async Task<SnapshotInfo?> CreateAsync(
        DocumentSession document,
        int maxSnapshots,
        CancellationToken cancellationToken = default)
    {
        if (document.FilePath is null)
        {
            return null;
        }

        maxSnapshots = Math.Clamp(maxSnapshots, 1, 200);

        // Decided inside the mutation rather than before it: both "the document is empty and there is
        // no history yet" and "nothing changed since the last snapshot" have to be judged against the
        // list we are about to write, not against one read before someone else's write landed.
        SnapshotInfo? created = null;
        await _store.MutateAsync(
            document.FilePath,
            snapshots =>
            {
                if (snapshots.Count == 0 && string.IsNullOrEmpty(document.Text))
                {
                    return false;
                }
                if (snapshots.Count > 0 && string.Equals(snapshots[0].Text, document.Text, StringComparison.Ordinal))
                {
                    return false;
                }

                created = new SnapshotInfo(
                    Guid.NewGuid(),
                    DateTime.UtcNow,
                    document.Text,
                    document.Encoding,
                    document.LineEnding,
                    document.LastSavedFingerprint);
                snapshots.Insert(0, created);
                if (snapshots.Count > maxSnapshots)
                {
                    snapshots.RemoveRange(maxSnapshots, snapshots.Count - maxSnapshots);
                }

                return true;
            },
            cancellationToken);

        if (created is null)
        {
            return null;
        }

        document.LastSnapshotUtc = created.CreatedUtc;
        document.LastSnapshotText = document.Text;
        return created;
    }

    public Task DeleteAsync(string sourcePath, Guid snapshotId, CancellationToken cancellationToken = default) =>
        _store.MutateAsync(
            sourcePath,
            snapshots => snapshots.RemoveAll(snapshot => snapshot.Id == snapshotId) > 0,
            cancellationToken);

    public Task<bool> UpdateNoteAsync(string sourcePath, Guid snapshotId, string note, CancellationToken cancellationToken = default) =>
        _store.MutateAsync(
            sourcePath,
            snapshots =>
            {
                var index = snapshots.FindIndex(snapshot => snapshot.Id == snapshotId);
                if (index < 0)
                {
                    return false;
                }

                snapshots[index] = snapshots[index] with { Note = note };
                return true;
            },
            cancellationToken);

    public Task DeleteAllAsync(string sourcePath, CancellationToken cancellationToken = default) =>
        _store.DeleteAllAsync(sourcePath, cancellationToken);

    public async Task<SnapshotInfo?> GetAsync(string sourcePath, Guid snapshotId, CancellationToken cancellationToken = default)
    {
        var snapshots = await _store.ReadAsync(sourcePath, cancellationToken);
        return snapshots.FirstOrDefault(snapshot => snapshot.Id == snapshotId);
    }
}
