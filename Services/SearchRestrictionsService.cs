using System.Text.Json;
using WorshipSearchWeb.Models;

namespace WorshipSearchWeb.Services;

public interface ISearchRestrictionsService
{
    Task<SearchRestrictions> LoadAsync();
    Task SaveAsync(SearchRestrictions restrictions);
}

public class SearchRestrictionsService : ISearchRestrictionsService
{
    private readonly string _filePath;
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public SearchRestrictionsService(IWebHostEnvironment env)
    {
        var jsonFolder = Path.Combine(env.ContentRootPath, "json");
        Directory.CreateDirectory(jsonFolder);
        _filePath = Path.Combine(jsonFolder, "restrictions.json");
    }

    public async Task<SearchRestrictions> LoadAsync()
    {
        if (!File.Exists(_filePath)) return new SearchRestrictions();
        var json = await File.ReadAllTextAsync(_filePath);
        return JsonSerializer.Deserialize<SearchRestrictions>(json) ?? new SearchRestrictions();
    }

    public async Task SaveAsync(SearchRestrictions restrictions)
    {
        var json = JsonSerializer.Serialize(restrictions, JsonOpts);
        await File.WriteAllTextAsync(_filePath, json);
    }
}
