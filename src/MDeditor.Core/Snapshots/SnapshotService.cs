using MDeditor.Core.Documents;

namespace MDeditor.Core.Snapshots;

public interface ISnapshotService
{
    Task<IReadOnlyList<SnapshotInfo>> ListAsync(string sourcePath, CancellationToken cancellationToken = default);
    Task<SnapshotInfo?> CreateAsync(DocumentSession document, int maxSnapshots, CancellationToken cancellationToken = default);
    Task DeleteAsync(string sourcePath, Guid snapshotId, CancellationToken cancellationToken = default);
    Task DeleteAllAsync(string sourcePath, CancellationToken cancellationToken = default);
    Task<SnapshotInfo?> GetAsync(string sourcePath, Guid snapshotId, CancellationToken cancellationToken = default);
}

public sealed class SnapshotService : ISnapshotService
{
    private readonly SnapshotSidecarStore _store;

    public SnapshotService(SnapshotSidecarStore? store = null)
    {
        _store = store ?? new SnapshotSidecarStore();
    }

    public Task<IReadOnlyList<SnapshotInfo>> ListAsync(string sourcePath, CancellationToken cancellationToken = default) =>
        _store.ReadAsync(sourcePath, cancellationToken);

    public async Task<SnapshotInfo?> CreateAsync(
        DocumentSession document,
        int maxSnapshots,
        CancellationToken cancellationToken = default)
    {
        if (document.FilePath is null || string.IsNullOrEmpty(document.Text))
        {
            return null;
        }

        maxSnapshots = Math.Clamp(maxSnapshots, 1, 200);
        var current = await _store.ReadAsync(document.FilePath, cancellationToken);
        if (current.Count > 0 && string.Equals(current[0].Text, document.Text, StringComparison.Ordinal))
        {
            return null;
        }

        var snapshot = new SnapshotInfo(
            Guid.NewGuid(),
            DateTime.UtcNow,
            document.Text,
            document.Encoding,
            document.LineEnding,
            document.LastSavedFingerprint);
        var next = current.Prepend(snapshot).Take(maxSnapshots).ToArray();
        await _store.WriteAsync(document.FilePath, next, cancellationToken);
        document.LastSnapshotUtc = snapshot.CreatedUtc;
        document.LastSnapshotText = document.Text;
        return snapshot;
    }

    public async Task DeleteAsync(string sourcePath, Guid snapshotId, CancellationToken cancellationToken = default)
    {
        var snapshots = await _store.ReadAsync(sourcePath, cancellationToken);
        var remaining = snapshots.Where(snapshot => snapshot.Id != snapshotId).ToArray();
        if (remaining.Length == snapshots.Count)
        {
            return;
        }

        if (remaining.Length == 0)
        {
            await _store.DeleteAllAsync(sourcePath, cancellationToken);
        }
        else
        {
            await _store.WriteAsync(sourcePath, remaining, cancellationToken);
        }
    }

    public Task DeleteAllAsync(string sourcePath, CancellationToken cancellationToken = default) =>
        _store.DeleteAllAsync(sourcePath, cancellationToken);

    public async Task<SnapshotInfo?> GetAsync(string sourcePath, Guid snapshotId, CancellationToken cancellationToken = default)
    {
        var snapshots = await _store.ReadAsync(sourcePath, cancellationToken);
        return snapshots.FirstOrDefault(snapshot => snapshot.Id == snapshotId);
    }
}
