using ICSharpCode.AvalonEdit.Document;

namespace Glystrata.Core.Documents;

public enum OpenMode
{
    ExistingOrNewView,
    NewView
}

public enum FileEncodingKind
{
    Utf8,
    Utf8WithBom,
    Utf16LittleEndian
}

public enum LineEndingKind
{
    CrLf,
    Lf,
    Cr
}

public sealed record FileFingerprint(long Length, DateTime LastWriteTimeUtc)
{
    public static FileFingerprint Read(string path)
    {
        var info = new FileInfo(path);
        return new FileFingerprint(info.Length, info.LastWriteTimeUtc);
    }
}

public sealed record DecodedText(
    string Text,
    FileEncodingKind Encoding,
    LineEndingKind LineEnding,
    FileFingerprint Fingerprint);

public sealed record SaveResult(bool Success, string? ErrorMessage, FileFingerprint? Fingerprint)
{
    public static SaveResult Failed(string message) => new(false, message, null);

    public static SaveResult Succeeded(FileFingerprint fingerprint) => new(true, null, fingerprint);
}

public sealed class DocumentSession : IDisposable, INotifyPropertyChanged
{
    private bool _isModified;
    private bool _suppressTextChange;
    private bool _disposed;
    private string? _filePath;
    private string _documentKey;
    private string _text;
    private FileFingerprint? _lastSavedFingerprint;
    private string _lastSavedText;
    private string? _lastError;

    public DocumentSession(
        string? filePath,
        string text,
        FileEncodingKind encoding,
        LineEndingKind lineEnding,
        FileFingerprint? fingerprint = null)
    {
        SessionId = Guid.NewGuid();
        _filePath = filePath is null ? null : Path.GetFullPath(filePath);
        _documentKey = _filePath is null ? $"untitled:{SessionId:N}" : CanonicalizePath(_filePath);
        TextDocument = new TextDocument(text);
        _text = text;
        TextDocument.UndoStack.ClearAll();
        TextDocument.UndoStack.MarkAsOriginalFile();
        TextDocument.TextChanged += OnTextChanged;
        Encoding = encoding;
        LineEnding = lineEnding;
        _lastSavedFingerprint = fingerprint;
        _lastSavedText = text;
    }

    public Guid SessionId { get; }

    public string? FilePath => _filePath;

    public string DocumentKey => _documentKey;

    public TextDocument TextDocument { get; }

    public string Text => Volatile.Read(ref _text);

    public FileEncodingKind Encoding { get; private set; }

    public LineEndingKind LineEnding { get; private set; }

    public bool IsUntitled => _filePath is null;

    public bool IsModified
    {
        get => _isModified;
        private set
        {
            if (_isModified == value)
            {
                return;
            }

            _isModified = value;
            OnPropertyChanged(nameof(IsModified));
        }
    }

    public FileFingerprint? LastSavedFingerprint => _lastSavedFingerprint;

    public string? LastError
    {
        get => _lastError;
        private set
        {
            if (string.Equals(_lastError, value, StringComparison.Ordinal))
            {
                return;
            }

            _lastError = value;
            OnPropertyChanged(nameof(LastError));
        }
    }

    public DateTime? LastSnapshotUtc { get; set; }

    public string? LastSnapshotText { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    public event EventHandler? TextChanged;

    public void ApplyLoadedContent(
        string text,
        FileEncodingKind encoding,
        LineEndingKind lineEnding,
        FileFingerprint? fingerprint)
    {
        ReplaceBuffer(text);
        TextDocument.UndoStack.MarkAsOriginalFile();

        Encoding = encoding;
        LineEnding = lineEnding;
        _lastSavedFingerprint = fingerprint;
        _lastSavedText = text;
        Volatile.Write(ref _text, text);
        IsModified = false;
        LastError = null;
        OnPropertyChanged(nameof(Text));
        OnPropertyChanged(nameof(Encoding));
        OnPropertyChanged(nameof(LineEnding));
    }

    public void ApplyRestoredContent(string text, FileEncodingKind encoding, LineEndingKind lineEnding)
    {
        ReplaceBuffer(text);
        Volatile.Write(ref _text, text);
        Encoding = encoding;
        LineEnding = lineEnding;
        IsModified = true;
        LastError = null;
        OnPropertyChanged(nameof(Text));
        OnPropertyChanged(nameof(Encoding));
        OnPropertyChanged(nameof(LineEnding));
    }

    public void MarkSaved(FileFingerprint fingerprint)
    {
        _lastSavedFingerprint = fingerprint;
        _lastSavedText = Text;
        IsModified = false;
        LastError = null;
    }

    public void SetSaveError(string message) => LastError = message;

    public void SetFilePath(string path)
    {
        _filePath = Path.GetFullPath(path);
        _documentKey = CanonicalizePath(_filePath);
        OnPropertyChanged(nameof(FilePath));
        OnPropertyChanged(nameof(DocumentKey));
        OnPropertyChanged(nameof(IsUntitled));
    }

    public void SetEncodingAndLineEnding(FileEncodingKind encoding, LineEndingKind lineEnding)
    {
        Encoding = encoding;
        LineEnding = lineEnding;
        OnPropertyChanged(nameof(Encoding));
        OnPropertyChanged(nameof(LineEnding));
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        TextDocument.TextChanged -= OnTextChanged;
    }

    private void OnTextChanged(object? sender, EventArgs e)
    {
        if (_suppressTextChange)
        {
            return;
        }

        var text = TextDocument.Text;
        Volatile.Write(ref _text, text);
        IsModified = !string.Equals(text, _lastSavedText, StringComparison.Ordinal);
        LastError = null;
        OnPropertyChanged(nameof(Text));
        TextChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ReplaceBuffer(string text)
    {
        _suppressTextChange = true;
        try
        {
            TextDocument.Text = text;
            TextDocument.UndoStack.ClearAll();
            Volatile.Write(ref _text, text);
        }
        finally
        {
            _suppressTextChange = false;
        }
    }

    private void OnPropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private static string CanonicalizePath(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var root = Path.GetPathRoot(fullPath);
        if (!string.Equals(fullPath, root, StringComparison.OrdinalIgnoreCase))
        {
            fullPath = fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        return fullPath.ToUpperInvariant();
    }
}

public sealed class DocumentViewState : INotifyPropertyChanged
{
    private int _caretOffset;
    private double _horizontalOffset;
    private double _verticalOffset;

    public DocumentViewState(DocumentSession document, Guid? sourceGroupId, Guid paneId)
    {
        ViewId = Guid.NewGuid();
        Document = document;
        SourceGroupId = sourceGroupId;
        PaneId = paneId;
    }

    public Guid ViewId { get; }

    public DocumentSession Document { get; }

    public Guid? SourceGroupId { get; set; }

    public Guid PaneId { get; set; }

    /// <summary>Position among the other views sharing this pane; drives tab display order. Lower sorts first.</summary>
    public int TabOrder { get; set; }

    public int CaretOffset
    {
        get => _caretOffset;
        set
        {
            if (_caretOffset == value)
            {
                return;
            }

            _caretOffset = value;
            OnPropertyChanged(nameof(CaretOffset));
        }
    }

    public double HorizontalOffset
    {
        get => _horizontalOffset;
        set
        {
            if (Math.Abs(_horizontalOffset - value) < 0.01)
            {
                return;
            }

            _horizontalOffset = value;
            OnPropertyChanged(nameof(HorizontalOffset));
        }
    }

    public double VerticalOffset
    {
        get => _verticalOffset;
        set
        {
            if (Math.Abs(_verticalOffset - value) < 0.01)
            {
                return;
            }

            _verticalOffset = value;
            OnPropertyChanged(nameof(VerticalOffset));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
