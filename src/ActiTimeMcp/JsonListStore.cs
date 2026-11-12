using System.Text.Json;

namespace ActiTimeMcp;

/// <summary>
/// A list of items kept in a local JSON file. Reads and writes are serialised; writes are atomic (temp file + move).
/// </summary>
public abstract class JsonListStore<T>
{
    private static readonly JsonSerializerOptions FileJson = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly SemaphoreSlim _lock = new(1, 1);

    public abstract string FilePath { get; }

    protected static string DefaultPath(string fileName)
        => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "actitime-mcp", fileName);

    public async Task<List<T>> LoadAsync(CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try { return await ReadAsync(ct); }
        finally { _lock.Release(); }
    }

    /// <summary>Applies a change to the list and saves it atomically.</summary>
    public async Task<List<T>> UpdateAsync(Action<List<T>> change, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var list = await ReadAsync(ct);
            change(list);
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            var tmp = FilePath + ".tmp";
            await File.WriteAllTextAsync(tmp, JsonSerializer.Serialize(list, FileJson), ct);
            File.Move(tmp, FilePath, overwrite: true);
            return list;
        }
        finally { _lock.Release(); }
    }

    private async Task<List<T>> ReadAsync(CancellationToken ct)
    {
        if (!File.Exists(FilePath)) return [];
        var text = await File.ReadAllTextAsync(FilePath, ct);
        if (string.IsNullOrWhiteSpace(text)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<T>>(text, FileJson) ?? [];
        }
        catch (JsonException ex)
        {
            throw new ModelContextProtocol.McpException($"{FilePath} is not valid JSON: {ex.Message}");
        }
    }
}
