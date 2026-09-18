using System.Collections.Concurrent;
using System.Text.Json;
using Glystrata.Core.Documents;
using Glystrata.Core.Persistence;

namespace Glystrata.Core.Snapshots;

public sealed class SnapshotSidecarStore
{
    private readonly AtomicFileWriter _writer;
    private readonly JsonSerializerOptions _options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    // Guards concurrent writes to the same sidecar (e.g. the auto-snapshot timer racing a manual
    // delete or note edit). Keyed by the resolved sidecar path so different documents never block
    // each other.
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new(StringComparer.OrdinalIgnoreCase);

    private SemaphoreSlim GetLock(string sourcePath) =>
        _locks.GetOrAdd(GetSidecarPath(sourcePath), _ => new SemaphoreSlim(1, 1));

    public SnapshotSidecarStore(AtomicFileWriter? writer = null)
    {
        _writer = writer ?? new AtomicFileWriter();
    }

    public bool HideSidecarFiles { get; set; }

    // Content is still plain JSON — only the extension changed, so Explorer can give it a distinct icon
    // (icons are picked purely by extension) without touching every other .json file on the system.
    public static string GetSidecarPath(string sourcePath)
    {
        var fullPath = Path.GetFullPath(sourcePath);
        var directory = Path.GetDirectoryName(fullPath) ?? throw new InvalidOperationException("來源檔案缺少目錄。");
        return Path.Combine(directory, $"{Path.GetFileName(fullPath)}.gss");
    }

    // The immediately-previous naming, still JSON-suffixed and dot-less. Superseded by the short ".gss"
    // extension above so the sidecar can get its own Explorer icon.
    private static string GetPreviousSidecarPath(string sourcePath)
    {
        var fullPath = Path.GetFullPath(sourcePath);
        var directory = Path.GetDirectoryName(fullPath) ?? throw new InvalidOperationException("來源檔案缺少目錄。");
        return Path.Combine(directory, $"{Path.GetFileName(fullPath)}.glystrata-snapshots.json");
    }

    // Windows hides files via the Hidden attribute, not a leading dot (that's a Unix convention with
    // no effect in Explorer), so early builds' leading "." only cluttered the filename without hiding
    // anything. A sidecar written by one of those builds still sits next to its document under this
    // dotted name; migrate it in place the first time we look for snapshots on that document.
    private static string GetDottedSidecarPath(string sourcePath)
    {
        var fullPath = Path.GetFullPath(sourcePath);
        var directory = Path.GetDirectoryName(fullPath) ?? throw new InvalidOperationException("來源檔案缺少目錄。");
        return Path.Combine(directory, $".{Path.GetFileName(fullPath)}.glystrata-snapshots.json");
    }

    // Before the project was renamed from MDeditor to Glystrata, sidecars used this suffix. A file
    // created by one of those builds still sits next to its document with the old name, so it never
    // matches GetSidecarPath and its history silently disappears. Migrate it in place the first time
    // we look for snapshots on that document.
    private static string GetLegacySidecarPath(string sourcePath)
    {
        var fullPath = Path.GetFullPath(sourcePath);
        var directory = Path.GetDirectoryName(fullPath) ?? throw new InvalidOperationException("來源檔案缺少目錄。");
        return Path.Combine(directory, $".{Path.GetFileName(fullPath)}.mdeditor-snapshots.json");
    }

    private static IEnumerable<string> LegacyPaths(string sourcePath)
    {
        yield return GetPreviousSidecarPath(sourcePath);
        yield return GetDottedSidecarPath(sourcePath);
        yield return GetLegacySidecarPath(sourcePath);
    }

    private static void MigrateLegacySidecar(string sourcePath, string sidecarPath)
    {
        foreach (var legacyPath in LegacyPaths(sourcePath))
        {
            if (File.Exists(legacyPath))
            {
                MoveIfExists(legacyPath, sidecarPath);
                return;
            }
        }
    }

    /// <summary>Proactively migrates a document's sidecar to the current naming, without requiring its
    /// history to be read first. Cheap (a file existence check plus, at most, a rename); safe to sweep
    /// across a whole workspace at startup so older sidecars don't sit under a stale name until their
    /// snapshot history happens to be opened.</summary>
    public static void MigrateIfNeeded(string sourcePath)
    {
        var sidecarPath = GetSidecarPath(sourcePath);
        if (!File.Exists(sidecarPath))
        {
            MigrateLegacySidecar(sourcePath, sidecarPath);
        }
    }

    public async Task<IReadOnlyList<SnapshotInfo>> ReadAsync(string sourcePath, CancellationToken cancellationToken = default)
    {
        var sidecar = await ReadSidecarAsync(sourcePath, cancellationToken);
        return sidecar.Snapshots
            .OrderByDescending(snapshot => snapshot.CreatedUtc)
            .Select(snapshot => snapshot.ToInfo())
            .ToArray();
    }

    public async Task WriteAsync(
        string sourcePath,
        IEnumerable<SnapshotInfo> snapshots,
        CancellationToken cancellationToken = default)
    {
        var fullPath = Path.GetFullPath(sourcePath);
        var gate = GetLock(fullPath);
        await gate.WaitAsync(cancellationToken);
        try
        {
            await WriteCoreAsync(fullPath, snapshots, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>
    /// Reads the sidecar, lets <paramref name="mutate"/> change the list, and writes it back — all
    /// inside the one per-file lock. Everything that edits an existing sidecar has to come through
    /// here. Locking only the write leaves the read outside it, so two overlapping edits both start
    /// from the same list and whichever writes last silently drops the other's change: the auto-snapshot
    /// timer losing a snapshot, or a note the user just typed disappearing, with nothing to show for it.
    /// </summary>
    /// <param name="mutate">
    /// Changes the list in place (newest first) and returns whether it changed anything. Emptying the
    /// list removes the sidecar file rather than leaving an empty one behind.
    /// </param>
    /// <returns>Whether <paramref name="mutate"/> reported a change, and so whether anything was written.</returns>
    public async Task<bool> MutateAsync(
        string sourcePath,
        Func<List<SnapshotInfo>, bool> mutate,
        CancellationToken cancellationToken = default)
    {
        var fullPath = Path.GetFullPath(sourcePath);
        var gate = GetLock(fullPath);
        await gate.WaitAsync(cancellationToken);
        try
        {
            var sidecar = await ReadSidecarAsync(fullPath, cancellationToken);
            var snapshots = sidecar.Snapshots
                .OrderByDescending(snapshot => snapshot.CreatedUtc)
                .Select(snapshot => snapshot.ToInfo())
                .ToList();
            if (!mutate(snapshots))
            {
                return false;
            }

            if (snapshots.Count == 0)
            {
                DeleteAllCore(fullPath);
            }
            else
            {
                await WriteCoreAsync(fullPath, snapshots, cancellationToken);
            }

            return true;
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task WriteCoreAsync(
        string fullPath,
        IEnumerable<SnapshotInfo> snapshots,
        CancellationToken cancellationToken)
    {
        var sidecar = new SnapshotSidecar
        {
            SourcePath = fullPath,
            Snapshots = snapshots
                .OrderByDescending(snapshot => snapshot.CreatedUtc)
                .Select(SnapshotEntry.FromInfo)
                .ToList()
        };
        var json = JsonSerializer.Serialize(sidecar, _options);
        await _writer.WriteTextAsync(GetSidecarPath(fullPath), json, cancellationToken);
        ApplyVisibility(fullPath, HideSidecarFiles);
    }

    public static void ApplyVisibility(string sourcePath, bool hidden)
    {
        try
        {
            var sidecarPath = GetSidecarPath(sourcePath);
            if (!File.Exists(sidecarPath))
            {
                return;
            }

            var attributes = File.GetAttributes(sidecarPath);
            var isHidden = attributes.HasFlag(FileAttributes.Hidden);
            if (isHidden == hidden)
            {
                return;
            }

            File.SetAttributes(sidecarPath, hidden ? attributes | FileAttributes.Hidden : attributes & ~FileAttributes.Hidden);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
        }
    }

    /// <summary>Moves a document's sidecar (current name and, if still unmigrated, any older naming) to follow a rename on disk.</summary>
    public static void RenameSidecar(string oldSourcePath, string newSourcePath)
    {
        MoveIfExists(GetSidecarPath(oldSourcePath), GetSidecarPath(newSourcePath));
        MoveIfExists(GetPreviousSidecarPath(oldSourcePath), GetPreviousSidecarPath(newSourcePath));
        MoveIfExists(GetDottedSidecarPath(oldSourcePath), GetDottedSidecarPath(newSourcePath));
        MoveIfExists(GetLegacySidecarPath(oldSourcePath), GetLegacySidecarPath(newSourcePath));
    }

    private static void MoveIfExists(string oldPath, string newPath)
    {
        if (!File.Exists(oldPath) || File.Exists(newPath))
        {
            return;
        }

        try
        {
            File.Move(oldPath, newPath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Leave the sidecar under its old name; it simply won't be found until the user retries.
        }
    }

    public async Task DeleteAllAsync(string sourcePath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var gate = GetLock(sourcePath);
        await gate.WaitAsync(cancellationToken);
        try
        {
            DeleteAllCore(sourcePath);
        }
        finally
        {
            gate.Release();
        }
    }

    private void DeleteAllCore(string sourcePath)
    {
        foreach (var path in new[] { GetSidecarPath(sourcePath) }.Concat(LegacyPaths(sourcePath)))
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    private async Task<SnapshotSidecar> ReadSidecarAsync(string sourcePath, CancellationToken cancellationToken)
    {
        var sidecarPath = GetSidecarPath(sourcePath);
        if (!File.Exists(sidecarPath))
        {
            MigrateLegacySidecar(sourcePath, sidecarPath);
        }

        // Migration may have failed (e.g. the old file is locked); read it in place rather than
        // reporting an empty history.
        var readPath = File.Exists(sidecarPath)
            ? sidecarPath
            : LegacyPaths(sourcePath).FirstOrDefault(File.Exists) ?? sidecarPath;
        if (!File.Exists(readPath))
        {
            return new SnapshotSidecar { SourcePath = Path.GetFullPath(sourcePath) };
        }

        ApplyVisibility(sourcePath, HideSidecarFiles);

        try
        {
            var json = await File.ReadAllTextAsync(readPath, cancellationToken);
            return JsonSerializer.Deserialize<SnapshotSidecar>(json, _options)
                ?? new SnapshotSidecar { SourcePath = Path.GetFullPath(sourcePath) };
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("快照檔案格式損壞。", exception);
        }
    }
}
