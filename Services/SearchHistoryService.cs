using System.Text.Json;
using WorshipSearchWeb.Models;

namespace WorshipSearchWeb.Services;

public interface ISearchHistoryService
{
    Task LogSearchAsync(string query, int resultCount);
    Task LogSelectionAsync(string query, string title, string artist, bool pickedManually);
    Task<List<SearchHistoryEntry>> LoadRecentAsync(int limit = 200);
    Task ClearAsync();
}

public class SearchHistoryService : ISearchHistoryService
{
    private const int SelectionRecord = -1;

    private readonly string _filePath;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public SearchHistoryService(IWebHostEnvironment env)
    {
        var jsonFolder = Path.Combine(env.ContentRootPath, "json");
        Directory.CreateDirectory(jsonFolder);
        _filePath = Path.Combine(jsonFolder, "search-history.json");
    }

    public async Task LogSearchAsync(string query, int resultCount)
    {
        if (string.IsNullOrWhiteSpace(query)) return;
        await AppendAsync(new SearchHistoryEntry
        {
            Query = query.Trim().ToLowerInvariant(),
            ResultCount = resultCount
        });
    }

    public async Task LogSelectionAsync(string query, string title, string artist, bool pickedManually)
    {
        if (string.IsNullOrWhiteSpace(query)) return;
        await AppendAsync(new SearchHistoryEntry
        {
            Query = query.Trim().ToLowerInvariant(),
            ResultCount = SelectionRecord,
            SelectedTitle = title,
            SelectedArtist = artist,
            PickedManually = pickedManually
        });
    }

    public async Task<List<SearchHistoryEntry>> LoadRecentAsync(int limit = 200)
    {
        var all = await LoadAllAsync();
        return all.OrderByDescending(e => e.SearchedAt).Take(limit).ToList();
    }

    public async Task ClearAsync()
    {
        await _lock.WaitAsync();
        try { File.Delete(_filePath); }
        finally { _lock.Release(); }
    }

    private async Task AppendAsync(SearchHistoryEntry entry)
    {
        await _lock.WaitAsync();
        try
        {
            var all = await LoadAllInternalAsync();
            all.Add(entry);
            // Keep last 1000 entries to bound file size
            if (all.Count > 1000)
                all = all.Skip(all.Count - 1000).ToList();
            await File.WriteAllTextAsync(_filePath, JsonSerializer.Serialize(all, JsonOpts));
        }
        finally { _lock.Release(); }
    }

    private async Task<List<SearchHistoryEntry>> LoadAllAsync()
    {
        await _lock.WaitAsync();
        try { return await LoadAllInternalAsync(); }
        finally { _lock.Release(); }
    }

    private async Task<List<SearchHistoryEntry>> LoadAllInternalAsync()
    {
        if (!File.Exists(_filePath)) return new();
        var json = await File.ReadAllTextAsync(_filePath);
        return JsonSerializer.Deserialize<List<SearchHistoryEntry>>(json) ?? new();
    }
}
