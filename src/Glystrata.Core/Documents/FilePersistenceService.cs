namespace Glystrata.Core.Documents;

public sealed class FilePersistenceService
{
    private readonly FileCodec _codec;

    public FilePersistenceService(FileCodec? codec = null)
    {
        _codec = codec ?? new FileCodec();
    }

    public async Task<FileFingerprint> SaveAsync(DocumentSession document, CancellationToken cancellationToken = default)
    {
        if (document.FilePath is null)
        {
            throw new InvalidOperationException("未命名文件必須先使用另存新檔指定路徑。");
        }

        var fullPath = Path.GetFullPath(document.FilePath);
        if (File.Exists(fullPath) && File.GetAttributes(fullPath).HasFlag(FileAttributes.ReadOnly))
        {
            throw new UnauthorizedAccessException("檔案是唯讀檔案。");
        }

        var bytes = _codec.Encode(document.Text, document.Encoding, document.LineEnding);
        var temporaryPath = $"{fullPath}.glystrata-write-{Guid.NewGuid():N}.tmp";

        try
        {
            await using (var stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                64 * 1024,
                FileOptions.WriteThrough | FileOptions.SequentialScan))
            {
                await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            if (File.Exists(fullPath))
            {
                File.Replace(temporaryPath, fullPath, null, true);
            }
            else
            {
                File.Move(temporaryPath, fullPath);
            }

            return FileFingerprint.Read(fullPath);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }
}
