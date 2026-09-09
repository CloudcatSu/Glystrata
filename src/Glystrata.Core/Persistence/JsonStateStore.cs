using System.Text.Json;

namespace Glystrata.Core.Persistence;

public interface IStateStore
{
    Task<AppSettings> LoadSettingsAsync(CancellationToken cancellationToken = default);
    Task SaveSettingsAsync(AppSettings settings, CancellationToken cancellationToken = default);
    Task<GroupsState> LoadGroupsAsync(CancellationToken cancellationToken = default);
    Task SaveGroupsAsync(GroupsState groups, CancellationToken cancellationToken = default);
    Task<SessionState> LoadSessionAsync(CancellationToken cancellationToken = default);
    Task SaveSessionAsync(SessionState session, CancellationToken cancellationToken = default);
}

public sealed class JsonStateStore : IStateStore
{
    private readonly string _baseDirectory;
    private readonly AtomicFileWriter _writer;
    private readonly JsonSerializerOptions _options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public JsonStateStore(string? baseDirectory = null, AtomicFileWriter? writer = null)
    {
        _baseDirectory = baseDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Glystrata");
        _writer = writer ?? new AtomicFileWriter();
    }

    public string BaseDirectory => _baseDirectory;

    public Task<AppSettings> LoadSettingsAsync(CancellationToken cancellationToken = default) =>
        LoadAsync("settings.json", new AppSettings(), cancellationToken, settings => settings.Normalize());

    public Task SaveSettingsAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        settings.Normalize();
        return SaveAsync("settings.json", settings, cancellationToken);
    }

    public Task<GroupsState> LoadGroupsAsync(CancellationToken cancellationToken = default) =>
        LoadAsync("groups.json", new GroupsState(), cancellationToken);

    public Task SaveGroupsAsync(GroupsState groups, CancellationToken cancellationToken = default) =>
        SaveAsync("groups.json", groups, cancellationToken);

    public Task<SessionState> LoadSessionAsync(CancellationToken cancellationToken = default) =>
        LoadAsync("session.json", new SessionState(), cancellationToken);

    public Task SaveSessionAsync(SessionState session, CancellationToken cancellationToken = default) =>
        SaveAsync("session.json", session, cancellationToken);

    private async Task<T> LoadAsync<T>(
        string fileName,
        T fallback,
        CancellationToken cancellationToken,
        Action<T>? normalize = null)
    {
        var path = Path.Combine(_baseDirectory, fileName);
        if (!File.Exists(path))
        {
            return fallback;
        }

        try
        {
            var json = await File.ReadAllTextAsync(path, cancellationToken);
            var value = JsonSerializer.Deserialize<T>(json, _options) ?? fallback;
            normalize?.Invoke(value);
            return value;
        }
        catch (JsonException)
        {
            var backup = $"{path}.corrupt-{DateTime.UtcNow:yyyyMMddHHmmss}.json";
            try
            {
                File.Move(path, backup, true);
            }
            catch (IOException)
            {
                // The safe defaults remain usable even if the diagnostic copy cannot be made.
            }

            return fallback;
        }
        catch (IOException)
        {
            return fallback;
        }
        catch (UnauthorizedAccessException)
        {
            return fallback;
        }
    }

    private Task SaveAsync<T>(string fileName, T value, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(value, _options);
        return _writer.WriteTextAsync(Path.Combine(_baseDirectory, fileName), json, cancellationToken);
    }
}
