namespace Glystrata.Core.Documents;

public sealed class DocumentManager : IDisposable
{
    private readonly Dictionary<string, DocumentSession> _documents = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<Guid, DocumentViewState> _views = new();
    private readonly Dictionary<Guid, SemaphoreSlim> _saveGates = new();
    private readonly FileCodec _codec;
    private readonly FilePersistenceService _persistence;
    private bool _disposed;

    public DocumentManager(FileCodec? codec = null, FilePersistenceService? persistence = null)
    {
        _codec = codec ?? new FileCodec();
        _persistence = persistence ?? new FilePersistenceService(_codec);
    }

    public IReadOnlyCollection<DocumentSession> Documents => _documents.Values;

    public IReadOnlyCollection<DocumentViewState> Views => _views.Values;

    public DocumentSession Open(string path, OpenMode mode = OpenMode.ExistingOrNewView)
    {
        var fullPath = Path.GetFullPath(path);
        var key = CanonicalizePath(fullPath);
        if (_documents.TryGetValue(key, out var existing))
        {
            return existing;
        }

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("找不到指定檔案。", fullPath);
        }

        var decoded = _codec.Read(fullPath);
        var document = new DocumentSession(fullPath, decoded.Text, decoded.Encoding, decoded.LineEnding, decoded.Fingerprint);
        _documents[key] = document;
        return document;
    }

    public DocumentViewState OpenView(string path, Guid paneId, Guid? sourceGroupId = null, bool newView = false)
    {
        var document = Open(path, newView ? OpenMode.NewView : OpenMode.ExistingOrNewView);
        var existingView = !newView
            ? _views.Values.FirstOrDefault(view => string.Equals(view.Document.DocumentKey, document.DocumentKey, StringComparison.OrdinalIgnoreCase))
            : null;
        if (existingView is not null)
        {
            existingView.SourceGroupId = sourceGroupId ?? existingView.SourceGroupId;
            return existingView;
        }

        return CreateView(document, sourceGroupId, paneId);
    }

    public DocumentViewState CreateUntitled(Guid paneId, Guid? sourceGroupId = null)
    {
        var document = new DocumentSession(null, string.Empty, FileEncodingKind.Utf8, LineEndingKind.Lf);
        _documents[document.DocumentKey] = document;
        return CreateView(document, sourceGroupId, paneId);
    }

    public DocumentViewState CreateView(DocumentSession document, Guid? sourceGroupId, Guid paneId)
    {
        ArgumentNullException.ThrowIfNull(document);
        var nextOrder = _views.Values.Where(candidate => candidate.PaneId == paneId)
            .Select(candidate => candidate.TabOrder)
            .DefaultIfEmpty(-1)
            .Max() + 1;
        var view = new DocumentViewState(document, sourceGroupId, paneId) { TabOrder = nextOrder };
        _views[view.ViewId] = view;
        return view;
    }

    /// <summary>Applies a new tab order within a pane after a drag-drop reorder.</summary>
    public void ReorderViews(Guid paneId, IReadOnlyList<Guid> orderedViewIds)
    {
        for (var index = 0; index < orderedViewIds.Count; index++)
        {
            if (_views.TryGetValue(orderedViewIds[index], out var view) && view.PaneId == paneId)
            {
                view.TabOrder = index;
            }
        }
    }

    public void CloseView(Guid viewId)
    {
        if (!_views.Remove(viewId, out var view))
        {
            return;
        }

        if (_views.Values.All(candidate => !ReferenceEquals(candidate.Document, view.Document)))
        {
            _documents.Remove(view.Document.DocumentKey);
            view.Document.Dispose();
        }
    }

    public async Task<SaveResult> SaveAsync(DocumentSession document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.FilePath is null)
        {
            return SaveResult.Failed("未命名文件必須先另存新檔。");
        }

        var gate = GetSaveGate(document.SessionId);
        var entered = false;
        try
        {
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            entered = true;
            var fingerprint = await _persistence.SaveAsync(document, cancellationToken).ConfigureAwait(false);
            document.MarkSaved(fingerprint);
            return SaveResult.Succeeded(fingerprint);
        }
        catch (Exception exception)
        {
            var message = $"儲存失敗：{exception.Message}";
            document.SetSaveError(message);
            return SaveResult.Failed(message);
        }
        finally
        {
            if (entered)
            {
                gate.Release();
            }
        }
    }

    public async Task<SaveResult> SaveAsAsync(DocumentSession document, string path, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var oldPath = document.FilePath;
        var oldKey = document.DocumentKey;
        document.SetFilePath(path);
        var result = await SaveAsync(document, cancellationToken);
        if (result.Success)
        {
            _documents.Remove(oldKey);
            _documents[document.DocumentKey] = document;
        }
        else
        {
            if (oldPath is not null)
            {
                document.SetFilePath(oldPath);
            }
        }

        return result;
    }

    /// <summary>Repoints a document at a path whose file was already moved on disk (e.g. a rename), without re-saving its content.</summary>
    public void RenamePath(DocumentSession document, string newPath)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(newPath);

        var oldKey = document.DocumentKey;
        document.SetFilePath(newPath);
        _documents.Remove(oldKey);
        _documents[document.DocumentKey] = document;
    }

    public DocumentViewState? FindView(Guid viewId) => _views.GetValueOrDefault(viewId);

    public DocumentSession? FindDocument(string path) => _documents.GetValueOrDefault(CanonicalizePath(path));

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        foreach (var document in _documents.Values)
        {
            document.Dispose();
        }

        foreach (var gate in _saveGates.Values)
        {
            gate.Dispose();
        }

        _documents.Clear();
        _views.Clear();
        _saveGates.Clear();
    }

    public static string CanonicalizePath(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var root = Path.GetPathRoot(fullPath);
        if (!string.Equals(fullPath, root, StringComparison.OrdinalIgnoreCase))
        {
            fullPath = fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        return fullPath.ToUpperInvariant();
    }

    private SemaphoreSlim GetSaveGate(Guid sessionId)
    {
        lock (_saveGates)
        {
            if (!_saveGates.TryGetValue(sessionId, out var gate))
            {
                gate = new SemaphoreSlim(1, 1);
                _saveGates[sessionId] = gate;
            }

            return gate;
        }
    }
}
