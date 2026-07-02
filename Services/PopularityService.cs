using System.Text.Json;
using System.Text.Json.Serialization;

namespace WorshipSearchWeb.Services;

public interface IPopularityService
{
    Task IncrementAsync(IEnumerable<string> fileNames);
    Task<Dictionary<string, int>> LoadAsync();
}

public class PopularityService : IPopularityService
{
    private readonly string _path;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public PopularityService(IWebHostEnvironment env)
    {
        var folder = Path.Combine(env.ContentRootPath, "json");
        Directory.CreateDirectory(folder);
        _path = Path.Combine(folder, "search-popularity.json");
    }

    public async Task<Dictionary<string, int>> LoadAsync()
    {
        if (!File.Exists(_path)) return new();
        try
        {
            var json = await File.ReadAllTextAsync(_path);
            return JsonSerializer.Deserialize<Dictionary<string, int>>(json) ?? new();
        }
        catch { return new(); }
    }

    public async Task IncrementAsync(IEnumerable<string> fileNames)
    {
        var names = fileNames.ToList();
        if (names.Count == 0) return;

        await _lock.WaitAsync();
        try
        {
            var counts = await LoadAsync();
            foreach (var name in names)
                counts[name] = counts.GetValueOrDefault(name) + 1;

            await File.WriteAllTextAsync(_path, JsonSerializer.Serialize(counts, JsonOpts));
        }
        finally { _lock.Release(); }
    }
}
