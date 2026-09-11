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

    public SnapshotSidecarStore(AtomicFileWriter? writer = null)
    {
        _writer = writer ?? new AtomicFileWriter();
    }

    public bool HideSidecarFiles { get; set; }

    public static string GetSidecarPath(string sourcePath)
    {
        var fullPath = Path.GetFullPath(sourcePath);
        var directory = Path.GetDirectoryName(fullPath) ?? throw new InvalidOperationException("來源檔案缺少目錄。");
        return Path.Combine(directory, $".{Path.GetFileName(fullPath)}.glystrata-snapshots.json");
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
        var sidecar = new SnapshotSidecar
        {
            SourcePath = fullPath,
            Snapshots = snapshots
                .OrderByDescending(snapshot => snapshot.CreatedUtc)
                .Select(SnapshotEntry.FromInfo)
                .ToList()
        };
        var json = JsonSerializer.Serialize(sidecar, _options);
        var sidecarPath = GetSidecarPath(fullPath);
        await _writer.WriteTextAsync(sidecarPath, json, cancellationToken);
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

    public Task DeleteAllAsync(string sourcePath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var sidecarPath = GetSidecarPath(sourcePath);
        if (File.Exists(sidecarPath))
        {
            File.Delete(sidecarPath);
        }

        return Task.CompletedTask;
    }

    private async Task<SnapshotSidecar> ReadSidecarAsync(string sourcePath, CancellationToken cancellationToken)
    {
        var sidecarPath = GetSidecarPath(sourcePath);
        if (!File.Exists(sidecarPath))
        {
            return new SnapshotSidecar { SourcePath = Path.GetFullPath(sourcePath) };
        }

        ApplyVisibility(sourcePath, HideSidecarFiles);

        try
        {
            var json = await File.ReadAllTextAsync(sidecarPath, cancellationToken);
            return JsonSerializer.Deserialize<SnapshotSidecar>(json, _options)
                ?? new SnapshotSidecar { SourcePath = Path.GetFullPath(sourcePath) };
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("快照檔案格式損壞。", exception);
        }
    }
}
